using Unity.Mathematics;

namespace TerrainGeneration.Brushes {

public interface IDeformableTerrain {

    void DeformTerrain (IBrush brush, BrushOperation operation);

}

}