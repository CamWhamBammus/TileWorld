using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Short messages in the corner: a quest finished, a landmark reached, a survey
/// taken. All of these already happened, they were just written to the console
/// where the player never sees them.
/// </summary>
public class Notices : MonoBehaviour
{
    private const int MaxShown = 4;
    private const float HoldSeconds = 4.5f;
    private const float FadeSeconds = 1.2f;

    private class Notice
    {
        public TMP_Text Text;
        public RectTransform Rect;
        public float Born;
    }

    private static Notices instance;

    private readonly List<Notice> live = new List<Notice>();
    private RectTransform root;
    private TMP_FontAsset font;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Notices>() == null)
        {
            new GameObject("Notices (runtime)").AddComponent<Notices>();
        }
    }

    /// <summary>Post a message. Safe to call before the screen exists.</summary>
    public static void Show(string message)
    {
        Debug.Log("[Notice] " + message);

        if (instance != null) instance.Add(message);
    }

    private void Awake()
    {
        instance = this;
    }

    private void Start()
    {
        font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        var canvasGo = new GameObject("Notice Canvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var rootGo = new GameObject("Stack");
        rootGo.transform.SetParent(canvasGo.transform, false);

        // A bare GameObject gets a Transform. RectTransform only appears when a
        // UI component is added, so it has to be asked for explicitly, and
        // reading it off a plain object throws before anything is drawn.
        root = rootGo.AddComponent<RectTransform>();
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = new Vector2(48f, -48f);
        root.sizeDelta = new Vector2(620f, 300f);
    }

    private void Add(string message)
    {
        if (root == null) return;

        var go = new GameObject("Notice");
        go.transform.SetParent(root, false);

        var background = go.AddComponent<RawImage>();
        background.texture = Texture2D.whiteTexture;
        background.color = new Color(0.06f, 0.07f, 0.06f, 0.55f);
        background.raycastTarget = false;

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);

        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = 22f;
        text.color = new Color(0.94f, 0.90f, 0.82f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        text.text = message;

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 0f);
        textRect.offsetMax = new Vector2(-18f, 0f);

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(560f, 44f);

        live.Add(new Notice { Text = text, Rect = rect, Born = Time.time });

        while (live.Count > MaxShown)
        {
            Destroy(live[0].Rect.gameObject);
            live.RemoveAt(0);
        }

        // No sound. A notice is the world remarking on something, and it was
        // using the click that belongs to opening a menu — several a minute
        // once the book started noticing things on its own.
    }

    private void Update()
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var notice = live[i];
            float age = Time.time - notice.Born;

            if (age > HoldSeconds + FadeSeconds)
            {
                Destroy(notice.Rect.gameObject);
                live.RemoveAt(i);
                continue;
            }

            float alpha = age < HoldSeconds ? 1f : 1f - (age - HoldSeconds) / FadeSeconds;

            var text = notice.Text;
            text.color = new Color(text.color.r, text.color.g, text.color.b, alpha);

            var bg = notice.Rect.GetComponent<RawImage>();
            if (bg != null) bg.color = new Color(bg.color.r, bg.color.g, bg.color.b, 0.55f * alpha);
        }

        // newest at the top, older ones pushed down
        for (int i = 0; i < live.Count; i++)
        {
            int fromTop = live.Count - 1 - i;
            live[i].Rect.anchoredPosition = new Vector2(0f, -fromTop * 52f);
        }
    }
}
