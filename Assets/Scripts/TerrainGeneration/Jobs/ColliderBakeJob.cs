using System;
using TerrainGeneration.TerrainUtils;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace TerrainGeneration.Jobs {

//A job to bake chunk colliders
public struct ColliderBakeJob : IJob, IConstructable, IDisposable {

    public const int MESHES_TO_BAKE_PER_JOB = 10;

    [ReadOnly] public NativeArray<int> meshIDs;

    [ReadOnly] public int batchSize;

    public void Execute () {
        for (var i = 0; i < batchSize; i++) {
            Physics.BakeMesh(meshIDs[i], false);
        }
    }

    public void Construct () {
        meshIDs = new NativeArray<int>(MESHES_TO_BAKE_PER_JOB, Allocator.Persistent);
    }

    public void Dispose () {
        meshIDs.Dispose();
    }

}

}