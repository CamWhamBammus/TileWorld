using UnityEngine;

public enum FaunaKind { Deer, Rabbit, Fox, Goat }

/// <summary>
/// What lives where. Like everything else in the world this is a function of
/// the ground and the hour rather than a list: deer keep to wooded lowland and
/// come out at either end of the day, rabbits to open meadow in daylight,
/// foxes to the same lowland after dark, and goats to ground too high for any
/// of them. Two animals of different kinds are rarely in the same place at the
/// same time, which is what makes each one worth walking towards.
/// </summary>
public static class Fauna
{
    public struct Traits
    {
        public string Name;
        public Color Coat;
        public Color Under;        // bone, tail tip, the pale markings
        public Color Dark;         // hooves, paws, the end of the nose
        public float Size;         // shoulder height, world units
        public float WalkSpeed;
        public float RunSpeed;
        public float Notices;      // how far off it becomes wary of you
        public float Bolts;        // how close you get before it goes
        public float Settles;      // how far it runs before calming down
    }

    public static Traits Of(FaunaKind kind)
    {
        switch (kind)
        {
            case FaunaKind.Deer:
                return new Traits
                {
                    Name = "deer",
                    Coat = new Color(0.52f, 0.37f, 0.24f),
                    Under = new Color(0.80f, 0.74f, 0.63f),
                    Dark = new Color(0.14f, 0.11f, 0.09f),
                    Size = 1.35f, WalkSpeed = 1.5f, RunSpeed = 8.5f,
                    Notices = 26f, Bolts = 15f, Settles = 55f
                };

            case FaunaKind.Rabbit:
                return new Traits
                {
                    Name = "rabbit",
                    Coat = new Color(0.55f, 0.48f, 0.40f),
                    Under = new Color(0.88f, 0.86f, 0.82f),
                    Dark = new Color(0.20f, 0.16f, 0.14f),
                    Size = 0.34f, WalkSpeed = 1.1f, RunSpeed = 6.5f,
                    Notices = 14f, Bolts = 8f, Settles = 26f
                };

            case FaunaKind.Fox:
                return new Traits
                {
                    Name = "fox",
                    Coat = new Color(0.68f, 0.34f, 0.14f),
                    Under = new Color(0.93f, 0.91f, 0.87f),
                    Dark = new Color(0.13f, 0.10f, 0.09f),
                    Size = 0.52f, WalkSpeed = 1.8f, RunSpeed = 7.5f,
                    Notices = 20f, Bolts = 11f, Settles = 38f
                };

            default:
                return new Traits
                {
                    Name = "goat",
                    Coat = new Color(0.86f, 0.84f, 0.79f),
                    Under = new Color(0.52f, 0.47f, 0.40f),
                    Dark = new Color(0.18f, 0.16f, 0.14f),
                    Size = 0.85f, WalkSpeed = 1.2f, RunSpeed = 6.0f,
                    Notices = 22f, Bolts = 10f, Settles = 34f
                };
        }
    }

    /// <summary>What the journal says about a creature once you have seen one.</summary>
    public static string Describe(FaunaKind kind)
    {
        switch (kind)
        {
            case FaunaKind.Deer: return "wooded lowland, at either end of the day";
            case FaunaKind.Rabbit: return "open meadow, in daylight";
            case FaunaKind.Fox: return "lowland and wood, after dark";
            default: return "high ground, in any weather";
        }
    }

    /// <summary>What is said the first time you see one.</summary>
    public static string OnFirstSight(FaunaKind kind)
    {
        switch (kind)
        {
            case FaunaKind.Deer: return "A deer, watching you from the trees";
            case FaunaKind.Rabbit: return "A rabbit, gone still in the grass";
            case FaunaKind.Fox: return "A fox, crossing the dark ahead of you";
            default: return "A goat, up where nothing else grazes";
        }
    }

    /// <summary>Whether this ground would suit the animal at this hour.</summary>
    public static bool Suits(FaunaKind kind, int tileX, int tileZ, int worldSeed, float timeOfDay)
    {
        return Awake(kind, timeOfDay) && Ground(kind, tileX, tileZ, worldSeed);
    }

    /// <summary>
    /// Whether the ground alone would suit it, whatever the hour. Wandering
    /// asks this rather than Suits: an animal part way through the evening
    /// should keep walking, not stop dead the moment its hour runs out.
    /// </summary>
    public static bool Ground(FaunaKind kind, int tileX, int tileZ, int worldSeed)
    {
        if (WaterSurface.IsUnderwater(tileX, tileZ, worldSeed)) return false;

        float relief = WorldHeight.HeightAt(tileX, tileZ, worldSeed) / WorldHeight.MaxRelief;

        switch (kind)
        {
            case FaunaKind.Deer: return relief < 0.55f;
            case FaunaKind.Rabbit: return relief < 0.32f;
            case FaunaKind.Fox: return relief < 0.60f;
            default: return relief > 0.52f && !SnowCover.IsSnowy(tileX, tileZ, worldSeed);
        }
    }

    /// <summary>Whether the animal is about at this hour at all.</summary>
    public static bool Awake(FaunaKind kind, float timeOfDay)
    {
        switch (kind)
        {
            // out at dawn and dusk, and not much in the middle of the day
            case FaunaKind.Deer:
                return (timeOfDay > 0.20f && timeOfDay < 0.36f)
                    || (timeOfDay > 0.66f && timeOfDay < 0.84f);

            case FaunaKind.Rabbit: return timeOfDay > 0.28f && timeOfDay < 0.78f;
            case FaunaKind.Fox: return timeOfDay < 0.22f || timeOfDay > 0.80f;
            default: return true;
        }
    }

    /// <summary>How many of a kind are worth having about at once.</summary>
    public static int Crowd(FaunaKind kind)
    {
        switch (kind)
        {
            case FaunaKind.Deer: return 4;
            case FaunaKind.Rabbit: return 5;
            case FaunaKind.Fox: return 2;
            default: return 3;
        }
    }
}
