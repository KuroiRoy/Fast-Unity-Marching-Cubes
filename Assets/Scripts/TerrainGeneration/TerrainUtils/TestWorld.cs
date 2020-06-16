using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using GeneralUtils;
using SkywardRay;
using SkywardRay.Utility;
using TerrainGeneration.Brushes;
using Unity.Mathematics;
using UnityEngine;
using WorldGeneration;

namespace TerrainGeneration.TerrainUtils {

public class TestWorld : MonoBehaviour, IDeformableTerrain, IDisposable {

    public static readonly List<IDisposable> disposeList = new List<IDisposable>();

    [SerializeField] private float voxelSize = 1;
    [SerializeField] private float chunkDrawDistance = 16;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private MeshObject chunkMeshObjectPrefab;
    [SerializeField] private NoiseSettings noiseSettings;

    private bool ShouldKeepEmptyChunks => chunksWithSignChange == 0;

    private int chunksWithSignChange;
    private readonly Pool<TerrainChunk> chunkPool = new Pool<TerrainChunk>();
    private readonly ConcurrentDictionary<ChunkKey, TerrainChunk> chunks = new ConcurrentDictionary<ChunkKey, TerrainChunk>();

    private void OnEnable () {
        disposeList.Add(this);
    }

    private void OnDestroy () {
        Dispose();

        disposeList.Remove(this);
    }

    private void Update () {
        if (chunksWithSignChange == 0 && chunks.Count == 0) {
            GetOrCreateChunkByKey(GetKeyFromTransformPosition(playerTransform));
        }
        
        var playerPosition = (float3) playerTransform.position;
        
        foreach (var chunk in chunks.Values) {
            if (chunk.runningJobs > 0) {
                continue;
            }
            
            HandleRemovalOfChunk(chunk, playerPosition);
            ExpandTerrainFromChunk(chunk);

            if (chunk.hasUpdatedDensities) {
                HandleChunkSignChangeUpdate(chunk);

                chunk.hasUpdatedDensities = false;
                chunk.hasUpdatedSigns = false;
            }
        }
    }

    private void LateUpdate () {
        foreach (var chunk in chunks.Values) {
            chunk.framesSinceJobScheduled++;
            
            if (FrameTimer.TimeElapsedThisFrame > 13 && chunk.framesSinceJobScheduled < 3) {
                continue;
            }
            
            chunk.CompleteJobs();
        }
    }

    private void HandleChunkSignChangeUpdate (TerrainChunk chunk) {
        if (!chunk.hasUpdatedSigns) {
            return;
        }

        if (chunk.hasSignChangeOnAnySide) {
            chunksWithSignChange++;
        }
        else {
            chunksWithSignChange--;
        }
    }

    private void HandleRemovalOfChunk (TerrainChunk chunk, float3 playerPosition) {
        if (chunk.hasBeenMarkedForRemoval) {
            return;
        }

        var isTooFarAway = math.distance(playerPosition, chunk.position) > chunkDrawDistance * 1.5 * TerrainChunk.CHUNK_SIZE;
        var isEmpty = !chunk.hasSignChangeOnAnySide && !ShouldKeepEmptyChunks;

        if (!isTooFarAway && !isEmpty) {
            return;
        }

        // Tell the neighbours this chunk no longer exists
        foreach (var (sideIndex, side) in EnumUtil<CubeSide>.valuePairs) {
            if (chunk.hasNeighbourOnSide[sideIndex]) {
                var neighbourKey = chunk.neighbourKeys[sideIndex];
                var neighbour = chunks[neighbourKey];
                var flippedSideIndex = (int) side.Flip();

                chunk.ClearNeighbour(sideIndex);
                neighbour.ClearNeighbour(flippedSideIndex);
            }
        }

        if (chunk.hasSignChangeOnAnySide) {
            chunksWithSignChange--;
        }

        chunk.hasBeenMarkedForRemoval = true;
        chunk.CompleteJobs();
        chunk.meshObject.gameObject.SetActive(false);

        if (chunk.hasBeenDeformed) {
            var saveData = new ChunkSaveData {
                fileKey = "world",
                chunkKey = chunk.key,
            };
            
            chunk.Save(saveData);
            ChunkSaveData.Save(saveData);
        }

        chunks.TryRemove(chunk.key, out _);
        chunkPool.AddItem(chunk);
    }

