using StarterAssets;
using UnityEngine;

/// <summary>
/// Buoyancy. The terrain collider follows the lakebed, so without this you walk
/// along the bottom of a lake with your head under the surface.
///
/// The hard part is not floating, it is not fighting the character controller.
/// In deep water its ground check fails, so it never stops accumulating gravity
/// and within a second is pulling down at terminal velocity. Any buoyancy large
/// enough to win that argument makes the two Move calls fight each other, which
/// is exactly what a shaking, unresponsive player looks like.
///
/// So instead of pushing harder, this stops the argument: while you are in the
/// water the controller is told it is grounded, which makes it clamp its own
/// vertical velocity to a gentle -2 rather than winding up, and a slow spring
/// lifts you the rest of the way to the surface.
/// </summary>
public class Swimming : MonoBehaviour
{
    [Tooltip("Where the surface sits relative to the player's feet when floating.")]
    [SerializeField] private float floatDepth = 1.0f;

    [Tooltip("How quickly the surface is reached. Kept low; this is a float, not a launch.")]
    [SerializeField] private float riseSpeed = 3.0f;

    [Tooltip("The fall the controller holds itself to while it believes it is grounded.")]
    [SerializeField] private float settledFall = 2.0f;

    [Tooltip("Swimming speed, as a fraction of walking.")]
    [SerializeField, Range(0.2f, 1f)] private float swimSpeed = 0.42f;

    [Tooltip("Swimming hard, also as a fraction of walking. Water does not let you sprint.")]
    [SerializeField, Range(0.2f, 1.2f)] private float hardSwim = 0.55f;

    [Tooltip("Depth at which you are considered in the water, and out of it again.")]
    [SerializeField] private float enterDepth = 0.35f;
    [SerializeField] private float exitDepth = 0.10f;

    private ChunkManager world;
    private Transform player;
    private CharacterController controller;
    private ThirdPersonController movement;

    /// <summary>Whether the player is in the water, for anything that has to look like it.</summary>
    public static bool InWater { get; private set; }

    private bool swimming;
    private float walkSpeed, sprintSpeed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Spawn()
    {
        if (FindFirstObjectByType<Swimming>() == null)
        {
            new GameObject("Swimming (runtime)").AddComponent<Swimming>();
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
        controller = player != null ? player.GetComponent<CharacterController>() : null;
        movement = player != null ? player.GetComponent<ThirdPersonController>() : null;

        if (controller == null)
        {
            enabled = false;
            return;
        }

        if (movement != null)
        {
            walkSpeed = movement.MoveSpeed;
            sprintSpeed = movement.SprintSpeed;
        }
    }

    private void LateUpdate()
    {
        if (player == null) return;

        float depth = WaterSurface.Level - player.position.y;

        // Separate thresholds for getting in and out, so bobbing at the surface
        // cannot flicker the state and spam everything that listens to it.
        if (!swimming && depth > enterDepth) Enter();
        else if (swimming && depth < exitDepth) Exit();

        if (!swimming) return;

        // Told it is grounded, the controller clamps its own fall to -2 instead
        // of winding gravity up to terminal velocity. Set here, after its
        // Update, so its next frame reads this before its own ground check.
        if (movement != null) movement.Grounded = true;

        // A slow approach to the float line. No reading of controller.velocity:
        // this Move feeds into that, and subtracting it was a feedback loop.
        //
        // The controller is still taking itself down at a steady rate all the
        // while, being told it is grounded, and the spring alone never won that
        // argument: it settled about a fifth of a metre under, which is to say
        // the whole person was below the water with their hat under it. So the
        // known fall is cancelled first and the spring works from there, and
        // the float line is where it says it is.
        float error = depth - floatDepth;
        float rise = settledFall + Mathf.Clamp(error * 3f, -riseSpeed, riseSpeed);

        controller.Move(Vector3.up * (rise * Time.deltaTime));
    }

    private void Enter()
    {
        swimming = true;
        InWater = true;

        if (movement != null)
        {
            // Both measured against walking rather than against running: a
            // sprint key in deep water should be worth something, but not much.
            movement.MoveSpeed = walkSpeed * swimSpeed;
            movement.SprintSpeed = walkSpeed * hardSwim;
        }

        Notices.Show("Swimming");
    }

    private void Exit()
    {
        swimming = false;
        InWater = false;

        if (movement != null)
        {
            movement.MoveSpeed = walkSpeed;
            movement.SprintSpeed = sprintSpeed;
        }
    }

    private void OnDisable()
    {
        if (swimming) Exit();
    }
}
