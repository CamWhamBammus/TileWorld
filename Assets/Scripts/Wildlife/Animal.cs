using UnityEngine;

/// <summary>
/// One animal, going about its business. There is no animator and no navmesh:
/// it walks between points it picks itself, stands and grazes, and watches you
/// when you get near. The only thing it really knows how to do is leave.
///
/// The interesting part is the watching. An animal that simply ran would be
/// scenery with a trigger on it; one that lifts its head, holds still while
/// you decide what to do, and only goes when you push it, is the reason to
/// stop walking for a moment.
/// </summary>
public class Animal : MonoBehaviour
{
    private enum State { Graze, Wander, Alert, Flee }

    public FaunaKind Kind { get; private set; }

    private Fauna.Traits traits;
    private AnimalBuilder.Body body;
    private Transform player;
    private int seed;

    private State state;
    private Vector3 target;
    private float until;
    private float gait;
    private float phase;

    public void Settle(FaunaKind kind, int worldSeed, Transform watching, Vector3 at)
    {
        Kind = kind;
        seed = worldSeed;
        player = watching;
        traits = Fauna.Of(kind);
        body = AnimalBuilder.Build(kind, transform);

        phase = Random.Range(0f, 10f);

        transform.position = Ground(at);
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Graze();
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

        Think(distance);
        Move(dt);
        Animate(dt, distance);
    }

    private void Think(float distance)
    {
        // Being seen matters more than whatever it was doing.
        if (state != State.Flee)
        {
            if (distance < traits.Bolts)
            {
                Flee();
                return;
            }

            if (distance < traits.Notices)
            {
                state = State.Alert;
                return;
            }

            if (state == State.Alert) Graze();      // you backed off
        }

        if (Time.time < until) return;

        switch (state)
        {
            case State.Graze:
                Wander();
                break;

            case State.Wander:
                Graze();
                break;

            case State.Flee:
                // far enough away, or it has simply run itself out
                if (distance > traits.Settles) Graze();
                else Flee();
                break;
        }
    }

    private void Move(float dt)
    {
        if (state == State.Graze || state == State.Alert)
        {
            if (state == State.Alert) Face(player.position - transform.position, dt, 4f);
            return;
        }

        Vector3 to = target - transform.position;
        to.y = 0f;

        if (to.sqrMagnitude < 0.4f)
        {
            if (state == State.Wander) Graze();
            return;
        }

        float speed = state == State.Flee ? traits.RunSpeed : traits.WalkSpeed;

        Face(to, dt, state == State.Flee ? 7f : 2.5f);

        Vector3 step = transform.position + transform.forward * (speed * dt);

        transform.position = Ground(step);

        gait += speed * dt * (state == State.Flee ? 3.4f : 4.2f);
    }

    private void Animate(float dt, float distance)
    {
        bool moving = state == State.Wander || state == State.Flee;

        // Legs swing in diagonal pairs, which is enough to read as a walk.
        if (body.Legs != null)
        {
            for (int i = 0; i < body.Legs.Length; i++)
            {
                float swing = moving ? Mathf.Sin(gait + (i % 3 == 0 ? 0f : Mathf.PI)) * (state == State.Flee ? 42f : 24f) : 0f;
                body.Legs[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
            }
        }

        // Head down in the grass, up the moment it hears you.
        if (body.Head != null)
        {
            float dip = state == State.Graze ? 52f : 0f;
            var want = Quaternion.Euler(dip, 0f, 0f);
            body.Head.localRotation = Quaternion.Slerp(body.Head.localRotation, want, dt * 4f);
        }

        if (body.Frame != null)
        {
            // A rabbit hops; everything else just carries itself.
            float bounce = Kind == FaunaKind.Rabbit && moving
                ? Mathf.Abs(Mathf.Sin(gait * 0.5f)) * traits.Size * 0.55f
                : (moving ? Mathf.Abs(Mathf.Sin(gait)) * traits.Size * 0.04f : 0f);

            var local = body.Frame.localPosition;
            local.y = bounce;
            body.Frame.localPosition = local;
        }

        if (body.Tail != null)
        {
            // The tail flicks when it is uneasy, which is the tell before it goes.
            float unease = state == State.Alert ? 5f : 1.2f;
            body.Tail.localRotation = Quaternion.Euler(Mathf.Sin(Time.time * unease + phase) * 12f, 0f, 0f);
        }
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
        until = Time.time + Random.Range(3f, 9f);
    }

    private void Wander()
    {
        for (int attempt = 0; attempt < 6; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(5f, 16f);
            Vector3 at = transform.position + new Vector3(offset.x, 0f, offset.y);

            if (!Walkable(at)) continue;

            target = at;
            state = State.Wander;
            until = Time.time + 14f;        // give up rather than walk forever
            return;
        }

        Graze();
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
