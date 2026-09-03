using UnityEngine;

public enum FaunaKind { Deer, Rabbit, Fox, Goat }

/// <summary>
/// What lives where. Like everything else in the world this is a function of
/// the ground and the hour rather than a list: deer keep to wooded lowland and
/// come out at either end of the day, rabbits to open meadow in daylight,
/// foxes to the same lowland after dark, and goats to ground too high for any
/// of them. Two animals of different kinds are rarely in the same place at the
/// same time, which is what makes each one worth walking towards.
///
/// One creature is one entry below. It used to be spread over eight switch
/// statements here and more in five other files, so adding a kind meant
/// remembering all of them, and any case left out fell quietly through to a
/// default and behaved like a deer instead of saying so.
/// </summary>
public static class Fauna
{
    /// <summary>
    /// Colours are taken from the tile pack's own palette texture rather than
    /// picked to look like the animal: a deer the colour of the ground it
    /// stands on belongs there, one painted from life does not.
    /// </summary>
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

    /// <summary>
    /// How a kind carries itself when it moves. A deer trotting and a rabbit
    /// bounding are not the same animation with different numbers in it: one
    /// swings its legs in diagonal pairs, the other throws both back legs
    /// forward together and rises over the gap.
    /// </summary>
    public struct Gait
    {
        public float Cadence;    // how fast the cycle runs per metre covered
        public float Swing;      // how far the legs swing at the hip, degrees
        public float Knee;       // how far the joint folds on the way forward
        public float Bounce;     // rise and fall of the body, against its height
        public float Pitch;      // nose up and down over the stride
        public float Roll;       // weight rocking from side to side
        public bool Bounds;      // both hind legs together, rather than diagonal pairs
    }

    /// <summary>
    /// The call, as the numbers it is made from. There is no recording of a
    /// fox anywhere in the project: the sound is built from these.
    /// </summary>
    public struct Voice
    {
        public float Length;
        public float Pitch;          // where it starts, in hertz
        public float Glide;          // and how far it slides over its length
        public float WobbleRate;
        public float WobbleDepth;
        public float Rasp;
        public float Noise;
        public float Thump;          // drumming rather than voice, as a rabbit does
    }

    /// <summary>One creature, entire.</summary>
    public struct Kind
    {
        public Traits Traits;

        public float Lowest;        // the band of ground it will stand on, as
        public float Highest;       // a share of the tallest ground there is
        public bool KeepsOffSnow;

        // When it is about. A window whose start is later than its end runs
        // through midnight; no windows at all means it keeps no hours.
        public Vector2[] Hours;

        public int Company;         // how many turn up together
        public int Crowd;           // how many of the kind are worth having about

        public Gait Walk;
        public Gait Run;

        public string Found;        // what the journal says once you have seen one
        public string Country;      // where the guide wants it found: "in the woods at dusk"
        public string Habit;        // what the guide wants it seen doing: "grazing"
        public Doing Doing;         // and what that is, to the simulation

        public Voice Call;

        // The guide asks for more than the simulation does: not merely ground
        // the animal would stand on, but the country it is known for, at the
        // hour it is known for. A window of nothing wide means any hour will do.
        public Vector2 ProperHours;
        public float ProperLowest;
        public float ProperHighest;
    }

