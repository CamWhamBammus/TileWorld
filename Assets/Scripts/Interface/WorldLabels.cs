using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Names floating over landmarks you have already found, with how far away they
/// are. The compass says a landmark lies that way; this says which one, so a
/// tower you climbed stays recognisable from across a valley.
/// </summary>
public class WorldLabels : MonoBehaviour
{
    [SerializeField] private float showWithin = 260f;
    [SerializeField] private float fadeFrom = 200f;

    private ChunkManager world;
    private Transform player;
    private Camera view;
    private TMP_FontAsset font;

    private readonly Dictionary<Vector2Int, TextMeshPro> labels = new Dictionary<Vector2Int, TextMeshPro>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<WorldLabels>() == null)
        {
            new GameObject("World Labels (runtime)").AddComponent<WorldLabels>();
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
    }

    private void LateUpdate()
    {
        if (player == null) return;

        if (view == null) view = Camera.main;
        if (view == null) return;

        int seed = world.WorldSeed;

        foreach (var pair in LandmarkLog.Found)
        {
            var placement = Landmarks.In(pair.Key, seed);
            if (!placement.Exists) continue;

            float distance = Vector3.Distance(player.position, placement.Position);

            if (!labels.TryGetValue(pair.Key, out var label))
            {
                label = Make(Landmarks.NameOf(pair.Value));
                labels[pair.Key] = label;
            }

            bool visible = distance <= showWithin;
            if (label.gameObject.activeSelf != visible) label.gameObject.SetActive(visible);
            if (!visible) continue;

            // Sits above the structure, and always faces the camera.
            label.transform.position = placement.Position + Vector3.up * HeightOf(pair.Value);
            label.transform.rotation = Quaternion.LookRotation(label.transform.position - view.transform.position);

            float alpha = 1f - Mathf.InverseLerp(fadeFrom, showWithin, distance);
            label.color = new Color(0.96f, 0.93f, 0.84f, alpha);
            label.text = Landmarks.NameOf(pair.Value) + "\n<size=60%>" + Mathf.RoundToInt(distance) + "m</size>";
        }
    }

    private static float HeightOf(LandmarkKind kind)
    {
        return Landmarks.All(kind).LabelHeight;
    }

    private TextMeshPro Make(string text)
    {
        var go = new GameObject("Label " + text);
        go.transform.SetParent(transform, false);

        var label = go.AddComponent<TextMeshPro>();
        label.font = font;
        label.text = text;
        label.fontSize = 5f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.96f, 0.93f, 0.84f);

        return label;
    }
}
