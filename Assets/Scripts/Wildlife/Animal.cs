using UnityEngine;

/// <summary>
/// One animal, going about its business. There is no animator and no navmesh:
/// it walks between points it picks itself, puts its head down to the grass,
/// goes to the water when it wants a drink, looks up to check on you, and now
/// and then lies down.
///
/// The interesting part is still the watching. An animal that simply ran would
/// be scenery with a trigger on it; one that lifts its head, holds still while
/// you decide what to do, and only goes when you push it, is the reason to
/// stop walking for a moment.
/// </summary>
public class Animal : MonoBehaviour
{
    private enum State { Stand, Graze, Look, Wander, ToWater, Drink, Rest, Alert, Flee, Hidden }

    /// <summary>
    /// The small things an animal does between one thing and the next, laid
    /// over whatever state it is in: a deer stamps a forefoot when it is
    /// unsure of you, a rabbit sits up to look, a fox grooms its flank and
    /// pounces on something in the grass, anything getting up from a rest
    /// stretches or shakes. Each runs for a moment and is gone.
    /// </summary>
    private enum Gesture { None, Stamp, SitUp, Groom, Scratch, Shake, Stretch, Pounce, Howl }

    public FaunaKind Kind { get; private set; }

    /// <summary>What it is doing, for anyone with a pencil out.</summary>
    public Doing Busy
    {
        get
        {
            switch (state)
            {
                case State.Graze: return Doing.Grazing;
                case State.Drink: return Doing.Drinking;
                case State.Rest: return Doing.Resting;
                case State.Alert: return Doing.Watching;
                case State.Flee:
                case State.Hidden: return Doing.Fleeing;
                case State.Wander:
                case State.ToWater: return Doing.Walking;
                default: return Doing.Standing;
            }
        }
    }

    /// <summary>Where its head is, which is what you would be drawing.</summary>
    public Vector3 Head => body.Head != null ? body.Head.position
                                             : transform.position + Vector3.up * traits.Size;

    private Fauna.Traits traits;
    private AnimalBuilder.Body body;
    private Transform player;
    private int seed;

    private State state;
    private Vector3 target;
    private float until;
    private float gait;
    private float lastStep;
    private float phase;
    private float thirst;

    private float yaw;               // where it is pointed, kept apart from how it is tilted
    private float lastYaw;
    private float pace;              // eased, so nothing starts or stops dead
    private float hop;               // the shove it gives itself when it bolts
    private Vector3 slope = Vector3.up;

    private AudioSource voice;
    private AudioSource steps;
    private float nextCall;
    private float nextNibble;

    private float altitude;          // how far off the ground, for what flies
    private Vector3 neckAt;          // where the head hangs when it is out

    private Gesture gesture;
    private float gestureFrom;       // when it began
    private float gestureUntil;
    private float nextGesture;
    private float restedSince;
    private readonly float[] earFlick = new float[2];   // how far each ear is mid-flick
    private readonly float[] nextFlick = new float[2];

    /// <summary>How far through the gesture, nought to one.</summary>
    private float GestureT => Mathf.Clamp01((Time.time - gestureFrom) / Mathf.Max(0.05f, gestureUntil - gestureFrom));

    public void Settle(FaunaKind kind, int worldSeed, Transform watching, Vector3 at)
    {
        Kind = kind;
        seed = worldSeed;
        player = watching;
        traits = Fauna.Of(kind);
        body = AnimalBuilder.Build(kind, transform);
        neckAt = body.Head != null ? body.Head.localPosition : Vector3.zero;

        phase = Random.Range(0f, 10f);
        thirst = Random.Range(0f, 1f);

        transform.position = Ground(at);

        yaw = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Heard from where the animal is rather than from everywhere at once,
        // so a call tells you which way to look.
        voice = Source(70f, 0.9f);
        steps = Source(34f, 1f);

        nextCall = Time.time + Random.Range(6f, 30f);

        Graze();
    }

    private AudioSource Source(float reach, float doppler)
    {
        var source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 5f;
        source.maxDistance = reach;
        source.dopplerLevel = 0f;

        return source;
    }

    /// <summary>How far off the player is, for the manager's bookkeeping.</summary>
    public float DistanceTo(Vector3 point)
    {
        return Vector3.Distance(transform.position, point);
    }

    private void Update()
    {
        if (player == null) return;

        float dt = Time.deltaTime;
        float distance = DistanceTo(player.position);

        thirst += dt * 0.022f;

        Think(distance);
        Move(dt);
        Stand(dt);
        Animate(dt);
        Talk();
    }

