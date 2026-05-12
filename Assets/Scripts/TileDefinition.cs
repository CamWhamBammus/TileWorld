using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using UnityEngine.Rendering;


[CreateAssetMenu(menuName = "Tiles/Tile Definition")]
public class TileDefinition : ScriptableObject
{
	public int blockID;
	public GameObject prefab;

	[HideInInspector] [SerializeField] private Mesh mesh;
	[HideInInspector] [SerializeField] private Material material;

	
	private void OnValidate()
	{
		 
		if (prefab == null)
		{
			mesh = null;
			material = null;
			return;
		}
		var meshFilters = prefab.GetComponentsInChildren<MeshFilter>(true);
		var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
		var uniqueMats = new HashSet<Material>();
		
		
		foreach (var r in renderers)
		{
			if (r.sharedMaterials != null && r.sharedMaterials.Length > 0)
			{
				uniqueMats.Add(r.sharedMaterials[0]);
			}
		}
		
		if (uniqueMats.Count == 0)
		{
			Debug.LogWarning($"[TileDefinition:{name}] No materials found in prefab '{prefab.name}'.");
			mesh = null; material = null; return;
		}
		
		var chosenMat = uniqueMats.First();
		
		Matrix4x4 rootInv = prefab.transform.worldToLocalMatrix;
		
		var combines = new List<CombineInstance>();
		
		foreach (var v in meshFilters)
		{
			if (v == null || v.sharedMesh == null) continue;

			var ci = new CombineInstance
			{
				mesh = v.sharedMesh,
				subMeshIndex = 0,
				transform = rootInv * v.transform.localToWorldMatrix
			};
			combines.Add(ci);	
		}

		if (combines.Count == 0) { mesh = null; material = null; return; }
		var combined = new Mesh();

		int verts = 0;
		foreach (var c in combines)
		{
			verts += c.mesh.vertexCount;
		}
		if (verts > 65535)
		{
			combined.indexFormat = IndexFormat.UInt32;
		}

		combined.CombineMeshes(combines.ToArray(), true, true);
		combined.RecalculateBounds();

		mesh = combined;
		material = chosenMat;
		if (material && !material.enableInstancing) material.enableInstancing = true;
	}

	public Mesh MeshGetter()
	{
		return mesh;
	}
	public Material MaterialGetter()
	{
		return material;
	}
}
