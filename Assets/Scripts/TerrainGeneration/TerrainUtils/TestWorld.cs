using System.Collections.Generic;
using GeneralUtils;
using SkywardRay;
using SkywardRay.Utility;
using TerrainGeneration.Brushes;
using Unity.Mathematics;
using UnityEngine;
using WorldGeneration;

namespace TerrainGeneration.TerrainUtils {

public class TestWorld : MonoBehaviour, IDeformableTerrain {

    [SerializeField] private int chunkSize = 32;
    [SerializeField] private float voxelSize = 1;
    [SerializeField] private float chunkDrawDistance = 16;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private MeshObject chunkMeshObjectPrefab;
    [SerializeField] private NoiseSettings noiseSettings;

    private int chunksWithSignChange;
    private readonly DisposablePool<TerrainChunk> chunkPool = new DisposablePool<TerrainChunk>();
    private readonly Dictionary<ChunkKey, TerrainChunk> chunks = new Dictionary<ChunkKey, TerrainChunk>();
    private readonly Queue<ChunkKey> chunksToRemove = new Queue<ChunkKey>();
    private readonly Queue<ChunkKey> chunksToExpandFrom = new Queue<ChunkKey>();

    private void Update () {
        if (chunksWithSignChange == 0 && chunks.Count == 0) {
            GetOrCreateChunkByKey(GetKeyFromTransformPosition(playerTransform));
        }
    }

    private void LateUpdate () {
        var playerPosition = (float3) playerTransform.position;
        
        foreach (var (chunkKey, chunk) in chunks) {
            chunk.UpdateJobs();
            
            if (chunk.hasBeenMarkedForRemoval) {
                continue;
            }
            
            if (chunk.hasUpdatedSigns) {
                if (chunk.hasSignChangeOnAnySide) {
                    chunksToExpandFrom.Enqueue(chunkKey);
                    
                    chunksWithSignChange++;
                }
                else {
                    chunksWithSignChange--;
                }
            }
            else if (chunksWithSignChange == 0 && chunk.hasUpdatedDensities) {
                chunksToExpandFrom.Enqueue(chunkKey);
            }

            if (!chunk.hasBeenMarkedForRemoval && math.distance(playerPosition, chunk.position) > chunkDrawDistance * 1.5 * chunkSize) {
                chunk.hasBeenMarkedForRemoval = true;

                if (chunk.hasSignChangeOnAnySide) {
                    chunksWithSignChange--;
                }
                
                chunksToRemove.Enqueue(chunkKey);
            }

            chunk.hasUpdatedDensities = false;
            chunk.hasUpdatedSigns = false;
        }

        var chunksToRemoveCount = chunksToRemove.Count;
        var chunksToExpandFromCount = chunksToExpandFrom.Count;

        for (var i = 0; i < chunksToRemoveCount; i++) {
            var chunkKey = chunksToRemove.Peek();
            var chunk = chunks[chunkKey];

            if (chunk.runningJobs == 0) {
                chunks.Remove(chunkKey);
                chunksToRemove.Dequeue();
            }
        }

        for (var i = 0; i < chunksToExpandFromCount; i++) {
            var chunkKey = chunksToExpandFrom.Dequeue();
            
            ExpandTerrainFromChunk(chunks[chunkKey]);
        }
    }

    private void OnApplicationQuit () {
        foreach (var chunk in chunks.Values) {
            chunk.Dispose();
        }
        
        chunkPool.Dispose();
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
        chunk.position = key.origin * chunkSize;
        chunk.key = key;
        chunk.meshObject.gameObject.name = $"Chunk {key.origin}";
        chunk.meshObject.transform.position = chunk.position;

        chunks.Add(key, chunk);
        
        chunk.ScheduleGenerateDensitiesJob(noiseSettings);

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
        return new TerrainChunk(chunkSize, voxelSize, Instantiate(chunkMeshObjectPrefab, transform));
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

}

}