    private void Think(float distance)
    {
        // Being seen matters more than whatever it was doing.
        if (state != State.Flee && state != State.Hidden)
        {
            float wariness = Stalking.Wariness;

            if (distance < traits.Bolts * wariness)
            {
                Speak(true);

                // the shove it gives itself getting away
                if (state != State.Flee) hop = traits.Size * 0.42f;

                Flee();
                return;
            }

            if (distance < traits.Notices * wariness)
            {
                if (state != State.Alert) until = Time.time + Random.Range(2f, 5f);
                state = State.Alert;

                // the forefoot stamp of something that has seen you and is
                // not yet sure what to do about it
                if ((Kind == FaunaKind.Deer || Kind == FaunaKind.Goat || Kind == FaunaKind.Boar) && gesture == Gesture.None && Time.time > nextGesture && Random.value < (Kind == FaunaKind.Boar ? 0.05f : 0.02f))
                {
                    Begin(Gesture.Stamp, 0.8f);
                    nextGesture = Time.time + Random.Range(3f, 7f);
                }
                if (Kind == FaunaKind.Rabbit && gesture == Gesture.None && Time.time > nextGesture)
                {
                    Begin(Gesture.SitUp, Random.Range(2.5f, 5f));
                    nextGesture = Time.time + Random.Range(6f, 12f);
                }
                return;
            }

            if (state == State.Alert) Graze();      // you backed off
        }

        // Gone to ground: it comes back up when its time is done and you are
        // not standing over the hole.
        if (state == State.Hidden)
        {
            if (Time.time > until && distance > traits.Notices * 0.8f) { state = State.Stand; until = Time.time + 1f; }
            return;
        }

        if (Time.time < until) return;

        // Getting up is a thing in itself: a stretch, or a shake.
        if (state == State.Rest)
        {
            Begin(Random.value < 0.6f ? Gesture.Stretch : Gesture.Shake, Random.Range(1.3f, 1.9f));
            return;
        }

        switch (state)
        {
            case State.Flee:
                if (Fauna.All(Kind).Burrows)
                {
                    // a short dash and down the hole
                    state = State.Hidden;
                    until = Time.time + Random.Range(14f, 36f);
                    break;
                }
                if (distance > traits.Settles) Graze();
                else Flee();
                break;

            case State.ToWater:
                Drinking();
                break;

            default:
                Decide(distance);
                break;
        }
    }

    /// <summary>
    /// What to do next. Weighted rather than cycled, so one animal is grazing
    /// while another wanders off and a third has its head up looking at
    /// nothing, instead of all of them doing the same thing in step.
    /// </summary>
    private void Decide(float distance)
    {
        float roll = Random.value;

        // Wet weather and the small hours are for sitting still. An animal
        // that grazed its way through a downpour exactly as it does through a
        // clear morning is one that has not noticed the weather at all.
        float overcast = TimeOfDay.Instance != null ? TimeOfDay.Instance.Overcast : 0f;
        float hour = TimeOfDay.Instance != null ? TimeOfDay.Instance.Normalized : 0.5f;

        bool nocturnal = Kind == FaunaKind.Fox;
        bool small = !nocturnal && (hour < 0.18f || hour > 0.88f);

        if ((overcast > 0.72f || small) && roll < 0.5f && distance > traits.Notices * 1.4f)
        {
            state = State.Rest;
            restedSince = Time.time;
            until = Time.time + Random.Range(16f, 40f);
            return;
        }

        // Now and then, one of the small things. Which ones depends on the
        // animal; none of them while you are close enough to worry it.
        if (Time.time > nextGesture && roll < 0.24f && distance > traits.Notices * 1.2f)
        {
            bool dark = hour > 0.80f || hour < 0.20f;
            var could = Kind switch
            {
                FaunaKind.Rabbit => new[] { Gesture.SitUp, Gesture.SitUp, Gesture.Scratch, Gesture.Shake },
                FaunaKind.Fox => new[] { Gesture.Groom, Gesture.Pounce, Gesture.Scratch, Gesture.Shake },
                FaunaKind.Goat => new[] { Gesture.Groom, Gesture.Stamp, Gesture.Shake },
                FaunaKind.Wolf => dark ? new[] { Gesture.Howl, Gesture.Howl, Gesture.Groom, Gesture.Shake } : new[] { Gesture.Groom, Gesture.Scratch, Gesture.Shake },
                FaunaKind.Tortoise => new[] { Gesture.Shake },
                FaunaKind.Heron => new[] { Gesture.Groom, Gesture.Shake, Gesture.Stretch },
                FaunaKind.Boar => new[] { Gesture.Shake, Gesture.Scratch, Gesture.Stamp },
                FaunaKind.Raven => new[] { Gesture.Stretch, Gesture.Shake, Gesture.Groom },
                FaunaKind.Marmot => new[] { Gesture.SitUp, Gesture.SitUp, Gesture.SitUp, Gesture.Scratch, Gesture.Groom },
                _ => new[] { Gesture.Stamp, Gesture.Shake, Gesture.Groom }
            };
            var pick = could[Random.Range(0, could.Length)];
            float length = pick switch
            {
                Gesture.SitUp => Random.Range(3f, 6.5f),
                Gesture.Groom => Random.Range(2.2f, 3.6f),
                Gesture.Scratch => Random.Range(1.4f, 2.2f),
                Gesture.Shake => 0.9f,
                Gesture.Pounce => 1.3f,
                Gesture.Howl => Random.Range(2.4f, 3.4f),
                _ => 1.0f
            };
            Begin(pick, length);
            nextGesture = Time.time + Random.Range(12f, 40f);
            return;
        }

        // wet weather: a shake now and then
        if (overcast > 0.6f && roll < 0.34f && Time.time > nextGesture)
        {
            Begin(Gesture.Shake, 0.9f);
            nextGesture = Time.time + Random.Range(10f, 30f);
            return;
        }

        if (thirst > 1f && roll < 0.35f && FindWater(out var shore))
        {
            target = shore;
            state = State.ToWater;
            until = Time.time + 18f;
            return;
        }

        // lying down only when nothing is near enough to worry about
        if (roll < 0.13f && distance > traits.Notices * 1.8f)
        {
            state = State.Rest;
            restedSince = Time.time;
            until = Time.time + Random.Range(14f, 34f);
            return;
        }

        if (roll < 0.30f)
        {
            state = State.Look;
            until = Time.time + Random.Range(2.5f, 6f);
            return;
        }

        if (roll < 0.62f)
        {
            Graze();
            return;
        }

        Wander();
    }

