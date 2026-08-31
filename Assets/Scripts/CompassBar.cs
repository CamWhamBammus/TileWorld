using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A compass strip across the top: the eight headings, and a tick for every
/// landmark already found. Until now the only way to work out which way you
/// were facing was to open the map.
/// </summary>
public class CompassBar : MonoBehaviour
{
    [SerializeField] private float width = 900f;
    [SerializeField] private float halfSpanDegrees = 90f;
    [SerializeField] private float landmarkRange = 400f;

    private static readonly (string label, float angle)[] Headings =
    {
        ("N", 0f), ("NE", 45f), ("E", 90f), ("SE", 135f),
        ("S", 180f), ("SW", 225f), ("W", 270f), ("NW", 315f)
    };

    private ChunkManager world;
    private Transform player;
    private RectTransform strip;
    private TMP_FontAsset font;

    private readonly List<(RectTransform rect, float angle)> marks = new List<(RectTransform, float)>();
    private readonly List<RectTransform> landmarkTicks = new List<RectTransform>();
    private RectTransform waypointMark;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<CompassBar>() == null)
        {
            new GameObject("Compass (runtime)").AddComponent<CompassBar>();
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
        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        var canvasGo = new GameObject("Compass Canvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var stripGo = new GameObject("Strip");
        stripGo.transform.SetParent(canvasGo.transform, false);

        strip = stripGo.GetComponent<RectTransform>();
        strip.anchorMin = new Vector2(0.5f, 1f);
        strip.anchorMax = new Vector2(0.5f, 1f);
        strip.pivot = new Vector2(0.5f, 1f);
        strip.anchoredPosition = new Vector2(0f, -26f);
        strip.sizeDelta = new Vector2(width, 34f);

        var bar = stripGo.AddComponent<RawImage>();
        bar.texture = Texture2D.whiteTexture;
        bar.color = new Color(0.06f, 0.07f, 0.06f, 0.35f);
        bar.raycastTarget = false;

        foreach (var heading in Headings)
        {
            var label = MakeLabel(heading.label, heading.label.Length == 1 ? 24f : 17f,
                heading.label.Length == 1 ? new Color(0.96f, 0.93f, 0.86f) : new Color(0.78f, 0.74f, 0.66f));

            marks.Add((label, heading.angle));
        }

        // a fixed mark showing dead ahead
        var centre = MakeLabel("▼", 16f, new Color(0.85f, 0.42f, 0.32f));
        centre.anchoredPosition = new Vector2(0f, 14f);
    }

    private RectTransform MakeLabel(string text, float size, Color colour)
    {
        var go = new GameObject(text);
        go.transform.SetParent(strip, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.font = font;
        tmp.fontSize = size;
        tmp.color = colour;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        tmp.text = text;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(70f, 30f);

        return rect;
    }

    private void Update()
    {
        if (player == null) return;

        float facing = player.eulerAngles.y;

        foreach (var mark in marks)
        {
            Place(mark.rect, Mathf.DeltaAngle(facing, mark.angle));
        }

        UpdateLandmarkTicks(facing);
        UpdateWaypoint(facing);
    }

    private void UpdateWaypoint(float facing)
    {
        if (waypointMark == null)
        {
            waypointMark = MakeLabel("▲", 20f, new Color(0.85f, 0.42f, 0.28f));
        }

        if (!Waypoint.IsSet)
        {
            waypointMark.gameObject.SetActive(false);
            return;
        }

        Vector3 to = Waypoint.Position - player.position;
        to.y = 0f;

        float bearing = Mathf.DeltaAngle(facing, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
        bool visible = Mathf.Abs(bearing) <= halfSpanDegrees;

        waypointMark.gameObject.SetActive(visible);

        if (!visible) return;

        waypointMark.anchoredPosition = new Vector2(bearing / halfSpanDegrees * (width * 0.5f), -30f);
        waypointMark.GetComponent<TMP_Text>().text = "▲ " + Mathf.RoundToInt(to.magnitude) + "m";
    }

    /// <summary>Position by bearing, and hide anything behind the player.</summary>
    private void Place(RectTransform rect, float bearing)
    {
        bool visible = Mathf.Abs(bearing) <= halfSpanDegrees;
        rect.gameObject.SetActive(visible);

        if (!visible) return;

        rect.anchoredPosition = new Vector2(bearing / halfSpanDegrees * (width * 0.5f), -2f);
    }

    private void UpdateLandmarkTicks(float facing)
    {
        int used = 0;
        int seed = world.WorldSeed;

        foreach (var pair in LandmarkLog.Found)
        {
            var placement = Landmarks.In(pair.Key, seed);
            if (!placement.Exists) continue;

            Vector3 to = placement.Position - player.position;
            to.y = 0f;

            if (to.sqrMagnitude > landmarkRange * landmarkRange) continue;

            while (landmarkTicks.Count <= used)
            {
                var tick = MakeLabel("◆", 15f, new Color(0.82f, 0.72f, 0.45f));
                landmarkTicks.Add(tick);
            }

            var rect = landmarkTicks[used++];
            rect.anchoredPosition = new Vector2(0f, -16f);

            float bearing = Mathf.DeltaAngle(facing, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg);
            bool visible = Mathf.Abs(bearing) <= halfSpanDegrees;

            rect.gameObject.SetActive(visible);

            if (visible)
            {
                rect.anchoredPosition = new Vector2(bearing / halfSpanDegrees * (width * 0.5f), -16f);
            }
        }

        for (int i = used; i < landmarkTicks.Count; i++)
        {
            landmarkTicks[i].gameObject.SetActive(false);
        }
    }
}
