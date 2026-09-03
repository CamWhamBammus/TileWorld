using UnityEngine;

/// <summary>
/// Things that stand on the ground without being part of it: mushrooms, sprigs,
/// stones. The models come out of the tile pack, which holds a good many more
/// than the ground itself uses, and this is the list of the ones we have taken.
/// </summary>
public class Flora : ScriptableObject
{
    [System.Serializable]
    public struct Sprout
    {
        public string Name;
        public Mesh Mesh;
        public float Size;      // how tall it stands, in world units
    }

    public Sprout[] Mushrooms;
    public Material Paint;
}
