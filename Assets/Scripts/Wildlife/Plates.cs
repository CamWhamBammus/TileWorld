using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The plates: what the book wants drawn of each creature. Not one drawing
/// but several, each of the animal doing a thing -- lying down, in a herd,
/// with a fish in its beak -- so that filling an entry means going back to
/// the animal at other hours and other moods rather than standing still
/// once. A drawing is made as it always was, and then asked which plates it
/// fits; every one it fits that is still blank is filled by it.
/// </summary>
public static class Plates
{
    public class Plate
    {
        public string Id;            // in the save and on the file
        public string Label;         // under the thumbnail: "lying down"
        public string Ask;           // what to do about it: "draw one lying down"
        public System.Func<Animal, Look, bool> Fits;
    }

    /// <summary>What was true of the animal when the drawing was made.</summary>
    public struct Look
    {
        public int Company;          // others of its kind within a dozen metres
        public float Hour;
        public float Relief;         // where it stood, against the highest ground
    }

    private static readonly Dictionary<FaunaKind, Plate[]> table = new Dictionary<FaunaKind, Plate[]>();

    private static Plate P(string id, string label, string ask, System.Func<Animal, Look, bool> fits)
        => new Plate { Id = id, Label = label, Ask = ask, Fits = fits };

    // the plates most animals share
    private static readonly Plate Standing = P("standing", "standing", "draw it standing", (a, l) => a.Busy == Doing.Standing || a.Busy == Doing.Grazing || a.Busy == Doing.Drinking || a.Busy == Doing.Watching);
    private static readonly Plate Moving = P("moving", "on the move", "draw it walking or running", (a, l) => a.Busy == Doing.Walking || a.Busy == Doing.Fleeing || a.Busy == Doing.Hunting);
    private static readonly Plate Resting = P("resting", "lying down", "draw it lying down", (a, l) => a.Busy == Doing.Resting);
    private static readonly Plate Flight = P("flight", "in flight", "draw it in the air", (a, l) => a.Aloft);
    private static bool Dusk(Look l) => l.Hour > 0.64f && l.Hour < 0.86f;
    private static bool Dark(Look l) => l.Hour > 0.80f || l.Hour < 0.20f;

