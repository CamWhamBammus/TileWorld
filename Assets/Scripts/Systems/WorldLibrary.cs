using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One saved world. Almost none of a world needs storing, because the terrain,
/// the tiles, the landmarks and the region names are all functions of the seed:
/// what is kept is the seed, and what this particular player has found in it.
/// </summary>
[System.Serializable]
public class WorldSave
{
    public string id;
    public string name;
    public int seed;

    public string createdUtc;
    public string lastPlayedUtc;

    public float timeOfDay = 0.30f;
    public Vector3 playerPosition;
    public float playerYaw;

    public List<Vector2Int> visited = new List<Vector2Int>();
    public List<Vector2Int> surveyed = new List<Vector2Int>();
    public List<Vector2Int> landmarkChunks = new List<Vector2Int>();
    public List<int> landmarkKinds = new List<int>();
    public List<int> creaturesSeen = new List<int>();
    public List<Vector2Int> creatureChunks = new List<Vector2Int>();
    public List<int> guideKinds = new List<int>();       // creatures only; kept for older saves
    public List<string> bookSubjects = new List<string>();
    public List<int> guideStudies = new List<int>();
    public List<int> bookStudies = new List<int>();
    public List<string> noticed = new List<string>();       // id, then where, then when
    public List<string> noticedWhere = new List<string>();
    public List<string> noticedWhen = new List<string>();

    public bool waypointSet;
    public Vector2Int waypointChunk;

    public int Charted => visited.Count + surveyed.Count;
    public int Landmarks => landmarkChunks.Count;
}

/// <summary>
/// The shelf of worlds. One file each, so a world can be made, left, come back
/// to and thrown away without touching any of the others.
/// </summary>
public static class WorldLibrary
{
    private const string Folder = "worlds";
    private const string CurrentKey = "tileworld.current";
    private const string LegacyFile = "tileworld-save.json";

    /// <summary>The world being played, or null when none has been chosen.</summary>
    public static WorldSave Current { get; private set; }

    public static bool HasCurrent => Current != null;

    private static string Root => System.IO.Path.Combine(Application.persistentDataPath, Folder);

    private static string PathFor(string id) => System.IO.Path.Combine(Root, "world-" + id + ".json");

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        System.IO.Directory.CreateDirectory(Root);

        AdoptLegacySave();

        // Come back to whatever was being played, so starting the game does not
        // ask the question again; failing that, the most recent world.
        string id = PlayerPrefs.GetString(CurrentKey, "");

        if (!string.IsNullOrEmpty(id)) Current = Read(id);

        if (Current == null)
        {
            var worlds = All();
            if (worlds.Count > 0) Current = worlds[0];
        }

