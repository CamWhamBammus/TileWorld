#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;

/// <summary>
/// Keys for working on the game rather than playing it. The first few minutes
/// happen once ever, which makes them the hardest part to look at twice, so
/// there is a key to run them again and a key to put a world back to nothing.
///
/// Only built into the editor and into development builds.
/// </summary>
public class DevTools : MonoBehaviour
{
    [SerializeField] private KeyCode replayKey = KeyCode.F8;

    private float askedAt = -99f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<DevTools>() == null)
        {
            new GameObject("Dev Tools (runtime)").AddComponent<DevTools>();
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(replayKey)) return;

        bool wiping = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (!wiping)
        {
            Arrival.Replay();
            Notices.Show("Dev: showing the first page again.");
            return;
        }

        // Losing a world's drawings cannot be undone, so it is asked for twice.
        if (Time.time - askedAt > 4f)
        {
            askedAt = Time.time;
            Notices.Show("Dev: shift-" + replayKey + " again to wipe this world's findings.");
            return;
        }

        askedAt = -99f;

        Wipe();

        Arrival.Replay();
        Notices.Show("Dev: this world is back to nothing.");
    }

    /// <summary>Everything this world has found, out of the save and off the disk.</summary>
    private static void Wipe()
    {
        var save = WorldLibrary.Current;

        if (save != null)
        {
            save.bookSubjects.Clear();
            save.bookStudies.Clear();
            save.bookWhere.Clear();

            save.guideKinds.Clear();
            save.guideStudies.Clear();

            save.drawingKeys.Clear();
            save.drawingQuality.Clear();
            save.drawingVerdict.Clear();
            save.drawingWhen.Clear();

            save.noticed.Clear();
            save.noticedWhere.Clear();
            save.noticedWhen.Clear();

            save.creaturesSeen.Clear();
            save.creatureChunks.Clear();

            WorldLibrary.Write(save);

            // the drawings themselves, which live as pictures beside the save
            string drawings = System.IO.Path.Combine(Application.persistentDataPath, "drawings", save.id);

            if (System.IO.Directory.Exists(drawings))
            {
                try { System.IO.Directory.Delete(drawings, true); }
                catch (System.Exception e) { Debug.LogWarning("[Dev] left the drawings alone: " + e.Message); }
            }
        }

        FieldGuide.Clear();
        Notebook.Clear();
        SketchBook.Clear();
        SightingLog.Clear();
        Stalking.Clear();
    }
}
#endif
