using System;
using System.Collections.Generic;
using SkywardRay;
using SkywardRay.Utility;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityTemplateProjects.TerrainGeneration;

namespace WorldGeneration {

public class TestWorld : MonoBehaviour {

    private static readonly VertexAttributeDescriptor[] meshVertexLayout = {new VertexAttributeDescriptor(VertexAttribute.Position)};

    [SerializeField] private int chunkSize = 32;
    [SerializeField] private float voxelSize = 1;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private MeshObject chunkMeshObjectPrefab;
    [SerializeField] private NoiseSettings noiseSettings;

    private int chunksWithSignChange = 0;
    private JobPool<MarchingCubesJob> marchingCubesJobs;
    private JobPool<NoiseJob> noiseJobs;
    private readonly Pool<TerrainChunk> chunkPool = new Pool<TerrainChunk>();
    private readonly Dictionary<ChunkKey, TerrainChunk> chunks = new Dictionary<ChunkKey, TerrainChunk>();

    private void Start () {
        noiseJobs = new JobPool<NoiseJob>((chunkSize + 1).Pow(3), 64);
        marchingCubesJobs = new JobPool<MarchingCubesJob>(chunkSize.Pow(3), 64);
    }

    private void Update () {
        if (chunksWithSignChange == 0 && chunks.Count == 0) {
            CreateChunkByKey(GetKeyFromTransformPosition(playerTransform));
            // StartGenerate();
        }
    }

    private void LateUpdate () {
        noiseJobs.Update();
        marchingCubesJobs.Update();
    }

    private void OnApplicationQuit () {
        noiseJobs.Dispose();
        marchingCubesJobs.Dispose();
    }

    private void CreateChunkByKey (ChunkKey key) {
        if (chunks.ContainsKey(key)) {
            return;
        }

        var chunk = chunkPool.GetItem(PoolCallbackCreateNewChunk, PoolCallbackResetChunk);
        chunk.position = key.origin * chunkSize;
        chunk.key = key;
        chunk.meshObject.gameObject.name = $"Chunk {key.origin}";
        chunk.meshObject.transform.position = chunk.position;

        chunks.Add(key, chunk);
        ScheduleChunkJobs(chunk);

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
    }

    private TerrainChunk PoolCallbackCreateNewChunk () {
        return new TerrainChunk {
            meshObject = Instantiate(chunkMeshObjectPrefab, transform),
            noiseMap = new NativeArray<float>((chunkSize + 1).Pow(3), Allocator.Persistent, NativeArrayOptions.UninitializedMemory)
        };
    }

    private void PoolCallbackResetChunk (ref TerrainChunk item) {
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

            CreateChunkByKey(new ChunkKey {origin = GetNeighbourChunkOrigin(chunk, (CubeSide) side)});
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

    private void StartGenerate () {
        var position = Vector3.zero;
        var amount = 20;
        
        var rootPosX = Mathf.FloorToInt(position.x / chunkSize) * chunkSize;
        var rootPosY = Mathf.FloorToInt(position.y / chunkSize) * chunkSize;
        var rootPosZ = Mathf.FloorToInt(position.z / chunkSize) * chunkSize;
        for (var x = -amount / 2; x < amount / 2; x++) {
            for (var y = -amount / 2; y < amount / 2; y++) {
                for (var z = -amount / 2; z < amount / 2; z++) {
                    var pos = new int3(rootPosX + x * chunkSize, rootPosY + y * chunkSize, rootPosZ + z * chunkSize);
        
                    if (math.distance(pos, position + Vector3.up) < 160) {
                        ScheduleChunkJobs(new TerrainChunk {
                            meshObject = Instantiate(chunkMeshObjectPrefab, (float3) pos, Quaternion.identity, transform),
                            noiseMap = new NativeArray<float>((chunkSize + 1).Pow(3), Allocator.Persistent, NativeArrayOptions.UninitializedMemory),
                            position = pos
                        });
                    }
                }
            }
        }
    }

    private void ScheduleChunkJobs (TerrainChunk chunk) {
        var noiseJobHandle = noiseJobs.ScheduleJob(InitializeNoiseJob, CompleteNoiseJob);

        marchingCubesJobs.ScheduleJob(InitializeMarchingCubesJob, CompleteMarchingCubesJob, noiseJobHandle);

        void InitializeNoiseJob (ref NoiseJob job) {
            job.noiseMap = chunk.noiseMap;
            job.surfaceLevel = noiseSettings.surfaceLevel;
            job.frequency = noiseSettings.freq;
            job.amplitude = noiseSettings.ampl;
            job.octaves = noiseSettings.oct;
            job.offset = chunk.position;
            job.size = chunkSize + 1;

            for (var i = 0; i < EnumUtil<CubeSide>.length; i++) {
                job.signTrackers[i] = 0;
            }
        }

        void CompleteNoiseJob (ref NoiseJob job) {
            // The length and width of each side
            var dataPointsPerSide = (chunkSize + 1) * (chunkSize + 1);

            foreach (var side in EnumUtil<CubeSide>.intValues) {
                chunk.hasSignChangeOnSide[side] = math.abs(job.signTrackers[side]) != dataPointsPerSide;
            }
        }

        void InitializeMarchingCubesJob (ref MarchingCubesJob job) {
            job.densities = chunk.noiseMap;
            job.isolevel = 0f;
            job.chunkSize = chunkSize;
            job.counter.Count = 0;

            if (job.vertices == null || job.vertices.Length == 0) {
                job.vertices = new NativeArray<Vector3>(chunkSize.Pow(3) * 3 * 5, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

            if (job.triangles == null || job.triangles.Length == 0) {
                job.triangles = new NativeArray<int>(chunkSize.Pow(3) * 3 * 5, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }
        }

        void CompleteMarchingCubesJob (ref MarchingCubesJob job) {
            var vertexCount = job.counter.Count * 3;

            if (vertexCount > 0) {
                var center = chunkSize / 2;
                var bounds = new Bounds(math.float3(center), math.float3(chunkSize * voxelSize));

                chunk.meshObject.FillMesh(vertexCount, vertexCount, job.vertices, job.triangles, bounds, meshVertexLayout);

                chunksWithSignChange++;
            }

            if (vertexCount > 0 || chunksWithSignChange == 0) {
                ExpandTerrainFromChunk(chunk);
            }

            //Start collider baking
            // var colliderJob = new ChunkColliderBakeJob() {
            //     meshId = mesh.GetInstanceID()
            // };
            // colliderHandle = colliderJob.Schedule();
            // meshBaking = true;
        }
    }

    [Serializable]
    public class NoiseSettings {

        public float surfaceLevel;
        public float freq;
        public float ampl;
        public int oct;
        public Vector3 offset;

    }

}

}