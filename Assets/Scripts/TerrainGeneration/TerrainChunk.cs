using System;
using GeneralUtils;
using SkywardRay;
using SkywardRay.Utility;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using WorldGeneration;

namespace TerrainGeneration {

public class TerrainChunk : IDisposable {

    public MeshObject meshObject;
    public float3 position;
    public ChunkKey key;
    public readonly bool[] hasSignChangeOnSide = new bool[EnumUtil<CubeSide>.length];
    public readonly bool[] hasNeighbourOnSide = new bool[EnumUtil<CubeSide>.length];
    public readonly ChunkKey[] neighbourKeys = new ChunkKey[EnumUtil<CubeSide>.length];
    
    public ChunkJobHandles jobHandles = default;
    public NativeArray<float> densityMap;

    public bool needsExpansion;

    public void Dispose () {
        densityMap.Dispose();
    }

    public struct ChunkJobHandles {

        public JobHandle newestJobHandle;
        public JobHandle densityMapHandle;
        public JobHandle meshHandle;
        public JobHandle collisionHandle;

    }

}

}