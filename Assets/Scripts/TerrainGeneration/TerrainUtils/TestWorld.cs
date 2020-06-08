using System;
using System.Collections.Generic;
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

namespace TerrainGeneration.TerrainUtils {

public class TestWorld : MonoBehaviour, IDeformableTerrain {

    private static readonly VertexAttributeDescriptor[] meshVertexLayout = {new VertexAttributeDescriptor(VertexAttribute.Position)};

    [SerializeField] private int chunkSize = 32;
    [SerializeField] private float voxelSize = 1;
    [SerializeField] private float chunkDrawDistance = 16;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private MeshObject chunkMeshObjectPrefab;
    [SerializeField] private NoiseSettings noiseSettings;

    private int chunksWithSignChange;
    private ColliderBakeJob colliderBakeJob;
    private JobHandle colliderBakeHandle = default;
    private bool isColliderBakeJobRunning;
    private readonly DisposablePool<NativeArray<Vector3>> vertexBufferPool = new DisposablePool<NativeArray<Vector3>>();
    private readonly DisposablePool<NativeArray<int>> triangleIndexBufferPool = new DisposablePool<NativeArray<int>>();
    private readonly DisposablePool<TerrainChunk> chunkPool = new DisposablePool<TerrainChunk>();
    private readonly Dictionary<ChunkKey, TerrainChunk> chunks = new Dictionary<ChunkKey, TerrainChunk>();
    private readonly Queue<int> meshBakingQueue = new Queue<int>();
    private readonly List<(JobHandle handle, Action onJobCompleted)> runningJobs = new List<(JobHandle handle, Action onJobCompleted)>();

    private void Start () {
        colliderBakeJob.Construct();
    }

    private void Update () {
        if (chunksWithSignChange == 0 && chunks.Count == 0) {
            GetOrCreateChunkByKey(GetKeyFromTransformPosition(playerTransform));
        }
    }

    private void LateUpdate () {
        for (var i = 0; i < runningJobs.Count; i++) {
            var (handle, onJobCompleted) = runningJobs[i];

            // Check if the job is no longer active
            if (handle.IsCompleted) {
                runningJobs.RemoveAt(i);

                // Tell the job system we are done with this job
                handle.Complete();

                // Send notice of the job's completion
                onJobCompleted.Invoke();

                i--;
            }
        }

        if (isColliderBakeJobRunning && colliderBakeHandle.IsCompleted) {
            isColliderBakeJobRunning = false;

            colliderBakeHandle.Complete();
        }

        if (!isColliderBakeJobRunning && meshBakingQueue.Count > 0) {
            colliderBakeJob.batchSize = Mathf.Min(ColliderBakeJob.MESHES_TO_BAKE_PER_JOB, meshBakingQueue.Count);

            for (var bakeIndex = 0; bakeIndex < colliderBakeJob.batchSize; bakeIndex++) {
                colliderBakeJob.meshIDs[bakeIndex] = meshBakingQueue.Dequeue();
            }

            // colliderBakeHandle = colliderBakeJob.Schedule();
            isColliderBakeJobRunning = true;
        }
    }

    private void OnApplicationQuit () {
        colliderBakeJob.Dispose();

        foreach (var chunk in chunks.Values) {
            chunk.Dispose();
        }
    }

    public void DeformTerrain (IBrush brush, BrushOperation operation) {
        var center = brush.GetCenter();
        var centerIndex = (int3) math.floor(center / chunkSize);

        var minIndex = centerIndex;
        var maxIndex = centerIndex;

        var c = 0;
        while (c < 10000) {
            var minPosition = minIndex * chunkSize;
            var maxPosition = maxIndex * chunkSize;

            var minChunkDistance = brush.GetDistanceToShape(minPosition);
            var maxChunkDistance = brush.GetDistanceToShape(maxPosition);

            if (minChunkDistance > chunkSize && maxChunkDistance > chunkSize) {
                break;
            }

            minIndex += math.int3(-1, -1, -1);
            maxIndex += math.int3(1, 1, 1);

            c++;
        }

        for (var x = minIndex.x; x <= maxIndex.x; x++) {
            for (var y = minIndex.x; y <= maxIndex.x; y++) {
                for (var z = minIndex.x; z <= maxIndex.x; z++) {
                    var chunk = GetOrCreateChunkByKey(new ChunkKey {origin = math.int3(x, y, z)});

                    ScheduleApplyBrushJob(chunk, brush, operation);
                }
            }
        }
    }

