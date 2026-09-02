using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What there is to notice.
///
/// The book used to hand out a list of tasks and wait for them to be ticked
/// off. This is the other way round: you go about the country, and when
/// something worth remarking on happens in front of you, the book remarks on
/// it. Nothing here has to be sought out and none of it can be failed.
///
/// Every one of these is a true thing about the world rather than an errand.
/// A deer will not cross deep water, a goat walks ground you would fall off,
/// wild things keep clear of the ruins — you are finding out how the place
/// actually works, which is why it is worth writing down.
/// </summary>
public static class Observations
{
    /// <summary>Everything the watcher knows at the moment it looks.</summary>
    public struct Sight
    {
        public Animal Animal;
        public FaunaKind Kind;
        public Doing Doing;

        public float Distance;      // from the player
        public int Company;         // others of its kind close by
        public bool Mixed;          // another kind close by as well
        public FaunaKind Other;

        public float Hour;
        public float Overcast;

        public float Relief;        // where the animal stands, against the highest ground
        public float Slope;         // degrees
        public bool Snow;
        public bool ByWater;
        public bool NearRuin;

        public float Standing;      // the player's own height against the highest ground
        public bool AtWater;        // and whether they are at the edge of any

        public Vector2Int Chunk;
    }

    public class Note
    {
        public string Id;
        public string Line;
        public System.Func<Sight, bool> When;
        public bool NeedsAnimal;
    }

    private static List<Note> all;

    public static IReadOnlyList<Note> All => all ?? (all = Build());

    private static bool Night(Sight s) => s.Hour < 0.22f || s.Hour > 0.80f;

    private static bool Dusk(Sight s) => s.Hour > 0.66f && s.Hour < 0.84f;

    private static bool Dawn(Sight s) => s.Hour > 0.16f && s.Hour < 0.32f;

    private static bool Wet(Sight s) => s.Overcast > 0.68f;

