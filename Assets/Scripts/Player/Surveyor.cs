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

    private float lever = 0.78f; // hip to ankle, measured off the figure itself
    private float gait;         // where we are in the stride, in whole strides
    private float stance;       // the share of a stride a foot spends on the ground
    private float step;         // half a step, along the ground, in metres
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

        // How far the ankle swings for a given angle at the hip is the whole
        // basis of the stride below, so it is measured off the built figure
        // rather than guessed from its height. Guessing it hip-to-sole rather
        // than hip-to-ankle is a fifth too long, and a fifth of every step is
        // then made up by the foot skidding along the ground.
        if (figure.Legs[0] != null && figure.Ankles[0] != null)
            lever = Vector3.Distance(figure.Legs[0].position, figure.Ankles[0].position);

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

        // A leg on the ground has to travel backwards at exactly the speed the
        // world goes past, or the foot skates. So the stride is not a wave with
        // a rate picked by eye: the reach sets how far a foot can carry, and the
        // rate falls out of how fast we are covering ground. At a walk the foot
        // is down for most of the stride; at a run it is barely down at all.
        stance = Mathf.Lerp(0.62f, 0.38f, Mathf.InverseLerp(2f, 5f, pace));
        step = Reach * Mathf.Sin((20f + pace * 7f) * Mathf.Deg2Rad);

        if (grounded) gait += pace * stance / Mathf.Max(0.05f, 2f * step) * dt;
        gait -= Mathf.Floor(gait);

        drawing = Mathf.MoveTowards(drawing, Sketching.Working ? 1f : 0f, dt * 4f);

        if (figure.Book != null) figure.Book.gameObject.SetActive(drawing > 0.02f);
        if (figure.Pencil != null) figure.Pencil.gameObject.SetActive(drawing > 0.02f);

        Limbs(dt, afoot, grounded);
        Carriage(dt, afoot && grounded);
    }

    /// <summary>How far the ankle reaches from the hip. Measured, not assumed.</summary>
    private float Reach => lever;

    private void Limbs(float dt, bool afoot, bool grounded)
    {
        for (int side = 0; side < 2; side++)
        {
            float phase = gait + (side == 0 ? 0f : 0.5f);
            phase -= Mathf.Floor(phase);

            float hip, knee, shoulder, elbow, ankle;
            float roll = 0f;

            if (!grounded)
            {
                float climbing = Mathf.InverseLerp(-2.5f, 3.5f, body != null ? body.velocity.y : 0f);
                float apart = side == 0 ? 1f : -1f;

                // gathered under on the way up, reaching down on the way back
                hip = Mathf.Lerp(-5f, -14f, climbing) + apart * 4f;
                knee = Mathf.Lerp(12f, 46f, climbing) + apart * 5f;
                ankle = Mathf.Lerp(6f, 16f, climbing);

                shoulder = Mathf.Lerp(-13f, -30f, climbing) - apart * 5f;
                elbow = Mathf.Lerp(-20f, -34f, climbing);
            }
            else if (afoot)
            {
                if (phase < stance)
                {
                    // on the ground: the foot holds still in the world and the
                    // body rides over it, so the angle comes from where the foot
                    // has to be rather than from a wave
                    float t = phase / stance;

                    hip = Angle(Mathf.Lerp(step, -step, t));
                    knee = 3f * Mathf.Sin(Mathf.PI * t);

                    // heel down, roll flat, push off the toe -- on top of
                    // whatever it takes to keep the sole on the ground
                    float heelToe = t < 0.5f ? Mathf.Lerp(-11f, 0f, t * 2f)
                                             : Mathf.Lerp(0f, 15f, (t - 0.5f) * 2f);

                    ankle = Mathf.Clamp(-(hip + knee) + heelToe, -30f, 30f);
                }
                else
                {
                    // and off it: the leg comes through fast, knee folded so the
                    // boot clears the ground
                    float t = (phase - stance) / (1f - stance);
                    float eased = t * t * (3f - 2f * t);

                    hip = Angle(Mathf.Lerp(-step, step, eased));
                    knee = (34f + pace * 6f) * Mathf.Sin(Mathf.PI * t);

                    // carried level, toe a shade up so it clears the ground
                    ankle = Mathf.Clamp(-(hip + knee) - 5f, -30f, 30f);
                }

                // the arms answer the opposite leg
                float across = Angle(Mathf.Cos(phase * Mathf.PI * 2f) * step);

                shoulder = -across * 0.78f;
                elbow = -12f - Mathf.Max(0f, -across) * 0.22f;
            }
            else
            {
                // standing: a little breath in it, and the arms hanging
                hip = 0f;
                knee = 0f;
                ankle = 0f;
                shoulder = Mathf.Sin(Time.time * 0.9f + side) * 1.6f;
                elbow = -10f + Mathf.Sin(Time.time * 0.7f + side * 2f) * 2.5f;
            }

            // holding the glass up: one hand to the eye, the other steadying
            // under it, elbows tucked in rather than winged out, legs still
            if (drawing > 0.01f)
            {
                bool holding = side == 0;

                // the drawing hand works: small, quick, and never quite still
                float working = holding ? 0f : Mathf.Sin(Time.time * 8.5f) * 2.6f
                                             + Mathf.Sin(Time.time * 3.1f) * 1.4f;

                shoulder = Mathf.Lerp(shoulder, (holding ? -40f : -34f) + working * 0.5f, drawing);
                elbow = Mathf.Lerp(elbow, (holding ? -68f : -72f) + working, drawing);
                roll = Mathf.Lerp(roll, holding ? -16f : 13f, drawing);
                hip = Mathf.Lerp(hip, 0f, drawing);
                knee = Mathf.Lerp(knee, 0f, drawing);
                ankle = Mathf.Lerp(ankle, 0f, drawing);
            }

            Turn(figure.Legs[side], hip, dt, afoot || !grounded);
            Turn(figure.Knees[side], knee, dt, afoot || !grounded);
            Turn(figure.Ankles[side], ankle, dt, afoot || !grounded);
            Turn(figure.Arms[side], shoulder, dt, true, roll);
            Turn(figure.Elbows[side], elbow, dt, true);
        }
    }

    /// <summary>
    /// The hip angle that puts the foot this far along the ground, forward
    /// being positive. Turning a hanging limb about its own x carries the far
    /// end backwards, so the sign here is the other way about from the reading
    /// you would expect: a leg reaching ahead is a negative angle.
    /// </summary>
    private float Angle(float along)
    {
        return -Mathf.Asin(Mathf.Clamp(along / Reach, -1f, 1f)) * Mathf.Rad2Deg;
    }

    private void Carriage(float dt, bool afoot)
    {
        // rise and fall on the stride, and lean into a run
        float rise = afoot ? Mathf.Abs(Mathf.Sin(gait * Mathf.PI * 2f)) * height * 0.012f
                           : Mathf.Sin(Time.time * 0.9f) * height * 0.004f;

        float lean = afoot ? Mathf.Sin(gait * Mathf.PI * 2f) * height * 0.007f * (1f - drawing) : 0f;

        var local = figure.Root.localPosition;
        local.y = Mathf.Lerp(local.y, rise, 1f - Mathf.Exp(-12f * dt));
        local.x = Mathf.Lerp(local.x, lean, 1f - Mathf.Exp(-12f * dt));
        figure.Root.localPosition = local;

        float tip = Mathf.Clamp(pace * 1.1f, 0f, 7f) * (1f - drawing);

        // the shoulders come round with the stride and the hips roll under it,
        // which is most of the difference between walking and being carried
        float twist = afoot ? Mathf.Sin(gait * Mathf.PI * 2f) * (2.2f + pace * 1.1f) * (1f - drawing) : 0f;
        float sway = afoot ? Mathf.Cos(gait * Mathf.PI * 2f) * (0.4f + pace * 0.22f) * (1f - drawing)
                           : Mathf.Sin(Time.time * 0.55f) * 0.5f;

        figure.Root.localRotation = Quaternion.Slerp(figure.Root.localRotation,
            Quaternion.Euler(tip, twist, sway), 1f - Mathf.Exp(-10f * dt));

        // the head holds its own line through all of that, and dips over the glass
        if (figure.Head != null)
        {
            float glance = Mathf.Sin(Time.time * 0.31f);
            float about = afoot ? 0f : glance * glance * glance * 14f * (1f - drawing);

            figure.Head.localRotation = Quaternion.Slerp(figure.Head.localRotation,
                Quaternion.Euler(-tip * 0.7f + drawing * 17f, about - twist * 0.85f, -sway * 0.6f),
                1f - Mathf.Exp(-7f * dt));
        }
    }

    private static void Turn(Transform joint, float degrees, float dt, bool quickly, float roll = 0f)
    {
        if (joint == null) return;

        joint.localRotation = Quaternion.Slerp(joint.localRotation, Quaternion.Euler(degrees, 0f, roll),
                                               quickly ? 1f - Mathf.Exp(-26f * dt) : 1f - Mathf.Exp(-8f * dt));
    }
}
