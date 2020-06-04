using SkywardRay;
using SkywardRay.Utility;
using Unity.Collections;
using Unity.Mathematics;
using WorldGeneration;

namespace UnityTemplateProjects.TerrainGeneration {

public class TerrainChunk {

    public MeshObject meshObject;
    public NativeArray<float> noiseMap;
    public float3 position;
    public ChunkKey key;
    public readonly bool[] hasSignChangeOnSide = new bool[EnumUtil<CubeSide>.length];
    public readonly bool[] hasNeighbourOnSide = new bool[EnumUtil<CubeSide>.length];
    public readonly ChunkKey[] neighbourKeys = new ChunkKey[EnumUtil<CubeSide>.length];

}

}