    private static List<Note> Build()
    {
        var notes = new List<Note>();

        void Add(string id, string line, System.Func<Sight, bool> when, bool needsAnimal = true)
        {
            notes.Add(new Note { Id = id, Line = line, When = when, NeedsAnimal = needsAnimal });
        }

        // --- the plain things, which is most of what there is to see ------
        Add("deer-graze", "a deer with its head down in the grass",
            s => s.Kind == FaunaKind.Deer && s.Doing == Doing.Grazing);

        Add("rabbit-hop", "how a rabbit gets about, which is not by walking",
            s => s.Kind == FaunaKind.Rabbit && s.Doing == Doing.Walking);

        Add("fox-trot", "a fox going somewhere, low and quick and not in a hurry",
            s => s.Kind == FaunaKind.Fox && s.Doing == Doing.Walking);

        Add("goat-moving", "a goat picking its way along, lifting its feet higher than it needs to",
            s => s.Kind == FaunaKind.Goat && s.Doing == Doing.Walking);

        Add("held-eye", "an animal that stood and watched you instead of leaving",
            s => s.Doing == Doing.Watching);

        Add("bolted", "how fast a thing goes once it decides you are too near",
            s => s.Doing == Doing.Fleeing);

        Add("looking-up", "a grazing animal lifting its head for no reason you can see",
            s => s.Doing == Doing.Standing);

        Add("any-drink", "an animal at the water's edge, drinking",
            s => s.Doing == Doing.Drinking);

        Add("any-rest", "something lying down with its legs folded under it",
            s => s.Doing == Doing.Resting);

        Add("company-two", "two of a kind on the same ground, keeping loose company",
            s => s.Company >= 2);

        // --- and where they keep ------------------------------------------
        Add("goat-high", "a goat on ground above where anything else grazes",
            s => s.Kind == FaunaKind.Goat && s.Relief > 0.5f);

        Add("rabbit-low", "a rabbit out in the open, well away from the high ground",
            s => s.Kind == FaunaKind.Rabbit && s.Relief < 0.2f);

        Add("deer-wood", "deer keeping to the lower, greener ground",
            s => s.Kind == FaunaKind.Deer && s.Relief < 0.35f);

        Add("near-water", "an animal that has not gone far from the water",
            s => s.ByWater);

        Add("clear-of-ruins", "nothing grazing within a stone's throw of the ruins",
            s => s.NearRuin);

        Add("goat-slope", "a goat crossing a slope you could not keep your feet on",
            s => s.Kind == FaunaKind.Goat && s.Slope > 22f);

        Add("goat-snow", "a goat standing in snow",
            s => s.Kind == FaunaKind.Goat && s.Snow);

        // --- what they make of you ----------------------------------------
        Add("close-quarters", "a creature that let you within five paces of it",
            s => s.Distance < 5f && s.Doing != Doing.Fleeing);

        Add("unbothered", "an animal that went on eating with you stood over it",
            s => s.Doing == Doing.Grazing && s.Distance < 9f);

        Add("watched-close", "being looked at, straight on, by something wild",
            s => s.Doing == Doing.Watching && s.Distance < 10f);

        Add("kept-distance", "how far off a thing will let you come, and no further",
            s => s.Doing == Doing.Fleeing && s.Distance > 18f);

        Add("water-turn", "an animal turning along the shore rather than going into the water",
            s => s.Doing == Doing.Fleeing && s.ByWater);

        // --- the hours -----------------------------------------------------
        Add("graze-dawn", "a deer with its head down at first light, undisturbed",
            s => s.Kind == FaunaKind.Deer && s.Doing == Doing.Grazing && Dawn(s));

        Add("graze-dusk", "deer out on the grass as the light goes",
            s => s.Kind == FaunaKind.Deer && s.Doing == Doing.Grazing && Dusk(s));

        Add("fox-night", "a fox abroad in the dark, when nothing else is",
            s => s.Kind == FaunaKind.Fox && Night(s));

        Add("fox-drink-night", "a fox at the water in the dark",
            s => s.Kind == FaunaKind.Fox && s.Doing == Doing.Drinking && Night(s));

        Add("rest-night", "an animal bedded down in the small hours",
            s => s.Doing == Doing.Resting && s.Hour < 0.16f);

        Add("midday-quiet", "the middle of the day, and the grass empty of everything but rabbits",
            s => s.Kind == FaunaKind.Rabbit && s.Hour > 0.42f && s.Hour < 0.58f);

        // --- the weather ---------------------------------------------------
        Add("rest-rain", "something lying up out of the weather rather than feeding",
            s => s.Doing == Doing.Resting && Wet(s));

        Add("drink-rain", "an animal drinking in the rain, which it did not need to do",
            s => s.Doing == Doing.Drinking && Wet(s));

        Add("wet-grazing", "an animal grazing straight through a downpour",
            s => s.Doing == Doing.Grazing && Wet(s));

        // --- the rarer conjunctions ----------------------------------------
        Add("herd-three", "three deer on the same ground at once",
            s => s.Kind == FaunaKind.Deer && s.Company >= 3);

        Add("herd-four", "a herd of four, which is as many as this country holds together",
            s => s.Company >= 4);

        Add("mixed", "two kinds sharing a hillside without minding one another",
            s => s.Mixed);

        Add("fox-and-rabbit", "a rabbit feeding with a fox in sight of it, and neither much troubled",
            s => s.Kind == FaunaKind.Rabbit && s.Mixed && s.Other == FaunaKind.Fox);

        Add("goat-snow-weather", "a goat in snow with the weather coming in behind it",
            s => s.Kind == FaunaKind.Goat && s.Snow && Wet(s));

        // --- the country itself, with nothing in front of you --------------
        Add("high-ground", "how far the country runs when you are stood on the top of it",
            s => s.Standing > 0.7f, false);

        Add("at-water", "standing at the edge of water with the bottom shelving away",
            s => s.AtWater, false);

        Add("rain-high", "rain coming across the high ground with nothing to shelter under",
            s => Wet(s) && s.Standing > 0.45f, false);

        Add("night-water", "still water at night, black and flat and going nowhere",
            s => Night(s) && s.AtWater, false);

        Add("first-light", "the light coming back onto ground you crossed in the dark",
            s => s.Hour > 0.18f && s.Hour < 0.26f, false);

        Add("weather-turn", "the weather closing in, and the hillside emptying before it",
            s => s.Overcast > 0.75f, false);

        Add("dark-alone", "how dark it gets out here with nothing of yours in sight",
            s => Night(s), false);

        Add("steep-going", "ground steep enough that you go up it sideways",
            s => s.Slope > 24f, false);

        return notes;
    }
}
