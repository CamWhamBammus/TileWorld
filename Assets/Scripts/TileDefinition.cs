using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Tiles/Tile Definition")]
public class TileDefinition : ScriptableObject
{
    public int blockID;
    public GameObject prefab;

    [SerializeField] private Mesh mesh;
    [SerializeField] private Material material;

    private void OnValidate()
    {
        BuildFromPrefab();
    }

    public Mesh MeshGetter()
    {
        if (mesh == null)
        {
            BuildFromPrefab();
        }

        return mesh;
    }

    public Material MaterialGetter()
    {
        if (material == null)
        {
            BuildFromPrefab();
        }

        if (material != null)
        {
            material.enableInstancing = true;
        }

        return material;
    }

    public void BuildFromPrefab()
    {
        if (prefab == null)
        {
            mesh = null;
            material = null;
            return;
        }

        MeshFilter[] meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
        MeshRenderer[] renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);

        if (meshFilters.Length == 0)
        {
            Debug.LogWarning("[TileDefinition] No MeshFilters found on prefab: " + prefab.name);
            mesh = null;
            return;
        }

        if (renderers.Length == 0)
        {
            Debug.LogWarning("[TileDefinition] No MeshRenderers found on prefab: " + prefab.name);
            material = null;
            return;
        }

        Material chosenMaterial = null;

        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer.sharedMaterial != null)
            {
                chosenMaterial = renderer.sharedMaterial;
                break;
            }
        }

        if (chosenMaterial == null)
        {
            Debug.LogWarning("[TileDefinition] No valid material found on prefab: " + prefab.name);
            material = null;
            return;
        }

        Matrix4x4 rootInverse = prefab.transform.worldToLocalMatrix;
        List<CombineInstance> combines = new List<CombineInstance>();

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter == null || meshFilter.sharedMesh == null)
            {
                continue;
            }

            CombineInstance combine = new CombineInstance
            {
                mesh = meshFilter.sharedMesh,
                subMeshIndex = 0,

                // IMPORTANT:
                // This makes child meshes relative to the prefab root,
                // instead of baking in the prefab's world position.
                transform = rootInverse * meshFilter.transform.localToWorldMatrix
            };

            combines.Add(combine);
        }

        if (combines.Count == 0)
        {
            Debug.LogWarning("[TileDefinition] No valid meshes found on prefab: " + prefab.name);
            mesh = null;
            return;
        }

        Mesh combinedMesh = new Mesh();

        int totalVertices = 0;
        foreach (CombineInstance combine in combines)
        {
            totalVertices += combine.mesh.vertexCount;
        }

        if (totalVertices > 65535)
        {
            combinedMesh.indexFormat = IndexFormat.UInt32;
        }

        combinedMesh.CombineMeshes(combines.ToArray(), true, true);
        combinedMesh.RecalculateBounds();

        mesh = combinedMesh;
        material = chosenMaterial;
        material.enableInstancing = true;
    }
}