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

    private static readonly string[] CampLines =
    {
        "Burned into the kiln door: \"{0}. Three days a burn. Do not open it.\"",
        "The wood is stacked by size. Whoever did it had done it a thousand times.",
        "Chalked on the chest: \"charcoal to the smith at {0}, one cart\".",
        "Ash to the ankles round the kiln. It has burned here a long while.",
        "Notches on the fence, five and a stroke, five and a stroke.",
        "\"Went for water\" scratched on the door, and a date a long way back."
    };

    private static readonly string[] BeaconLines =
    {
        "Cut into the plinth: \"{0}. Keep it lit. They steer by it.\"",
        "The lamp is full and the wick is trimmed. Somebody climbs up here.",
        "A list of nights on the wall, each with a mark: lit, lit, lit, out.",
        "\"Seen from the {0} road\" chalked under the sign, and a small drawing of the tower.",
        "The fence has been mended with rope. Sheep, probably.",
        "Chalked on the chest: \"oil for a month, no more\"."
    };

    private static readonly string[] CairnLines =
    {
        "On the sign, faint: \"{0} below. Add a stone if you got here.\"",
        "The top stone has been set and reset. Every hand that came up moved it.",
        "Names cut into the flat stones, dozens of them, the newest still sharp.",
        "The pole has held a flag. Only the knots are left.",
        "Scratched on a stone: \"could see the sea from here. Could not see home.\"",
        "\"{0}, the long way\" and an arrow, pointing down the wrong side."
    };

    private static readonly string[] ShrineLines =
    {
        "On the sign: \"{0}. Leave a little for the road.\"",
        "Coins in the chest, none of them worth anything. Something is.",
        "The stone has been touched smooth at one place, about hand height.",
        "Chalked on the fence: \"lamp lit, all well\". The lamp is lit.",
        "Under the sign, in a child's hand: \"we passed here going to {0}\".",
        "Flowers, dried, tied to the fence. Not old."
    };

    private static readonly string[] StonesLines =
    {
        "On the sign, newer than the stones by a thousand years: \"{0}. Do not move them.\"",
        "The fallen one fell inward. Whoever set them meant it to.",
        "Marks on the flat stone line up with the tallest at sunrise. You checked.",
        "Nothing is written on the stones. That is what is written on them.",
        "The ring is seven. There is a hollow where an eighth stood.",
        "Scratched on the sign, in a hurry: \"{0}. Not at night.\""
    };

    private static readonly string[] LighthouseLines =
    {
        "Cut over the stair: \"{0}. Lit at dusk, out at dawn, no exceptions.\"",
        "A tally of wrecks on the wall, and after the light was built, none.",
        "The lamp is full. Someone rows out.",
        "Chalked on the plinth: \"tide to here in the big storm\", well above your head.",
        "\"Watched a ship go past {0} all night and not come in\" scratched by the rail.",
        "The chest holds rope, oil, and a letter nobody sent."
    };

    private static readonly string[] HideLines =
    {
        "On the sign: \"{0}. Quiet. They come at first light.\"",
        "Tallies on the rail, in two columns: seen, and taken.",
        "The chest holds a blanket and a horn. It gets cold up here.",
        "Chalked on the platform: \"stag, seven points, went east\".",
        "Someone slept here. The grass is flat where they lay.",
        "\"Missed\" scratched into the rail, and under it, \"missed again\"."
    };

    private static readonly string[] BuriedLines =
    {
        "Just above the sand, a line of letters: \"...{0} was green then...\"",
        "The door lies flat. The sand is over half of it already.",
        "Scratched on the tower, at what is now knee height: \"stand here and you can see the wall\". There is no wall.",
        "Chalked on the chest, recently: \"dug for an hour. Nothing.\"",
        "The lean has a mark on the sign: how far it has gone since somebody last checked.",
        "\"{0}\" cut deep into the stone, and under it a number too worn to read."
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
            LandmarkKind.CharcoalCamp => CampLines,
            LandmarkKind.HilltopBeacon => BeaconLines,
            LandmarkKind.SummitCairn => CairnLines,
            LandmarkKind.WaysideShrine => ShrineLines,
            LandmarkKind.StandingStones => StonesLines,
            LandmarkKind.Lighthouse => LighthouseLines,
            LandmarkKind.HuntersHide => HideLines,
            LandmarkKind.BuriedTower => BuriedLines,
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
