using Unity.Mathematics;
using UnityEngine;

namespace TerrainGeneration.Brushes {

public interface IBrush {
    
    Bounds GetBounds ();

    float3 GetCenter ();

    float GetDistanceToShape (float3 position);

}

}