    private void Move(float dt)
    {
        if (state != State.Wander && state != State.Flee && state != State.ToWater)
        {
            if (state == State.Alert && !Fauna.All(Kind).Withdraws) Face(player.position - transform.position, dt, Fauna.All(Kind).Roots ? 7f : 4f);

            pace = Mathf.MoveTowards(pace, 0f, 8f * dt);
            return;
        }

        Vector3 to = target - transform.position;
        to.y = 0f;

        if (to.sqrMagnitude < 0.5f)
        {
            if (state == State.ToWater) Drinking();
            else if (state == State.Wander) Graze();
            return;
        }

        float want = state == State.Flee ? traits.RunSpeed : traits.WalkSpeed;

        // Weather is worth slowing for; nothing crosses a hillside in the rain
        // at the pace it would on a clear evening.
        if (TimeOfDay.Instance != null) want *= Mathf.Lerp(1f, 0.82f, TimeOfDay.Instance.Overcast);

        // Eased rather than switched, so it leans into a run and out of it.
        pace = Mathf.MoveTowards(pace, want, (state == State.Flee ? 14f : 4f) * dt);

        Face(to, dt, state == State.Flee ? 7f : 2.5f);

        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        transform.position += forward * (pace * dt);

        gait += pace * dt * Fauna.Moving(Kind, state == State.Flee).Cadence;

        if (!(Fauna.Flies(Kind) && state == State.Flee)) Footfall(state == State.Flee);
    }

    /// <summary>Whether it is in the air, which only a flier ever is.</summary>
    private bool Flying => Fauna.Flies(Kind) && (state == State.Flee || altitude > 0.05f);

    /// <summary>
    /// Puts the animal on the ground and lies it along the hill.
    ///
    /// The terrain is terraced, so the height under an animal changes in
    /// steps. Snapped to it, anything crossing a slope climbs a flight of
    /// stairs; eased onto it, it walks up the hill. The same goes for which
    /// way is up: standing bolt upright on a hillside is the thing that most
    /// gives away something dropped onto a world rather than living in it.
    /// </summary>
    private void Stand(float dt)
    {
        Vector3 at = transform.position;

        int x = Mathf.RoundToInt(at.x / WorldGrid.TileSize);
        int z = Mathf.RoundToInt(at.z / WorldGrid.TileSize);

        float ground = WorldHeight.SurfaceY(x, z, seed);

        // Close the gap quickly but never instantly, and never let it wade far
        // from the surface if it has been dropped a long way.
        at.y = Mathf.Abs(at.y - ground) > 3f ? ground : Mathf.Lerp(at.y, ground, 1f - Mathf.Exp(-12f * dt));

        transform.position = at;

        // the lie of the land, from the tiles either side of it
        float west = WorldHeight.SurfaceY(x - 1, z, seed);
        float east = WorldHeight.SurfaceY(x + 1, z, seed);
        float south = WorldHeight.SurfaceY(x, z - 1, seed);
        float north = WorldHeight.SurfaceY(x, z + 1, seed);

        var normal = new Vector3(west - east, 2f * WorldGrid.TileSize, south - north).normalized;

        slope = Vector3.Slerp(slope, normal, 1f - Mathf.Exp(-6f * dt));

        var tilt = Flying ? Quaternion.identity : Quaternion.FromToRotation(Vector3.up, slope);

        // a flier climbs while it is getting away and comes down once it has
        float wantAltitude = Fauna.Flies(Kind) && state == State.Flee ? traits.Size * 5.5f : 0f;
        altitude = Mathf.MoveTowards(altitude, wantAltitude, dt * traits.Size * (wantAltitude > altitude ? 3.2f : 2.6f));

        lastYaw = Mathf.LerpAngle(lastYaw, yaw, 1f - Mathf.Exp(-3f * dt));

        transform.rotation = Quaternion.Slerp(transform.rotation, tilt * Quaternion.Euler(0f, yaw, 0f),
                                              1f - Mathf.Exp(-9f * dt));
    }

