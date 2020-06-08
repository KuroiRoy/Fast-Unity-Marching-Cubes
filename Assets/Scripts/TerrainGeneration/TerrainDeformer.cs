using System;
using TerrainGeneration.Brushes;
using TerrainGeneration.TerrainUtils;
using UnityEngine;

namespace TerrainGeneration {

public class TerrainDeformer : MonoBehaviour {

    [SerializeField] private float radius;
    [SerializeField] private TestWorld terrain;
    [SerializeField] private new Camera camera;

    private bool didHit;
    private Vector3 hitPoint;

    private void Update () {
        var ray = camera.ScreenPointToRay(Input.mousePosition);

        didHit = Physics.Raycast(ray, out var hit, float.PositiveInfinity);
        hitPoint = hit.point;
        
        if (Input.GetKeyDown(KeyCode.C)) {
            DeformTerrain(BrushOperation.Difference);
        }
        else if (Input.GetKeyDown(KeyCode.F)) {
            DeformTerrain(BrushOperation.Union);
        }
    }

    private void OnDrawGizmos () {
        if (didHit) {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(hitPoint, radius);
        }
    }

    private void DeformTerrain (BrushOperation operation) {
        var brush = new SphereBrush {
            origin = hitPoint,
            radius = radius,
        };

        terrain.DeformTerrain(brush, operation);
    }

}

}