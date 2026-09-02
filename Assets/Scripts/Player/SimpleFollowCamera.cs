using UnityEngine;

public class SimpleFollowCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Camera Position")]
    public Vector3 offset = new Vector3(0f, 3f, -6f);
    public float followSmoothTime = 0.08f;

    [Header("Mouse Look")]
    public float mouseSensitivity = 2f;
    [Tooltip("How far up and down you can look. Up was held to twenty five degrees, which is not far enough to see the top of a tower you are standing under.")]
    public float minPitch = -45f;
    public float maxPitch = 70f;

    [Tooltip("How far the camera keeps off the ground, since looking up swings it low behind you.")]
    public float clearance = 0.45f;

    [Tooltip("How far above the player it aims when looking up as far as it goes. Kept small: aim far over their head and the player slides out of shot and the whole thing appears to swing around a point in mid air.")]
    public float lift = 1.7f;

    [Tooltip("How much the camera closes in as it looks up, so it can get low without meeting the ground.")]
    public float drawIn = 0.88f;

    private Vector3 velocity;
    private float yaw;
    private float pitch = 15f;
    private ChunkManager world;

    private void Start()
    {
        world = FindFirstObjectByType<ChunkManager>();

        if (target != null)
        {
            yaw = target.eulerAngles.y;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // A screen with the cursor gets it exclusively; reading the mouse here
        // would spin the camera while the player is clicking the map.
        if (PauseMenu.Paused || ScreenState.WantsCursor) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 pivot = target.position + Vector3.up * 1.4f;

        // Coming in closer as it looks up lets the camera get below the player
        // without burying itself in the hillside, which is what the ground
        // clamp was otherwise fighting.
        float raised = Mathf.InverseLerp(0f, minPitch, pitch);

        Vector3 desiredPosition = target.position + rotation * (offset * Mathf.Lerp(1f, drawIn, raised));

        // Looking up swings the camera low behind the player, which put it
        // inside the hill. A ray from the player cannot help here — on a slope
        // it starts inside the ground already and hits nothing on the way out —
        // so the ground is asked directly how high it is under the camera.
        int cx = Mathf.RoundToInt(desiredPosition.x / WorldGrid.TileSize);
        int cz = Mathf.RoundToInt(desiredPosition.z / WorldGrid.TileSize);

        float floor = WorldHeight.SurfaceY(cx, cz, world != null ? world.WorldSeed : 0) + clearance;

        if (desiredPosition.y < floor) desiredPosition.y = floor;

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            followSmoothTime
        );

        // Orbiting alone cannot look up: to see above you the camera has to get
        // below you, and the ground is in the way. So past level it also raises
        // where it is aiming, and at the top of its travel it is looking well
        // over the player's head rather than at it.
        transform.LookAt(pivot + Vector3.up * (raised * lift));
    }
}
