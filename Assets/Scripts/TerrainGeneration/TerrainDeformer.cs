using Unity.Mathematics;
using UnityEngine;

public class TerrainDeformer : MonoBehaviour {

    public WorldGeneration.WorldBase worldSetup;
    public float deformRadius;
    private Camera _camera;

    private void Start () {
        _camera = Camera.main;
    }

    private void Update () {
        var ray = _camera.ScreenPointToRay(Input.mousePosition);

        if (Input.GetKeyDown(KeyCode.C)) {
            if (Physics.Raycast(ray, out var hit, 100f)) {
                worldSetup.ModifyTerrainBallShape((int3) math.floor(hit.point), deformRadius, 1f);
            }
        }
        else if (Input.GetKeyDown(KeyCode.F)) {
            if (Physics.Raycast(ray, out var hit, 100f)) {
                worldSetup.ModifyTerrainBallShape((int3) math.floor(hit.point), deformRadius, -1f);
            }
        }
    }

}