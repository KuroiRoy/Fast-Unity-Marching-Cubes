using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace GeneralUtils {

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MeshObject : MonoBehaviour {

    [SerializeField] private MeshCollider meshCollider;
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;

    public int MeshInstanceID => mesh.GetInstanceID();

    private bool HasCollider { get; set; }
    
    private Mesh mesh;

    private void Awake () {
        meshCollider = meshCollider != null ? meshCollider : GetComponent<MeshCollider>();
        meshFilter = meshFilter != null ? meshFilter : GetComponent<MeshFilter>();
        meshRenderer = meshRenderer != null ? meshRenderer : GetComponent<MeshRenderer>();

        HasCollider = meshCollider != null;

        CreateMesh();
    }

    private void CreateMesh () {
        if (mesh != null) {
            return;
        }

        mesh = new Mesh();
        meshFilter.sharedMesh = mesh;

        if (HasCollider) {
            meshCollider.sharedMesh = mesh;
        }
    }

    // public void SetMesh (MeshDraft meshDraft, bool setCollider = true) {
    //     CreateMesh();
    //
    //     meshDraft.ToMesh(ref mesh);
    //
    //     if (setCollider && meshCollider && meshCollider.enabled) {
    //         meshCollider.enabled = false;
    //         meshCollider.enabled = true;
    //     }
    // }

    public void FillMesh (int vertexCount, int indexCount, NativeArray<Vector3> vertices, NativeArray<int> triangles, Bounds bounds, VertexAttributeDescriptor[] meshVertexLayout) {
        mesh.SetVertexBufferParams(vertexCount, meshVertexLayout);
        mesh.SetVertexBufferData(vertices, 0, 0, vertexCount, 0, MeshUpdateFlags.DontValidateIndices);
        mesh.SetIndexBufferParams(indexCount, IndexFormat.UInt32);
        mesh.SetIndexBufferData(triangles, 0, 0, vertexCount, MeshUpdateFlags.DontValidateIndices);

        var descriptor = new SubMeshDescriptor {
            topology = MeshTopology.Triangles,
            indexCount = indexCount
        };
        mesh.SetSubMesh(0, descriptor, MeshUpdateFlags.DontValidateIndices);

        mesh.bounds = bounds;
        mesh.RecalculateNormals();

        if (HasCollider) {
            meshCollider.enabled = false;
            meshCollider.enabled = true;
        }
    }

}

}