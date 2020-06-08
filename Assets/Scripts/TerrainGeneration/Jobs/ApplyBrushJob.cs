using System;
using SkywardRay;
using SkywardRay.Utility;
using TerrainGeneration.Brushes;
using TerrainGeneration.TerrainUtils;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TerrainGeneration.Jobs {

//Chunk noisemap update job, in a ball shape. Could implement more complex logic for this as well.
[BurstCompile]
public struct ApplyBrushJob<TBrush> : IJobParallelFor, IConstructable, IDisposable where TBrush : struct, IBrush {

    //Chunk's noisemap to edit.
    public NativeArray<float> noiseMap;

    [ReadOnly] public float3 chunkPosition;

    //Chunk size + 1 to account for borders
    [ReadOnly] public int size;

    [ReadOnly] public TBrush brush;

    [ReadOnly] public BrushOperation operation;

    /// <summary>
    /// The amount of positive or negative densities per side
    /// </summary>
    [NativeDisableParallelForRestriction]
    public NativeArray<int> signTrackers;

    public void Execute (int index) {
        var position = chunkPosition + new float3(index / (size * size), index / size % size, index % size);

        var distanceToShape = brush.GetDistanceToShape(position);

        var newDensity = operation switch {
            BrushOperation.Union => math.min(noiseMap[index], distanceToShape),
            BrushOperation.Difference => math.max(noiseMap[index], -distanceToShape),
            _ => throw new ArgumentOutOfRangeException()
        };

        TrackSign(index, newDensity);

        noiseMap[index] = newDensity;
    }

    private void TrackSign (int3 index, float density) {
        var sign = density <= 0 ? -1 : 1;

        if (index.x == 0) {
            signTrackers[(int) CubeSide.Left] += sign;
        }

        if (index.x == size - 1) {
            signTrackers[(int) CubeSide.Right] += sign;
        }

        if (index.y == 0) {
            signTrackers[(int) CubeSide.Down] += sign;
        }

        if (index.y == size - 1) {
            signTrackers[(int) CubeSide.Up] += sign;
        }

        if (index.z == 0) {
            signTrackers[(int) CubeSide.Back] += sign;
        }

        if (index.z == size - 1) {
            signTrackers[(int) CubeSide.Forward] += sign;
        }
    }

    public void Construct () {
        signTrackers = new NativeArray<int>(EnumUtil<CubeSide>.length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    }

    public void Dispose () {
        signTrackers.Dispose();
    }

}

}