using UnityEngine;

/// <summary>
/// Things that stand on the ground without being part of it: mushrooms, cactus,
/// palms, stones. The models come out of the tile pack, which carries a good
/// many more than the ground itself uses, and this is the list of the ones we
/// have taken and what is needed to stand one up.
/// </summary>
public class Flora : ScriptableObject
{
    [System.Serializable]
    public struct Sprout
    {
        public string Name;
        public Mesh Mesh;

        /// <summary>How tall the model is in its own units.</summary>
        public float Size;

        /// <summary>And how broad. A narrow tree is a conifer; a wide one is not.</summary>
        public float Wide;

        /// <summary>
        /// How far to lift it so its foot is on the ground. Some of these models
        /// are drawn about their middle and some stand on their own origin, and
        /// which is which is not something to find out by eye afterwards.
        /// </summary>
        public float Foot;
    }

    public Sprout[] Mushrooms;
    public Sprout[] Cacti;
    public Sprout[] Palms;

    /// <summary>The sand update's stones, which are sand-coloured.</summary>
    public Sprout[] Stones;

    /// <summary>And the pack's own, which are grey.</summary>
    public Sprout[] Boulders;

    public Sprout[] Trees;

    /// <summary>Bare and leafless, out of the sand update.</summary>
    public Sprout[] DeadTrees;

    /// <summary>Thin standing poles, for the reedbeds.</summary>
    public Sprout[] Reeds;

    public Material Paint;
}
