using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one look the world is painted in, and the only place that decides it.
///
/// Every builder used to keep its own copy of this: find a shader, make a
/// material, set a colour and a smoothness, and each had drifted to its own
/// idea of how shiny the world is. Worse, the ruins made theirs fresh every
/// time one was built, seven a piece, so two houses on a hillside were
/// fourteen materials that could not be drawn together.
///
/// Asking here instead means a colour is one material however many things are
/// painted with it, and when the look of the world changes it changes once.
/// </summary>
public static class Paint
{
    /// <summary>Barely any shine. Flat faces and honest colour is the whole style.</summary>
    private const float Sheen = 0.08f;

    private static readonly Dictionary<int, Material> made = new Dictionary<int, Material>();

    public static Material Flat(Color colour)
    {
        int key = colour.GetHashCode();

        // The kept one can have been destroyed under us on the way out of a
        // world, and a destroyed material is not null in the ordinary way.
        if (made.TryGetValue(key, out var kept) && kept != null) return kept;

        Shader lit = Shaders.First("Universal Render Pipeline/Lit", "Standard");

        // Without a shader there is no material to make, and a grey animal is
        // better than an exception part way through building one.
        if (lit == null) return null;

        var fresh = new Material(lit);

        fresh.SetColor("_BaseColor", colour);
        fresh.color = colour;
        fresh.SetFloat("_Smoothness", Sheen);
        fresh.SetFloat("_Metallic", 0f);
        fresh.SetFloat("_Glossiness", 0f);
        fresh.enableInstancing = true;

        made[key] = fresh;

        return fresh;
    }

    /// <summary>How many have been made, which is a fair measure of the drawing.</summary>
    public static int Count => made.Count;

    public static void Forget() => made.Clear();
}
