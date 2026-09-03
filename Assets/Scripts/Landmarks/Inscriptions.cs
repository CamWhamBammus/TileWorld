using UnityEngine;

/// <summary>
/// Something written at each landmark. Reaching one gave a diamond on the map
/// and a tick in a list; nothing about the place itself was worth arriving for.
///
/// Lines are assembled from the structure, the region it stands in and a hash,
/// so a given place always carries the same words, and those words are about
/// where it actually is.
/// </summary>
public static class Inscriptions
{
    private static readonly string[] WatchLines =
    {
        "Cut into the signboard: \"{0}. Fire watch. Ring twice.\"",
        "A tally on the rail, one notch a day, and a season's worth of them.",
        "Chalked on the tower door: \"gone down for water, back by dark\".",
        "Above the stair: \"we saw the smoke from here first\".",
        "The lamp has been filled. Someone still comes up.",
        "A logbook in the chest, swollen shut. The cover reads {0}."
    };

    private static readonly string[] WreckLines =
    {
        "On the signboard, in a sailor's hand: \"{0}. Ran aground in fog. All ashore.\"",
        "The chest is empty. Whatever was worth carrying was carried.",
        "Scratched on the stern: a name, and under it, \"she was a good boat\".",
        "Marks up the mast, a foot apart: how high the water came, night by night.",
        "Someone has been back for the rope. The knots are recent.",
        "Cut into a plank: \"we walked to {0} from here. Two days.\""
    };

    private static readonly string[] GateLines =
    {
        "Over the gap, worn nearly away: \"{0}. Pay at the far tower.\"",
        "Tallies on the inside wall, in fives. Thousands passed through here.",
        "The gate is off its hinges and leaning. Nobody needed it shut.",
        "Scratched at eye height: \"turn back, there is nothing past this\". Somebody added: \"there is water\".",
        "Sand up to the second course. It is being buried a little every year.",
        "Chalk on the walk above: \"kept the gate for {0}, forty summers\"."
    };

    private static readonly string[] CabinLines =
    {
        "Burned into the door: \"{0}. Trap line north. Back before the thaw.\"",
        "Pelts tallied on the wall by the door: a good year, a bad one, a good one.",
        "The woodpile is stacked to last. They meant to come back.",
        "On the sign: \"snow to the eaves in the hard winter\". A line cut higher than you can reach.",
        "Under the window, in pencil: \"heard wolves. Did not see them.\"",
        "\"Kettle's on the hook\" chalked inside. It is."
    };

    private static readonly string[] JettyLines =
    {
        "On the post: \"{0}. Best fishing at first light. Leave the small ones.\"",
        "Notches along the rail, each one a fish, worn smooth by hands.",
        "A hook and line are still tied off at the far end.",
        "Painted on the sign, mostly gone: \"boats for hire\". There are no boats.",
        "Under the planks, in the water, something is written that you cannot read.",
        "\"{0} froze to the far side, the year I could walk across.\""
    };

    private static readonly string[] AltarLines =
    {
        "Cut into the first step: \"{0}. Leave what you carry at the top.\"",
        "The idol has been turned to face the sunrise. The marks show it once faced the other way.",
        "Every tread is worn in the middle. A great many feet, a very long time.",
        "On the sign, in a newer hand: \"do not climb the last step\".",
        "Ash on the top tier, old and cold, in a ring.",
        "Something written round the idol's base in a script nobody in {0} can read."
    };

    private static readonly string[] RingLines =
    {
        "On the signboard: \"{0}. Do not eat these. Do not stand inside at dusk.\"",
        "The fence was put up around it, then taken down from the inside.",
        "The bust has been polished, at the mouth, by a great many hands.",
        "Chalked on the chest: \"they were knee high in spring\".",
        "Spores on everything. Your notes will smell of it for days.",
        "Scratched on the tallest stalk: \"{0}. Still growing.\""
    };

    public static string For(Vector2Int chunk, LandmarkKind kind, int worldSeed)
    {
        var region = Regions.At(chunk, worldSeed);

        string[] lines = kind switch
        {
            LandmarkKind.Shipwreck => WreckLines,
            LandmarkKind.SandGate => GateLines,
            LandmarkKind.TrappersCabin => CabinLines,
            LandmarkKind.FishingJetty => JettyLines,
            LandmarkKind.SteppedAltar => AltarLines,
            LandmarkKind.ToadstoolRing => RingLines,
            _ => WatchLines
        };

        int hash = Hash(chunk.x, chunk.y, worldSeed ^ 0x51F3);

        return string.Format(lines[hash % lines.Length], region.Name);
    }

    private static int Hash(int x, int y, int seed)
    {
        uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695040888963407L);
        h = (h ^ (h >> 13)) * 1274126177u;
        h ^= h >> 16;

        return (int)(h & 0x7FFFFFFF);
    }
}
