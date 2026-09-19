using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public PlayerController playerController;

    [Header("Camera Position")]
    public float distance = 6f;
    public float height = 0.5f;

    [Header("Mouse")]
    public float sensitivity = 0.15f;

    [Header("Vertical Limits")]
    public float minPitch = -20f;
    public float maxPitch = 65f;

    [Header("Smoothness")]
    public float smoothSpeed = 12f;

    [Tooltip("How quickly camera follows changes in ground height.")]
    public float verticalSmoothSpeed = 12f;

    [Header("Camera Collision")]
    [Tooltip("Layers the camera should NOT pass through.")]
    public LayerMask collisionLayers;

    [Tooltip("Thickness of the camera collision check.")]
    public float collisionRadius = 0.25f;

    [Tooltip("Small gap between camera and walls/ground.")]
    public float collisionOffset = 0.15f;

    [Tooltip("Closest camera can get to the player.")]
    public float minimumDistance = 0.4f;


    private float yaw;
    private float pitch = 15f;
    private float followY;


    void Start()
    {
        SetupCamera();
    }


    void OnEnable()
    {
        if (target != null)
            followY = target.position.y;
    }


    void SetupCamera()
    {
        if (target == null)
            return;

        yaw = target.eulerAngles.y;
        followY = target.position.y;

        if (playerController == null)
            playerController = target.GetComponent<PlayerController>();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }


    void LateUpdate()
    {
        if (target == null)
            return;

        if (playerController == null)
            playerController = target.GetComponent<PlayerController>();

        HandleMouse();


        // =================================================
        // VERTICAL FOLLOW
        // =================================================

        bool playerIsJumping =
            playerController != null &&
            playerController.IsJumping;

        if (!playerIsJumping)
        {
            followY = Mathf.Lerp(
                followY,
                target.position.y,
                verticalSmoothSpeed * Time.deltaTime
            );
        }


        Vector3 followPoint = new Vector3(
            target.position.x,
            followY,
            target.position.z
        );


        Vector3 lookPoint =
            followPoint +
            Vector3.up * height;


        // =================================================
        // CAMERA ROTATION
        // =================================================

        Quaternion rotation = Quaternion.Euler(
            pitch,
            yaw,
            0f
        );


        // Direction going backwards from player
        Vector3 cameraDirection =
            -(rotation * Vector3.forward);


        // Desired normal camera position
        Vector3 desiredPosition =
            lookPoint +
            cameraDirection * distance;


        // =================================================
        // CAMERA COLLISION
        // =================================================

        float finalDistance = distance;

        RaycastHit hit;

        if (Physics.SphereCast(
            lookPoint,
            collisionRadius,
            cameraDirection,
            out hit,
            distance,
            collisionLayers,
            QueryTriggerInteraction.Ignore))
        {
            finalDistance =
                hit.distance -
                collisionOffset;

            finalDistance = Mathf.Max(
                finalDistance,
                minimumDistance
            );
        }


        Vector3 targetPosition =
            lookPoint +
            cameraDirection * finalDistance;


        // =================================================
        // MOVE CAMERA
        // =================================================

        transform.position = Vector3.Lerp(
            transform.position,
            targetPosition,
            smoothSpeed * Time.deltaTime
        );


        transform.LookAt(lookPoint);
    }


    // =====================================================
    // MOUSE LOOK
    // =====================================================

    void HandleMouse()
    {
        if (Mouse.current == null)
            return;

        Vector2 mouseDelta =
            Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * sensitivity;
        pitch -= mouseDelta.y * sensitivity;

        pitch = Mathf.Clamp(
            pitch,
            minPitch,
            maxPitch
        );
    }
}