    private TerrainChunk GetOrCreateChunkByKey (ChunkKey key) {
        if (chunks.TryGetValue(key, out var existingChunk)) {
            return existingChunk;
        }

        var chunk = chunkPool.GetItem(PoolCallbackCreateNewChunk, PoolCallbackResetChunk);
        chunk.position = key.origin * chunkSize;
        chunk.key = key;
        chunk.meshObject.gameObject.name = $"Chunk {key.origin}";
        chunk.meshObject.transform.position = chunk.position;

        chunks.Add(key, chunk);
        ScheduleNoiseJob(chunk);

        // Fix neighbours
        foreach (var (sideIndex, side) in EnumUtil<CubeSide>.valuePairs) {
            var neighbourKey = new ChunkKey {origin = GetNeighbourChunkOrigin(chunk, side)};
            if (!chunks.TryGetValue(neighbourKey, out var neighbour)) {
                continue;
            }

            chunk.hasNeighbourOnSide[sideIndex] = true;
            chunk.neighbourKeys[sideIndex] = neighbourKey;

            var flippedSideIndex = (int) side.Flip();
            neighbour.hasNeighbourOnSide[flippedSideIndex] = true;
            neighbour.neighbourKeys[flippedSideIndex] = chunk.key;
        }

        return chunk;
    }