    public void DeformTerrain (IBrush brush, BrushOperation operation) {
        var bounds = brush.GetBounds();

        var minIndex = (int3) math.floor(bounds.min / (TerrainChunk.CHUNK_SIZE * voxelSize));
        var maxIndex = (int3) math.floor(bounds.max / (TerrainChunk.CHUNK_SIZE * voxelSize));

        for (var x = minIndex.x; x <= maxIndex.x; x++) {
            for (var y = minIndex.y; y <= maxIndex.y; y++) {
                for (var z = minIndex.z; z <= maxIndex.z; z++) {
                    var chunk = GetOrCreateChunkByKey(new ChunkKey {origin = math.int3(x, y, z)});

                    chunk.ScheduleApplyBrushJob(brush, operation);
                }
            }
        }
    }

    private TerrainChunk GetOrCreateChunkByKey (ChunkKey key) {
        if (chunks.TryGetValue(key, out var existingChunk)) {
            return existingChunk;
        }

        var chunk = chunkPool.GetItem(PoolCallbackCreateNewChunk, PoolCallbackResetChunk);
        chunk.position = (float3) key.origin * TerrainChunk.CHUNK_SIZE * voxelSize;
        chunk.key = key;
        chunk.meshObject.gameObject.name = $"Chunk {key.origin}";
        chunk.meshObject.transform.position = chunk.position;
        chunk.bounds = new Bounds {
            min = chunk.position,
            max = chunk.position + math.float3(chunk.size * chunk.voxelSize)
        };

        chunks.TryAdd(key, chunk);

        var loadedData = ChunkSaveData.Load("world", key);
        var dataLoadedSuccesfully = false;
        
        if (loadedData != null) {
            dataLoadedSuccesfully = chunk.Load(loadedData);
        }
        
        if (!dataLoadedSuccesfully) {
            chunk.ScheduleGenerateDensitiesJob(noiseSettings);
        }

        // Fix neighbours
        foreach (var side in EnumUtil<CubeSide>.valuePairs) {
            var neighbourKey = new ChunkKey {origin = GetNeighbourChunkOrigin(chunk, side.value)};
            if (!chunks.TryGetValue(neighbourKey, out var neighbour)) {
                continue;
            }

            chunk.SetNeighbour(neighbour, side);
        }

        return chunk;
    }

    private TerrainChunk PoolCallbackCreateNewChunk () {
        return new TerrainChunk(TerrainChunk.CHUNK_SIZE, voxelSize, Instantiate(chunkMeshObjectPrefab, transform));
    }

    private static void PoolCallbackResetChunk (ref TerrainChunk chunk) {
        chunk.hasBeenMarkedForRemoval = false;
        chunk.hasUpdatedSigns = false;
        chunk.hasUpdatedDensities = false;
        chunk.hasSignChangeOnAnySide = false;

        for (var side = 0; side < EnumUtil<CubeSide>.length; side++) {
            chunk.hasNeighbourOnSide[side] = false;
            chunk.hasSignChangeOnSide[side] = false;
            chunk.neighbourKeys[side] = default;
        }
    }

    private void ExpandTerrainFromChunk (TerrainChunk chunk) {
        if (!chunk.canExpandToNeighbours) {
            return;
        }

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

            if (math.distance(playerTransform.position, (float3) newChunkOrigin * (TerrainChunk.CHUNK_SIZE * voxelSize)) >
                chunkDrawDistance * (TerrainChunk.CHUNK_SIZE * voxelSize)) {
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
        var chunkOrigin = (float3) transform.position / (TerrainChunk.CHUNK_SIZE * voxelSize);

        return new ChunkKey {origin = (int3) math.floor(chunkOrigin)};
    }

    public void Dispose () {
        foreach (var chunk in chunks.Values) {
            chunk.Dispose();
        }

        foreach (var chunk in chunkPool.items) {
            chunk.Dispose();
        }
    }

}

}