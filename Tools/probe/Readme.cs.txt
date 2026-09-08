// A probe: the README's pictures, taken from the build. The title first; then, in a world, the
// structures the old pictures showed, framed the way the gallery framed them, at hours that suit them;
// then the ground itself in a few countries, the beach, a night; and a few animals stood in place.
using System.Collections;
using System.IO;
using UnityEngine;

public class _Probe : MonoBehaviour
{
    private const string Stages = "__STAGES__";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        Application.runInBackground = true;
        if (FindFirstObjectByType<_Probe>() != null) return;
        var go = new GameObject("_Probe");
        DontDestroyOnLoad(go);
        go.AddComponent<_Probe>();
    }

    private static void Mark(string what)
    {
        try { File.AppendAllText(Stages, System.DateTime.Now.ToString("HH:mm:ss ") + what + "\n"); } catch { }
    }

    private static void HideInterface()
    {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None)) c.enabled = false;
        foreach (var beh in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (beh.GetType().Name == "WorldLabels" || beh.GetType().Name == "Notices" || beh.GetType().Name == "CompassBar") beh.enabled = false;
        foreach (var t in FindObjectsByType<TMPro.TextMeshPro>(FindObjectsSortMode.None)) t.enabled = false;
    }

    private IEnumerator Shot(string name)
    {
        yield return new WaitForSecondsRealtime(0.4f);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(Application.persistentDataPath, "probe-rm-" + name + ".png"));
        yield return new WaitForSecondsRealtime(0.6f);
        Mark("shot " + name);
    }

    private Camera cam; private Transform player; private CharacterController body; private ChunkManager world; private int seed;
    private System.Reflection.MethodInfo goTo, goWater; private object tools;
    private readonly System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

    private void Stand(Vector3 at, Vector3 facing)
    {
        body.enabled = false;
        player.position = at; if (facing.sqrMagnitude > 0.01f) player.rotation = Quaternion.LookRotation(new Vector3(facing.x, 0f, facing.z).normalized);
        body.enabled = true;
    }

    private void Look(Vector3 from, Vector3 at) { cam.transform.position = from; cam.transform.rotation = Quaternion.LookRotation(at - from, Vector3.up); }

    private IEnumerator Landmark(string name, LandmarkKind want, float hour, float back, float up)
    {
        var home = WorldGrid.WorldToChunk(player.position);
        Landmarks.Placement at = default; bool found = false;
        for (int r = 0; r < 90 && !found; r++)
        for (int dx = -r; dx <= r && !found; dx++)
        for (int dz = -r; dz <= r && !found; dz++)
        {
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
            var p = Landmarks.In(new Vector2Int(home.x + dx, home.y + dz), seed);
            if (p.Exists && p.Kind == want) { at = p; found = true; }
        }
        if (!found) { Mark(name + ": no " + want + " within 90 chunks"); yield break; }
        TimeOfDay.Instance.SetTime(hour); TimeOfDay.Instance.Paused = true; TimeOfDay.Instance.ForceOvercast(0.3f);
        var turn = Quaternion.Euler(0f, at.Yaw, 0f);
        var about = Landmarks.All(want);
        Vector3 stand = at.Position + turn * new Vector3(about.Ahead * 2f + 4f, 0f, 3f);
        int tx = Mathf.RoundToInt(stand.x / WorldGrid.TileSize), tz = Mathf.RoundToInt(stand.z / WorldGrid.TileSize);
        Stand(new Vector3(stand.x, Mathf.Max(WorldHeight.SurfaceY(tx, tz, seed), WaterSurface.Level) + 0.3f, stand.z), Vector3.zero);
        yield return new WaitForSecondsRealtime(12f);
        HideInterface();
        float reach = (about.Ahead * 2f + about.Behind * 2f + 6f + about.LabelHeight * 0.8f) * back;
        Vector3 focus = at.Position + Vector3.up * (1f + about.LabelHeight * 0.25f);
        Look(at.Position + turn * new Vector3(reach * 0.9f, reach * up, reach * 0.7f), focus);
        yield return new WaitForSecondsRealtime(1.5f);
        HideInterface();
        yield return Shot(name);
    }

    private IEnumerator Country(string name, Regions.Character where, float hour, float clouds, float back, float up, float aside)
    {
        goTo.Invoke(tools, new object[] { where });
        TimeOfDay.Instance.SetTime(hour); TimeOfDay.Instance.Paused = true; TimeOfDay.Instance.ForceOvercast(clouds);
        yield return new WaitForSecondsRealtime(11f);
        HideInterface();
        Vector3 fwd = player.forward; Vector3 side = Vector3.Cross(Vector3.up, fwd);
        Look(player.position + Vector3.up * up - fwd * back + side * aside, player.position + fwd * 9f + Vector3.up * 1.0f);
        yield return new WaitForSecondsRealtime(1.5f);
        HideInterface();
        yield return Shot(name);
    }

    private IEnumerator Start()
    {
        // the title, with the country up behind it
        yield return new WaitForSecondsRealtime(17f);
        var title = FindFirstObjectByType<TitleMenu>();
        if (title != null) title.Look("afternoon");
        yield return new WaitForSecondsRealtime(3f);
        yield return Shot("title");

        var save = WorldLibrary.Create("Probe readme", 5, true, true, 0.5f, 20f, true, true);
        WorldLibrary.Enter(save);
        yield return new WaitForSecondsRealtime(9f);
        var arrival = FindFirstObjectByType<Arrival>();
        if (arrival != null) typeof(Arrival).GetMethod("Close", flags).Invoke(arrival, null);
        tools = FindFirstObjectByType<DevTools>();
        goTo = typeof(DevTools).GetMethod("GoTo", flags, null, new[] { typeof(Regions.Character) }, null);
        goWater = typeof(DevTools).GetMethod("GoToWater", flags);
        world = FindFirstObjectByType<ChunkManager>(); player = world.PlayerTransform; body = player.GetComponent<CharacterController>(); seed = world.WorldSeed;
        world.SetViewRadius(8);
        cam = Camera.main;
        foreach (var beh in cam.GetComponents<Behaviour>()) if (beh.GetType().Name == "SimpleFollowCamera") beh.enabled = false;
        cam.fieldOfView = 50f;
        HideInterface();

        yield return Landmark("hero", LandmarkKind.ForestersWatch, 0.36f, 1.3f, 0.32f);
        yield return Landmark("lake", LandmarkKind.FishingJetty, 0.30f, 1.5f, 0.3f);
        yield return Landmark("desert", LandmarkKind.SandGate, 0.35f, 1.4f, 0.35f);
        yield return Landmark("snow", LandmarkKind.TrappersCabin, 0.40f, 1.4f, 0.3f);
        yield return Landmark("dusk", LandmarkKind.Lighthouse, 0.70f, 1.4f, 0.22f);
        yield return Landmark("ruin", LandmarkKind.BuriedTower, 0.36f, 1.4f, 0.35f);

        yield return Country("forest", Regions.Character.Forest, 0.40f, 0.3f, 2.5f, 1.8f, 0.8f);
        yield return Country("grass", Regions.Character.Lowland, 0.42f, 0.3f, 3f, 2.2f, 1f);
        yield return Country("snowfield", Regions.Character.Snow, 0.45f, 0.35f, 3f, 2.2f, 1f);
        yield return Country("marsh", Regions.Character.Reed, 0.34f, 0.4f, 3f, 2.0f, 1f);
        yield return Country("rock", Regions.Character.Stone, 0.50f, 0.25f, 3f, 2.4f, 1f);

        // the beach and its wave, from low on the sand
        goWater.Invoke(tools, new object[] { WaterSurface.Body.Beach });
        TimeOfDay.Instance.SetTime(0.68f); TimeOfDay.Instance.ForceOvercast(0.35f);
        yield return new WaitForSecondsRealtime(11f);
        HideInterface();
        { Vector3 fwd = player.forward; Vector3 side = Vector3.Cross(Vector3.up, fwd); Vector3 at = player.position + fwd * 5f; at.y = WaterSurface.Level + 0.2f; Look(player.position + Vector3.up * 1.4f - fwd * 1.5f + side * 4f, at); }
        yield return new WaitForSecondsRealtime(4f);
        yield return Shot("beach");

        // animals stood in place: deer on the grass at dusk, a hare gone flat on the snow, a scorpion on the sand at night
        goTo.Invoke(tools, new object[] { Regions.Character.Lowland });
        TimeOfDay.Instance.SetTime(0.70f); TimeOfDay.Instance.ForceOvercast(0.3f);
        yield return new WaitForSecondsRealtime(9f);
        HideInterface();
        {
            Vector3 fwd = player.forward; Vector3 side = Vector3.Cross(Vector3.up, fwd);
            for (int i = 0; i < 3; i++) { var d = Wildlife.Summon(FaunaKind.Deer, player.position + fwd * (6f + i * 2f) + side * (i - 1) * 2.5f, i == 2); if (d != null) d.Direct("graze"); }
            yield return new WaitForSecondsRealtime(3f);
            Look(player.position + Vector3.up * 1.5f - fwd * 1f + side * 3f, player.position + fwd * 8f + Vector3.up * 0.8f);
        }
        yield return Shot("deer");

        goTo.Invoke(tools, new object[] { Regions.Character.Snow });
        TimeOfDay.Instance.SetTime(0.45f); TimeOfDay.Instance.ForceOvercast(0.3f);
        yield return new WaitForSecondsRealtime(9f);
        HideInterface();
        {
            Vector3 fwd = player.forward; Vector3 side = Vector3.Cross(Vector3.up, fwd);
            var hare = Wildlife.Summon(FaunaKind.Hare, player.position + fwd * 5f);
            if (hare != null) hare.Direct("alert");
            yield return new WaitForSecondsRealtime(2.5f);
            Look(player.position + Vector3.up * 1.0f + fwd * 1.5f + side * 2.5f, player.position + fwd * 5f + Vector3.up * 0.3f);
            yield return Shot("hare");
            if (hare != null) hare.Direct("run");
            yield return new WaitForSecondsRealtime(4f);
            Look(player.position + Vector3.up * 2.2f + fwd * 2f, player.position + fwd * 8f);
            yield return Shot("prints");
        }

        goTo.Invoke(tools, new object[] { Regions.Character.Desert });
        TimeOfDay.Instance.SetTime(0.93f); TimeOfDay.Instance.ForceOvercast(0.2f);
        yield return new WaitForSecondsRealtime(9f);
        HideInterface();
        {
            Vector3 fwd = player.forward; Vector3 side = Vector3.Cross(Vector3.up, fwd);
            var s = Wildlife.Summon(FaunaKind.Scorpion, player.position + fwd * 3f);
            if (s != null) s.Direct("alert");
            yield return new WaitForSecondsRealtime(2.5f);
            Look(player.position + Vector3.up * 0.9f + fwd * 0.5f + side * 1.6f, player.position + fwd * 3f + Vector3.up * 0.2f);
            yield return Shot("scorpion");
        }

        // a night on the low ground
        goTo.Invoke(tools, new object[] { Regions.Character.Lowland });
        TimeOfDay.Instance.SetTime(0.97f); TimeOfDay.Instance.ForceOvercast(0.15f);
        yield return new WaitForSecondsRealtime(12f);
        HideInterface();
        { Vector3 fwd = player.forward; Vector3 side = Vector3.Cross(Vector3.up, fwd); Look(player.position + Vector3.up * 1.8f - fwd * 3f + side * 1f, player.position + fwd * 10f + Vector3.up * 4f); }
        yield return new WaitForSecondsRealtime(3f);
        yield return Shot("night");

        WorldLibrary.Delete(save.id);

        // the toadstool ring lives in the fungal country of seed 24
        var save2 = WorldLibrary.Create("Probe readme 2", 24, true, true, 0.4f, 20f, true, true);
        WorldLibrary.Enter(save2);
        yield return new WaitForSecondsRealtime(9f);
        arrival = FindFirstObjectByType<Arrival>();
        if (arrival != null) typeof(Arrival).GetMethod("Close", flags).Invoke(arrival, null);
        tools = FindFirstObjectByType<DevTools>();
        world = FindFirstObjectByType<ChunkManager>(); player = world.PlayerTransform; body = player.GetComponent<CharacterController>(); seed = world.WorldSeed;
        world.SetViewRadius(8);
        cam = Camera.main;
        foreach (var beh in cam.GetComponents<Behaviour>()) if (beh.GetType().Name == "SimpleFollowCamera") beh.enabled = false;
        cam.fieldOfView = 50f;
        goTo.Invoke(tools, new object[] { Regions.Character.Fungal });
        yield return new WaitForSecondsRealtime(8f);
        yield return Landmark("fungal", LandmarkKind.ToadstoolRing, 0.40f, 1.3f, 0.35f);
        WorldLibrary.Delete(save2.id);
        Mark("done");
    }
}