        if (Current != null)
        {
            PlayerPrefs.SetString(CurrentKey, Current.id);
            Debug.Log("[Worlds] Entering '" + Current.name + "' (seed " + Current.seed
                    + ", " + Current.Charted + " chunks charted).");
        }
    }

    /// <summary>
    /// Files a world that was started without going through the library, so a
    /// first run still ends up as a named world rather than an unsaved one.
    /// </summary>
    public static void Adopt(int seed)
    {
        if (Current != null) return;

        Current = Create(null, seed);
        PlayerPrefs.SetString(CurrentKey, Current.id);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Brings the old single save file into the library, so progress made
    /// before there were several worlds is not left behind.
    /// </summary>
    private static void AdoptLegacySave()
    {
        string legacy = System.IO.Path.Combine(Application.persistentDataPath, LegacyFile);

        if (!System.IO.File.Exists(legacy)) return;

        try
        {
            var save = JsonUtility.FromJson<WorldSave>(System.IO.File.ReadAllText(legacy));

            if (save != null && save.seed != 0)
            {
                save.id = NewId();
                save.name = "First World";
                save.createdUtc = save.lastPlayedUtc = Now();

                Write(save);
                PlayerPrefs.SetString(CurrentKey, save.id);

                Debug.Log("[Worlds] Brought the previous save in as '" + save.name + "'.");
            }

            System.IO.File.Delete(legacy);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Worlds] Could not read the previous save: " + e.Message);
        }
    }

    public static List<WorldSave> All()
    {
        var worlds = new List<WorldSave>();

        if (!System.IO.Directory.Exists(Root)) return worlds;

        foreach (string file in System.IO.Directory.GetFiles(Root, "world-*.json"))
        {
            try
            {
                var save = JsonUtility.FromJson<WorldSave>(System.IO.File.ReadAllText(file));
                if (save != null && !string.IsNullOrEmpty(save.id)) worlds.Add(save);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Worlds] Skipping unreadable save " + file + ": " + e.Message);
            }
        }

        worlds.Sort((a, b) => string.CompareOrdinal(b.lastPlayedUtc, a.lastPlayedUtc));

        return worlds;
    }

    public static WorldSave Read(string id)
    {
        string path = PathFor(id);

        if (!System.IO.File.Exists(path)) return null;

        try
        {
            return JsonUtility.FromJson<WorldSave>(System.IO.File.ReadAllText(path));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Worlds] Could not read world " + id + ": " + e.Message);
            return null;
        }
    }

    public static void Write(WorldSave save)
    {
        if (save == null) return;

        try
        {
            System.IO.Directory.CreateDirectory(Root);
            save.lastPlayedUtc = Now();
            System.IO.File.WriteAllText(PathFor(save.id), JsonUtility.ToJson(save));
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Worlds] Could not write world " + save.id + ": " + e.Message);
        }
    }

    /// <summary>A new world. Seed of zero means pick one.</summary>
    public static WorldSave Create(string name, int seed)
    {
        if (seed == 0) seed = Random.Range(1, int.MaxValue);

        var save = new WorldSave
        {
            id = NewId(),
            seed = seed,
            createdUtc = Now(),
            lastPlayedUtc = Now()
        };

        // Named after the ground it starts on, if nothing better was given.
        save.name = string.IsNullOrWhiteSpace(name)
            ? Regions.At(Vector2Int.zero, seed).Name
            : name.Trim();

        Write(save);

        return save;
    }

    public static void Delete(string id)
    {
        try
        {
            string path = PathFor(id);
            if (System.IO.File.Exists(path)) System.IO.File.Delete(path);

            if (Current != null && Current.id == id)
            {
                Current = null;
                PlayerPrefs.DeleteKey(CurrentKey);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Worlds] Could not delete world " + id + ": " + e.Message);
        }
    }

    /// <summary>
    /// Switches world and restarts the scene. Everything in the world comes
    /// from the seed, so a reload is all a different world takes.
    /// </summary>
    public static void Enter(WorldSave save)
    {
        if (save == null) return;

        // Whatever has been found in the world being left is written before it
        // is put back on the shelf.
        Object.FindFirstObjectByType<SaveCoordinator>()?.SaveNow();

        Current = save;
        PlayerPrefs.SetString(CurrentKey, save.id);
        PlayerPrefs.Save();

        ClearRuntimeState();

        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>
    /// What a player has found lives in statics, and those are only wiped when
    /// the game starts, not when a scene reloads. Without this a new world
    /// would open with the last one's chart already on it.
    /// </summary>
    public static void ClearRuntimeState()
    {
        ExplorationLog.Clear();
        LandmarkLog.Clear();
        RegionWatcher.Clear();
        LandmarkSpawner.ClearEvent();
        SightingLog.Clear();
        FieldGuide.Clear();
        Notebook.Clear();
        SketchBook.Clear();
        Stalking.Clear();
        Waypoint.Forget();
        ScreenState.Clear();
        Time.timeScale = 1f;

        // The world list was holding the cursor; the next world wants it back
        // on the camera.
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static string NewId() => System.Guid.NewGuid().ToString("N").Substring(0, 8);

    private static string Now() => System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

    /// <summary>How long ago, in words, for the world list.</summary>
    public static string Ago(string utc)
    {
        if (!System.DateTime.TryParse(utc, out var then)) return "";

        var span = System.DateTime.UtcNow - then;

        if (span.TotalMinutes < 2) return "just now";
        if (span.TotalHours < 1) return (int)span.TotalMinutes + " minutes ago";
        if (span.TotalDays < 1) return (int)span.TotalHours + " hours ago";
        if (span.TotalDays < 30) return (int)span.TotalDays + " days ago";

        return then.ToString("d MMM yyyy");
    }
}
