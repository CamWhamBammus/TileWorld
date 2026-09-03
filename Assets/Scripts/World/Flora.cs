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
        /// What colour it is, read off the pack's palette. The stones are not
        /// all stone-coloured: mixed in with the grey ones are bright gems, and
        /// scattered over a snowfield those read as something growing.
        /// </summary>
        public Color Colour;

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

    /// <summary>
    /// The narrow trees again, with the green taken out of them: the same
    /// models, their needles pointed at the white of the pack's palette
    /// instead of at a green. For the snowfields, where a tree in summer
    /// colours standing in deep snow looks like a mistake.
    /// </summary>
    public Sprout[] SnowTrees;

    /// <summary>Conifers, built rather than shipped: the pack has none.</summary>
    public Sprout[] Pines;

    /// <summary>And the same under snow.</summary>
    public Sprout[] SnowPines;

    /// <summary>Thin standing poles, for the reedbeds.</summary>
    public Sprout[] Reeds;

    public Material Paint;
}