    /// <summary>A step whenever a leg comes down, which is twice a gait cycle.</summary>
    private void Footfall(bool running)
    {
        if (gait - lastStep < Mathf.PI) return;

        lastStep = gait;

        if (steps == null) return;

        steps.pitch = Random.Range(0.9f, 1.12f);
        steps.PlayOneShot(AnimalVoice.Step(Kind, running), running ? 0.34f : 0.16f);
    }

    private void Animate(float dt)
    {
        bool moving = state == State.Wander || state == State.Flee || state == State.ToWater;
        bool running = state == State.Flee;

        var walk = Fauna.Moving(Kind, running);

        if (gesture != Gesture.None && Time.time > gestureUntil) gesture = Gesture.None;

        Limbs(dt, moving, walk);
        Carriage(dt, moving, walk);
        Poise(dt, moving);
        Ears(dt, moving);
        Tail(dt, moving);

        // a pounce carries it forward through the air
        if (gesture == Gesture.Pounce && GestureT > 0.4f)
            transform.position += Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * (traits.Size * 2.2f * dt);

        Nibble();
    }

    /// <summary>
    /// Ears. Forward when it is watching you, flat back when it runs, out to
    /// the sides when it is at ease with its head down; and the odd flick,
    /// each ear on its own clock, which is most of what makes a standing
    /// animal look alive.
    /// </summary>
    private void Ears(float dt, bool moving)
    {
        if (body.Ears == null) return;

        for (int i = 0; i < body.Ears.Length; i++)
        {
            if (body.Ears[i] == null) continue;
            float side = i == 0 ? 1f : -1f;

            float forward, splay;   // pitch toward the nose (negative) or flat back; roll outward
            switch (state)
            {
                case State.Alert: forward = -22f; splay = -4f; break;
                case State.Flee: forward = 55f; splay = -10f; break;
                case State.Graze:
                case State.Drink: forward = 8f; splay = 22f; break;
                case State.Rest: forward = 14f; splay = 26f; break;
                default: forward = moving ? 4f : 0f; splay = 8f; break;
            }
            if (gesture == Gesture.SitUp) { forward = -18f; splay = -6f; }
            if (Kind == FaunaKind.Goat) splay += 30f;     // carried out and down whatever it is doing

            // the flick: a quick lay-back and return, when its clock comes round
            if (Time.time > nextFlick[i]) { earFlick[i] = 1f; nextFlick[i] = Time.time + Random.Range(2.5f, 9f); }
            earFlick[i] = Mathf.MoveTowards(earFlick[i], 0f, dt * 4.5f);
            float flick = Mathf.Sin(earFlick[i] * Mathf.PI) * 32f;

            var want = Quaternion.Euler(forward + flick, 0f, -side * splay);
            body.Ears[i].localRotation = Quaternion.Slerp(body.Ears[i].localRotation, want, 1f - Mathf.Exp(-9f * dt));
        }
    }

    /// <summary>
    /// The tail, which is a different thing on each of them: a deer's flags
    /// up white when it is alarmed, a fox's brush swings and is carried out
    /// level on the move, a goat's is up and wagging, a rabbit's is a tuft.
    /// </summary>
    private void Tail(float dt, bool moving)
    {
        if (body.Tail == null) return;

        float lift = 0f, swing = 0f;

        switch (Kind)
        {
            case FaunaKind.Deer:
                bool flagged = state == State.Alert || state == State.Flee;
                lift = flagged ? -75f : Mathf.Sin(Time.time * 1.2f + phase) * 8f;
                swing = flagged ? Mathf.Sin(Time.time * 9f) * 8f : 0f;
                break;

            case FaunaKind.Fox:
                lift = moving ? (state == State.Flee ? 12f : 4f) : (state == State.Rest ? 22f : 6f);
                swing = moving ? Mathf.Sin(gait * 0.5f) * 10f : Mathf.Sin(Time.time * 1.1f + phase) * 22f;
                if (gesture == Gesture.Pounce) lift = -30f + GestureT * 40f;
                break;

            case FaunaKind.Goat:
                lift = -55f + Mathf.Sin(Time.time * 3.2f + phase) * 12f;
                swing = Mathf.Sin(Time.time * 3.2f + phase) * 25f;
                break;

            case FaunaKind.Wolf:
                lift = state == State.Alert ? -20f : (moving ? 10f : 24f);
                swing = moving ? Mathf.Sin(gait * 0.5f) * 8f : Mathf.Sin(Time.time * 0.8f + phase) * 10f;
                break;

            case FaunaKind.Heron:
            case FaunaKind.Raven:
                lift = Flying ? -10f : 4f;
                break;

            case FaunaKind.Boar:
                lift = state == State.Alert || moving ? -40f : 10f;
                swing = Mathf.Sin(Time.time * 4f + phase) * 14f;
                break;

            case FaunaKind.Marmot:
                lift = state == State.Alert ? -30f : 10f;
                swing = state == State.Alert ? Mathf.Sin(Time.time * 12f) * 15f : 0f;
                break;

            default:
                lift = Mathf.Sin(Time.time * 2f + phase) * 6f;
                break;
        }

        var want = Quaternion.Euler(lift, swing, 0f);
        body.Tail.localRotation = Quaternion.Slerp(body.Tail.localRotation, want, 1f - Mathf.Exp(-7f * dt));
    }

