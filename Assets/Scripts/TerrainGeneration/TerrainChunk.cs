using System;
using GeneralUtils;
using SkywardRay;
using SkywardRay.Utility;
using TerrainGeneration.Brushes;
using TerrainGeneration.Jobs;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGeneration;

namespace TerrainGeneration {

public class TerrainChunk : IDisposable {
    
    private static readonly VertexAttributeDescriptor[] meshVertexLayout = {new VertexAttributeDescriptor(VertexAttribute.Position)};

    public MeshObject meshObject;
    public float3 position;
    public ChunkKey key;
    public int size;
    public float voxelSize;
    public readonly bool[] hasSignChangeOnSide = new bool[EnumUtil<CubeSide>.length];
    public readonly bool[] hasNeighbourOnSide = new bool[EnumUtil<CubeSide>.length];
    public readonly ChunkKey[] neighbourKeys = new ChunkKey[EnumUtil<CubeSide>.length];
    public bool hasSignChangeOnAnySide;

    public int runningJobs;
    public bool hasUpdatedSigns;
    public bool hasUpdatedDensities;
    public bool hasBeenMarkedForRemoval;

    private NativeArray<float> densityMap;
    private Counter vertexCounter;
    private ChunkJobs jobs;

public TerrainChunk (int size, float voxelSize, MeshObject meshObject) {
    this.meshObject = meshObject;
    this.voxelSize = voxelSize;
    this.size = size;

    densityMap = new NativeArray<float>((size + 1).Pow(3), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    vertexCounter = new Counter(Allocator.Persistent);
}

public void Dispose () {
    CompleteRunningJobs();
    
    densityMap.Dispose();
}

    #region Jobs

    public void UpdateJobs () {
        if (runningJobs == 0) {
            return;
        }

        if (jobs.isDensityJobRunning && jobs.densityHandle.IsCompleted) {
            CompleteDensityJob();
        }

        if (jobs.isSurfaceExtractionJobRunning && jobs.surfaceExtractionHandle.IsCompleted) {
            CompleteSurfaceExtractionJob();
        }

        if (jobs.isCollisionJobRunning && jobs.collisionHandle.IsCompleted) {
            CompleteColliderBakeJob();
        }
    }

    private void CompleteRunningJobs () {
        CompleteDensityJob();
        CompleteSurfaceExtractionJob();
        CompleteColliderBakeJob();
    }

    public void ScheduleApplyBrushJob (IBrush brush, BrushOperation brushOperation) {
        if (hasBeenMarkedForRemoval) {
            return;
        }

        if (runningJobs > 0) {
            CompleteRunningJobs();
        }

        var job = new ApplyBrushJob<SphereBrush> {
            brush = (SphereBrush) brush,
            operation = brushOperation,
            size = size + 1,
            densityMap = densityMap,
            chunkPosition = position,
            signTrackers = new NativeArray<int>(EnumUtil<CubeSide>.length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
        };
        
        jobs.densityJob = job;
        jobs.densityHandle = ScheduleParallelJob((size + 1).Pow(3), job);
        jobs.isDensityJobRunning = true;

        ScheduleSurfaceExtractionJob();
    }

    public void ScheduleGenerateDensitiesJob (NoiseSettings noiseSettings) {
        if (hasBeenMarkedForRemoval) {
            return;
        }

        if (runningJobs > 0) {
            CompleteRunningJobs();
        }

        var job = new GenerateDensitiesJob {
            noiseMap = densityMap,
            surfaceLevel = noiseSettings.surfaceLevel,
            frequency = noiseSettings.freq,
            amplitude = noiseSettings.ampl,
            octaves = noiseSettings.oct,
            offset = position,
            size = size + 1,
            signTrackers = new NativeArray<int>(EnumUtil<CubeSide>.length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
        };

        jobs.densityJob = job;
        jobs.densityHandle = ScheduleParallelJob((size + 1).Pow(3), job);
        jobs.isDensityJobRunning = true;

        ScheduleSurfaceExtractionJob();
    }

    private void ScheduleSurfaceExtractionJob () {
        if (hasBeenMarkedForRemoval) {
            return;
        }

        vertexCounter.Count = 0;

        var job = new MarchingCubesJob {
            isolevel = 0f,
            chunkSize = size,
            densities = densityMap,
            vertexCounter = vertexCounter,
            vertexBuffer = MarchingCubesJob.CreateVertexBuffer(size),
            triangleIndexBuffer = MarchingCubesJob.CreateTriangleIndexBuffer(size),
        };

        jobs.surfaceExtractionJob = job;
        jobs.surfaceExtractionHandle = ScheduleParallelJob(size.Pow(3), job);
        jobs.isSurfaceExtractionJobRunning = true;
    }

    private void ScheduleColliderBakeJob () {
        if (hasBeenMarkedForRemoval) {
            return;
        }

        if (runningJobs > 0) {
            CompleteRunningJobs();
        }
        
        var job = new ColliderBakeJob {
            meshID = meshObject.MeshInstanceID
        };

        jobs.collisionHandle = ScheduleJob(job);
        jobs.isCollisionJobRunning = true;
    }

    private JobHandle ScheduleJob<T> (T job) where T : struct, IJob {
        runningJobs++;
        
        return jobs.newestJobHandle = job.Schedule(jobs.newestJobHandle);
    }

    private JobHandle ScheduleParallelJob<T> (int size, T job) where T : struct, IJobParallelFor {
        const int batchSize = 64;

        runningJobs++;
        
        return jobs.newestJobHandle = job.Schedule(size, batchSize, jobs.newestJobHandle);
    }

    private void CompleteDensityJob () {
        jobs.densityHandle.Complete();
        jobs.isDensityJobRunning = false;
        runningJobs--;

        var signTrackers = jobs.densityJob.GetSignTrackers();

        // The length and width of each side
        var dataPointsPerSide = (size + 1) * (size + 1);

        var hadSignChangeBeforeUpdate = hasSignChangeOnAnySide;

        hasSignChangeOnAnySide = false;

        foreach (var side in EnumUtil<CubeSide>.intValues) {
            var hasSignChange = math.abs(signTrackers[side]) != dataPointsPerSide;

            hasSignChangeOnSide[side] = hasSignChange;
            
            hasSignChangeOnAnySide = hasSignChangeOnAnySide || hasSignChange;
        }

        signTrackers.Dispose();

        if (hadSignChangeBeforeUpdate != hasSignChangeOnAnySide) {
            hasUpdatedSigns = true;
        }

        hasUpdatedDensities = true;
    }

    private void CompleteSurfaceExtractionJob () {
        jobs.surfaceExtractionHandle.Complete();
        jobs.isSurfaceExtractionJobRunning = false;
        runningJobs--;
        
        var vertexCount = vertexCounter.Count * 3;

        var vertexBuffer = jobs.surfaceExtractionJob.GetVertexBuffer();
        var triangleIndexBuffer = jobs.surfaceExtractionJob.GetTriangleIndexBuffer();

        if (vertexCount > 0) {
            var center = size / 2;
            var bounds = new Bounds(math.float3(center), math.float3(size * voxelSize));

            meshObject.FillMesh(vertexCount, vertexCount, vertexBuffer, triangleIndexBuffer, bounds, meshVertexLayout);
            
            ScheduleColliderBakeJob();
        }

        vertexBuffer.Dispose();
        triangleIndexBuffer.Dispose();
    }

    private void CompleteColliderBakeJob () {
        jobs.collisionHandle.Complete();
        jobs.isCollisionJobRunning = false;
        runningJobs--;
    }

    private struct ChunkJobs {

        public JobHandle newestJobHandle;

        public bool isDensityJobRunning;
        public IDensityJob densityJob;
        public JobHandle densityHandle;

        public bool isSurfaceExtractionJobRunning;
        public IMeshDataJob surfaceExtractionJob;
        public JobHandle surfaceExtractionHandle;

        public bool isCollisionJobRunning;
        public JobHandle collisionHandle;
    }

    #endregion

}

}