using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The keys, and how they are changed. Moving, jumping and sprinting are
/// actions in the player's input asset, and a change to those is a binding
/// override kept as JSON and put back on the asset whenever a scene comes
/// up. The rest -- the sketchbook, drawing, the map, the journal, resting,
/// saving the map, the statistics -- are read straight off the keyboard,
/// and each is a KeyCode kept by name. Defaults are what the README says.
/// </summary>
public static class Keys
{
    /// <summary>A key read straight off the keyboard.</summary>
    public class Named { public string Id, Name; public KeyCode Default; }

    public static readonly Named[] Legacy =
    {
        new Named { Id = "sketchbook", Name = "Sketchbook", Default = KeyCode.G },
        new Named { Id = "draw", Name = "Draw (hold)", Default = KeyCode.F },
        new Named { Id = "map", Name = "Map", Default = KeyCode.M },
        new Named { Id = "journal", Name = "Journal", Default = KeyCode.J },
        new Named { Id = "rest", Name = "Rest at a find", Default = KeyCode.E },
        new Named { Id = "mappicture", Name = "Save the map", Default = KeyCode.F9 },
        new Named { Id = "stats", Name = "World statistics", Default = KeyCode.F3 },
    };

    private static Named Find(string id) { foreach (var n in Legacy) if (n.Id == id) return n; return null; }

    public static KeyCode Get(string id) => (KeyCode)PlayerPrefs.GetInt("tileworld.key." + id, (int)Find(id).Default);
    public static void Set(string id, KeyCode key) { PlayerPrefs.SetInt("tileworld.key." + id, (int)key); PlayerPrefs.Save(); }

    public static KeyCode Sketchbook => Get("sketchbook");
    public static KeyCode Draw => Get("draw");
    public static KeyCode Map => Get("map");
    public static KeyCode Journal => Get("journal");
    public static KeyCode Rest => Get("rest");
    public static KeyCode MapPicture => Get("mappicture");
    public static KeyCode Stats => Get("stats");

    /// <summary>An action in the player's asset, or one part of its keyboard composite.</summary>
    public class Bound { public string Name, Action, Part; }

    public static readonly Bound[] Actions =
    {
        new Bound { Name = "Forward", Action = "Move", Part = "up" },
        new Bound { Name = "Back", Action = "Move", Part = "down" },
        new Bound { Name = "Left", Action = "Move", Part = "left" },
        new Bound { Name = "Right", Action = "Move", Part = "right" },
        new Bound { Name = "Jump", Action = "Jump", Part = null },
        new Bound { Name = "Sprint", Action = "Sprint", Part = null },
    };

    private const string BindingsKey = "tileworld.bindings";
    private static InputActionAsset asset;

    /// <summary>The player's input asset, found among what is loaded: the player's prefab holds it.</summary>
    public static InputActionAsset Asset
    {
        get
        {
            if (asset == null)
                foreach (var a in Resources.FindObjectsOfTypeAll<InputActionAsset>())
                    if (a.name == "StarterAssets") { asset = a; break; }
            return asset;
        }
    }

    /// <summary>The first keyboard binding for one of the actions, or -1.</summary>
    public static int BindingIndex(Bound b)
    {
        var action = Asset != null ? Asset.FindAction(b.Action) : null;
        if (action == null) return -1;
        for (int i = 0; i < action.bindings.Count; i++)
        {
            var binding = action.bindings[i];
            if (!binding.effectivePath.StartsWith("<Keyboard>")) continue;
            if (b.Part == null) { if (!binding.isComposite && !binding.isPartOfComposite) return i; }
            else if (binding.isPartOfComposite && binding.name == b.Part) return i;
        }
        return -1;
    }

    /// <summary>The key one of the actions is on, as a person would say it.</summary>
    public static string Describe(Bound b)
    {
        int i = BindingIndex(b);
        if (i < 0) return "?";
        return InputControlPath.ToHumanReadableString(Asset.FindAction(b.Action).bindings[i].effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice);
    }

    /// <summary>Puts one of the actions on a key, by control path, and keeps it.</summary>
    public static void SetBinding(Bound b, string path)
    {
        int i = BindingIndex(b);
        if (i < 0) return;
        Asset.FindAction(b.Action).ApplyBindingOverride(i, path);
        Save();
    }

    /// <summary>Keeps whatever overrides the asset carries now.</summary>
    public static void Save()
    {
        if (Asset != null) PlayerPrefs.SetString(BindingsKey, Asset.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    /// <summary>Every key back to what it was.</summary>
    public static void ResetAll()
    {
        if (Asset != null) Asset.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(BindingsKey);
        foreach (var n in Legacy) PlayerPrefs.DeleteKey("tileworld.key." + n.Id);
        PlayerPrefs.Save();
    }

    /// <summary>The kept overrides put back on the asset, on every scene.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Apply()
    {
        var a = Asset;
        if (a == null) return;
        string json = PlayerPrefs.GetString(BindingsKey, "");
        if (!string.IsNullOrEmpty(json)) a.LoadBindingOverridesFromJson(json);
    }
}
