using UnityEngine;

/// <summary>
/// Bakes the posed SkinnedMeshRenderer into a MeshCollider at runtime/editor start,
/// ensuring all physical raycasts, closest-point checks, and decal projections
/// conform directly and with 100% precision to the mother's visible 3D anatomy.
/// </summary>
[RequireComponent(typeof(SkinnedMeshRenderer))]
public class PosedMeshColliderBaker : MonoBehaviour
{
    private SkinnedMeshRenderer skinnedRenderer;
    private MeshCollider meshCollider;
    private Mesh bakedMesh;

    private void Awake()
    {
        BakeCollider();
    }

    public void BakeCollider()
    {
        if (skinnedRenderer == null) skinnedRenderer = GetComponent<SkinnedMeshRenderer>();
        if (meshCollider == null) meshCollider = GetComponent<MeshCollider>();

        if (meshCollider == null) meshCollider = gameObject.AddComponent<MeshCollider>();

        if (skinnedRenderer != null)
        {
            if (bakedMesh == null) bakedMesh = new Mesh();
            skinnedRenderer.BakeMesh(bakedMesh);
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = bakedMesh;
            Debug.Log($"[PosedMeshColliderBaker] Successfully baked posed mesh collider on '{gameObject.name}' ({bakedMesh.vertexCount} vertices).");
        }
    }
}
