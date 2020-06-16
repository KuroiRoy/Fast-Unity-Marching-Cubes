using System;
using GeneralUtils;
using SkywardRay;
using SkywardRay.Utility;
using TerrainGeneration.Brushes;
using TerrainGeneration.Jobs;
using TerrainGeneration.TerrainUtils;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using WorldGeneration;

namespace TerrainGeneration {

public class TerrainChunk : IDisposable {

    public const int CHUNK_SIZE = 20;
    private static readonly VertexAttributeDescriptor[] meshVertexLayout = {new VertexAttributeDescriptor(VertexAttribute.Position)};

    public int VertexCount => nativeCollectionStash.vertexCounter.Count * 3;

    public readonly MeshObject meshObject;
    public float3 position;
    public ChunkKey key;
    public readonly int size;
    public Bounds bounds;
    public readonly float voxelSize;
    public bool canExpandToNeighbours;
    public readonly bool[] hasSignChangeOnSide = new bool[EnumUtil<CubeSide>.length];
    public readonly bool[] hasNeighbourOnSide = new bool[EnumUtil<CubeSide>.length];
    public readonly ChunkKey[] neighbourKeys = new ChunkKey[EnumUtil<CubeSide>.length];
    public bool hasSignChangeOnAnySide;

    public int runningJobs;
    public int framesSinceJobScheduled;
    public bool hasUpdatedSigns;
    public bool hasUpdatedDensities;
    public bool hasBeenMarkedForRemoval;
    public bool hasBeenDeformed;

    private readonly ChunkNativeCollectionStash nativeCollectionStash;
    private ChunkJobs jobs;

