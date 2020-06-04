using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace WorldGeneration {

//A job to bake chunk colliders
public struct ChunkColliderBakeJob : IJob {

    [ReadOnly] public int meshId;

    public void Execute () {
        Physics.BakeMesh(meshId, false);
    }

}

}