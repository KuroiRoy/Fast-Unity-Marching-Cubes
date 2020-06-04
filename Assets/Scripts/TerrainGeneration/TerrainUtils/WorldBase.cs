using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System;

namespace WorldGeneration {

[Serializable]
public class NoiseData {

    public float surfaceLevel;
    public float freq;
    public float ampl;
    public int oct;
    public float offset;

}

public class WorldBase : MonoBehaviour {

    public Material voxelMaterial;
    public ChunkData chunkPrefab;
    internal int chunkSize = 0;

    public Transform player;
    private Vector3 lastPos;

    [SerializeField] private NoiseData _noiseData;
    public static NoiseData noiseData;

    //Currently visible chunks
    public static Dictionary<int3, ChunkData> currentChunks = new Dictionary<int3, ChunkData>();

    //Pooled chunks free for use
    public static Queue<ChunkData> freeChunks = new Queue<ChunkData>();

    private void Init () {
        chunkSize = chunkPrefab.GetComponent<ChunkData>().size;
        noiseData = _noiseData;
    }

    //Efficient way of modifying terrain in a ball shape
    public void ModifyTerrainBallShape (int3 point, float range, float densitySign) {
        var hitChunkPos = PositionToChunkCoordinate(point);
        var intRange = Mathf.CeilToInt(range / chunkSize);

        for (var x = -intRange; x <= intRange; x++) {
            for (var y = -intRange; y <= intRange; y++) {
                for (var z = -intRange; z <= intRange; z++) {
                    var chunkPos = new int3(hitChunkPos.x + x * chunkSize, hitChunkPos.y + y * chunkSize, hitChunkPos.z + z * chunkSize);
                    if (currentChunks.TryGetValue(chunkPos, out var chunk)) {
                        chunk.Explode(point, range, densitySign);
                    }
                }
            }
        }
    }

    private int3 PositionToChunkCoordinate (int3 pos) {
        return pos.FloorToMultipleOfX(chunkSize);
    }

    private void Awake () {
        Init();
    }

    //Creates new chunk objects at startup.
    public void GenerateChunk (int3 pos) {
        var chunk = Instantiate(chunkPrefab, (float3)pos, Quaternion.identity, transform);
        chunk.pos = pos;
    }

    public virtual void UpdateChunks () { }

    private void Update () {
        //Check if player has moved enough to require an update
        if (Vector3.Distance(lastPos, player.position) > chunkSize) {
            UpdateChunks();
            lastPos = player.position;
        }
    }

}

}