    private static readonly Kind[] kinds =
    {
        // deer: wooded lowland, out at either end of the day and rarely alone
        new Kind
        {
            Traits = new Traits
            {
                Name = "deer",
                Coat = new Color(0.612f, 0.447f, 0.271f),
                Under = new Color(0.855f, 0.741f, 0.588f),
                Dark = new Color(0.255f, 0.247f, 0.220f),
                Size = 1.55f, WalkSpeed = 1.5f, RunSpeed = 8.5f,
                Notices = 26f, Bolts = 15f, Settles = 55f
            },
            Lowest = 0f, Highest = 0.55f,

            // The windows overlap the others on purpose: kept strictly to
            // their own hours the animals were so seldom about that the world
            // read as empty.
            Hours = new[] { new Vector2(0.14f, 0.46f), new Vector2(0.58f, 0.90f) },

            Company = 4, Crowd = 9,

            // a trot, breaking into a bound when it is frightened
            Walk = new Gait { Cadence = 3.6f, Swing = 24f, Knee = 30f, Bounce = 0.045f,
                              Pitch = 2.4f, Roll = 3.2f, Bounds = false },
            Run = new Gait { Cadence = 2.2f, Swing = 46f, Knee = 44f, Bounce = 0.20f,
                             Pitch = 8f, Roll = 2f, Bounds = true },

            Found = "woods and low ground, dawn and dusk",
            Country = "in the woods at dusk",
            Habit = "grazing", Doing = Doing.Grazing,

            // a short chesty grunt, dropping as it ends
            Call = new Voice { Length = 0.40f, Pitch = 152f, Glide = -0.22f, WobbleRate = 7f,
                               WobbleDepth = 9f, Rasp = 0.30f, Noise = 0.14f, Thump = 0f },

            ProperHours = new Vector2(0.62f, 0.86f), ProperLowest = 0f, ProperHighest = 1f
        },

        // rabbit: open meadow in daylight, and never really walks
        new Kind
        {
            Traits = new Traits
            {
                Name = "rabbit",
                Coat = new Color(0.573f, 0.506f, 0.451f),
                Under = new Color(0.941f, 0.835f, 0.698f),
                Dark = new Color(0.278f, 0.231f, 0.184f),
                Size = 0.44f, WalkSpeed = 1.1f, RunSpeed = 6.5f,
                Notices = 14f, Bolts = 8f, Settles = 26f
            },
            Lowest = 0f, Highest = 0.32f,
            Hours = new[] { new Vector2(0.22f, 0.84f) },
            Company = 3, Crowd = 10,

            // a series of hops with pauses in them
            Walk = new Gait { Cadence = 3.0f, Swing = 34f, Knee = 62f, Bounce = 0.34f,
                              Pitch = 13f, Roll = 0f, Bounds = true },
            Run = new Gait { Cadence = 2.4f, Swing = 52f, Knee = 62f, Bounce = 0.55f,
                             Pitch = 13f, Roll = 0f, Bounds = true },

            Found = "open ground, in daylight",
            Country = "in the open in daylight",
            Habit = "resting", Doing = Doing.Resting,

            // rabbits are near enough silent, so this is the foot drumming
            Call = new Voice { Length = 0.16f, Pitch = 74f, Glide = -0.45f, WobbleRate = 0f,
                               WobbleDepth = 0f, Rasp = 0f, Noise = 0.55f, Thump = 1f },

            ProperHours = new Vector2(0.30f, 0.72f), ProperLowest = 0f, ProperHighest = 0.22f
        },

        // fox: the same low ground after dark, low and quick and mostly alone
        new Kind
        {
            Traits = new Traits
            {
                Name = "fox",
                Coat = new Color(0.780f, 0.420f, 0.200f),
                Under = new Color(0.965f, 0.847f, 0.698f),
                Dark = new Color(0.231f, 0.192f, 0.145f),
                Size = 0.64f, WalkSpeed = 1.8f, RunSpeed = 7.5f,
                Notices = 20f, Bolts = 11f, Settles = 38f
            },
            Lowest = 0f, Highest = 0.60f,
            Hours = new[] { new Vector2(0.72f, 0.30f) },      // through the night
            Company = 2, Crowd = 5,

            Walk = new Gait { Cadence = 4.2f, Swing = 26f, Knee = 40f, Bounce = 0.035f,
                              Pitch = 1.6f, Roll = 2.2f, Bounds = false },
            Run = new Gait { Cadence = 2.8f, Swing = 44f, Knee = 40f, Bounce = 0.14f,
                             Pitch = 1.6f, Roll = 2.2f, Bounds = true },

            Found = "low ground, at night",
            Country = "at night",
            Habit = "drinking", Doing = Doing.Drinking,

            // the bark: high, thin and rough, and it carries a long way
            Call = new Voice { Length = 0.34f, Pitch = 590f, Glide = 0.30f, WobbleRate = 22f,
                               WobbleDepth = 60f, Rasp = 0.45f, Noise = 0.18f, Thump = 0f },

            ProperHours = new Vector2(0.82f, 0.22f), ProperLowest = 0f, ProperHighest = 1f
        },

        // goat: ground too high for any of the others, and out at any hour
        new Kind
        {
            Traits = new Traits
            {
                Name = "goat",
                Coat = new Color(0.678f, 0.678f, 0.678f),
                Under = new Color(0.392f, 0.353f, 0.314f),
                Dark = new Color(0.231f, 0.192f, 0.145f),
                Size = 1.00f, WalkSpeed = 1.2f, RunSpeed = 6.0f,
                Notices = 22f, Bolts = 10f, Settles = 34f
            },
            Lowest = 0.52f, Highest = 1f, KeepsOffSnow = true,
            Hours = null,
            Company = 3, Crowd = 7,

            // picks its way, lifting its feet higher than it needs to
            Walk = new Gait { Cadence = 3.2f, Swing = 22f, Knee = 52f, Bounce = 0.05f,
                              Pitch = 2f, Roll = 4f, Bounds = false },
            Run = new Gait { Cadence = 2.6f, Swing = 40f, Knee = 46f, Bounce = 0.16f,
                             Pitch = 2f, Roll = 4f, Bounds = false },

            Found = "high ground, any time",
            Country = "up high",
            Habit = "walking", Doing = Doing.Walking,

            // the bleat, which is mostly its wobble
            Call = new Voice { Length = 0.62f, Pitch = 366f, Glide = -0.14f, WobbleRate = 15f,
                               WobbleDepth = 52f, Rasp = 0.34f, Noise = 0.10f, Thump = 0f },

            ProperHours = Vector2.zero, ProperLowest = 0.55f, ProperHighest = 1f
        }
    };

