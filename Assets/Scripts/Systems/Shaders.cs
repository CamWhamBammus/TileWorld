using UnityEngine;

/// <summary>
/// Finding a shader that this build actually carries.
///
/// The obvious way to write it is Shader.Find(one) ?? Shader.Find(other), and
/// that does not work: the null coalescing operator compares references, while
/// a Unity object that no longer exists — or never did — is a live reference
/// wrapping nothing. So the first call appears to have succeeded, the fallback
/// never runs, and what comes back looks like a shader until you use it.
/// </summary>
public static class Shaders
{
    public static Shader First(params string[] names)
    {
        foreach (string name in names)
        {
            var found = Shader.Find(name);

            // Unity's own comparison, which knows the difference between a
            // reference to nothing and no reference at all.
            if (found != null) return found;
        }

        return null;
    }
}
