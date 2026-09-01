using UnityEngine;

/// <summary>
/// How much of a nuisance the player is being.
///
/// Everything wild in this world runs on a distance at which it notices you
/// and a shorter one at which it goes. Those were fixed, which made the only
/// way to see an animal closely a matter of luck. Now they scale with how you
/// are moving: come crashing along at a run and a deer is gone before you see
/// it; stand still and it will let you much nearer.
/// </summary>
public static class Stalking
{
    /// <summary>Metres per second, smoothed, of whoever is being watched.</summary>
    public static float Pace { get; private set; }

    /// <summary>
    /// What an animal's distances are multiplied by. Under one it lets you
    /// closer; over one it is off sooner.
    /// </summary>
    public static float Wariness => Mathf.Lerp(0.55f, 1.65f, Mathf.InverseLerp(0.2f, 5.2f, Pace));

    /// <summary>True when the player is still enough to hold a pencil steady.</summary>
    public static bool Steady => Pace < 1.1f;

    private static Vector3 was;
    private static bool started;

    public static void Watch(Transform player, float dt)
    {
        if (player == null || dt <= 0f) return;

        if (!started)
        {
            was = player.position;
            started = true;
            return;
        }

        Vector3 moved = player.position - was;
        moved.y = 0f;

        was = player.position;

        Pace = Mathf.Lerp(Pace, moved.magnitude / dt, 1f - Mathf.Exp(-6f * dt));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Clear()
    {
        Pace = 0f;
        started = false;
    }
}
