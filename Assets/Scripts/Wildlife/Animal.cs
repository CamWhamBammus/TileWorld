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
    private enum State { Stand, Graze, Look, Wander, ToWater, Drink, Rest, Alert, Flee }

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

    public void Settle(FaunaKind kind, int worldSeed, Transform watching, Vector3 at)
    {
        Kind = kind;
        seed = worldSeed;
        player = watching;
        traits = Fauna.Of(kind);
        body = AnimalBuilder.Build(kind, transform);

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
        if (state != State.Flee)
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
                return;
            }

            if (state == State.Alert) Graze();      // you backed off
        }

        if (Time.time < until) return;

        switch (state)
        {
            case State.Flee:
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
            until = Time.time + Random.Range(16f, 40f);
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
            if (state == State.Alert) Face(player.position - transform.position, dt, 4f);

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

        Footfall(state == State.Flee);
    }

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

        var tilt = Quaternion.FromToRotation(Vector3.up, slope);

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

        Limbs(dt, moving, walk);
        Carriage(dt, moving, walk);
        Poise(dt, moving);

        if (body.Tail != null)
        {
            // The tail flicks when it is uneasy, which is the tell before it goes.
            float unease = state == State.Alert ? 5f : (state == State.Rest ? 0.5f : 1.2f);
            body.Tail.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * unease + phase) * 12f, 0f, 0f);
        }

        Nibble();
    }

    /// <summary>
    /// Hips and knees. The hip swings the leg through, and the knee folds on
    /// the way forward and straightens to take the weight — which is the whole
    /// difference between an animal walking and a table sliding along.
    /// </summary>
    private void Limbs(float dt, bool moving, Fauna.Gait walk)
    {
        if (body.Legs == null) return;

        for (int i = 0; i < body.Legs.Length; i++)
        {
            bool fore = i < 2;

            // Bounding throws both front legs forward together and both back
            // legs after them; a trot moves diagonal pairs.
            float legPhase = walk.Bounds
                ? (fore ? 0f : Mathf.PI * 0.62f)
                : (i % 3 == 0 ? 0f : Mathf.PI);

            float hip, knee;

            if (state == State.Rest)
            {
                // folded under it
                hip = fore ? 62f : -54f;
                knee = fore ? -96f : 104f;
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

        if (state == State.Rest)
        {
            rise = Mathf.Lerp(body.Frame.localPosition.y, -traits.Size * 0.30f, dt * 3f);
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
                dip = 16f;
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

            if (!Walkable(at)) continue;

            target = at;
            state = State.Flee;
            until = Time.time + 5f;
            return;
        }

        target = transform.position + away * 20f;
        state = State.Flee;
        until = Time.time + 5f;
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
