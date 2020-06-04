using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WorldGeneration {

//Chunk noisemap update job, in a ball shape. Could implement more complex logic for this as well.
[BurstCompile]
public struct ChunkExplodeJob : IJobParallelFor {

    //Chunk's noisemap to edit.
    public NativeArray<float> noiseMap;

    //Chunk size + 1 to account for borders
    [ReadOnly] public int size;
    
    [ReadOnly] public int isolevel;

    //New density value
    [ReadOnly] public float newDensity;

    //Where the "explosion" happens, in local coordinates relative to chunk
    [ReadOnly] public int3 explosionOrigin;

    //How big of an explosion should happen
    [ReadOnly] public float explosionRange;

    public void Execute (int index) {
        var pos = new float3(index / (size * size), index / size % size, index % size);
        var distance = math.distance(pos, explosionOrigin);
        
        if (newDensity < isolevel) {
            noiseMap[index] = math.min(noiseMap[index], distance - explosionRange);
        }
        else {
            noiseMap[index] = math.max(noiseMap[index], explosionRange - distance);
        }
    }

}

}