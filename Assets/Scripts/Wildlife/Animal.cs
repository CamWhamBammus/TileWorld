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
    private enum State { Stand, Graze, Look, Wander, ToWater, Drink, Rest, Alert, Flee, Hidden, Hunt }

    /// <summary>
    /// The small things an animal does between one thing and the next, laid
    /// over whatever state it is in: a deer stamps a forefoot when it is
    /// unsure of you, a rabbit sits up to look, a fox grooms its flank and
    /// pounces on something in the grass, anything getting up from a rest
    /// stretches or shakes. Each runs for a moment and is gone.
    /// </summary>
    private enum Gesture { None, Stamp, SitUp, Groom, Scratch, Shake, Stretch, Pounce, Howl, Rise }

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
                case State.Flee: return Doing.Fleeing;
                case State.Hidden: return gesture == Gesture.Rise ? Doing.Walking : Doing.Fleeing;
                case State.Wander:
                case State.ToWater: return Doing.Walking;
                case State.Hunt: return Doing.Hunting;
                default: return Doing.Standing;
            }
        }
    }

    /// <summary>Where its head is, which is what you would be drawing.</summary>
    /// <summary>
    /// Whether there is anything to see. Down a burrow or under the water
    /// there is not, and nothing hidden should count as seen, or be drawn.
    /// </summary>
    public bool Visible => state != State.Hidden || (Fauna.All(Kind).Surfaces && gesture == Gesture.Rise);

    /// <summary>How big this one is against its kind: a large fox is 1.2, a small one 0.8.</summary>
    public float Scale => body.Frame != null ? body.Frame.localScale.x : 1f;

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
    private float perchHeight;       // how high up the ruin it is sitting, if it is
    private bool perched;
    private GameObject snag;         // a dead trunk built for it to sit on, where there was no ruin
    private bool landing;            // a flier on its way down to ground that suits it

    // What it is afraid of, when that is not you: a fox after it, or the
    // alarm of something that saw a fox. Fled from the same way.
    private Vector3 threat;
    private bool threatened;

    // What it is after, when it hunts, and when it last looked for something
    private Animal quarry;
    private float nextHuntLook;
    private float nextFollow;
    private float nextRooting;
    private Vector2Int lastTile = new Vector2Int(int.MinValue, int.MinValue);

    /// <summary>The one it keeps with, if it came with company.</summary>
    public Animal Leader { get; set; }

    /// <summary>
    /// Frightened by something other than you: the alarm of another animal,
    /// or a hunter. Takes the same fright, from the same quarter, a moment
    /// later -- a herd does not go all at once as one piece.
    /// </summary>
    public void Startle(Vector3 from, bool run, float delay = 0f)
    {
        if (state == State.Flee || state == State.Hidden || state == State.Hunt) return;
        if (delay > 0f) { StartCoroutine(StartleLater(from, run, delay)); return; }

        threat = from;
        threatened = true;

        if (run) Flee();
        else { state = State.Alert; until = Time.time + Random.Range(2.5f, 6f); }
    }

    private System.Collections.IEnumerator StartleLater(Vector3 from, bool run, float delay)
    {
        yield return new WaitForSeconds(delay);
        Startle(from, run, 0f);
    }

    /// <summary>Where the fright is coming from: you, unless it is something else.</summary>
    private Vector3 Threat => threatened ? threat : player.position;

    // How much of a stride it is taking: eased in and out over a quarter of
    // a second, so standing does not snap into walking.
    private float stride;

    // Ears and tail on springs rather than lerps: they lag the head and body
    // and settle a beat after them, with a little overshoot, which is most of
    // what reads as alive.
    private readonly float[] earPitch = new float[2], earRoll = new float[2], earPitchV = new float[2], earRollV = new float[2];
    private float tailLift, tailSwing, tailLiftV, tailSwingV;

    /// <summary>Where each sole is, in the world, as last placed.</summary>
    public Vector3[] Feet { get; } = new Vector3[4];

    /// <summary>Which soles are on the ground this frame, as against swinging.</summary>
    public bool[] Planted { get; } = new bool[4];

    // The visible ground is not the walking surface. A grass tile carries a
    // cap of blades a third of a unit above it, snow lies a drift deep on it,
    // sand and stone sit a little proud of it. Feet go on what can be seen.
    private float footing;
    private float baseline;          // how far the frame is dropped so the soles reach the footing

    /// <summary>How far above the walking surface the ground looks to be, on this tile.</summary>
    public static float FootingAt(int tileX, int tileZ, int seed)
    {
        if (WaterSurface.IsUnderwater(tileX, tileZ, seed)) return 0f;
        if (SnowCover.IsSnowy(tileX, tileZ, seed)) return 0.17f;
        switch (Regions.CharacterAtTile(tileX, tileZ, seed))
        {
            case Regions.Character.Desert: return 0.14f;
            case Regions.Character.Stone:
            case Regions.Character.Peaks: return 0.08f;
            default: return 0.32f;
        }
    }
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

        // An owl sits up on a ruin if there is one close. The perch is the
        // structure's own place, a way up it; the ruin's collider is not
        // consulted, so it sits at about roof height.
        if (Fauna.All(kind).Perches)
        {
            var chunk = WorldGrid.WorldToChunk(at);
            for (int dx = -1; dx <= 1 && !perched; dx++)
            for (int dz = -1; dz <= 1 && !perched; dz++)
            {
                var placed = Landmarks.In(new Vector2Int(chunk.x + dx, chunk.y + dz), worldSeed);
                if (!placed.Exists || Vector3.Distance(placed.Position, at) > 60f) continue;
                float up = Landmarks.All(placed.Kind).LabelHeight * 0.55f;
                transform.position = Ground(placed.Position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)));
                perchHeight = Mathf.Max(1.5f, placed.Position.y + up - transform.position.y);
                altitude = perchHeight;
                perched = true;
            }

            // No ruin near: a dead snag is put up for it, a bare trunk with
            // one stub of a branch, so it is never sat on nothing. The snag
            // stands in the world rather than under the bird, since the bird
            // will leave it; it goes when the bird is taken away.
            if (!perched)
            {
                var snagKit = new Kit.Builder(Mathf.RoundToInt(at.x * 7f + at.z * 13f));
                float tall = Random.Range(2.3f, 3.3f);
                snagKit.Log(Vector3.zero, Vector3.up * tall, 0.13f, Kit.Swatch.OldWood, 7);
                snagKit.Log(Vector3.up * (tall - 0.7f), Vector3.up * (tall - 0.25f) + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 0.55f, 0.05f, Kit.Swatch.OldWood, 5);
                snagKit.Log(Vector3.zero, Vector3.up * 0.3f + Vector3.right * 0.35f, 0.08f, Kit.Swatch.OldWood, 5);
                var flora = Resources.Load<Flora>("Flora");
                snag = snagKit.Finish("snag", transform.parent, Vector3.zero, flora != null ? flora.Paint : null);
                snag.transform.position = transform.position;
                perchHeight = tall;
                altitude = tall;
                perched = true;
            }
        }

        yaw = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        // Heard from where the animal is rather than from everywhere at once,
        // so a call tells you which way to look.
        voice = Source(70f, 0.9f);
        steps = Source(34f, 1f);

        nextCall = Time.time + Random.Range(6f, 30f);

        Graze();

        // what lives under the water begins there, and rises in its own time
        if (Fauna.All(kind).Surfaces)
        {
            state = State.Hidden;
            until = Time.time + Random.Range(3f, 12f);
        }
    }

    private void OnDestroy()
    {
        if (snag != null) Destroy(snag);
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

            if (state == State.Alert) { Graze(); threatened = false; }      // you backed off
        }

        // The hunt, kept up every frame: the quarry moves, so the target does.
        if (state == State.Hunt)
        {
            if (quarry == null || !quarry.Visible || Time.time > until || DistanceTo(quarry.transform.position) > traits.Notices * 1.3f)
            {
                quarry = null;
                nextHuntLook = Time.time + Random.Range(25f, 50f);
                Graze();
                until = Time.time + Random.Range(3f, 7f);
                return;
            }

            target = quarry.transform.position;

            // caught up with it: the lunge, and the quarry away again. Nothing
            // is ever caught; the chase is the thing worth seeing.
            if (DistanceTo(quarry.transform.position) < traits.Size * 1.6f)
            {
                Begin(Kind == FaunaKind.Fox ? Gesture.Pounce : Gesture.Stamp, Kind == FaunaKind.Fox ? 1.3f : 0.6f);
                quarry.Startle(transform.position, true);
                quarry = null;
                nextHuntLook = Time.time + Random.Range(25f, 50f);
                state = State.Stand;
                until = Time.time + Random.Range(3f, 6f);
            }
            return;
        }

        // A hunter looks about for something to hunt, now and then, when it
        // is not busy being afraid of you.
        if (Fauna.Hunts(Kind) && state != State.Flee && state != State.Hidden && state != State.Alert && Time.time > nextHuntLook)
        {
            nextHuntLook = Time.time + 0.7f;
            var prey = Wildlife.Nearest(this, Fauna.All(Kind).Preys, traits.Notices * 0.8f);
            if (prey != null && distance > traits.Bolts * Stalking.Wariness)
            {
                quarry = prey;
                state = State.Hunt;
                until = Time.time + Random.Range(6f, 10f);
                target = prey.transform.position;
                prey.Startle(transform.position, true);
                return;
            }
        }

        // Left behind: one that came with a leader goes after it when the
        // leader has got more than a dozen metres off, whatever its clock says.
        if (Leader != null && Leader != this && (state == State.Graze || state == State.Stand || state == State.Look)
            && Vector3.Distance(Leader.transform.position, transform.position) > 12f && Time.time > nextFollow)
        {
            nextFollow = Time.time + 4f;
            Wander();
            return;
        }

        // Gone to ground: it comes back up when its time is done and you are
        // not standing over the hole.
        if (state == State.Hidden)
        {
            if (Fauna.All(Kind).Surfaces)
            {
                // up for a moment, when nothing is close, then down again
                if (gesture == Gesture.None && Time.time > until && distance > traits.Bolts * Stalking.Wariness)
                {
                    gesture = Gesture.Rise;
                    gestureFrom = Time.time;
                    gestureUntil = Time.time + Random.Range(2.4f, 4f);
                    until = gestureUntil + Random.Range(7f, 22f);
                    Speak(false);
                }
                if (gesture == Gesture.Rise && distance < traits.Bolts * Stalking.Wariness) { gesture = Gesture.None; until = Time.time + Random.Range(10f, 25f); }
                return;
            }
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
                // far enough from whatever it was, and the fright is over
                if (threatened && Vector3.Distance(transform.position, threat) > traits.Settles) threatened = false;
                if (Fauna.Flies(Kind) && !Fauna.All(Kind).Airborne && distance > traits.Settles)
                {
                    // far enough: find ground of its own sort to come down on,
                    // and fly there before dropping, rather than landing on
                    // whatever hillside it happens to be over
                    var spot = LandingSpot();
                    if (spot.HasValue) { target = spot.Value; landing = true; state = State.Wander; until = Time.time + 25f; }
                    else Graze();
                    break;
                }
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

        // on the wing there is nothing but the next turn
        if (Fauna.All(Kind).Airborne)
        {
            Wander();
            until = Time.time + (Fauna.All(Kind).Soars ? Random.Range(6f, 14f) : Random.Range(1.5f, 4f));
            return;
        }

        // up on its perch there is nothing to do but look, one way and then the other
        if (perched)
        {
            state = roll < 0.6f ? State.Look : State.Stand;
            until = Time.time + Random.Range(3f, 8f);
            if (roll > 0.92f && Time.time > nextGesture) { Begin(Gesture.Stretch, 1.6f); nextGesture = Time.time + 20f; }
            return;
        }

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
                FaunaKind.Crab => new[] { Gesture.Shake },
                FaunaKind.Owl => new[] { Gesture.Stretch, Gesture.Shake, Gesture.Groom },
                FaunaKind.Frog => new[] { Gesture.Shake },
                FaunaKind.Hedgehog => new[] { Gesture.Shake, Gesture.Scratch },
                FaunaKind.Bat => new Gesture[0],
                FaunaKind.Eagle => new Gesture[0],
                FaunaKind.Scorpion => new Gesture[0],
                FaunaKind.Hare => new[] { Gesture.SitUp, Gesture.Scratch, Gesture.Shake, Gesture.Groom },
                FaunaKind.Fish => new Gesture[0],
                _ => new[] { Gesture.Stamp, Gesture.Shake, Gesture.Groom }
            };
            if (could.Length == 0) { nextGesture = Time.time + 30f; return; }
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
        if (state != State.Wander && state != State.Flee && state != State.ToWater && state != State.Hunt)
        {
            if (state == State.Alert && !Fauna.All(Kind).Withdraws) Face(Threat - transform.position, dt, Fauna.All(Kind).Roots ? 7f : 4f);

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

        float want = state == State.Flee || state == State.Hunt || landing ? traits.RunSpeed : traits.WalkSpeed;

        // Weather is worth slowing for; nothing crosses a hillside in the rain
        // at the pace it would on a clear evening.
        if (TimeOfDay.Instance != null) want *= Mathf.Lerp(1f, 0.82f, TimeOfDay.Instance.Overcast);

        // Eased rather than switched, so it leans into a run and out of it.
        pace = Mathf.MoveTowards(pace, want, (state == State.Flee ? 14f : 4f) * dt);

        Face(to, dt, state == State.Flee ? 7f : 2.5f);

        // a bat's flight is all jinks: the heading wavers on its own; an
        // eagle's is one long slow circle
        if (Fauna.All(Kind).Soars) yaw += 24f * dt * (Mathf.Repeat(phase, 1f) > 0.5f ? 1f : -1f);
        else if (Fauna.All(Kind).Airborne) yaw += Mathf.Sin(Time.time * 5.3f + phase) * 90f * dt + Mathf.Sin(Time.time * 11f + phase * 2f) * 40f * dt;

        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;

        transform.position += forward * (pace * dt);

        // The cycle runs at whatever rate keeps a planted foot still: half a
        // cycle on the ground has to carry the foot back exactly as far as the
        // body goes forward, so the rate follows the stride, not a fixed number.
        gait += pace * dt * CycleRate(Fauna.Moving(Kind, state == State.Flee || state == State.Hunt));

        // the tile it is on: each new one crossed is a crossing, toward a trail
        if (!Flying)
        {
            var tile = new Vector2Int(Mathf.RoundToInt(transform.position.x / WorldGrid.TileSize), Mathf.RoundToInt(transform.position.z / WorldGrid.TileSize));
            if (tile != lastTile) { lastTile = tile; Tracks.Cross(tile.x, tile.y); }
        }

        if (landing)
        {
            Vector3 left = target - transform.position; left.y = 0f;
            if (left.magnitude < 2.5f) { landing = false; Graze(); }
        }

        if (!(Fauna.Flies(Kind) && state == State.Flee) && !landing) Footfall(state == State.Flee);
    }

    /// <summary>Whether it is in the air, which only a flier ever is.</summary>
    private bool Flying => Fauna.All(Kind).Airborne || (Fauna.Flies(Kind) && (state == State.Flee || landing || (altitude > 0.05f && !perched)));

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
        // Under something on the wing the ground is followed slowly, so a
        // sharp ridge is a long rise in its flight and not a step in it.
        bool aloft = Fauna.All(Kind).Airborne;
        at.y = !aloft && Mathf.Abs(at.y - ground) > 3f ? ground : Mathf.Lerp(at.y, ground, 1f - Mathf.Exp((aloft ? -0.7f : -12f) * dt));

        transform.position = at;

        // the lie of the land, from the tiles either side of it
        float west = WorldHeight.SurfaceY(x - 1, z, seed);
        float east = WorldHeight.SurfaceY(x + 1, z, seed);
        float south = WorldHeight.SurfaceY(x, z - 1, seed);
        float north = WorldHeight.SurfaceY(x, z + 1, seed);

        var normal = new Vector3(west - east, 2f * WorldGrid.TileSize, south - north).normalized;

        slope = Vector3.Slerp(slope, normal, 1f - Mathf.Exp(-6f * dt));

        // Half the lie of the land in the body; the legs take up the rest, so
        // an animal across a hillside stands with its uphill legs folded and
        // its downhill legs straight, the way one does.
        var tilt = Flying ? Quaternion.identity : Quaternion.FromToRotation(Vector3.up, Vector3.Slerp(Vector3.up, slope, 0.5f));

        // a flier climbs while it is getting away and comes down once it has
        if (state == State.Flee) perched = false;
        float wantAltitude = Fauna.All(Kind).Soars ? traits.Size * 22f + Mathf.Sin(Time.time * 0.25f + phase) * traits.Size * 4f
                           : Fauna.All(Kind).Airborne ? traits.Size * 9f + Mathf.Sin(Time.time * 0.7f + phase) * traits.Size * 2.5f
                           : Fauna.Flies(Kind) && (state == State.Flee || landing) ? traits.Size * 5.5f : (perched ? perchHeight : 0f);
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
        bool moving = state == State.Wander || state == State.Flee || state == State.ToWater || state == State.Hunt;
        bool running = state == State.Flee || state == State.Hunt;

        var walk = Fauna.Moving(Kind, running);

        if (gesture != Gesture.None && Time.time > gestureUntil) gesture = Gesture.None;

        stride = Mathf.MoveTowards(stride, moving && !Flying && state != State.Hidden ? 1f : 0f, dt * 4f);

        // where the soles should rest, and how far the body has to come down
        // for them to: the legs were drawn to end a little above the frame's
        // origin, and the visible ground is a little above the walking one
        footing = FootingAt(Mathf.RoundToInt(transform.position.x / WorldGrid.TileSize), Mathf.RoundToInt(transform.position.z / WorldGrid.TileSize), seed);
        // The soles a little below the footing, by six percent of the leg:
        // a leg exactly as long as the drop stands locked straight and has
        // nothing to give when the foot moves; the slack is a slight bend.
        float gap = 0f, reach = 0f;
        if (body.Thigh != null && body.Legs != null && body.Legs.Length > 2 && body.Legs[2] != null)
        {
            gap = body.Legs[2].localPosition.y + body.Thigh[2].y + body.Shin[2].y;
            reach = body.Thigh[2].magnitude + body.Shin[2].magnitude;
        }
        float scale = body.Frame != null ? body.Frame.localScale.x : 1f;
        float wantBaseline = Flying || perched || state == State.Hidden ? 0f : footing - (Mathf.Max(0f, gap) + reach * 0.09f) * scale;
        baseline = Mathf.Lerp(baseline, wantBaseline, 1f - Mathf.Exp(-6f * dt));

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
            if (Fauna.All(Kind).Freezes && state == State.Alert) { forward = 62f; splay = -8f; }   // laid flat along the back
            if (Kind == FaunaKind.Goat) splay += 30f;     // carried out and down whatever it is doing

            // the flick: a quick lay-back and return, when its clock comes round
            if (Time.time > nextFlick[i]) { earFlick[i] = 1f; nextFlick[i] = Time.time + Random.Range(2.5f, 9f); }
            earFlick[i] = Mathf.MoveTowards(earFlick[i], 0f, dt * 4.5f);
            float flick = Mathf.Sin(earFlick[i] * Mathf.PI) * 32f;

            Spring(ref earPitch[i], ref earPitchV[i], forward + flick, 320f, 14f, dt);
            Spring(ref earRoll[i], ref earRollV[i], -side * splay, 260f, 13f, dt);
            body.Ears[i].localRotation = Quaternion.Euler(earPitch[i], 0f, earRoll[i]);
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
            case FaunaKind.Owl:
                lift = Flying ? -10f : 4f;
                break;

            case FaunaKind.Crab:
            case FaunaKind.Frog:
            case FaunaKind.Hedgehog:
                break;

            case FaunaKind.Fish:
                swing = Mathf.Sin(Time.time * 6f + phase) * 25f;
                break;

            case FaunaKind.Scorpion:
                // curled over the back, and higher, quivering, when it is at bay
                lift = state == State.Alert ? -60f + Mathf.Sin(Time.time * 14f) * 5f : -20f;
                break;

            case FaunaKind.Eagle:
                lift = Flying ? 2f : 6f;
                swing = Flying ? (Mathf.Repeat(phase, 1f) > 0.5f ? 10f : -10f) : 0f;
                break;

            case FaunaKind.Bat:
                lift = 10f;
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

        Spring(ref tailLift, ref tailLiftV, lift, 140f, 9f, dt);
        Spring(ref tailSwing, ref tailSwingV, swing, 140f, 9f, dt);
        body.Tail.localRotation = Quaternion.Euler(tailLift, tailSwing, 0f);
    }

    /// <summary>
    /// A damped spring on one angle: pulled toward its target, its speed
    /// bled off, so it arrives a beat late and a touch over.
    /// </summary>
    private static void Spring(ref float value, ref float velocity, float target, float stiffness, float damping, float dt)
    {
        dt = Mathf.Min(dt, 0.05f);
        velocity += (target - value) * stiffness * dt;
        velocity *= Mathf.Exp(-damping * dt);
        value += velocity * dt;
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
                if (flying && Fauna.All(Kind).Soars && state != State.Flee)
                {
                    // set wings, held a little up, with a few slow beats now and then
                    bool beating = Mathf.Sin(Time.time * 0.23f + phase) > 0.75f;
                    float beat = beating ? Mathf.Sin(Time.time * 3.2f) * 22f : 6f + Mathf.Sin(Time.time * 0.9f + phase) * 3f;
                    wing = Quaternion.AngleAxis(side * beat, Vector3.forward) * Quaternion.AngleAxis(-side * 88f, Vector3.up);
                }
                else if (flying)
                {
                    float beat = Mathf.Sin(Time.time * (traits.Size < 0.3f ? 18f : 8.5f) + phase) * 34f - 8f;
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
            else if ((Fauna.All(Kind).Withdraws || Fauna.All(Kind).Freezes) && state == State.Alert)
            {
                // tucked in under it, or folded flat to the ground
                hip = fore ? 50f : -50f;
                knee = fore ? -70f : 70f;
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
            else if (body.Knees != null && body.Knees[i] != null && body.Thigh != null)
            {
                // Standing or moving: the foot is put where it should be and
                // the leg is bent to reach it. On the ground the foot stays
                // where it was set down; in the air it swings forward in an
                // arc; and the ground is the ground under that foot, not a
                // plane through the middle of the animal.
                Place(i, fore, legPhase, walk, dt);
                continue;
            }
            else
            {
                hip = 0f;
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
    /// One leg, placed. The foot's target is worked out in the frame's own
    /// space -- its rest position under the hip, moved back and forth by the
    /// stride and lifted in an arc while it swings -- dropped to the ground
    /// under it, and the hip and knee turned to reach it: two bones, solved
    /// in the plane the leg swings in.
    /// </summary>
    private void Place(int i, bool fore, float legPhase, Fauna.Gait walk, float dt)
    {
        var hip = body.Legs[i];
        var knee = body.Knees[i];
        var frame = body.Frame;

        Vector3 thigh = body.Thigh[i];
        Vector3 shin = body.Shin[i];
        float a = thigh.magnitude, b = shin.magnitude;
        float reach = a + b;

        // the stride, in the frame's units: half a cycle carries the body
        // pi / cadence forward, and the planted foot has to go the same way back
        float worldScale = Mathf.Max(0.01f, frame.lossyScale.x);
        float half = StrideHalf(walk, reach, worldScale) * stride;
        float turn = gait + legPhase;

        // Half the cycle in the air, swinging forward in an arc; half on the
        // ground, going back at one speed. A sine here slowed the planted foot
        // at both ends of the stance and the body outran it.
        float u = Mathf.Repeat(turn, Mathf.PI * 2f);
        float along, lift;
        if (u < Mathf.PI)
        {
            float p = u / Mathf.PI;
            along = -Mathf.Cos(p * Mathf.PI) * half;
            lift = Mathf.Sin(p * Mathf.PI) * reach * (walk.Bounds ? 0.30f : 0.22f) * stride;
        }
        else
        {
            float p = (u - Mathf.PI) / Mathf.PI;
            along = half * (1f - 2f * p);
            lift = 0f;
        }

        // the shuffle of a turn on the spot, when it is not going anywhere
        if (stride < 0.05f && Mathf.Abs(Mathf.DeltaAngle(yaw, lastYaw)) > 0.05f)
        {
            along = Mathf.Sin(Time.time * 7f + i * 1.9f) * reach * 0.06f;
            lift = Mathf.Max(0f, Mathf.Sin(Time.time * 7f + i * 1.9f)) * reach * 0.04f;
        }

        Vector3 restFoot = hip.localPosition + thigh + shin;
        Vector3 wantLocal = restFoot + new Vector3(0f, 0f, along);

        // to the ground under that foot, in the world, then back
        Vector3 wantWorld = frame.TransformPoint(wantLocal);
        float ground = GroundUnder(wantWorld) + footing;
        wantWorld.y = ground + lift * worldScale;
        Vector3 target = frame.InverseTransformPoint(wantWorld);

        // in the leg's plane: y and z about the hip
        Vector2 H = new Vector2(hip.localPosition.y, hip.localPosition.z);
        Vector2 T = new Vector2(target.y, target.z);
        Vector2 toFoot = T - H;
        float d = Mathf.Clamp(toFoot.magnitude, Mathf.Abs(a - b) + 0.001f, reach - 0.001f);
        toFoot = toFoot.normalized * d;

        Vector2 t2 = new Vector2(thigh.y, thigh.z);
        Vector2 s2 = new Vector2(shin.y, shin.z);

        // how far the knee has to close: the angle between thigh and shin
        // that makes the leg exactly d long, against the angle it was built with
        float cosBend = Mathf.Clamp((d * d - a * a - b * b) / (2f * a * b), -1f, 1f);
        float bend = Mathf.Acos(cosBend);                       // angle between the two bones
        float built = Mathf.Acos(Mathf.Clamp(Vector2.Dot(t2, s2) / (a * b), -1f, 1f));

        // Two ways to fold; the right one has the knee forward of the line
        // from hip to foot on a foreleg and behind it on a hind leg.
        float best = 0f; Vector2 bestKnee = Vector2.zero; float bestHip = 0f; bool any = false;
        foreach (float sign in new[] { 1f, -1f })
        {
            float kneeTurn = sign * (built - bend) * Mathf.Rad2Deg * -1f;
            Vector2 shinTurned = Rotate(s2, kneeTurn);
            Vector2 w = t2 + shinTurned;
            float hipTurn = Vector2.SignedAngle(w, toFoot) * -1f;   // the sign of Rotate() below
            Vector2 kneeAt = Rotate(t2, hipTurn);
            float lineSide = Vector3.Cross(new Vector3(toFoot.y, toFoot.x, 0f), new Vector3(kneeAt.y, kneeAt.x, 0f)).z;   // z of knee against the hip->foot line, in (z,y)
            bool forwardOfLine = lineSide < 0f;
            bool right = fore ? forwardOfLine : !forwardOfLine;
            if (right || !any) { best = kneeTurn; bestHip = hipTurn; bestKnee = kneeAt; any = true; if (right) break; }
        }

        var hipRot = Quaternion.Euler(bestHip, 0f, 0f);
        var kneeRot = Quaternion.Euler(best, 0f, 0f);
        float ease = stride > 0.5f ? 1f : 1f - Mathf.Exp(-14f * dt);
        hip.localRotation = Quaternion.Slerp(hip.localRotation, hipRot, ease);
        knee.localRotation = Quaternion.Slerp(knee.localRotation, kneeRot, ease);

        Feet[i] = frame.TransformPoint(new Vector3(hip.localPosition.x, H.x + toFoot.x, H.y + toFoot.y));
        bool down = lift <= 0.0005f;
        if (down && !Planted[i] && stride > 0.5f) Tracks.Print(Feet[i], yaw, traits.Size * 0.14f, Kind, seed);
        Planted[i] = down;
    }

    /// <summary>
    /// Half a stride, in the frame's units: as long as the cadence asks, but
    /// never more than a third of the leg, which is as far as a leg can reach
    /// and keep its foot on the ground.
    /// </summary>
    private float StrideHalf(Fauna.Gait walk, float reach, float worldScale)
    {
        return Mathf.Min(Mathf.PI / (2f * Mathf.Max(0.5f, walk.Cadence)) / worldScale, reach * 0.32f);
    }

    /// <summary>Radians of cycle per metre travelled, so the stance foot goes back at the body's speed.</summary>
    private float CycleRate(Fauna.Gait walk)
    {
        if (body.Thigh == null || body.Legs == null || body.Legs.Length < 3 || body.Legs[2] == null) return walk.Cadence;
        float worldScale = Mathf.Max(0.01f, body.Frame.lossyScale.x);
        float reach = body.Thigh[2].magnitude + body.Shin[2].magnitude;
        float half = StrideHalf(walk, reach, worldScale);
        return Mathf.PI / (2f * Mathf.Max(0.01f, half * worldScale));
    }

    /// <summary>A rotation about x, as Unity applies it, in the (y, z) plane.</summary>
    private static Vector2 Rotate(Vector2 yz, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(yz.x * c - yz.y * s, yz.x * s + yz.y * c);
    }

    /// <summary>
    /// The ground under a point near the animal: the plane through its own
    /// place with the lie of the land it stands on, which is what the
    /// collision ramps between the terraces amount to.
    /// </summary>
    private float GroundUnder(Vector3 at)
    {
        Vector3 root = transform.position;
        Vector3 n = slope.sqrMagnitude < 0.01f ? Vector3.up : slope;
        if (n.y < 0.2f) return root.y;
        return root.y - (n.x * (at.x - root.x) + n.z * (at.z - root.z)) / n.y;
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

        if (state == State.Hidden && gesture == Gesture.Rise)
        {
            // up through the surface, a roll along it, and down again
            float surface = Mathf.Max(WaterSurface.Level - transform.position.y, traits.Size * 0.6f);
            float arc = Mathf.Sin(Mathf.PI * t);
            rise = Mathf.Lerp(-traits.Size * 1.6f, surface - traits.Size * 0.35f + arc * traits.Size * 0.3f, Mathf.Clamp01(arc * 2.2f));
            pitch = (t < 0.5f ? -30f : 30f) * (1f - arc) ;
            roll = Mathf.Sin(t * Mathf.PI * 2f) * 35f;
        }
        else if (state == State.Hidden)
        {
            // down the hole: the whole animal goes under the ground
            rise = Mathf.MoveTowards(body.Frame.localPosition.y - baseline, -traits.Size * 1.6f, dt * traits.Size * 4f);
        }
        else if (perched)
        {
            // up on its perch, where the altitude has carried it, and still
            rise = altitude + Mathf.Sin(Time.time * 1.1f + phase) * traits.Size * 0.01f;
        }
        else if (Flying)
        {
            // up, nose a little high, with the slow lift and fall of the beat
            rise = altitude + Mathf.Sin(Time.time * 8.5f + phase + 1.2f) * traits.Size * 0.04f;
            pitch = state == State.Flee ? -6f : 8f;
            if (Fauna.All(Kind).Soars) { pitch = -2f; roll = (Mathf.Repeat(phase, 1f) > 0.5f ? -1f : 1f) * 18f; }
            else if (Fauna.All(Kind).Airborne) { pitch = -4f; roll = Mathf.Sin(Time.time * 5.3f + phase) * 30f; }
        }
        else if ((Fauna.All(Kind).Withdraws || Fauna.All(Kind).Freezes) && state == State.Alert)
        {
            // down onto the sand, or flat to the snow, and stays there
            rise = Mathf.Lerp(body.Frame.localPosition.y - baseline, -traits.Size * (Fauna.All(Kind).Freezes ? 0.24f : 0.12f), dt * 4f);
        }
        else if (state == State.Rest)
        {
            rise = Mathf.Lerp(body.Frame.localPosition.y - baseline, -traits.Size * 0.30f, dt * 3f);
        }
        else if (gesture == Gesture.SitUp)
        {
            // the whole front lifted, sat back on the haunches
            rise = Mathf.Lerp(body.Frame.localPosition.y - baseline, traits.Size * 0.14f, dt * 6f);
            pitch = -48f;
        }
        else if (gesture == Gesture.Stretch)
        {
            rise = Mathf.Lerp(body.Frame.localPosition.y - baseline, -traits.Size * 0.10f, dt * 5f);
            pitch = 16f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.2f));
        }
        else if (gesture == Gesture.Shake)
        {
            rise = Mathf.Lerp(body.Frame.localPosition.y - baseline, 0f, dt * 5f);
            roll = Mathf.Sin(Time.time * 42f) * 10f * (1f - t);
            pitch = Mathf.Sin(Time.time * 42f + 1f) * 3f * (1f - t);
        }
        else if (gesture == Gesture.Scratch)
        {
            rise = Mathf.Lerp(body.Frame.localPosition.y - baseline, -traits.Size * 0.05f, dt * 5f);
            roll = -9f;
        }
        else if (gesture == Gesture.Pounce)
        {
            // a crouch, then up and over, nose down into the grass at the end
            if (t < 0.4f) { rise = Mathf.Lerp(body.Frame.localPosition.y - baseline, -traits.Size * 0.14f, dt * 6f); pitch = 6f; }
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

            rise = Mathf.Lerp(body.Frame.localPosition.y - baseline, breath, dt * 5f);
        }

        var local = body.Frame.localPosition;
        local.y = rise + hop + baseline;
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
                Vector3 to = Threat - body.Head.position;
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

        // an owl looks right round, and holds each look
        if (Kind == FaunaKind.Owl && (state == State.Look || state == State.Stand))
        {
            float cycle = Mathf.Sin(Time.time * 0.45f + phase);
            turn = Mathf.Clamp(cycle * 1.6f, -1f, 1f) * 120f;
            dip = -4f;
        }
        if (Kind == FaunaKind.Owl && state == State.Alert) { turn = Mathf.Clamp(turn, -120f, 120f); }

        // a hare gone flat keeps its head down, and its eye on you
        if (Fauna.All(Kind).Freezes && state == State.Alert) { dip = 14f; turn = Mathf.Clamp(turn, -40f, 40f); }

        // a scorpion's pincers come up with its sting
        if (Kind == FaunaKind.Scorpion) { dip = state == State.Alert ? -30f : 2f; turn = 0f; }

        // a crab's claws come up when it is stood at bay
        if (Kind == FaunaKind.Crab)
        {
            dip = state == State.Alert ? -48f + Mathf.Sin(Time.time * 6f) * 6f : 4f;
            turn = 0f;
        }

        // a frog's croak: the throat lifts with each one
        if (Kind == FaunaKind.Frog)
        {
            float pulse = Mathf.Max(0f, Mathf.Sin(Time.time * 3.2f + phase));
            dip = state == State.Alert ? -8f : -pulse * 10f;
            turn = 0f;
        }

        // a boar rooting: snout in the ground, worked side to side, and the
        // ground turned over where it has been at it
        if (Fauna.All(Kind).Roots && state == State.Graze)
        {
            dip = 46f + Mathf.Sin(Time.time * 5f + phase) * 4f;
            turn = Mathf.Sin(Time.time * 2.6f + phase) * 16f;
            if (Time.time > nextRooting)
            {
                nextRooting = Time.time + Random.Range(2.5f, 5f);
                var snout = transform.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * traits.Size * 0.5f;
                snout.y = GroundUnder(snout) + footing;
                Tracks.Root(snout, traits.Size * Random.Range(0.5f, 0.8f));
            }
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

        float hourNow = TimeOfDay.Instance != null ? TimeOfDay.Instance.Normalized : 0.5f;
        bool chorus = Fauna.All(Kind).Chorus && (hourNow > 0.72f || hourNow < 0.22f);
        nextCall = Time.time + (chorus ? Random.Range(2.5f, 7f) : Random.Range(14f, 46f));

        if (state == State.Flee || state == State.Rest || state == State.Hidden) return;

        Speak(state == State.Alert);
    }

    private void Speak(bool alarmed)
    {
        if (voice == null || !AnimalVoice.Ready) return;

        voice.pitch = Random.Range(0.92f, 1.10f);
        voice.PlayOneShot(AnimalVoice.Call(Kind, alarmed), alarmed ? 0.75f : 0.5f);

        nextCall = Mathf.Max(nextCall, Time.time + (Fauna.All(Kind).Chorus ? 2f : 9f));
    }

    /// <summary>Turns towards something. Only the yaw: the tilt is the hill's business.</summary>
    private void Face(Vector3 direction, float dt, float speed)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) return;

        float want = Quaternion.LookRotation(direction).eulerAngles.y;

        // a crab goes side on: it faces a right angle from the way it travels,
        // and it does not turn round to do it
        if (Fauna.All(Kind).Sideways && (state == State.Wander || state == State.Flee || state == State.ToWater))
            want += Mathf.DeltaAngle(yaw, want + 90f) < Mathf.DeltaAngle(yaw, want - 90f) ? 90f : -90f;

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

        // one that came with a leader keeps with the leader, wherever the
        // leader has got to, rather than with whichever of its kind is nearest
        if (Leader != null && Leader != this && Vector3.Distance(Leader.transform.position, transform.position) > 7f)
            pull = Leader.transform.position + new Vector3(Random.Range(-3f, 3f), 0f, Random.Range(-3f, 3f));

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

    private float lastAlarm = -100f;

    private void Flee()
    {
        // a bird going up from the ground now and then leaves a feather where it stood
        if (Fauna.Flies(Kind) && !Fauna.All(Kind).Airborne && altitude < 0.1f && !perched && state != State.Flee && Random.value < 0.6f)
            Tracks.Feather(transform.position + Vector3.up * footing, Kind == FaunaKind.Heron || Kind == FaunaKind.Owl);

        // The fright carries: whatever put this one to flight puts the others
        // within earshot to flight too, a moment later. Once each few seconds,
        // or a herd would keep frightening itself.
        if (Time.time > lastAlarm + 5f)
        {
            lastAlarm = Time.time;
            Wildlife.Alarm(this, Threat, true, traits.Size < 0.5f ? 14f : 26f);
        }

        Vector3 away = transform.position - Threat;
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

    /// <summary>The nearest ground this kind would stand on, within a few tiles.</summary>
    private Vector3? LandingSpot()
    {
        int cx = Mathf.RoundToInt(transform.position.x / WorldGrid.TileSize);
        int cz = Mathf.RoundToInt(transform.position.z / WorldGrid.TileSize);

        for (int r = 0; r <= 16; r++)
        for (int dx = -r; dx <= r; dx++)
        for (int dz = -r; dz <= r; dz++)
        {
            if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
            int tx = cx + dx, tz = cz + dz;
            if (!Fauna.Ground(Kind, tx, tz, seed)) continue;
            float y = Mathf.Max(WorldHeight.SurfaceY(tx, tz, seed), WaterSurface.IsUnderwater(tx, tz, seed) ? WaterSurface.Level - Fauna.All(Kind).Wades : 0f);
            return new Vector3(tx * WorldGrid.TileSize, y, tz * WorldGrid.TileSize);
        }

        return null;
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
