using Unity.Mathematics;

namespace TerrainGeneration.Brushes {

public struct SphereBrush : IBrush {

    public float3 origin;
    public float radius;

    public float3 GetCenter () {
        return origin;
    }

    public float GetDistanceToShape (float3 position) {
        return math.distance(origin, position) - radius;
    }

}

}