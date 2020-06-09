using Unity.Mathematics;
using UnityEngine;

namespace TerrainGeneration.Brushes {

public struct SphereBrush : IBrush {

    public float3 origin;
    public float radius;

    public Bounds GetBounds () {
        return new Bounds(origin, math.float3(radius * 2));
    }

    public float3 GetCenter () {
        return origin;
    }

    public float GetDistanceToShape (float3 position) {
        return math.distance(origin, position) - radius;
    }

}

}