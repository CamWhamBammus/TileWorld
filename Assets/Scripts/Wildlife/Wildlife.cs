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
    [SerializeField] private int population = 16;

    // Brought on closer than they were. Spread thinly over a wide ring most of
    // them were somewhere behind you the whole time and the world read as empty.
    [SerializeField] private float nearest = 24f;
    [SerializeField] private float furthest = 60f;
    [SerializeField] private float forget = 100f;

    [Tooltip("How close, and how plainly in view, before it counts as seen.")]
    [SerializeField] private float sightRange = 52f;

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

            if (gone || bedded)
            {
                living.RemoveAt(i);
                Destroy(animal.gameObject);
            }
        }

        // Groups arrive whole, so how many turn up is not known in advance.
        for (int attempt = 0; attempt < 6 && living.Count < population; attempt++) TryBring(now);
    }

    /// <summary>
    /// Looks for somewhere out of the way that would suit something, and puts
    /// one there. Animals arrive behind you or far enough off not to appear out
    /// of nothing in front of your face.
    /// </summary>
    private void TryBring(float now)
    {
        int seed = world.WorldSeed;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(nearest, furthest);

            Vector3 at = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

            if (InPlainView(at, radius)) continue;

            int x = Mathf.RoundToInt(at.x / WorldGrid.TileSize);
            int z = Mathf.RoundToInt(at.z / WorldGrid.TileSize);

            var kind = KindFor(x, z, seed, now);

            if (kind == null) continue;

            // However many of that kind keep company, as long as there is room
            // for them and ground to put them on.
            int company = Random.Range(1, Fauna.Company(kind.Value) + 1);

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

                Bring(kind.Value, seed, spot);
            }

            return;
        }
    }

    private void Bring(FaunaKind kind, int seed, Vector3 at)
    {
        var go = new GameObject(Fauna.Of(kind).Name);
        go.transform.SetParent(transform, false);

        var animal = go.AddComponent<Animal>();
        animal.Settle(kind, seed, player, at);

        living.Add(animal);
    }

    /// <summary>Which of the kinds abroad at this hour would suit this ground.</summary>
    private FaunaKind? KindFor(int tileX, int tileZ, int seed, float now)
    {
        // Start somewhere different each time, or the first kind in the list
        // takes every spot that suits more than one animal.
        int start = Random.Range(0, 4);

        for (int i = 0; i < 4; i++)
        {
            var kind = (FaunaKind)((start + i) % 4);

            if (!Fauna.Suits(kind, tileX, tileZ, seed, now)) continue;
            if (CountOf(kind) >= Fauna.Crowd(kind)) continue;

            return kind;
        }

        return null;
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

    /// <summary>Whether a spot is somewhere the player would watch it appear.</summary>
    private bool InPlainView(Vector3 at, float radius)
    {
        if (radius > 62f) return false;         // far enough not to notice

        var camera = Eye();

        if (camera == null) return false;

        Vector3 to = at - camera.transform.position;
        to.y = 0f;

        return Vector3.Dot(camera.transform.forward, to.normalized) > 0.35f;
    }

    /// <summary>Notices when a kind has been seen properly for the first time.</summary>
    private void Watch()
    {
        var camera = Eye();

        if (camera == null) return;

        foreach (var animal in living)
        {
            if (animal == null || SightingLog.Has(animal.Kind)) continue;

            Vector3 head = animal.transform.position + Vector3.up * Fauna.Of(animal.Kind).Size;
            Vector3 to = head - camera.transform.position;

            if (to.magnitude > sightRange) continue;
            if (Vector3.Dot(camera.transform.forward, to.normalized) < 0.55f) continue;

            // A hill between you and it does not count as having seen it.
            if (Physics.Linecast(camera.transform.position, head)) continue;

            if (SightingLog.Record(animal.Kind))
            {
                Notices.Show(Fauna.OnFirstSight(animal.Kind));
            }
        }
    }

    private Camera Eye()
    {
        if (eye == null) eye = Camera.main;

        return eye;
    }
}
