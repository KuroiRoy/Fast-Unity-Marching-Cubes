using Unity.Mathematics;

namespace TerrainGeneration.Brushes {

public interface IBrush {

    float3 GetCenter ();

    float GetDistanceToShape (float3 position);

}

}