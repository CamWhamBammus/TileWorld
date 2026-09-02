using UnityEngine;

/// <summary>
/// Puts the surveyor in place of the robot the project came with, and walks
/// them about.
///
/// There is no animator and no rig: the same trick the animals use, where the
/// limbs hang from joints and are turned by hand from how fast the player is
/// actually moving. That keeps the person and the wildlife looking like they
/// were made for one another, which they were.
/// </summary>
public class Surveyor : MonoBehaviour
{
    private ChunkManager world;
    private Transform player;
    private CharacterController body;

    private SurveyorBuilder.Figure figure;
    private float height = 1.8f;

    private float gait;
    private float pace;
    private float drawing;      // how far into holding the glass up

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Surveyor>() == null)
        {
            new GameObject("Surveyor (runtime)").AddComponent<Surveyor>();
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

        if (player == null)
        {
            enabled = false;
            return;
        }

        body = player.GetComponent<CharacterController>();

        if (body != null) height = body.height;

        // The robot goes quietly: its renderers are switched off rather than
        // deleted, so nothing that expects the rig to exist is upset by it.
        foreach (var skin in player.GetComponentsInChildren<SkinnedMeshRenderer>(true)) skin.enabled = false;

        foreach (var piece in player.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (piece.GetComponentInParent<Surveyor>() == null) piece.enabled = false;
        }

        figure = SurveyorBuilder.Build(player, height);

        Debug.Log("[Surveyor] Took over from the robot, " + height.ToString("F1") + " units tall.");
    }

    private void Update()
    {
        if (figure.Root == null) return;

        float dt = Time.deltaTime;

        Vector3 moving = body != null ? body.velocity : Vector3.zero;
        moving.y = 0f;

        pace = Mathf.Lerp(pace, moving.magnitude, 1f - Mathf.Exp(-8f * dt));

        bool afoot = pace > 0.15f;
        bool grounded = body == null || body.isGrounded;

        gait += pace * dt * 2.6f;

        drawing = Mathf.MoveTowards(drawing, Sketching.Raised > 0.05f ? 1f : 0f, dt * 4f);

        Limbs(dt, afoot, grounded);
        Carriage(dt, afoot);
    }

    private void Limbs(float dt, bool afoot, bool grounded)
    {
        float swing = 13f + pace * 3.4f;
        float reach = 9f + pace * 5f;

        for (int side = 0; side < 2; side++)
        {
            float phase = side == 0 ? 0f : Mathf.PI;
            float turn = gait + phase;

            float hip, knee, shoulder, elbow;
            float roll = 0f;

            if (!grounded)
            {
                // legs gathered under, arms out a little
                hip = side == 0 ? 26f : -14f;
                knee = -42f;
                shoulder = -22f;
                elbow = -28f;
            }
            else if (afoot)
            {
                hip = Mathf.Sin(turn) * swing;
                knee = -Mathf.Max(0f, Mathf.Cos(turn)) * reach;

                shoulder = -Mathf.Sin(turn) * (swing * 0.8f);
                elbow = -18f - Mathf.Max(0f, -Mathf.Sin(turn)) * 14f;
            }
            else
            {
                // standing: a little breath in it, and the arms hanging
                hip = 0f;
                knee = 0f;
                shoulder = Mathf.Sin(Time.time * 0.9f + side) * 1.6f;
                elbow = -10f;
            }

            // holding the glass up: one hand to the eye, the other steadying
            // under it, elbows tucked in rather than winged out, legs still
            if (drawing > 0.01f)
            {
                bool holding = side == 0;

                shoulder = Mathf.Lerp(shoulder, holding ? -74f : -52f, drawing);
                elbow = Mathf.Lerp(elbow, holding ? -104f : -74f, drawing);
                roll = Mathf.Lerp(roll, (holding ? -26f : -14f) * (side == 0 ? 1f : -1f), drawing);
                hip = Mathf.Lerp(hip, 0f, drawing);
                knee = Mathf.Lerp(knee, 0f, drawing);
            }

            Turn(figure.Legs[side], hip, dt, afoot || !grounded);
            Turn(figure.Knees[side], knee, dt, afoot || !grounded);
            Turn(figure.Arms[side], shoulder, dt, true, roll);
            Turn(figure.Elbows[side], elbow, dt, true);
        }
    }

    private void Carriage(float dt, bool afoot)
    {
        // rise and fall on the stride, and lean into a run
        float rise = afoot ? Mathf.Abs(Mathf.Sin(gait)) * height * 0.012f
                           : Mathf.Sin(Time.time * 0.9f) * height * 0.004f;

        var local = figure.Root.localPosition;
        local.y = Mathf.Lerp(local.y, rise, 1f - Mathf.Exp(-12f * dt));
        figure.Root.localPosition = local;

        float lean = Mathf.Clamp(pace * 1.1f, 0f, 7f) * (1f - drawing);

        figure.Root.localRotation = Quaternion.Slerp(figure.Root.localRotation,
            Quaternion.Euler(lean, 0f, 0f), 1f - Mathf.Exp(-8f * dt));

        // the head stays level while the body leans, and dips over the glass
        if (figure.Head != null)
        {
            figure.Head.localRotation = Quaternion.Slerp(figure.Head.localRotation,
                Quaternion.Euler(-lean * 0.7f + drawing * 12f, 0f, 0f), 1f - Mathf.Exp(-8f * dt));
        }
    }

    private static void Turn(Transform joint, float degrees, float dt, bool quickly, float roll = 0f)
    {
        if (joint == null) return;

        joint.localRotation = Quaternion.Slerp(joint.localRotation, Quaternion.Euler(degrees, 0f, roll),
                                               quickly ? 1f - Mathf.Exp(-18f * dt) : 1f - Mathf.Exp(-8f * dt));
    }
}
