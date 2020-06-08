using System;
using System.Runtime.CompilerServices;
using TerrainGeneration.TerrainUtils;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using WorldGeneration;

namespace TerrainGeneration.Jobs {

[BurstCompile]
public struct FillMeshJob : IJobParallelFor {

    [NativeDisableParallelForRestriction, ReadOnly, DeallocateOnJobCompletion]
    public Counter vertexCount;
    
    /// <summary>
    /// The generated vertices
    /// </summary>
    [NativeDisableParallelForRestriction, ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<Vector3> vertexBuffer;

    /// <summary>
    /// The generated triangles
    /// </summary>
    [NativeDisableParallelForRestriction, ReadOnly, DeallocateOnJobCompletion]
    public NativeArray<int> triangleIndexBuffer;

    /// <summary>
    /// The execute method required by the Unity Job System's IJobParallelFor
    /// </summary>
    /// <param name="index">The iteration index</param>
    public void Execute (int index) {
        
    }

}

}