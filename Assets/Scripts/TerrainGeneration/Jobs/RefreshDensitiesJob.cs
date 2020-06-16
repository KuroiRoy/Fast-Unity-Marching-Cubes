using SkywardRay;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace TerrainGeneration.Jobs {

[BurstCompile]
public struct RefreshDensitiesJob : IJobParallelFor, IDensityJob {
    
    [ReadOnly] public int size;

    [NativeDisableParallelForRestriction, ReadOnly]
    public NativeArray<float> densityMap;

    /// <summary>
    /// The amount of positive or negative densities per side
    /// </summary>
    [NativeDisableParallelForRestriction] 
    public NativeArray<int> signTrackers;

    public void Execute (int index) {
        var positionInChunk = new int3(index / (size * size), index / size % size, index % size);

        TrackSign(positionInChunk, densityMap[index]);
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