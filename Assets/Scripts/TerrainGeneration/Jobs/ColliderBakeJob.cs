using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace TerrainGeneration.Jobs {

public struct ColliderBakeJob : IJob {

    [ReadOnly] public int meshID;

    public void Execute () {
        Physics.BakeMesh(meshID, false);
    }

}

}