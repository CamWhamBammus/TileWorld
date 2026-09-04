using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Keeps a handful of animals about wherever the player happens to be. They
/// are not placed in the world and not saved: which creature is where is a
/// question with no lasting answer, so a small population is kept near you and
/// quietly retired once you have walked away from it.
///
/// What the world does remember is which kinds you have seen, which is the
/// only part a player would notice losing.
/// </summary>
public class Wildlife : MonoBehaviour
{
    [Tooltip("How many animals are about at once, across every kind.")]
    [SerializeField] private int population = 24;

    [SerializeField] private float nearest = 22f;
    [SerializeField] private float furthest = 82f;
    [SerializeField] private float forget = 135f;

    [Tooltip("How much of the population is brought on ahead of the player.")]
    [SerializeField, Range(0f, 1f)] private float aheadShare = 0.72f;

    [Tooltip("Inside this, an animal arriving in front of you would be seen to appear.")]
    [SerializeField] private float tooCloseToArrive = 40f;

    [Tooltip("How close, and how plainly in view, before it counts as seen.")]
    [SerializeField] private float sightRange = 52f;

    private static Wildlife instance;

    private ChunkManager world;
    private Transform player;
    private Camera eye;

    private readonly List<Animal> living = new List<Animal>();
    private float nextCensus;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Wildlife>() == null)
        {
            new GameObject("Wildlife (runtime)").AddComponent<Wildlife>();
        }
    }

    private void Start()
    {
        instance = this;
        world = FindFirstObjectByType<ChunkManager>();

        if (world == null)
        {
            enabled = false;
            return;
        }

        player = world.PlayerTransform;
    }

    private void Update()
    {
        if (player == null) return;

        // The population only needs revisiting now and then; the animals
        // themselves move every frame.
        if (Time.time >= nextCensus)
        {
            nextCensus = Time.time + 1f;
            Census();
        }

        Watch();
    }

    /// <summary>Retires what has been left behind, and brings on what is missing.</summary>
    private void Census()
    {
        float now = TimeOfDay.Instance != null ? TimeOfDay.Instance.Normalized : 0.5f;

        for (int i = living.Count - 1; i >= 0; i--)
        {
            var animal = living[i];

            if (animal == null)
            {
                living.RemoveAt(i);
                continue;
            }

            bool gone = animal.DistanceTo(player.position) > forget;
            bool bedded = !Fauna.Awake(animal.Kind, now);

            // A bird on the ground out of its hours roosts where it stands
            // rather than vanishing; the rest go, as they always did.
            bool roosts = Fauna.Flies(animal.Kind) && !Fauna.All(animal.Kind).Airborne;
            if (roosts && !gone) { animal.Roost(bedded); continue; }

            if (gone || bedded)
            {
                living.RemoveAt(i);
                Destroy(animal.gameObject);
            }
        }

        // Hard weather keeps some of them under cover, so the hillside in a
        // downpour is not as busy as it is on a clear evening.
        float overcast = TimeOfDay.Instance != null ? TimeOfDay.Instance.Overcast : 0f;
        int wanted = Mathf.RoundToInt(population * Mathf.Lerp(1f, 0.62f, overcast));

        // Groups arrive whole, so how many turn up is not known in advance.
        for (int attempt = 0; attempt < 6 && living.Count < wanted; attempt++) TryBring(now);
    }

    /// <summary>
    /// Looks for somewhere out of the way that would suit something, and puts
    /// one there. Animals arrive behind you or far enough off not to appear out
    /// of nothing in front of your face.
    /// </summary>
    /// <summary>
    /// Puts one animal out in front, near enough to be found. For somebody who
    /// has never played: the first thing to draw should not be a matter of luck.
    /// </summary>
    public static bool BringOneClose()
    {
        var it = instance;

        if (it == null || it.player == null || it.world == null) return false;

        int seed = it.world.WorldSeed;
        float now = TimeOfDay.Instance != null ? TimeOfDay.Instance.Normalized : 0.5f;

        // Two passes. The first asks for something that suits both the ground
        // and the hour, which is what the country would ordinarily put there.
        // The second drops the hour: for the one animal that has to be found,
        // standing somewhere at the wrong time of day beats not being there.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                float angle = Random.Range(-70f, 70f);
                Vector3 at = it.player.position
                           + Quaternion.Euler(0f, angle, 0f) * it.Facing() * Random.Range(17f, 28f);

                int x = Mathf.RoundToInt(at.x / WorldGrid.TileSize);
                int z = Mathf.RoundToInt(at.z / WorldGrid.TileSize);

                FaunaKind? kind = pass == 0 ? it.KindFor(x, z, seed, now) : AnySuiting(x, z, seed);

                if (kind == null) continue;
                if (!Fauna.Ground(kind.Value, x, z, seed)) continue;

                it.Bring(kind.Value, seed, at);
                return true;
            }
        }

        return false;
    }

    /// <summary>Any kind at all that would stand on this ground.</summary>
    private static FaunaKind? AnySuiting(int tileX, int tileZ, int seed)
    {
        foreach (FaunaKind kind in System.Enum.GetValues(typeof(FaunaKind)))
        {
            if (Fauna.Ground(kind, tileX, tileZ, seed)) return kind;
        }

        return null;
    }

    private void TryBring(float now)
    {
        int seed = world.WorldSeed;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            float radius = Random.Range(nearest, furthest);

            // Most of them out in front, where you are going to walk. The rest
            // anywhere, so the country behind you is not conspicuously empty.
            Vector3 heading = Facing();

            float spread = Random.value < aheadShare ? 70f : 180f;
            float angle = Random.Range(-spread, spread);

            Vector3 out_ = Quaternion.Euler(0f, angle, 0f) * heading;

            Vector3 at = player.position + out_ * radius;

            if (PopsIntoView(at, radius)) continue;

            int x = Mathf.RoundToInt(at.x / WorldGrid.TileSize);
            int z = Mathf.RoundToInt(at.z / WorldGrid.TileSize);

            var kind = KindFor(x, z, seed, now);

            if (kind == null) continue;

            // However many of that kind keep company, as long as there is room
            // for them and ground to put them on.
            int company = Random.Range(1, Fauna.Company(kind.Value) + 1);

            // the first of a company leads it; the rest keep with the leader
            Animal leader = null;

            for (int i = 0; i < company && living.Count < population; i++)
            {
                if (CountOf(kind.Value) >= Fauna.Crowd(kind.Value)) break;

                Vector3 spot = at;

                if (i > 0)
                {
                    Vector2 apart = Random.insideUnitCircle * 9f;
                    spot += new Vector3(apart.x, 0f, apart.y);

                    int cx = Mathf.RoundToInt(spot.x / WorldGrid.TileSize);
                    int cz = Mathf.RoundToInt(spot.z / WorldGrid.TileSize);

                    if (!Fauna.Ground(kind.Value, cx, cz, seed)) continue;
                }

                // some of the company are this year's young, small and kept
                // close, on the kinds that keep company at all
                bool young = leader != null && Fauna.Company(kind.Value) >= 3 && Random.value < 0.35f;
                var one = Bring(kind.Value, seed, spot, young);
                if (leader == null) leader = one; else one.Leader = leader;
            }

            return;
        }
    }

    private Animal Bring(FaunaKind kind, int seed, Vector3 at, bool young = false)
    {
        var go = new GameObject(Fauna.Of(kind).Name);
        go.transform.SetParent(transform, false);

        var animal = go.AddComponent<Animal>();
        animal.Settle(kind, seed, player, at, young ? Random.Range(0.55f, 0.68f) : 0f, young);

        living.Add(animal);
        return animal;
    }

    /// <summary>
    /// One animal's alarm reaches the others. Whatever is within earshot and
    /// is not itself a hunter takes the same fright, from the same quarter:
    /// one deer's bark sends the herd off, a marmot's whistle puts every
    /// marmot on the slope down its hole.
    /// </summary>
    public static void Alarm(Animal from, Vector3 threat, bool run, float reach)
    {
        if (instance == null) return;
        foreach (var other in instance.living)
        {
            if (other == null || other == from) continue;
            if (Fauna.Hunts(other.Kind)) continue;
            if (Vector3.Distance(other.transform.position, from.transform.position) > reach) continue;
            other.Startle(threat, run, Random.Range(0.1f, 0.6f));
        }
    }

    /// <summary>A call answered: the nearest other of its kind within earshot calls back, once.</summary>
    public static void Answer(Animal from, float reach, float delay)
    {
        if (instance == null) return;
        Animal nearest = null; float closest = reach;
        foreach (var other in instance.living)
        {
            if (other == null || other == from || other.Kind != from.Kind || !other.Visible) continue;
            float d = Vector3.Distance(other.transform.position, from.transform.position);
            if (d < closest) { closest = d; nearest = other; }
        }
        if (nearest != null) nearest.SpeakBack(delay);
    }

    /// <summary>One wolf's howl is taken up by the others within earshot, a moment apart.</summary>
    public static void Chorus(Animal from, float reach)
    {
        if (instance == null) return;
        float delay = 0.6f;
        foreach (var other in instance.living)
        {
            if (other == null || other == from || other.Kind != from.Kind) continue;
            if (Vector3.Distance(other.transform.position, from.transform.position) > reach) continue;
            other.Answer(delay);
            delay += Random.Range(0.4f, 1.1f);
        }
    }

    /// <summary>The nearest of the kinds asked for within reach of a hunter, if any is out to be seen.</summary>
    public static Animal Nearest(Animal hunter, FaunaKind[] kinds, float within)
    {
        if (instance == null || kinds == null) return null;
        Animal best = null; float closest = within;
        foreach (var other in instance.living)
        {
            if (other == null || other == hunter || !other.Visible) continue;
            bool wanted = false;
            foreach (var k in kinds) if (other.Kind == k) wanted = true;
            if (!wanted) continue;
            float d = Vector3.Distance(other.transform.position, hunter.transform.position);
            if (d < closest) { closest = d; best = other; }
        }
        return best;
    }

    /// <summary>Which of the kinds abroad at this hour would suit this ground.</summary>
    private FaunaKind? KindFor(int tileX, int tileZ, int seed, float now)
    {
        // Start somewhere different each time, or the first kind in the list
        // takes every spot that suits more than one animal.
        int count = Fauna.Count;
        int start = Random.Range(0, count);

        for (int i = 0; i < count; i++)
        {
            var kind = (FaunaKind)((start + i) % count);

            if (!Fauna.Suits(kind, tileX, tileZ, seed, now)) continue;
            if (CountOf(kind) >= Fauna.Crowd(kind)) continue;

            return kind;
        }

        return null;
    }

    /// <summary>Where the player is looking, flattened.</summary>
    private Vector3 Facing()
    {
        var camera = Eye();

        Vector3 forward = camera != null ? camera.transform.forward : player.forward;
        forward.y = 0f;

        return forward.sqrMagnitude < 0.001f ? Vector3.forward : forward.normalized;
    }

    private int CountOf(FaunaKind kind)
    {
        int count = 0;

        foreach (var animal in living)
        {
            if (animal != null && animal.Kind == kind) count++;
        }

        return count;
    }

    /// <summary>
    /// Whether a spot is close enough, and square enough in front of the
    /// player, that they would watch the animal appear out of nothing.
    ///
    /// This wants to be a narrow rule. Written as "anywhere ahead of you", the
    /// only places left to put an animal are behind your back — and since you
    /// walk forwards, everything that was ever brought on fell behind you and
    /// was retired again without being seen once.
    /// </summary>
    private bool PopsIntoView(Vector3 at, float radius)
    {
        if (radius > tooCloseToArrive) return false;

        var camera = Eye();

        if (camera == null) return false;

        Vector3 to = at - camera.transform.position;
        to.y = 0f;

        return Vector3.Dot(camera.transform.forward, to.normalized) > 0.55f;
    }

    /// <summary>Notices when a kind has been seen properly for the first time.</summary>
    private void Watch()
    {
        var camera = Eye();

        if (camera == null) return;

        foreach (var animal in living)
        {
            if (animal == null || SightingLog.Has(animal.Kind)) continue;
            if (!animal.Visible) continue;

            // the head itself, wherever it is: up a snag, on the wing, or
            // sunk down a burrow -- not a point above the animal's root
            Vector3 head = animal.Head;
            Vector3 to = head - camera.transform.position;

            if (to.magnitude > sightRange) continue;
            if (Vector3.Dot(camera.transform.forward, to.normalized) < 0.55f) continue;

            // A hill between you and it does not count as having seen it.
            if (Physics.Linecast(camera.transform.position, head)) continue;

            if (SightingLog.Record(animal.Kind, WorldGrid.WorldToChunk(animal.transform.position)))
            {
                var region = Regions.At(WorldGrid.WorldToChunk(animal.transform.position), world.WorldSeed);

                Notices.Show("First " + Fauna.Of(animal.Kind).Name + " seen — " + region.Name);
            }
        }
    }

    private Camera Eye()
    {
        if (eye == null) eye = Camera.main;

        return eye;
    }
}
