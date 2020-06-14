using System;
using SkywardRay;
using TerrainGeneration.Brushes;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TerrainGeneration.Jobs {

//Chunk noisemap update job, in a ball shape. Could implement more complex logic for this as well.
[BurstCompile]
public struct ApplyBrushJob<TBrush> : IJobParallelFor, IDensityJob where TBrush : struct, IBrush {

    //Chunk's noisemap to edit.
    public NativeArray<float> densityMap;

    [ReadOnly] public float3 chunkPosition;

    //Chunk size + 1 to account for borders
    [ReadOnly] public int size;

    [ReadOnly] public float voxelSize;

    [ReadOnly] public TBrush brush;

    [ReadOnly] public BrushOperation operation;

    /// <summary>
    /// The amount of positive or negative densities per side
    /// </summary>
    [NativeDisableParallelForRestriction]
    public NativeArray<int> signTrackers;

    public void Execute (int index) {
        var position = chunkPosition + new float3(index / (size * size), index / size % size, index % size) * voxelSize;

        var distanceToShape = brush.GetDistanceToShape(position);

        var newDensity = operation switch {
            BrushOperation.Union => math.min(densityMap[index], distanceToShape),
            BrushOperation.Difference => math.max(densityMap[index], -distanceToShape),
            _ => throw new ArgumentOutOfRangeException()
        };

        TrackSign(index, newDensity);

        densityMap[index] = newDensity;
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

    public NativeArray<int> GetSignTrackers () {
        return signTrackers;
    }

}

}