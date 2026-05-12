using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Tiles/Tile Library")]
public class TileLibrary : ScriptableObject
{
    [SerializeField] private List<TileDefinition> definitions = new();
    private Dictionary<int, TileDefinition> byId = new();

    private void Awake()  
    {
        Build();
    }

    private void OnValidate()  
    {
        Build();
    }

    private void Build()
    {
        byId.Clear();
        foreach (var def in definitions)
        {
            if (!def) continue;
            if (byId.ContainsKey(def.blockID)) continue;
            byId.Add(def.blockID, def);
        }
    }

    public bool TryGet(int blockId, out TileDefinition def) => byId.TryGetValue(blockId, out def);

   
    public bool Contains(int id) => byId.ContainsKey(id);
    public IEnumerable<int> AllIds() => byId.Keys;
}
