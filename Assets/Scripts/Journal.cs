using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A list of everything found, with where it is and how far off. The map shows
/// landmarks as diamonds but cannot tell you what they are or put them in
/// order, and after a long session that is the question worth answering.
/// </summary>
public class Journal : MonoBehaviour
{
    [SerializeField] private KeyCode toggleKey = KeyCode.J;

    private ChunkManager world;
    private Transform player;
    private GameObject panel;
    private TMP_Text body;
    private bool open;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Journal>() == null)
        {
            new GameObject("Journal (runtime)").AddComponent<Journal>();
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
        BuildUi();
    }

    private void OnEnable()
    {
        ScreenState.Changed += OnScreenChanged;
    }

    private void OnDisable()
    {
        ScreenState.Changed -= OnScreenChanged;
    }

    private void OnScreenChanged(ScreenState.Screen screen)
    {
        if (open && screen != ScreenState.Screen.Journal)
        {
            open = false;
            if (panel != null) panel.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            open = !open;
            panel.SetActive(open);

            if (open) ScreenState.Open(ScreenState.Screen.Journal);
            else ScreenState.Close(ScreenState.Screen.Journal);

            Ambience.Instance?.Click();
        }

        if (open) Refresh();
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("Journal Canvas");
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 480;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGo.transform, false);

        var shade = panel.AddComponent<RawImage>();
        shade.texture = Texture2D.whiteTexture;
        shade.color = new Color(0f, 0f, 0f, 0.45f);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var cardGo = new GameObject("Card");
        cardGo.transform.SetParent(panel.transform, false);

        var card = cardGo.AddComponent<RawImage>();
        card.texture = ParchmentPanel.Create(448, 512);

        var cardRect = cardGo.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.pivot = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(760f, 840f);

        var textGo = new GameObject("Body");
        textGo.transform.SetParent(cardGo.transform, false);

        body = textGo.AddComponent<TextMeshProUGUI>();
        body.font = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
        body.alignment = TextAlignmentOptions.TopLeft;
        body.enableAutoSizing = true;
        body.fontSizeMin = 11f;
        body.fontSizeMax = 21f;
        body.color = Color.white;

        var textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = new Vector2(664f, 744f);

        panel.SetActive(false);
    }

    private void Refresh()
    {
        const string head = "#33291D", dim = "#8A7C63", ink = "#4A4032", done = "#4A6B33";

        var found = new List<(string name, Vector2Int chunk, float distance, bool surveyed)>();
        int seed = world.WorldSeed;

        foreach (var pair in LandmarkLog.Found)
        {
            var placement = Landmarks.In(pair.Key, seed);
            if (!placement.Exists) continue;

            float distance = Vector3.Distance(player.position, placement.Position);
            bool surveyed = Landmarks.SurveyHeight(pair.Value) > 0f;

            found.Add((Landmarks.NameOf(pair.Value), pair.Key, distance, surveyed));
        }

        found.Sort((a, b) => a.distance.CompareTo(b.distance));

        var text = new System.Text.StringBuilder();
        text.Append("<size=145%><b><color=").Append(head).Append(">FIELD JOURNAL</color></b></size>\n");
        text.Append("<color=").Append(dim).Append(">").Append(new string('─', 30)).Append("</color>\n\n");

        if (found.Count == 0)
        {
            text.Append("<color=").Append(dim).Append(">Nothing found yet. Walk out and look.</color>");
        }
        else
        {
            foreach (var entry in found)
            {
                text.Append("<color=").Append(entry.surveyed ? done : ink).Append("><b>")
                    .Append(entry.name).Append("</b></color>\n");
                text.Append("<indent=18px><size=85%><color=").Append(dim).Append(">grid ")
                    .Append(entry.chunk.x).Append(", ").Append(entry.chunk.y)
                    .Append("   ").Append(Mathf.RoundToInt(entry.distance)).Append("m away")
                    .Append(entry.surveyed ? "   climbable" : "")
                    .Append("</color></size></indent>\n\n");
            }
        }

        text.Append("<color=").Append(dim).Append(">").Append(new string('─', 30)).Append("</color>\n");
        text.Append("<size=85%><color=").Append(dim).Append(">")
            .Append(found.Count).Append(" found   ")
            .Append(ExplorationLog.Count).Append(" chunks charted   J to close</color></size>");

        body.text = text.ToString();
    }
}
