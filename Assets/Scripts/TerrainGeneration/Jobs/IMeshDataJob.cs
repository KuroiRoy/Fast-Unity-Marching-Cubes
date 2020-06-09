using Unity.Collections;
using UnityEngine;

namespace TerrainGeneration.Jobs {

public interface IMeshDataJob {

    NativeArray<Vector3> GetVertexBuffer ();
    NativeArray<int> GetTriangleIndexBuffer ();

}

}