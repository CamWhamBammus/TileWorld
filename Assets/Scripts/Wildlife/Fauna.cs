using UnityEngine;

public enum FaunaKind { Deer, Rabbit, Fox, Goat, Tortoise, Wolf, Heron, Boar, Raven, Marmot }

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

        // The country it keeps to, if it keeps to one: a tortoise is a desert
        // animal whatever the height of the sand. Empty means any country.
        public Regions.Character[] Countries;

        // How deep a water it will stand in; nought is none. A heron lives in
        // the shallows and nowhere else, which is what WadesOnly says.
        public float Wades;
        public bool WadesOnly;

        public bool Flies;          // gets away through the air, not over the ground
        public bool Withdraws;      // pulls in and sits tight rather than running
        public bool Howls;          // a call with its head up, in the dark
        public bool Burrows;        // goes to ground when it bolts, and comes back up later
        public bool Roots;          // feeds with its snout in the ground, not its teeth in the grass

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

    /// <summary>What sort of thing this is, for anyone who needs to know before building it.</summary>
    public static bool Flies(FaunaKind kind) => All(kind).Flies;

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
        },

        // tortoise: the sand, in the heat of the day, and never in a hurry.
        // It does not run from you; it stops, pulls in, and waits you out.
        new Kind
        {
            Traits = new Traits
            {
                Name = "tortoise",
                Coat = new Color(0.447f, 0.420f, 0.251f),
                Under = new Color(0.753f, 0.678f, 0.447f),
                Dark = new Color(0.251f, 0.220f, 0.153f),
                Size = 0.36f, WalkSpeed = 0.28f, RunSpeed = 0.28f,
                Notices = 7f, Bolts = 0f, Settles = 4f
            },
            Lowest = 0f, Highest = 0.7f,
            Countries = new[] { Regions.Character.Desert },
            Withdraws = true,
            Hours = new[] { new Vector2(0.22f, 0.80f) },
            Company = 1, Crowd = 4,

            // a plod: short strides, the shell rocking side to side
            Walk = new Gait { Cadence = 7f, Swing = 16f, Knee = 8f, Bounce = 0.01f,
                              Pitch = 0.5f, Roll = 5f, Bounds = false },
            Run = new Gait { Cadence = 7f, Swing = 18f, Knee = 8f, Bounce = 0.01f,
                             Pitch = 0.5f, Roll = 5f, Bounds = false },

            Found = "the sand, in the heat of the day",
            Country = "out on the sand in daylight",
            Habit = "sunning itself", Doing = Doing.Standing,

            // a hiss, which is all it has to say
            Call = new Voice { Length = 0.5f, Pitch = 90f, Glide = -0.1f, WobbleRate = 0f,
                               WobbleDepth = 0f, Rasp = 0.2f, Noise = 0.9f, Thump = 0f },

            ProperHours = new Vector2(0.34f, 0.66f), ProperLowest = 0f, ProperHighest = 1f
        },

        // wolf: the snowfields, from dusk through to morning, in twos. It
        // gives ground rather than bolting, and comes back.
        new Kind
        {
            Traits = new Traits
            {
                Name = "wolf",
                Coat = new Color(0.522f, 0.529f, 0.549f),
                Under = new Color(0.839f, 0.831f, 0.784f),
                Dark = new Color(0.180f, 0.180f, 0.200f),
                Size = 0.84f, WalkSpeed = 1.7f, RunSpeed = 8.2f,
                Notices = 34f, Bolts = 11f, Settles = 26f
            },
            Lowest = 0f, Highest = 0.95f,
            Countries = new[] { Regions.Character.Snow },
            Howls = true,
            Hours = new[] { new Vector2(0.60f, 0.30f) },
            Company = 2, Crowd = 4,

            // a lope, low and even; a run that stretches out
            Walk = new Gait { Cadence = 3.4f, Swing = 28f, Knee = 42f, Bounce = 0.03f,
                              Pitch = 1.5f, Roll = 2f, Bounds = false },
            Run = new Gait { Cadence = 2.4f, Swing = 48f, Knee = 44f, Bounce = 0.16f,
                             Pitch = 4f, Roll = 1.5f, Bounds = true },

            Found = "the snowfields, after dark",
            Country = "on the snow after dark",
            Habit = "watching", Doing = Doing.Watching,

            // the howl: long, rising, and held
            Call = new Voice { Length = 1.9f, Pitch = 330f, Glide = 0.28f, WobbleRate = 2.6f,
                               WobbleDepth = 10f, Rasp = 0.08f, Noise = 0.04f, Thump = 0f },

            ProperHours = new Vector2(0.84f, 0.16f), ProperLowest = 0f, ProperHighest = 1f
        },

        // heron: the shallows of any lake or shore, standing stock still; it
        // gets away by air, and lands a long way off.
        new Kind
        {
            Traits = new Traits
            {
                Name = "heron",
                Coat = new Color(0.549f, 0.620f, 0.678f),
                Under = new Color(0.922f, 0.929f, 0.922f),
                Dark = new Color(0.157f, 0.169f, 0.180f),
                Size = 1.05f, WalkSpeed = 0.55f, RunSpeed = 7.5f,
                Notices = 24f, Bolts = 13f, Settles = 64f
            },
            Lowest = 0f, Highest = 1f,
            KeepsOffSnow = true,
            Wades = 0.75f, WadesOnly = true,
            Flies = true,
            Hours = new[] { new Vector2(0.14f, 0.86f) },
            Company = 1, Crowd = 3,

            // a high careful step through the water; the run is flight
            Walk = new Gait { Cadence = 2.4f, Swing = 26f, Knee = 70f, Bounce = 0.01f,
                              Pitch = 1f, Roll = 1.5f, Bounds = false },
            Run = new Gait { Cadence = 1.2f, Swing = 10f, Knee = 20f, Bounce = 0f,
                             Pitch = 0f, Roll = 0f, Bounds = false },

            Found = "the shallows, standing still",
            Country = "standing in the shallows",
            Habit = "fishing", Doing = Doing.Drinking,

            // a harsh croak, going down
            Call = new Voice { Length = 0.38f, Pitch = 230f, Glide = -0.30f, WobbleRate = 0f,
                               WobbleDepth = 0f, Rasp = 0.6f, Noise = 0.3f, Thump = 0f },

            ProperHours = new Vector2(0f, 0f), ProperLowest = 0f, ProperHighest = 1f
        },

        // boar: the dead woods and the mushroom woods, in a sounder of a few,
        // rooting. It faces you and stamps before it goes.
        new Kind
        {
            Traits = new Traits
            {
                Name = "boar",
                Coat = new Color(0.322f, 0.278f, 0.220f),
                Under = new Color(0.769f, 0.694f, 0.561f),
                Dark = new Color(0.118f, 0.098f, 0.078f),
                Size = 0.78f, WalkSpeed = 1.3f, RunSpeed = 6.8f,
                Notices = 18f, Bolts = 9f, Settles = 30f
            },
            Lowest = 0f, Highest = 0.7f,
            Countries = new[] { Regions.Character.Dead, Regions.Character.Fungal },
            Roots = true,
            Hours = new[] { new Vector2(0.10f, 0.50f), new Vector2(0.60f, 0.96f) },
            Company = 3, Crowd = 6,

            // a busy trot, low to the ground; a flat-out run
            Walk = new Gait { Cadence = 3.8f, Swing = 22f, Knee = 34f, Bounce = 0.03f,
                              Pitch = 1.5f, Roll = 3f, Bounds = false },
            Run = new Gait { Cadence = 2.6f, Swing = 40f, Knee = 40f, Bounce = 0.12f,
                             Pitch = 3f, Roll = 2f, Bounds = false },

            Found = "the dead wood, rooting",
            Country = "in the dead wood",
            Habit = "rooting", Doing = Doing.Grazing,

            // a grunt, low and rough
            Call = new Voice { Length = 0.32f, Pitch = 108f, Glide = -0.12f, WobbleRate = 9f,
                               WobbleDepth = 6f, Rasp = 0.55f, Noise = 0.30f, Thump = 0f },

            ProperHours = new Vector2(0f, 0f), ProperLowest = 0f, ProperHighest = 1f
        },

        // raven: the dead wood, on the ground, hopping and pecking, off by air
        new Kind
        {
            Traits = new Traits
            {
                Name = "raven",
                Coat = new Color(0.098f, 0.098f, 0.118f),
                Under = new Color(0.251f, 0.251f, 0.290f),
                Dark = new Color(0.051f, 0.051f, 0.059f),
                Size = 0.36f, WalkSpeed = 0.7f, RunSpeed = 7.2f,
                Notices = 16f, Bolts = 8f, Settles = 48f
            },
            Lowest = 0f, Highest = 1f,
            Countries = new[] { Regions.Character.Dead, Regions.Character.Lowland, Regions.Character.Stone },
            Flies = true,
            Hours = new[] { new Vector2(0.15f, 0.85f) },
            Company = 2, Crowd = 5,

            // a hop, both feet together
            Walk = new Gait { Cadence = 4.2f, Swing = 20f, Knee = 30f, Bounce = 0.25f,
                              Pitch = 6f, Roll = 0f, Bounds = true },
            Run = new Gait { Cadence = 1.2f, Swing = 10f, Knee = 20f, Bounce = 0f,
                             Pitch = 0f, Roll = 0f, Bounds = false },

            Found = "the dead wood, on the ground",
            Country = "in the dead wood",
            Habit = "pecking at the ground", Doing = Doing.Grazing,

            // a croak
            Call = new Voice { Length = 0.30f, Pitch = 175f, Glide = -0.18f, WobbleRate = 0f,
                               WobbleDepth = 0f, Rasp = 0.7f, Noise = 0.35f, Thump = 0f },

            ProperHours = new Vector2(0f, 0f), ProperLowest = 0f, ProperHighest = 1f
        },

        // marmot: the bare rock, sitting up to keep watch; a whistle, a
        // short dash, and gone to ground
        new Kind
        {
            Traits = new Traits
            {
                Name = "marmot",
                Coat = new Color(0.620f, 0.502f, 0.322f),
                Under = new Color(0.851f, 0.753f, 0.549f),
                Dark = new Color(0.251f, 0.200f, 0.149f),
                Size = 0.40f, WalkSpeed = 0.9f, RunSpeed = 4.6f,
                Notices = 20f, Bolts = 12f, Settles = 10f
            },
            Lowest = 0.15f, Highest = 1f,
            Countries = new[] { Regions.Character.Stone, Regions.Character.Peaks },
            Burrows = true,
            Hours = new[] { new Vector2(0.20f, 0.80f) },
            Company = 3, Crowd = 6,

            // a waddle, and a bounding dash
            Walk = new Gait { Cadence = 4.6f, Swing = 26f, Knee = 40f, Bounce = 0.06f,
                              Pitch = 2f, Roll = 4f, Bounds = false },
            Run = new Gait { Cadence = 3.0f, Swing = 40f, Knee = 50f, Bounce = 0.22f,
                             Pitch = 8f, Roll = 0f, Bounds = true },

            Found = "the bare rock, sitting up",
            Country = "on the bare rock",
            Habit = "keeping watch", Doing = Doing.Standing,

            // the whistle
            Call = new Voice { Length = 0.26f, Pitch = 2100f, Glide = -0.08f, WobbleRate = 0f,
                               WobbleDepth = 0f, Rasp = 0f, Noise = 0.04f, Thump = 0f },

            ProperHours = new Vector2(0f, 0f), ProperLowest = 0.15f, ProperHighest = 1f
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
        var it = All(kind);

        if (WaterSurface.IsUnderwater(tileX, tileZ, worldSeed))
        {
            float deep = WaterSurface.Level - WorldHeight.SurfaceY(tileX, tileZ, worldSeed);
            if (it.Wades <= 0f || deep > it.Wades) return false;
        }
        else if (it.WadesOnly) return false;

        if (it.KeepsOffSnow && SnowCover.IsSnowy(tileX, tileZ, worldSeed)) return false;

        if (it.Countries != null && it.Countries.Length > 0)
        {
            var here = Regions.CharacterAtTile(tileX, tileZ, worldSeed);
            bool home = false;
            foreach (var c in it.Countries) if (c == here) home = true;
            if (!home) return false;
        }

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
