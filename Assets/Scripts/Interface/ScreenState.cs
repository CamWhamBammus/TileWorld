using UnityEngine;

/// <summary>
/// Which full screen panel is open, if any. Two of them wanting the mouse at
/// once, or the camera turning while you click a map, are the sort of thing
/// that only shows up when you actually play it.
/// </summary>
public static class ScreenState
{
    public enum Screen { None, Map, Journal, Quests, Guide, Pause }

    public static Screen Current { get; private set; } = Screen.None;

    /// <summary>True when a screen wants the cursor rather than the camera.</summary>
    public static bool WantsCursor => Current != Screen.None;

    /// <summary>Raised when a screen other than the caller takes over.</summary>
    public static event System.Action<Screen> Changed;

    public static bool IsOpen(Screen screen) => Current == screen;

    public static void Open(Screen screen)
    {
        if (Current == screen) return;

        Current = screen;
        Apply();
        Changed?.Invoke(screen);
    }

    public static void Close(Screen screen)
    {
        if (Current != screen) return;

        Current = Screen.None;
        Apply();
        Changed?.Invoke(Screen.None);
    }

    /// <summary>The cursor belongs to whatever is open, and to the camera otherwise.</summary>
    private static void Apply()
    {
        bool free = WantsCursor;

        Cursor.lockState = free ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = free;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        Current = Screen.None;
        Changed = null;
    }
}