    /// <summary>
    /// Hips and knees. The hip swings the leg through, and the knee folds on
    /// the way forward and straightens to take the weight — which is the whole
    /// difference between an animal walking and a table sliding along.
    /// </summary>
    private void Limbs(float dt, bool moving, Fauna.Gait walk)
    {
        if (body.Legs == null) return;

        bool flying = Flying;

        for (int i = 0; i < body.Legs.Length; i++)
        {
            bool fore = i < 2;
            if (body.Legs[i] == null) continue;

            // Wings: folded along the flank on the ground, opened and beating
            // in the air. Yawed out to the side first, then flapped about
            // the body's own axis, each wing the mirror of the other.
            if (body.Winged && fore)
            {
                float side = i == 0 ? 1f : -1f;
                Quaternion wing;
                if (flying)
                {
                    float beat = Mathf.Sin(Time.time * 8.5f + phase) * 34f - 8f;
                    wing = Quaternion.AngleAxis(side * beat, Vector3.forward) * Quaternion.AngleAxis(-side * 86f, Vector3.up);
                }
                else if (gesture == Gesture.Stretch)
                {
                    // one wing out and down, the way a heron airs itself
                    wing = i == 0 ? Quaternion.AngleAxis(-25f, Vector3.forward) * Quaternion.AngleAxis(-70f, Vector3.up) : Quaternion.identity;
                }
                else wing = Quaternion.AngleAxis(side * 3f, Vector3.forward);

                body.Legs[i].localRotation = Quaternion.Slerp(body.Legs[i].localRotation, wing, flying ? 1f : dt * 6f);
                continue;
            }

            // Bounding throws both front legs forward together and both back
            // legs after them; a trot moves diagonal pairs.
            float legPhase = walk.Bounds
                ? (fore ? 0f : Mathf.PI * 0.62f)
                : (i % 3 == 0 ? 0f : Mathf.PI);

            float hip, knee;
            float t = GestureT;

            if (flying)
            {
                // trailing, the way a heron's do
                hip = 70f;
                knee = -15f;
            }
            else if (state == State.Rest)
            {
                // folded under it
                hip = fore ? 62f : -54f;
                knee = fore ? -96f : 104f;
            }
            else if (gesture == Gesture.SitUp)
            {
                // up on its haunches: forelegs tucked to the chest, hind legs flat
                hip = fore ? 70f : -30f;
                knee = fore ? -100f : 60f;
            }
            else if (gesture == Gesture.Stretch)
            {
                // forelegs out along the ground, rump in the air
                hip = fore ? 58f : -12f;
                knee = fore ? -8f : 10f;
            }
            else if (gesture == Gesture.Stamp && i == 0)
            {
                // the near forefoot lifted and brought down
                float arc = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t * 1.5f));
                hip = -44f * arc;
                knee = -70f * arc;
            }
            else if (gesture == Gesture.Scratch && i == 3)
            {
                // a hind leg up to the ear, going like anything
                hip = -70f + Mathf.Sin(Time.time * 22f) * 9f;
                knee = 50f;
            }
            else if (gesture == Gesture.Pounce)
            {
                float leap = Mathf.Clamp01((t - 0.4f) / 0.6f);
                hip = t < 0.4f ? (fore ? 20f : -30f) : (fore ? -35f + leap * 70f : 40f - leap * 60f);
                knee = t < 0.4f ? (fore ? -40f : 60f) : (fore ? -20f : 30f);
            }
            else if (moving)
            {
                float turn = gait + legPhase;

                hip = Mathf.Sin(turn) * walk.Swing;

                // fold while the leg is coming forward, straight while it is down
                float lift = Mathf.Max(0f, Mathf.Cos(turn));
                knee = lift * walk.Knee * (fore ? -1f : 1f);
            }
            else
            {
                // A shuffle while it turns on the spot, so it does not swing
                // round as one piece.
                float shuffle = Mathf.Abs(Mathf.DeltaAngle(yaw, lastYaw)) > 0.05f
                    ? Mathf.Sin(Time.time * 7f + i * 1.9f) * 5f
                    : 0f;

                hip = shuffle;
                knee = 0f;
            }

            body.Legs[i].localRotation = Quaternion.Slerp(body.Legs[i].localRotation,
                Quaternion.Euler(hip, 0f, 0f), moving || state == State.Rest ? 1f : dt * 6f);

