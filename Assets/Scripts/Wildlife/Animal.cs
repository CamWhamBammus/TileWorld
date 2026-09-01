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
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

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
        Animate(dt);
        Talk();
    }

    private void Think(float distance)
    {
        // Being seen matters more than whatever it was doing.
        if (state != State.Flee)
        {
            if (distance < traits.Bolts)
            {
                Speak(true);
                Flee();
                return;
            }

            if (distance < traits.Notices)
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

        float speed = state == State.Flee ? traits.RunSpeed : traits.WalkSpeed;

        Face(to, dt, state == State.Flee ? 7f : 2.5f);

        transform.position = Ground(transform.position + transform.forward * (speed * dt));

        gait += speed * dt * Fauna.Moving(Kind, state == State.Flee).Cadence;

        Footfall(state == State.Flee);
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
    /// What the body does over the stride: rising and falling, nose tipping up
    /// and down, weight rocking from one side to the other.
    /// </summary>
    private void Carriage(float dt, bool moving, Fauna.Gait walk)
    {
        if (body.Frame == null) return;

        float rise;
        float pitch = 0f;
        float roll = 0f;

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
            rise = Mathf.Lerp(body.Frame.localPosition.y, 0f, dt * 5f);
        }

        var local = body.Frame.localPosition;
        local.y = rise;
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
                dip = -6f;
                turn = Mathf.Sin(Time.time * 0.8f + phase) * 6f;
                break;

            default:
                // the head nods with the stride rather than riding along level
                dip = moving ? 8f + Mathf.Sin(gait + 1.1f) * 5f : 22f;
                break;
        }

        var want = Quaternion.Euler(dip, turn, 0f);
        body.Head.localRotation = Quaternion.Slerp(body.Head.localRotation, want, dt * 5f);
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

    private void Face(Vector3 direction, float dt, float speed)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f) return;

        var want = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, dt * speed);
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
