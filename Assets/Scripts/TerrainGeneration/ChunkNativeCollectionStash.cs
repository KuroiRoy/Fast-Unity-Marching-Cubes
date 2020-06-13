using System;
using Unity.Collections;
using UnityEngine;
using WorldGeneration;

namespace TerrainGeneration {

public class ChunkNativeCollectionStash : MonoBehaviour {

    [NonSerialized] public NativeArray<float> densityMap;
    [NonSerialized] public Counter vertexCounter;

    private void OnDestroy () {
        densityMap.Dispose();
        vertexCounter.Dispose();
    }

}

}