    /// <summary>How many kinds there are, so nothing has to be told twice.</summary>
    public static int Count => kinds.Length;

    static Fauna()
    {
        int named = System.Enum.GetValues(typeof(FaunaKind)).Length;

        // Better said out loud at the start than found later as an animal that
        // behaves like whichever one happens to be first in the list.
        if (kinds.Length != named)
        {
            Debug.LogError("[Fauna] " + named + " kinds are named but " + kinds.Length
                + " are described. Every kind in FaunaKind needs its entry, in the same order.");
        }
    }

    public static Kind All(FaunaKind kind) => kinds[Mathf.Clamp((int)kind, 0, kinds.Length - 1)];

    public static Traits Of(FaunaKind kind) => All(kind).Traits;

    /// <summary>What the journal says about a creature once you have seen one.</summary>
    public static string Describe(FaunaKind kind) => All(kind).Found;

    /// <summary>Where the guide wants one found, as a phrase to follow "found".</summary>
    public static string Country(FaunaKind kind) => All(kind).Country;

    /// <summary>What the guide wants one seen doing, as a word to follow "one".</summary>
    public static string Habit(FaunaKind kind) => All(kind).Habit;

    /// <summary>And whether what it is doing now is that.</summary>
    public static bool Habit(FaunaKind kind, Doing doing) => All(kind).Doing == doing;

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

        var it = All(kind);

        if (it.KeepsOffSnow && SnowCover.IsSnowy(tileX, tileZ, worldSeed)) return false;

        float relief = WorldHeight.HeightAt(tileX, tileZ, worldSeed) / WorldHeight.MaxRelief;

        return relief >= it.Lowest && relief < it.Highest;
    }

    /// <summary>Whether the animal is about at this hour at all.</summary>
    public static bool Awake(FaunaKind kind, float timeOfDay)
    {
        var hours = All(kind).Hours;

        if (hours == null || hours.Length == 0) return true;

        foreach (var window in hours)
        {
            // a window that starts later than it ends runs through midnight
            bool inside = window.x < window.y
                ? timeOfDay > window.x && timeOfDay < window.y
                : timeOfDay > window.x || timeOfDay < window.y;

            if (inside) return true;
        }

        return false;
    }

    /// <summary>
    /// Whether this is the country and the hour the guide wants one found in,
    /// which is narrower than merely somewhere it would stand.
    /// </summary>
    public static bool Proper(FaunaKind kind, float hour, float relief)
    {
        var it = All(kind);

        if (relief < it.ProperLowest || relief > it.ProperHighest) return false;

        var when = it.ProperHours;

        if (Mathf.Approximately(when.x, when.y)) return true;

        return when.x < when.y
            ? hour > when.x && hour < when.y
            : hour > when.x || hour < when.y;
    }

    public static Gait Moving(FaunaKind kind, bool running)
    {
        var it = All(kind);
        return running ? it.Run : it.Walk;
    }

    /// <summary>
    /// How many turn up together. Deer are rarely alone, rabbits keep loose
    /// company, and a fox is a fox on its own.
    /// </summary>
    public static int Company(FaunaKind kind) => All(kind).Company;

    /// <summary>How many of a kind are worth having about at once.</summary>
    public static int Crowd(FaunaKind kind) => All(kind).Crowd;
}
