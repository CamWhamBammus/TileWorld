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
        Add("deer-graze", "a deer grazing",
            s => s.Kind == FaunaKind.Deer && s.Doing == Doing.Grazing);

        Add("rabbit-hop", "a rabbit hopping",
            s => s.Kind == FaunaKind.Rabbit && s.Doing == Doing.Walking);

        Add("fox-trot", "a fox trotting past",
            s => s.Kind == FaunaKind.Fox && s.Doing == Doing.Walking);

        Add("goat-moving", "a goat picking its way along",
            s => s.Kind == FaunaKind.Goat && s.Doing == Doing.Walking);

        Add("tortoise-sun", "a tortoise out on the sand",
            s => s.Kind == FaunaKind.Tortoise);

        Add("tortoise-shut", "a tortoise pulled into its shell",
            s => s.Kind == FaunaKind.Tortoise && s.Doing == Doing.Watching);

        Add("wolf-night", "a wolf abroad in the dark",
            s => s.Kind == FaunaKind.Wolf && Night(s));

        Add("wolf-pair", "two wolves together",
            s => s.Kind == FaunaKind.Wolf && s.Company >= 2);

        Add("heron-still", "a heron standing in the shallows",
            s => s.Kind == FaunaKind.Heron && s.ByWater);

        Add("heron-flight", "a heron taking flight",
            s => s.Kind == FaunaKind.Heron && s.Doing == Doing.Fleeing);

        Add("boar-root", "a boar rooting",
            s => s.Kind == FaunaKind.Boar && s.Doing == Doing.Grazing);

        Add("boar-sounder", "a sounder of boar",
            s => s.Kind == FaunaKind.Boar && s.Company >= 3);

        Add("raven-ground", "a raven on the ground",
            s => s.Kind == FaunaKind.Raven);

        Add("raven-flight", "a raven taking flight",
            s => s.Kind == FaunaKind.Raven && s.Doing == Doing.Fleeing);

        Add("marmot-watch", "a marmot sitting up on the rock",
            s => s.Kind == FaunaKind.Marmot && s.Doing == Doing.Standing);

        Add("marmot-gone", "a marmot gone to ground",
            s => s.Kind == FaunaKind.Marmot && s.Doing == Doing.Fleeing);

        Add("crab-shore", "a crab on the shore",
            s => s.Kind == FaunaKind.Crab);

        Add("crab-claws", "a crab with its claws up",
            s => s.Kind == FaunaKind.Crab && s.Doing == Doing.Watching);

        Add("owl-night", "an owl abroad in the dark",
            s => s.Kind == FaunaKind.Owl && Night(s));

        Add("owl-ruin", "an owl up on a ruin",
            s => s.Kind == FaunaKind.Owl && s.NearRuin);

        Add("frog-pond", "a frog at the water",
            s => s.Kind == FaunaKind.Frog && s.ByWater);

        Add("frog-chorus", "frogs calling after dark",
            s => s.Kind == FaunaKind.Frog && Night(s) && s.Company >= 2);

        Add("bat-dusk", "bats over the water",
            s => s.Kind == FaunaKind.Bat && s.ByWater);

        Add("hedgehog-night", "a hedgehog in the litter",
            s => s.Kind == FaunaKind.Hedgehog);

        Add("hedgehog-ball", "a hedgehog curled up",
            s => s.Kind == FaunaKind.Hedgehog && s.Doing == Doing.Watching);

        Add("fish-rise", "a fish rising",
            s => s.Kind == FaunaKind.Fish && s.Doing == Doing.Walking);

        Add("held-eye", "an animal watching you",
            s => s.Doing == Doing.Watching);

        Add("bolted", "an animal running off",
            s => s.Doing == Doing.Fleeing);

        Add("looking-up", "an animal looking up from the grass",
            s => s.Doing == Doing.Standing);

        Add("any-drink", "an animal drinking",
            s => s.Doing == Doing.Drinking);

        Add("any-rest", "an animal lying down",
            s => s.Doing == Doing.Resting);

        Add("company-two", "two of a kind together",
            s => s.Company >= 2);

        // --- and where they keep ------------------------------------------
        Add("goat-high", "a goat up on the high ground",
            s => s.Kind == FaunaKind.Goat && s.Relief > 0.5f);

        Add("rabbit-low", "a rabbit out in the open",
            s => s.Kind == FaunaKind.Rabbit && s.Relief < 0.2f);

        Add("deer-wood", "deer down on the low ground",
            s => s.Kind == FaunaKind.Deer && s.Relief < 0.35f);

        Add("near-water", "an animal close to water",
            s => s.ByWater);

        Add("clear-of-ruins", "empty ground around a ruin",
            s => s.NearRuin);

        Add("goat-slope", "a goat on a steep slope",
            s => s.Kind == FaunaKind.Goat && s.Slope > 22f);

        Add("goat-snow", "a goat standing in snow",
            s => s.Kind == FaunaKind.Goat && s.Snow);

        // --- what they make of you ----------------------------------------
        Add("close-quarters", "getting within five paces of something",
            s => s.Distance < 5f && s.Doing != Doing.Fleeing);

        Add("unbothered", "an animal that kept eating with you next to it",
            s => s.Doing == Doing.Grazing && s.Distance < 9f);

        Add("watched-close", "an animal looking straight at you",
            s => s.Doing == Doing.Watching && s.Distance < 10f);

        Add("kept-distance", "how close an animal will let you get",
            s => s.Doing == Doing.Fleeing && s.Distance > 18f);

        Add("water-turn", "an animal running along the shore instead of into the water",
            s => s.Doing == Doing.Fleeing && s.ByWater);

        // --- the hours -----------------------------------------------------
        Add("graze-dawn", "a deer grazing at first light",
            s => s.Kind == FaunaKind.Deer && s.Doing == Doing.Grazing && Dawn(s));

        Add("graze-dusk", "deer out on the grass at dusk",
            s => s.Kind == FaunaKind.Deer && s.Doing == Doing.Grazing && Dusk(s));

        Add("fox-night", "a fox out after dark",
            s => s.Kind == FaunaKind.Fox && Night(s));

        Add("fox-drink-night", "a fox drinking at night",
            s => s.Kind == FaunaKind.Fox && s.Doing == Doing.Drinking && Night(s));

        Add("rest-night", "an animal bedded down at night",
            s => s.Doing == Doing.Resting && s.Hour < 0.16f);

        Add("midday-quiet", "a rabbit out at midday",
            s => s.Kind == FaunaKind.Rabbit && s.Hour > 0.42f && s.Hour < 0.58f);

        // --- the weather ---------------------------------------------------
        Add("rest-rain", "an animal lying up in the rain",
            s => s.Doing == Doing.Resting && Wet(s));

        Add("drink-rain", "an animal drinking in the rain",
            s => s.Doing == Doing.Drinking && Wet(s));

        Add("wet-grazing", "an animal grazing in the rain",
            s => s.Doing == Doing.Grazing && Wet(s));

        // --- the rarer conjunctions ----------------------------------------
        Add("herd-three", "three deer together",
            s => s.Kind == FaunaKind.Deer && s.Company >= 3);

        Add("herd-four", "four deer together",
            s => s.Company >= 4);

        Add("mixed", "two different kinds on the same ground",
            s => s.Mixed);

        Add("fox-and-rabbit", "a fox and a rabbit near each other",
            s => s.Kind == FaunaKind.Rabbit && s.Mixed && s.Other == FaunaKind.Fox);

        Add("goat-snow-weather", "a goat in snow with the weather coming in",
            s => s.Kind == FaunaKind.Goat && s.Snow && Wet(s));

        // --- the country itself, with nothing in front of you --------------
        Add("high-ground", "the view from high ground",
            s => s.Standing > 0.7f, false);

        Add("at-water", "standing at the water's edge",
            s => s.AtWater, false);

        Add("rain-high", "rain up on the high ground",
            s => Wet(s) && s.Standing > 0.45f, false);

        Add("night-water", "water at night",
            s => Night(s) && s.AtWater, false);

        Add("first-light", "first light",
            s => s.Hour > 0.18f && s.Hour < 0.26f, false);

        Add("weather-turn", "the weather closing in",
            s => s.Overcast > 0.75f, false);

        Add("dark-alone", "being out in the dark",
            s => Night(s), false);

        Add("steep-going", "ground too steep to walk straight up",
            s => s.Slope > 24f, false);

        return notes;
    }
}