    public TerrainChunk (int size, float voxelSize, MeshObject meshObject) {
        this.meshObject = meshObject;
        this.voxelSize = voxelSize;
        this.size = size;

        nativeCollectionStash = meshObject.gameObject.AddComponent<ChunkNativeCollectionStash>();
        nativeCollectionStash.densityMap = new NativeArray<float>((size + 1).Pow(3), Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        nativeCollectionStash.vertexCounter = new Counter(Allocator.Persistent);
    }

    public void Dispose () {
        hasBeenMarkedForRemoval = true;

        CompleteRunningJobs();
    }

    public void ClearNeighbour (int sideIndex) {
        hasNeighbourOnSide[sideIndex] = false;
        neighbourKeys[sideIndex] = default;
        
        TrackExpandability();
    }

    public bool Load (ChunkSaveData saveData) {
        if (saveData.densityMap.Length != nativeCollectionStash.densityMap.Length) {
            Debug.LogError($"Size of loaded chunk {key.origin} does not match the current chunk size");
            return false;
        }
        
        nativeCollectionStash.densityMap.CopyFrom(saveData.densityMap);
        
        ScheduleRefreshDensitiesJob();
        return true;
    }

    public void Save (ChunkSaveData saveData) {
        saveData.densityMap = new float[nativeCollectionStash.densityMap.Length];
        
        nativeCollectionStash.densityMap.CopyToFast(saveData.densityMap);
    }

    public void SetNeighbour (TerrainChunk neighbour, (int index, CubeSide value) side) {
        hasNeighbourOnSide[side.index] = true;
        neighbourKeys[side.index] = neighbour.key;

        var flippedSideIndex = (int) side.value.Flip();
        neighbour.hasNeighbourOnSide[flippedSideIndex] = true;
        neighbour.neighbourKeys[flippedSideIndex] = key;
        
        TrackExpandability();
    }

    private void TrackSigns (NativeArray<int> signTrackers) {
        // The length and width of each side
        var dataPointsPerSide = (size + 1) * (size + 1);

        var hadSignChangeBeforeUpdate = hasSignChangeOnAnySide;

        hasSignChangeOnAnySide = false;

        foreach (var side in EnumUtil<CubeSide>.intValues) {
            var hasSignChange = math.abs(signTrackers[side]) != dataPointsPerSide;

            hasSignChangeOnSide[side] = hasSignChange;

            hasSignChangeOnAnySide = hasSignChangeOnAnySide || hasSignChange;
        }

        if (hadSignChangeBeforeUpdate != hasSignChangeOnAnySide) {
            hasUpdatedSigns = true;
        }

        hasUpdatedDensities = true;

        TrackExpandability();
    }

    private void TrackExpandability () {
        canExpandToNeighbours = false;

        foreach (var side in EnumUtil<CubeSide>.intValues) {
            canExpandToNeighbours = canExpandToNeighbours || hasSignChangeOnSide[side] && !hasNeighbourOnSide[side];
        }
    }

    #region Jobs
    
    #region Density

    public void ScheduleApplyBrushJob (IBrush brush, BrushOperation brushOperation) {
        if (hasBeenMarkedForRemoval) {
            return;
        }

        hasBeenDeformed = true;

        if (runningJobs > 0) {
            CompleteRunningJobs();
        }

        var job = new ApplyBrushJob<SphereBrush> {
            brush = (SphereBrush) brush,
            operation = brushOperation,
            size = size + 1,
            voxelSize = voxelSize,
            densityMap = nativeCollectionStash.densityMap,
            chunkPosition = position,
            signTrackers = new NativeArray<int>(EnumUtil<CubeSide>.length, Allocator.TempJob),
        };

        jobs.densityJob = job;
        jobs.densityHandle = ScheduleParallelJob((size + 1).Pow(3), job);
        jobs.isDensityJobRunning = true;
    }

    public void ScheduleGenerateDensitiesJob (NoiseSettings noiseSettings) {
        if (hasBeenMarkedForRemoval) {
            return;
        }

        if (runningJobs > 0) {
            CompleteRunningJobs();
        }

        var job = new GenerateDensitiesJob {
            densityMap = nativeCollectionStash.densityMap,
            surfaceLevel = noiseSettings.surfaceLevel,
            frequency = noiseSettings.freq,
            amplitude = noiseSettings.ampl,
            octaves = noiseSettings.oct,
            offset = position,
            size = size + 1,
            voxelSize = voxelSize,
            signTrackers = new NativeArray<int>(EnumUtil<CubeSide>.length, Allocator.TempJob),
        };

        jobs.densityJob = job;
        jobs.densityHandle = ScheduleParallelJob((size + 1).Pow(3), job);
        jobs.isDensityJobRunning = true;
    }

    private void ScheduleRefreshDensitiesJob () {
        if (hasBeenMarkedForRemoval) {
            return;
        }

        if (runningJobs > 0) {
            CompleteRunningJobs();
        }

        var job = new RefreshDensitiesJob() {
            densityMap = nativeCollectionStash.densityMap,
            size = size + 1,
            signTrackers = new NativeArray<int>(EnumUtil<CubeSide>.length, Allocator.TempJob),
        };

        jobs.densityJob = job;
        jobs.densityHandle = ScheduleParallelJob((size + 1).Pow(3), job);
        jobs.isDensityJobRunning = true;
    }

    private void CompleteDensityJob () {
        jobs.densityHandle.Complete();
        jobs.isDensityJobRunning = false;
        runningJobs--;
        
        if (hasBeenMarkedForRemoval) {
            return;
        }

        var signTrackers = jobs.densityJob.GetSignTrackers();

        TrackSigns(signTrackers);
        
        signTrackers.Dispose();

        ScheduleSurfaceExtractionJob();
    }
    
    #endregion
    
    #region Surface extraction

    private void ScheduleSurfaceExtractionJob () {
        if (hasBeenMarkedForRemoval) {
            return;
        }

        if (runningJobs > 0) {
            CompleteRunningJobs();
        }

        nativeCollectionStash.vertexCounter.Count = 0;

        var job = new MarchingCubesJob {
            isolevel = 0f,
            chunkSize = size,
            voxelSize = voxelSize,
            densities = nativeCollectionStash.densityMap,
            vertexCounter = nativeCollectionStash.vertexCounter,
            vertexBuffer = MarchingCubesJob.CreateVertexBuffer(size),
            triangleIndexBuffer = MarchingCubesJob.CreateTriangleIndexBuffer(size),
        };

        jobs.surfaceExtractionJob = job;
        jobs.surfaceExtractionHandle = ScheduleParallelJob(size.Pow(3), job);
        jobs.isSurfaceExtractionJobRunning = true;
    }

    private void CompleteSurfaceExtractionJob () {
        jobs.surfaceExtractionHandle.Complete();
        jobs.isSurfaceExtractionJobRunning = false;
        runningJobs--;
        
        if (hasBeenMarkedForRemoval) {
            return;
        }

        var vertexBuffer = jobs.surfaceExtractionJob.GetVertexBuffer();
        var triangleIndexBuffer = jobs.surfaceExtractionJob.GetTriangleIndexBuffer();

        if (VertexCount > 0) {
            var meshBounds = new Bounds {min = Vector3.zero, max = math.float3(size * voxelSize)};

            meshObject.FillMesh(VertexCount, VertexCount, vertexBuffer, triangleIndexBuffer, meshBounds, meshVertexLayout);
            meshObject.gameObject.SetActive(true);

            ScheduleColliderBakeJob();
        }
        else {
            meshObject.gameObject.SetActive(false);
        }

        vertexBuffer.Dispose();
        triangleIndexBuffer.Dispose();
    }
    
    #endregion

    #region Collision

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

    private void CompleteColliderBakeJob () {
        jobs.collisionHandle.Complete();
        jobs.isCollisionJobRunning = false;
        runningJobs--;
    }

    #endregion

    private JobHandle ScheduleJob<T> (T job) where T : struct, IJob {
        runningJobs++;
        framesSinceJobScheduled = 0;

        return jobs.newestJobHandle = job.Schedule(jobs.newestJobHandle);
    }

    private JobHandle ScheduleParallelJob<T> (int size, T job) where T : struct, IJobParallelFor {
        const int batchSize = 64;

        runningJobs++;
        framesSinceJobScheduled = 0;

        return jobs.newestJobHandle = job.Schedule(size, batchSize, jobs.newestJobHandle);
    }

    public void CompleteJobs () {
        if (jobs.isDensityJobRunning) {
            CompleteDensityJob();
        }

        if (jobs.isSurfaceExtractionJobRunning) {
            CompleteSurfaceExtractionJob();
        }

        if (jobs.isCollisionJobRunning) {
            CompleteColliderBakeJob();
        }
    }

    private void CompleteRunningJobs () {
        CompleteDensityJob();
        CompleteSurfaceExtractionJob();
        CompleteColliderBakeJob();
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

    #region Equality

    public bool Equals (TerrainChunk other) {
        return key.Equals(other.key);
    }

    public override bool Equals (object obj) {
        if (ReferenceEquals(null, obj)) return false;
        
        if (ReferenceEquals(this, obj)) return true;
        
        if (obj.GetType() != GetType()) return false;
        
        return Equals((TerrainChunk) obj);
    }

    public override int GetHashCode () {
        return key.GetHashCode();
    }

    #endregion

}

}