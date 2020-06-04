using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace WorldGeneration {

//Infinite world logic
public class InfiniteWorld : WorldBase {

    //How far should chunks be spawned.
    public float chunkDrawDistance;

    //Because foreach, we cant remove chunks from currentChunks straight away. Need to store the values to this list and remove after the loop
    private readonly List<int3> toRemove = new List<int3>();

    private void Start () {
        StartGenerate();
    }

    //Create new chunk objects at startup
    private void StartGenerate () {
        var position = player.position;
        var amount = Mathf.FloorToInt(chunkDrawDistance / chunkSize);
        
        var rootPosX = Mathf.FloorToInt(position.x / chunkSize) * chunkSize;
        var rootPosY = Mathf.FloorToInt(position.y / chunkSize) * chunkSize;
        var rootPosZ = Mathf.FloorToInt(position.z / chunkSize) * chunkSize;
        for (var x = -amount / 2; x < amount / 2; x++) {
            for (var y = -amount / 2; y < amount / 2; y++) {
                for (var z = -amount / 2; z < amount / 2; z++) {
                    var pos = new int3(rootPosX + x * chunkSize, rootPosY + y * chunkSize, rootPosZ + z * chunkSize);
                    if (math.distance(pos, player.position + Vector3.up) < chunkDrawDistance / 2)
                        GenerateChunk(pos);
                }
            }
        }
    }

    public override void UpdateChunks () {
        toRemove.Clear();

        foreach (var pos in currentChunks.Keys) {
            if (math.distance(pos, player.position + Vector3.up) > chunkDrawDistance / 2) {
                toRemove.Add(pos);
            }
        }

        toRemove.ForEach(x => currentChunks[x].gameObject.SetActive(false));
        
        var position = player.position;
        var amount = Mathf.FloorToInt(chunkDrawDistance / chunkSize);
        var rootPosX = Mathf.FloorToInt(position.x / chunkSize) * chunkSize;
        var rootPosY = Mathf.FloorToInt(position.y / chunkSize) * chunkSize;
        var rootPosZ = Mathf.FloorToInt(position.z / chunkSize) * chunkSize;
        
        for (var x = -amount / 2; x < amount / 2; x++) {
            for (var y = -amount / 2; y < amount / 2; y++) {
                for (var z = -amount / 2; z < amount / 2; z++) {
                    //If no chunks are pooled, don't do anything and wait for next frame instead. Could also be set to spawn new chunks, but that wasn't necessary
                    if (freeChunks.Count == 0)
                        return;
                    var pos = new int3(rootPosX + x * chunkSize, rootPosY + y * chunkSize, rootPosZ + z * chunkSize);
                    //If there is a chunk at this position already, don't do anything
                    if (currentChunks.ContainsKey(pos))
                        continue;
                    //Check if chunk is close enough.
                    if (math.distance(pos, player.position + Vector3.up) < chunkDrawDistance / 2) {
                        //Get pooled chunk from the queue.
                        var chunk = freeChunks.Dequeue();
                        chunk.gameObject.transform.position = (float3)pos;
                        chunk.gameObject.SetActive(true);
                        chunk.pos = pos;
                    }
                }
            }
        }
    }

}

}