    private TerrainChunk PoolCallbackCreateNewChunk () {
        return new TerrainChunk {
            meshObject = Instantiate(chunkMeshObjectPrefab, transform),
            densityMap = new NativeArray<float>((chunkSize + 1).Pow(3), Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
        };
    }

    private static void PoolCallbackResetChunk (ref TerrainChunk item) {
        for (var side = 0; side < EnumUtil<CubeSide>.length; side++) {
            item.hasNeighbourOnSide[side] = false;
            item.hasSignChangeOnSide[side] = false;
            item.neighbourKeys[side] = default;
        }
    }

    private void ExpandTerrainFromChunk (TerrainChunk chunk) {
        foreach (var side in EnumUtil<CubeSide>.intValues) {
            if (chunk.hasNeighbourOnSide[side]) {
                continue;
            }

            // Skip expansions to sides that don't contain a surface, but only
            // when there already is a chunk with a surface in the terrain
            if (!chunk.hasSignChangeOnSide[side] && chunksWithSignChange > 0) {
                continue;
            }

            var newChunkOrigin = GetNeighbourChunkOrigin(chunk, (CubeSide) side);

            if (math.distance(playerTransform.position, newChunkOrigin * chunkSize) > chunkDrawDistance * chunkSize) {
                continue;
            }

            GetOrCreateChunkByKey(new ChunkKey {origin = newChunkOrigin});
        }
    }

    private static int3 GetNeighbourChunkOrigin (TerrainChunk chunk, CubeSide side) {
        return side switch {
            CubeSide.Left => chunk.key.origin + math.int3(-1, 0, 0),
            CubeSide.Right => chunk.key.origin + math.int3(1, 0, 0),
            CubeSide.Up => chunk.key.origin + math.int3(0, 1, 0),
            CubeSide.Down => chunk.key.origin + math.int3(0, -1, 0),
            CubeSide.Forward => chunk.key.origin + math.int3(0, 0, 1),
            CubeSide.Back => chunk.key.origin + math.int3(0, 0, -1),
            _ => chunk.key.origin
        };
    }

    private ChunkKey GetKeyFromTransformPosition (Transform transform) {
        var chunkOrigin = (float3) transform.position / chunkSize;

        return new ChunkKey {origin = (int3) math.floor(chunkOrigin)};
    }

    #region Jobs

    private void ScheduleApplyBrushJob (TerrainChunk chunk, IBrush brush, BrushOperation brushOperation) {
        var job = new ApplyBrushJob<SphereBrush> {
            brush = (SphereBrush) brush,
            operation = brushOperation,
            noiseMap = chunk.densityMap,
            size = chunkSize + 1,
            chunkPosition = chunk.position,
        };
        job.Construct();

        if (!chunk.jobHandles.densityMapHandle.IsCompleted) {
            chunk.jobHandles.densityMapHandle.Complete();
        }

        var handle = job.Schedule((chunkSize + 1).Pow(3), 64, chunk.jobHandles.newestJobHandle);

        runningJobs.Add((handle, OnJobCompleted));

        chunk.jobHandles.newestJobHandle = handle;

        ScheduleMarchingCubesJob(chunk);

        void OnJobCompleted () {
            // The length and width of each side
            var dataPointsPerSide = (chunkSize + 1) * (chunkSize + 1);

            foreach (var side in EnumUtil<CubeSide>.intValues) {
                chunk.hasSignChangeOnSide[side] = math.abs(job.signTrackers[side]) != dataPointsPerSide;
            }

            job.Dispose();
        }
    }

    private void ScheduleNoiseJob (TerrainChunk chunk) {
        var job = new NoiseJob {
            noiseMap = chunk.densityMap,
            surfaceLevel = noiseSettings.surfaceLevel,
            frequency = noiseSettings.freq,
            amplitude = noiseSettings.ampl,
            octaves = noiseSettings.oct,
            offset = chunk.position,
            size = chunkSize + 1
        };
        job.Construct();

        var handle = job.Schedule((chunkSize + 1).Pow(3), 64, chunk.newestJobHandle);

        runningJobs.Add((handle, OnJobCompleted));

        chunk.newestJobHandle = handle;

        ScheduleMarchingCubesJob(chunk);

        void OnJobCompleted () {
            // The length and width of each side
            var dataPointsPerSide = (chunkSize + 1) * (chunkSize + 1);
            var anySignChange = false;

            foreach (var side in EnumUtil<CubeSide>.intValues) {
                var hasSignChange = math.abs(job.signTrackers[side]) != dataPointsPerSide;

                anySignChange = anySignChange || hasSignChange;

                chunk.hasSignChangeOnSide[side] = hasSignChange;
            }

            if (anySignChange || chunksWithSignChange == 0) {
                ExpandTerrainFromChunk(chunk);
            }

            job.Dispose();
        }
    }

    private void ScheduleMarchingCubesJob (TerrainChunk chunk) {
        var job = new MarchingCubesJob {
            densities = chunk.densityMap,
            isolevel = 0f,
            chunkSize = chunkSize,
            vertexBuffer = vertexBufferPool.GetItem(CreateVertexBuffer),
            triangleIndexBuffer = triangleIndexBufferPool.GetItem(CreateTriangleIndexBuffer),
        };
        job.Construct();

        var handle = job.Schedule(chunkSize.Pow(3), 64, chunk.newestJobHandle);

        runningJobs.Add((handle, OnJobCompleted));

        chunk.newestJobHandle = handle;

        void OnJobCompleted () {
            var vertexCount = job.counter.Count * 3;

            if (vertexCount <= 0) return;

            var center = chunkSize / 2;
            var bounds = new Bounds(math.float3(center), math.float3(chunkSize * voxelSize));

            chunk.meshObject.FillMesh(vertexCount, vertexCount, job.vertexBuffer, job.triangleIndexBuffer, bounds, meshVertexLayout);

            chunksWithSignChange++;

            meshBakingQueue.Enqueue(chunk.meshObject.MeshInstanceID);

            // vertexBufferPool.AddItem(job.vertexBuffer);
            // triangleIndexBufferPool.AddItem(job.triangleIndexBuffer);

            job.Dispose();
        }

        NativeArray<Vector3> CreateVertexBuffer () => MarchingCubesJob.CreateVertexBuffer(chunkSize);
        NativeArray<int> CreateTriangleIndexBuffer () => MarchingCubesJob.CreateTriangleIndexBuffer(chunkSize);
    }

    #endregion

}

}