    public static Plate[] For(FaunaKind kind)
    {
        if (table.TryGetValue(kind, out var known)) return known;

        Plate[] plates;
        switch (kind)
        {
            case FaunaKind.Deer:
                plates = new[] { Standing, Moving, Resting,
                    P("herd", "in a herd", "draw one with others about it", (a, l) => l.Company >= 2),
                    P("bellow", "bellowing at dusk", "draw a stag with its head up at dusk", (a, l) => a.Calling && Dusk(l)) };
                break;
            case FaunaKind.Rabbit:
                plates = new[] { Standing, Moving, Resting,
                    P("situp", "sitting up", "draw one up on its haunches", (a, l) => a.Pose == "SitUp") };
                break;
            case FaunaKind.Fox:
                plates = new[] { Standing, Moving, Resting,
                    P("night", "abroad at night", "draw one after dark", (a, l) => Dark(l)),
                    P("hunt", "hunting", "draw one after a rabbit", (a, l) => a.Busy == Doing.Hunting || a.Pose == "Pounce") };
                break;
            case FaunaKind.Goat:
                plates = new[] { Standing, Moving, Resting,
                    P("heights", "on the heights", "draw one high up on the rock", (a, l) => l.Relief > 0.5f) };
                break;
            case FaunaKind.Tortoise:
                plates = new[] { Standing, Moving,
                    P("shut", "shut in its shell", "get close, and draw it pulled in", (a, l) => a.Busy == Doing.Watching) };
                break;
            case FaunaKind.Wolf:
                plates = new[] { Standing, Moving, Resting,
                    P("howl", "howling", "draw one with its muzzle to the sky", (a, l) => a.Calling),
                    P("pair", "the pair", "draw one with the other near", (a, l) => l.Company >= 1) };
                break;
            case FaunaKind.Heron:
                plates = new[] { Standing, Flight,
                    P("fishing", "fishing", "draw it with its head over the water", (a, l) => a.Busy == Doing.Grazing && !a.HasCatch),
                    P("catch", "with a fish", "draw it with a fish in its beak", (a, l) => a.HasCatch) };
                break;
            case FaunaKind.Boar:
                plates = new[] { Standing, Moving, Resting,
                    P("rooting", "rooting", "draw it with its snout in the ground", (a, l) => a.Busy == Doing.Grazing),
                    P("sounder", "a sounder", "draw one with others about it", (a, l) => l.Company >= 2) };
                break;
            case FaunaKind.Raven:
                plates = new[] { Standing, Flight,
                    P("pecking", "pecking", "draw it at the ground", (a, l) => a.Busy == Doing.Grazing) };
                break;
            case FaunaKind.Marmot:
                plates = new[] { Standing, Moving,
                    P("situp", "sitting up", "draw one up on its haunches, keeping watch", (a, l) => a.Pose == "SitUp") };
                break;
            case FaunaKind.Crab:
                plates = new[] { Standing, Moving,
                    P("claws", "claws up", "get close, and draw it standing its ground", (a, l) => a.Busy == Doing.Watching) };
                break;
            case FaunaKind.Owl:
                plates = new[] { P("perched", "perched", "draw it on its perch", (a, l) => a.Perched), Flight };
                break;
            case FaunaKind.Frog:
                plates = new[] { Standing,
                    P("chorus", "in a chorus", "draw one with others calling round it", (a, l) => l.Company >= 2) };
                break;
            case FaunaKind.Bat:
                plates = new[] { P("wing", "on the wing", "draw it over the water at dusk", (a, l) => true) };
                break;
            case FaunaKind.Hedgehog:
                plates = new[] { Moving,
                    P("curled", "curled up", "get close, and draw it in a ball", (a, l) => a.Busy == Doing.Watching) };
                break;
            case FaunaKind.Fish:
                plates = new[] { P("rising", "rising", "draw it the moment it comes up", (a, l) => true) };
                break;
            case FaunaKind.Eagle:
                plates = new[] { P("circling", "circling", "draw it against the sky", (a, l) => true) };
                break;
            case FaunaKind.Hare:
                plates = new[] { Standing, Resting,
                    P("flat", "lying flat", "draw one gone flat, before it runs", (a, l) => a.Busy == Doing.Watching),
                    P("run", "at full stretch", "draw one running", (a, l) => a.Busy == Doing.Fleeing) };
                break;
            case FaunaKind.Scorpion:
                plates = new[] { Standing,
                    P("sting", "sting up", "get close, and draw it with its sting raised", (a, l) => a.Busy == Doing.Watching) };
                break;
            default:
                plates = new[] { Standing, Moving };
                break;
        }

        table[kind] = plates;
        return plates;
    }

    /// <summary>The plate with this id, for a kind, or null.</summary>
    public static Plate Find(FaunaKind kind, string id)
    {
        foreach (var p in For(kind)) if (p.Id == id) return p;
        return null;
    }

    /// <summary>Every plate this animal, as it is now, would fill.</summary>
    public static List<Plate> Matching(Animal animal, Look look)
    {
        var fits = new List<Plate>();
        foreach (var p in For(animal.Kind))
        {
            try { if (p.Fits(animal, look)) fits.Add(p); }
            catch { }
        }
        return fits;
    }

    /// <summary>What is true of an animal now, for matching.</summary>
    public static Look LookAt(Animal animal, int seed)
    {
        int company = 0;
        foreach (var other in Wildlife.Near(animal.transform.position, 12f))
            if (other != animal && other.Kind == animal.Kind && other.Visible) company++;

        int x = Mathf.RoundToInt(animal.transform.position.x / WorldGrid.TileSize);
        int z = Mathf.RoundToInt(animal.transform.position.z / WorldGrid.TileSize);

        return new Look
        {
            Company = company,
            Hour = TimeOfDay.Instance != null ? TimeOfDay.Instance.Normalized : 0.5f,
            Relief = WorldHeight.HeightAt(x, z, seed) / WorldHeight.MaxRelief
        };
    }

    /// <summary>The drawing's key on the shelf and on disk: the subject, then the plate.</summary>
    public static string Key(Subject subject, string plateId) => subject.Key + "-" + plateId;
}
