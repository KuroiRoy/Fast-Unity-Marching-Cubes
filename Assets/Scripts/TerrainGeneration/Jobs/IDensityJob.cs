using Unity.Collections;

namespace TerrainGeneration.Jobs {

public interface IDensityJob {

    public NativeArray<int> GetSignTrackers ();

}

}