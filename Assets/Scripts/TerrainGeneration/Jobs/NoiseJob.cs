using SkywardRay;
using SkywardRay.Utility;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace WorldGeneration {

//Calculate noise in jobs
[BurstCompile]
public struct NoiseJob : IJobParallelFor, IConstructableJob, IDisposableJob {

    [ReadOnly] public float surfaceLevel;
    [ReadOnly] public float3 offset;
    [ReadOnly] public float3 seed;
    [ReadOnly] public float amplitude;
    [ReadOnly] public float frequency;
    [ReadOnly] public int octaves;
    [ReadOnly] public int size;

    [NativeDisableParallelForRestriction, WriteOnly]
    public NativeArray<float> noiseMap;

    /// <summary>
    /// The amount of positive or negative densities per side
    /// </summary>
    [NativeDisableParallelForRestriction]
    public NativeArray<int> signTrackers;

    public void Construct () {
        signTrackers = new NativeArray<int>(EnumUtil<CubeSide>.length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
    }

    public void Dispose () {
        noiseMap.Dispose();
    }

    public void Execute (int index) {
        var positionInChunk = new int3(index / (size * size), index / size % size, index % size);
        var density = FinalNoise(positionInChunk);
        
        TrackSign(positionInChunk, density);
                
        noiseMap[index] = density;
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

    private float FinalNoise (float3 pos) {
        pos += offset + seed;

        var value = pos.y - surfaceLevel - PerlinNoise3D(pos);

        return value;
    }

    private float PerlinNoise3D (float3 pos) {
        float total = 0;
        var currentAmplitude = amplitude;
        var currentFrequency = frequency;
        for (var i = 0; i < octaves; i++) {
            total += noise.snoise(pos * currentFrequency) * currentAmplitude;

            currentAmplitude *= 2;
            currentFrequency *= 0.5f;
        }

        return total;
    }

    private float PerlinNoise3DSnake (float3 pos) {
        float total = 0;
        var currentAmplitude = amplitude;
        var currentFrequency = frequency * 0.5f;
        for (var i = 0; i < octaves; i++) {
            total += noise.snoise(pos * currentFrequency) * currentAmplitude;

            currentAmplitude *= 2;
            currentFrequency *= 0.5f;
        }

        return total;
    }

    private float SurfaceNoise2D (float x, float z) {
        float total = 0;
        var currentAmplitude = amplitude;
        var currentFrequency = frequency;
        for (var i = 0; i < octaves; i++) {
            total += noise.snoise(math.float2((x + offset.x + seed.x) * currentFrequency, (z + offset.z + seed.z) * currentFrequency) * currentAmplitude);

            currentAmplitude *= 2;
            currentFrequency *= 0.5f;
        }

        return total;
    }

}

}