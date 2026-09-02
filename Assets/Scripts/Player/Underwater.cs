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

        tint.color = new Color(0.10f, 0.29f, 0.38f, under ? 0.42f : 0f);

        if (!under) return;

        // close, green water rather than the sky's fog
        RenderSettings.fogColor = new Color(0.09f, 0.25f, 0.31f);
        RenderSettings.fogStartDistance = 0.5f;
        RenderSettings.fogEndDistance = 26f;
    }
}
