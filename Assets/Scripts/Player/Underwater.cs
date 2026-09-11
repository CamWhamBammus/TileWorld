using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// What it looks like with your head under the surface. The terrain collider
/// runs along the lakebed, so you can walk into a lake and keep going, and
/// until now nothing about the view changed when you did.
/// </summary>
public class Underwater : MonoBehaviour
{
    private Camera view;
    private RawImage tint;
    private bool submerged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (TitleMenu.IsUp) return;
        if (FindFirstObjectByType<Underwater>() == null)
        {
            new GameObject("Underwater (runtime)").AddComponent<Underwater>();
        }
    }

    private void Start()
    {
        var canvasGo = new GameObject("Underwater Canvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;      // under the screens, over the world

        var go = new GameObject("Tint");
        go.transform.SetParent(canvasGo.transform, false);

        tint = go.AddComponent<RawImage>();
        tint.texture = Texture2D.whiteTexture;
        tint.color = new Color(0.10f, 0.29f, 0.38f, 0f);
        tint.raycastTarget = false;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // After TimeOfDay, which writes the fog every frame and would undo this.
    private void LateUpdate()
    {
        if (view == null)
        {
            view = Camera.main;
            if (view == null) return;
        }

        bool under = view.transform.position.y < WaterSurface.Level;

        if (under != submerged)
        {
            submerged = under;
            // No sound here either: this was the menu click standing in for a splash.
        }

        if (!under) { tint.color = new Color(0.10f, 0.29f, 0.38f, 0f); return; }

        // How far under the surface the eye is. This used to be ignored: the same flat wash of
        // dark blue went over everything at four tenths whether you were ankle deep or six
        // metres down, and a reef, whose whole point is its colour, came out nearly black a
        // hand's breadth under the top of the water.
        float down = Mathf.Clamp01((WaterSurface.Level - view.transform.position.y) / 5.0f);

        // And what kind of water it is. The open sea is clear and you can see across a reef;
        // a lake or a pond inland is full of what a lake is full of, and closes in.
        float clarity = 0.55f;

        if (world == null) world = FindFirstObjectByType<ChunkManager>();

        if (world != null)
        {
            int tx = Mathf.RoundToInt(view.transform.position.x / WorldGrid.TileSize);
            int tz = Mathf.RoundToInt(view.transform.position.z / WorldGrid.TileSize);
            var here = Regions.CharacterAtTile(tx, tz, world.WorldSeed, false);
            if (here == Regions.Character.Reef) clarity = 1.0f;
            else if (Regions.Sea(here)) clarity = 0.85f;
        }

        var near = new Color(0.22f, 0.52f, 0.55f);      // a wash you can see through
        var far = new Color(0.06f, 0.19f, 0.27f);       // the dark of the deep
        var water = Color.Lerp(near, far, down);

        tint.color = new Color(water.r, water.g, water.b, Mathf.Lerp(0.16f, 0.46f, down));

        RenderSettings.fogColor = Color.Lerp(new Color(0.20f, 0.46f, 0.48f), new Color(0.07f, 0.19f, 0.26f), down);
        RenderSettings.fogStartDistance = Mathf.Lerp(7f, 0.5f, down);
        RenderSettings.fogEndDistance = Mathf.Lerp(78f, 26f, down) * clarity;
    }

    private ChunkManager world;
}