            if (body.Knees != null && body.Knees[i] != null)
            {
                body.Knees[i].localRotation = Quaternion.Slerp(body.Knees[i].localRotation,
                    Quaternion.Euler(knee, 0f, 0f), moving || state == State.Rest ? 1f : dt * 6f);
            }
        }
    }

    /// <summary>
    /// What the body does over the stride: rising and falling, nose tipping up
    /// and down, weight rocking from one side to the other.
    /// </summary>
    private void Carriage(float dt, bool moving, Fauna.Gait walk)
    {
        if (body.Frame == null) return;

        float rise;
        float pitch = 0f;
        float roll = 0f;

        hop = Mathf.MoveTowards(hop, 0f, traits.Size * 1.6f * dt);

        float t = GestureT;

        if (state == State.Hidden)
        {
            // down the hole: the whole animal goes under the ground
            rise = Mathf.MoveTowards(body.Frame.localPosition.y, -traits.Size * 1.6f, dt * traits.Size * 4f);
        }
        else if (Flying)
        {
            // up, nose a little high, with the slow lift and fall of the beat
            rise = altitude + Mathf.Sin(Time.time * 8.5f + phase + 1.2f) * traits.Size * 0.04f;
            pitch = state == State.Flee ? -6f : 8f;
        }
        else if (Fauna.All(Kind).Withdraws && state == State.Alert)
        {
            // down onto the sand, and stays there
            rise = Mathf.Lerp(body.Frame.localPosition.y, -traits.Size * 0.12f, dt * 4f);
        }
        else if (state == State.Rest)
        {
            rise = Mathf.Lerp(body.Frame.localPosition.y, -traits.Size * 0.30f, dt * 3f);
        }
        else if (gesture == Gesture.SitUp)
        {
            // the whole front lifted, sat back on the haunches
            rise = Mathf.Lerp(body.Frame.localPosition.y, traits.Size * 0.14f, dt * 6f);
            pitch = -48f;
        }
        else if (gesture == Gesture.Stretch)
        {
            rise = Mathf.Lerp(body.Frame.localPosition.y, -traits.Size * 0.10f, dt * 5f);
            pitch = 16f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.2f));
        }
        else if (gesture == Gesture.Shake)
        {
            rise = Mathf.Lerp(body.Frame.localPosition.y, 0f, dt * 5f);
            roll = Mathf.Sin(Time.time * 42f) * 10f * (1f - t);
            pitch = Mathf.Sin(Time.time * 42f + 1f) * 3f * (1f - t);
        }
        else if (gesture == Gesture.Scratch)
        {
            rise = Mathf.Lerp(body.Frame.localPosition.y, -traits.Size * 0.05f, dt * 5f);
            roll = -9f;
        }
        else if (gesture == Gesture.Pounce)
        {
            // a crouch, then up and over, nose down into the grass at the end
            if (t < 0.4f) { rise = Mathf.Lerp(body.Frame.localPosition.y, -traits.Size * 0.14f, dt * 6f); pitch = 6f; }
            else
            {
                float leap = (t - 0.4f) / 0.6f;
                rise = Mathf.Sin(leap * Mathf.PI) * traits.Size * 0.55f;
                pitch = -22f + leap * 50f;
            }
        }
        else if (moving)
        {
            // A bound has one rise to a cycle; a walk has two, one per pair.
            rise = walk.Bounds
                ? Mathf.Max(0f, Mathf.Sin(gait)) * traits.Size * walk.Bounce
                : Mathf.Abs(Mathf.Sin(gait)) * traits.Size * walk.Bounce;

            pitch = -Mathf.Sin(gait + 0.6f) * walk.Pitch;
            roll = Mathf.Sin(gait * 0.5f) * walk.Roll;
        }
        else
        {
            // Standing still is not standing frozen: it breathes.
            float breath = Mathf.Sin(Time.time * 0.9f + phase) * traits.Size * 0.007f;

            rise = Mathf.Lerp(body.Frame.localPosition.y, breath, dt * 5f);
        }

        var local = body.Frame.localPosition;
        local.y = rise + hop;
        body.Frame.localPosition = local;

        body.Frame.localRotation = Quaternion.Slerp(body.Frame.localRotation,
            Quaternion.Euler(pitch, 0f, roll), moving ? 0.35f : dt * 5f);
    }

    /// <summary>Where the head is held, which is what the animal is doing.</summary>
    private void Poise(float dt, bool moving)
    {
        if (body.Head == null) return;

        float dip = 0f;
        float turn = 0f;

        switch (state)
        {
            case State.Graze:
                // down in the grass, with the small movements of chewing
                dip = 54f + Mathf.Sin(Time.time * 7f + phase) * 3.5f;
                break;

            case State.Drink:
                dip = 72f + Mathf.Sin(Time.time * 5f + phase) * 2.5f;
                break;

            case State.Rest:
                // settled a while, the head goes round onto the flank to sleep
                bool asleep = Time.time - restedSince > 7f;
                dip = asleep ? 24f : 16f;
                turn = asleep ? (Mathf.Repeat(phase, 1f) > 0.5f ? 125f : -125f) : 0f;
                break;

            case State.Look:
                // casting about, the way anything grazing checks on things
                turn = Mathf.Sin(Time.time * 1.4f + phase) * 38f;
                dip = 4f;
                break;

            case State.Alert:
                // Watching you, not swaying in your general direction. An
                // animal that holds your eye is the reason to stand still.
                Vector3 to = player.position - body.Head.position;
                Vector3 local = transform.InverseTransformDirection(to);

                turn = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -62f, 62f);
                dip = Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude)
                                  * Mathf.Rad2Deg, -22f, 30f);
                break;

            default:
                // the head nods with the stride rather than riding along level
                dip = moving ? 8f + Mathf.Sin(gait + 1.1f) * 5f : 22f;
                break;
        }

        // a tortoise pulls its head into the shell rather than watching you
        if (Fauna.All(Kind).Withdraws)
        {
            bool shut = state == State.Alert;
            body.Head.localPosition = Vector3.Lerp(body.Head.localPosition,
                shut ? neckAt - Vector3.forward * traits.Size * 0.30f - Vector3.up * traits.Size * 0.05f : neckAt, dt * 5f);
            if (shut) { dip = 12f; turn = 0f; }
        }

        // a boar rooting: snout in the ground, worked side to side
        if (Fauna.All(Kind).Roots && state == State.Graze)
        {
            dip = 46f + Mathf.Sin(Time.time * 5f + phase) * 4f;
            turn = Mathf.Sin(Time.time * 2.6f + phase) * 16f;
        }

        // a raven pecking: quick stabs at the ground between looks about
        if (Kind == FaunaKind.Raven && state == State.Graze)
        {
            float peck = Mathf.Sin(Time.time * 4f + phase);
            dip = peck > 0.6f ? 62f : 14f;
            turn = Mathf.Sin(Time.time * 0.7f + phase) * 40f;
        }

        // a heron fishing: head over the water, then the spear, then up again
        if (Kind == FaunaKind.Heron && (state == State.Graze || state == State.Drink))
        {
            float jab = Mathf.Sin(Time.time * 0.55f + phase);
            dip = jab > 0.96f ? 75f : 38f + jab * 6f;
            turn = Mathf.Sin(Time.time * 0.3f + phase) * 12f;
        }
        if (Flying) { dip = -4f; turn = 0f; }

        // the gestures that are done with the head
        if (gesture == Gesture.Howl)
        {
            // muzzle to the sky and held there
            dip = -42f;
            turn = 0f;
        }
        else if (gesture == Gesture.Groom)
        {
            turn = (Mathf.Repeat(phase, 1f) > 0.5f ? 118f : -118f);
            dip = 30f + Mathf.Sin(Time.time * 13f) * 4f;
        }
        else if (gesture == Gesture.Scratch)
        {
            turn = -38f;
            dip = 22f;
        }
        else if (gesture == Gesture.SitUp)
        {
            dip = -6f;
            turn = Mathf.Sin(Time.time * 0.9f + phase) * 30f;
        }
        else if (gesture == Gesture.Shake)
        {
            turn = Mathf.Sin(Time.time * 42f) * 9f * (1f - GestureT);
        }
        else if (gesture == Gesture.Stretch)
        {
            dip = -8f;
        }
        else if (gesture == Gesture.Pounce && GestureT > 0.8f)
        {
            dip = 45f;
        }

        // The odd twitch, so a standing animal is never quite still. Driven
        // off its own phase, so no two of them twitch together.
        if (state != State.Alert)
        {
            float tick = Mathf.Sin(Time.time * 0.37f + phase * 3f);
            if (tick > 0.985f) turn += (Mathf.Repeat(phase, 1f) > 0.5f ? 9f : -9f);
        }

        var want = Quaternion.Euler(dip, turn, 0f);
        body.Head.localRotation = Quaternion.Slerp(body.Head.localRotation, want,
                                                   dt * (state == State.Alert ? 8f : 5f));
    }

    /// <summary>The small sounds of an animal with its head down.</summary>
    private void Nibble()
    {
        if (state != State.Graze && state != State.Drink) return;
        if (Time.time < nextNibble || voice == null) return;

        nextNibble = Time.time + Random.Range(1.1f, 2.6f);

        voice.pitch = Random.Range(0.94f, 1.12f);
        voice.PlayOneShot(state == State.Drink ? AnimalVoice.Drink(Kind) : AnimalVoice.Chew(Kind), 0.28f);
    }

    /// <summary>
    /// The odd call while it is settled. Grazing animals are quiet most of the
    /// time, and something that called constantly would be a nuisance rather
    /// than a thing you look up for.
    /// </summary>
    private void Talk()
    {
        if (Time.time < nextCall) return;

        nextCall = Time.time + Random.Range(14f, 46f);

        if (state == State.Flee || state == State.Rest) return;

        Speak(state == State.Alert);
    }

    private void Speak(bool alarmed)
    {
        if (voice == null || !AnimalVoice.Ready) return;

        voice.pitch = Random.Range(0.92f, 1.10f);
        voice.PlayOneShot(AnimalVoice.Call(Kind, alarmed), alarmed ? 0.75f : 0.5f);

        nextCall = Mathf.Max(nextCall, Time.time + 9f);
    }

    /// <summary>Turns towards something. Only the yaw: the tilt is the hill's business.</summary>
    private void Face(Vector3 direction, float dt, float speed)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) return;

        float want = Quaternion.LookRotation(direction).eulerAngles.y;

        yaw = Mathf.LerpAngle(yaw, want, 1f - Mathf.Exp(-speed * dt));
    }

    private void Graze()
    {
        state = State.Graze;
        until = Time.time + Random.Range(4f, 11f);
    }

    /// <summary>Starts a gesture. It holds the animal on the spot for as long as it runs.</summary>
    private void Begin(Gesture what, float length)
    {
        gesture = what;
        gestureFrom = Time.time;
        gestureUntil = Time.time + length;

        if (what == Gesture.Howl) Speak(false);

        if (state != State.Alert)
        {
            state = State.Stand;
            until = gestureUntil + 0.2f;
        }
    }

    private void Drinking()
    {
        state = State.Drink;
        until = Time.time + Random.Range(5f, 10f);
        thirst = 0f;
    }

    private void Wander()
    {
        // Keep loose company: something of the same kind nearby is worth
        // drifting towards, which is what turns four deer into a herd.
        Vector3 pull = Company();

        for (int attempt = 0; attempt < 6; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(5f, 16f);
            Vector3 at = transform.position + new Vector3(offset.x, 0f, offset.y);

            if (pull != Vector3.zero) at = Vector3.Lerp(at, pull, 0.45f);

            if (!Walkable(at)) continue;

            target = at;
            state = State.Wander;
            until = Time.time + 14f;
            return;
        }

        Graze();
    }

    /// <summary>The nearest of its own kind, if one is close enough to matter.</summary>
    private Vector3 Company()
    {
        var others = transform.parent;

        if (others == null) return Vector3.zero;

        float best = 26f;
        Vector3 at = Vector3.zero;

        for (int i = 0; i < others.childCount; i++)
        {
            var other = others.GetChild(i);

            if (other == transform) continue;

            var animal = other.GetComponent<Animal>();

            if (animal == null || animal.Kind != Kind) continue;

            float distance = Vector3.Distance(other.position, transform.position);

            if (distance > 7f && distance < best)
            {
                best = distance;
                at = other.position;
            }
        }

        return at;
    }

    /// <summary>Somewhere to stand at the edge of water, if there is any about.</summary>
    private bool FindWater(out Vector3 shore)
    {
        shore = Vector3.zero;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(6f, 30f);
            Vector3 at = transform.position + new Vector3(offset.x, 0f, offset.y);

            int x = Mathf.RoundToInt(at.x / WorldGrid.TileSize);
            int z = Mathf.RoundToInt(at.z / WorldGrid.TileSize);

            if (!WaterSurface.IsUnderwater(x, z, seed)) continue;

            // stand on the bank rather than wade in
            for (int dx = -1; dx <= 1; dx++)
            for (int dz = -1; dz <= 1; dz++)
            {
                if (WaterSurface.IsUnderwater(x + dx, z + dz, seed)) continue;

                var bank = new Vector3((x + dx) * WorldGrid.TileSize, 0f, (z + dz) * WorldGrid.TileSize);

                if (!Walkable(bank)) continue;

                shore = bank;
                return true;
            }
        }

        return false;
    }

    private void Flee()
    {
        Vector3 away = transform.position - player.position;
        away.y = 0f;

        if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;

        away = away.normalized;

        // Straight away from you if it can, at an angle if the ground there is
        // no good — animals run round a lake rather than into it.
        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector3 heading = Quaternion.Euler(0f, attempt * 24f * (attempt % 2 == 0 ? 1f : -1f), 0f) * away;
            Vector3 at = transform.position + heading * Random.Range(18f, 34f);

            if (!Fauna.Flies(Kind) && !Walkable(at)) continue;

            target = at;
            state = State.Flee;
            until = Time.time + (Fauna.All(Kind).Burrows ? 1.1f : 5f);
            return;
        }

        target = transform.position + away * 20f;
        state = State.Flee;
        until = Time.time + (Fauna.All(Kind).Burrows ? 1.1f : 5f);
    }

    private bool Walkable(Vector3 at)
    {
        int x = Mathf.RoundToInt(at.x / WorldGrid.TileSize);
        int z = Mathf.RoundToInt(at.z / WorldGrid.TileSize);

        if (!Fauna.Ground(Kind, x, z, seed)) return false;

        // Wild things keep clear of the ruins, which is also the reason a
        // landmark you walk up to is a quiet place.
        var placement = Landmarks.In(WorldGrid.WorldToChunk(at), seed);

        if (placement.Exists)
        {
            Vector3 apart = placement.Position - at;
            apart.y = 0f;

            if (apart.sqrMagnitude < 11f * 11f) return false;
        }

        // Nothing here climbs a cliff to get away from you.
        return Mathf.Abs(WorldHeight.SurfaceY(x, z, seed) - transform.position.y) < 8f;
    }

    private Vector3 Ground(Vector3 at)
    {
        int x = Mathf.RoundToInt(at.x / WorldGrid.TileSize);
        int z = Mathf.RoundToInt(at.z / WorldGrid.TileSize);

        at.y = WorldHeight.SurfaceY(x, z, seed);

        return at;
    }
}
