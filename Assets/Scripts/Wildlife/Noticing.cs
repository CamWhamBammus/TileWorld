using UnityEngine;

/// <summary>
/// The eye behind the book. Every so often it looks at whatever is in front of
/// the player, works out what is true of it, and if any of that is worth
/// remarking on and has not been remarked on before, the book writes it down.
///
/// One at a time, and never in a hurry: a dozen notes arriving at once would
/// read as a scoreboard rather than as somebody noticing things.
/// </summary>
public class Noticing : MonoBehaviour
{
    [SerializeField] private float look = 46f;      // how far off a thing still counts as seen
    [SerializeField] private float every = 0.5f;

    private ChunkManager world;
    private Transform player;
    private Camera eye;

    private float next;
    private float quiet;        // a pause between notes, so each one lands

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Noticing>() == null)
        {
            new GameObject("Noticing (runtime)").AddComponent<Noticing>();
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
        if (player == null || Time.time < next) return;

        next = Time.time + every;

        if (ScreenState.WantsCursor) return;

        // Six at once reads as a scoreboard; one every few seconds reads as
        // somebody looking up and remarking on things.
        if (Time.time < quiet) return;

        var sight = Look();

        foreach (var note in Observations.All)
        {
            if (Notebook.Has(note.Id)) continue;
            if (note.NeedsAnimal && sight.Animal == null) continue;
            if (!note.When(sight)) continue;

            string where = Regions.At(sight.Chunk, world.WorldSeed).Name;
            string when = TimeOfDay.Instance != null ? TimeOfDay.Instance.Clock() : "";

            if (Notebook.Write(note.Id, note.Line, where, when))
            {
                Notices.Show("Noted: " + note.Line);

                quiet = Time.time + 4.5f;
            }

            return;         // one at a time
        }
    }

    /// <summary>Everything that is true of what is in front of you just now.</summary>
    private Observations.Sight Look()
    {
        int seed = world.WorldSeed;

        var sight = new Observations.Sight
        {
            Hour = TimeOfDay.Instance != null ? TimeOfDay.Instance.Normalized : 0.5f,
            Overcast = TimeOfDay.Instance != null ? TimeOfDay.Instance.Overcast : 0f,
            Distance = 999f,
            Standing = Relief(player.position, seed),
            AtWater = ByWater(player.position, seed),
            NearSign = Tracks.Near(player.position, 2.5f, out var signSort, out var signOf),
            Sign = signSort,
            SignOf = signOf,
            RingSeen = Tracks.Near(player.position, 14f, out var ringSort, out _) && ringSort == Tracks.Sort.Ring
        };

        sight.Animal = Seen();

        Vector3 at = sight.Animal != null ? sight.Animal.transform.position : player.position;

        if (sight.Animal != null)
        {
            sight.Kind = sight.Animal.Kind;
            sight.Doing = sight.Animal.Busy;
            sight.Scale = sight.Animal.Scale;
            sight.Young = sight.Animal.Young;
            sight.Calling = sight.Animal.Calling;
            sight.HasCatch = sight.Animal.HasCatch;
            sight.Distance = Vector3.Distance(player.position, at);

            foreach (var other in FindObjectsByType<Animal>(FindObjectsSortMode.None))
            {
                if (other == sight.Animal) continue;

                float apart = Vector3.Distance(other.transform.position, at);

                if (apart > 24f) continue;

                if (other.Kind == sight.Kind) sight.Company++;
                else { sight.Mixed = true; sight.Other = other.Kind; }
            }

            sight.Company++;        // counting itself
        }

        sight.Chunk = WorldGrid.WorldToChunk(at);
        sight.Relief = Relief(at, seed);
        sight.Slope = sight.Animal != null ? Steepness(at, seed) : Steepness(player.position, seed);
        sight.Snow = SnowCover.IsSnowy(Tile(at.x), Tile(at.z), seed);
        sight.ByWater = ByWater(at, seed);

        var placement = Landmarks.In(sight.Chunk, seed);
        sight.NearRuin = placement.Exists && Vector3.Distance(placement.Position, player.position) < 26f;

        return sight;
    }

    /// <summary>The nearest animal actually in view.</summary>
    private Animal Seen()
    {
        var camera = Eye();

        if (camera == null) return null;

        Animal best = null;
        float closest = look;

        foreach (var animal in FindObjectsByType<Animal>(FindObjectsSortMode.None))
        {
            if (!animal.Visible) continue;
            Vector3 head = animal.Head;
            Vector3 to = head - camera.transform.position;
            float distance = to.magnitude;

            if (distance > closest) continue;
            if (Vector3.Dot(camera.transform.forward, to.normalized) < 0.34f) continue;
            if (Physics.Linecast(camera.transform.position, head)) continue;

            closest = distance;
            best = animal;
        }

        return best;
    }

    private Camera Eye()
    {
        if (eye == null) eye = Camera.main;

        return eye;
    }

    private static int Tile(float world) => Mathf.RoundToInt(world / WorldGrid.TileSize);

    private static float Relief(Vector3 at, int seed)
    {
        return WorldHeight.HeightAt(Tile(at.x), Tile(at.z), seed) / WorldHeight.MaxRelief;
    }

    private static float Steepness(Vector3 at, int seed)
    {
        int x = Tile(at.x), z = Tile(at.z);

        float west = WorldHeight.SurfaceY(x - 1, z, seed);
        float east = WorldHeight.SurfaceY(x + 1, z, seed);
        float south = WorldHeight.SurfaceY(x, z - 1, seed);
        float north = WorldHeight.SurfaceY(x, z + 1, seed);

        var normal = new Vector3(west - east, 2f * WorldGrid.TileSize, south - north).normalized;

        return Vector3.Angle(normal, Vector3.up);
    }

    private static bool ByWater(Vector3 at, int seed)
    {
        int x = Tile(at.x), z = Tile(at.z);

        for (int dx = -2; dx <= 2; dx++)
        for (int dz = -2; dz <= 2; dz++)
        {
            if (WaterSurface.IsUnderwater(x + dx, z + dz, seed)) return true;
        }

        return false;
    }
}
