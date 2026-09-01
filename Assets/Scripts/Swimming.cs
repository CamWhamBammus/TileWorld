using UnityEngine;

/// <summary>
/// Buoyancy. The terrain collider runs along the lakebed, so walking into deep
/// water meant strolling along the bottom of it with your head under the
/// surface, which is the one thing water should not let you do.
///
/// This works alongside the StarterAssets controller rather than replacing it:
/// that keeps applying its own gravity, and this pushes back against it in
/// proportion to how deep you are, so you settle at the surface.
/// </summary>
public class Swimming : MonoBehaviour
{
    [Tooltip("How hard the water pushes back, against the controller's gravity.")]
    [SerializeField] private float buoyancy = 14f;

    [Tooltip("Where the surface sits relative to the player's feet when floating.")]
    [SerializeField] private float floatDepth = 1.1f;

    [Tooltip("Swimming is slower than walking.")]
    [SerializeField, Range(0.1f, 1f)] private float dragWhileSwimming = 0.55f;

    private ChunkManager world;
    private Transform player;
    private CharacterController controller;
    private bool swimming;

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

        if (controller == null) enabled = false;
    }

    private void LateUpdate()
    {
        if (player == null) return;

        float feet = player.position.y;
        float surface = WaterSurface.Level;
        float depth = surface - feet;

        bool inWater = depth > 0.05f;

        if (inWater != swimming)
        {
            swimming = inWater;
            Notices.Show(inWater ? "Swimming" : "Out of the water");
        }

        if (!inWater) return;

        // Rise while below the float line, sink gently while above it, so the
        // player settles at the surface instead of bobbing.
        float error = depth - floatDepth;
        float rise = Mathf.Clamp(error, -1f, 1f) * buoyancy;

        // Sideways drag, applied by pulling back a fraction of this frame's
        // horizontal movement.
        Vector3 horizontal = controller.velocity;
        horizontal.y = 0f;

        Vector3 push = Vector3.up * rise - horizontal * (1f - dragWhileSwimming);

        controller.Move(push * Time.deltaTime);
    }
}
