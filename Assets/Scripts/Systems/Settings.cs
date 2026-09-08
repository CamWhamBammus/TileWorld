using UnityEngine;

/// <summary>
/// What the player has set and kept between runs: volume, how far the
/// world is drawn, how fast the mouse turns the view, and whether the game
/// fills the screen. Read wherever they apply; written from the title's
/// Options page and the pause menu.
/// </summary>
public static class Settings
{
    private const string VolumeKey = "tileworld.volume";
    private const string RadiusKey = "tileworld.viewradius";
    private const string LookKey = "tileworld.look";
    private const string FullKey = "tileworld.fullscreen";
    private const string MusicKey = "tileworld.titlemusic";

    public static float Volume
    {
        get => PlayerPrefs.GetFloat(VolumeKey, 0.8f);
        set { PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); PlayerPrefs.Save(); Apply(); }
    }

    public static int ViewRadius
    {
        get => PlayerPrefs.GetInt(RadiusKey, 4);
        set { PlayerPrefs.SetInt(RadiusKey, Mathf.Clamp(value, 1, 8)); PlayerPrefs.Save(); }
    }

    /// <summary>How fast the view turns for the mouse: one is as it was.</summary>
    public static float LookSpeed
    {
        get => PlayerPrefs.GetFloat(LookKey, 1f);
        set { PlayerPrefs.SetFloat(LookKey, Mathf.Clamp(value, 0.3f, 2.5f)); PlayerPrefs.Save(); }
    }

    public static bool Fullscreen
    {
        get => PlayerPrefs.GetInt(FullKey, Screen.fullScreen ? 1 : 0) == 1;
        set { PlayerPrefs.SetInt(FullKey, value ? 1 : 0); PlayerPrefs.Save(); Apply(); }
    }

    /// <summary>Whether the title plays its soft pad.</summary>
    public static bool TitleMusic
    {
        get => PlayerPrefs.GetInt(MusicKey, 1) == 1;
        set { PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    /// <summary>The settings that take effect at once, applied.</summary>
    public static void Apply()
    {
        AudioListener.volume = Volume;
        if (Screen.fullScreen != Fullscreen) Screen.fullScreen = Fullscreen;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot() { AudioListener.volume = Volume; }
}
