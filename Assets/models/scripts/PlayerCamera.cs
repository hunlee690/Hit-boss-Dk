using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-50)]
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

    [Header("Race Look / Grenade Aim")]
    public float raceMinPitch = -35f;
    public float raceMaxPitch = 60f;

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
    private bool raceChase;
    private float raceLookYaw, raceLookPitch = 12f, lastRaceLook;
    private readonly RaycastHit[] chaseHits = new RaycastHit[16];
    private Camera raceCamera;
    private Rigidbody followedBody;
    private RagdollController followedRagdoll;
    private HitBoss.Multiplayer.Skate.SkateCombat aimingCombat;
    private Vector3 previousTargetPosition;
    private float baseFieldOfView, smoothedSpeed;

    public float RaceSpeedEffect { get; private set; }


    void Start()
    {
        SetupCamera();
    }


    void OnEnable()
    {
        if (target != null)
        {
            followY = target.position.y;
            if (raceChase)
            {
                yaw = target.eulerAngles.y;
                UpdateRaceChase(false);
            }
        }
    }

    public void FollowRaceSkater(PlayerController skater)
    {
        if (skater == null) return;
        raceChase = true;
        target = skater.transform;
        playerController = skater;
        followedBody = skater.GetComponent<Rigidbody>();
        followedRagdoll = skater.GetComponent<RagdollController>();
        aimingCombat = GetComponentInParent<HitBoss.Multiplayer.Skate.SkateCombat>();
        previousTargetPosition = target.position;
        if (raceCamera == null && TryGetComponent(out raceCamera))
            baseFieldOfView = raceCamera.fieldOfView;
        yaw = target.eulerAngles.y;
        followY = target.position.y;
        raceLookYaw = 0;
        raceLookPitch = 12f;
        UpdateRaceChase(false);
    }

    void UpdateRaceChase(bool readLook = true)
    {
        float blend = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
        yaw = Mathf.LerpAngle(yaw, target.eulerAngles.y, blend);
        if (readLook && Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            Vector2 mouse = Mouse.current.delta.ReadValue();
            if (mouse.sqrMagnitude > .01f)
            {
                raceLookYaw = Mathf.DeltaAngle(0, raceLookYaw + mouse.x * sensitivity);
                raceLookPitch = Mathf.Clamp(raceLookPitch - mouse.y * sensitivity, raceMinPitch, raceMaxPitch);
                lastRaceLook = Time.unscaledTime;
            }
            else if ((aimingCombat == null || !aimingCombat.IsChargingThrow) && Time.unscaledTime - lastRaceLook > 1.5f)
            {
                raceLookYaw = Mathf.MoveTowardsAngle(raceLookYaw, 0, 135f * Time.deltaTime);
                raceLookPitch = Mathf.MoveTowards(raceLookPitch, 12f, 40f * Time.deltaTime);
            }
        }

        bool followRagdoll = followedRagdoll != null && followedRagdoll.IsRagdolled && followedRagdoll.hips != null;
        UpdateRaceSpeed(followRagdoll);
        Vector3 lookPoint = followRagdoll ? followedRagdoll.hips.position : target.position;
        followY = Mathf.Lerp(followY, lookPoint.y, blend);
        followY = Mathf.Clamp(followY, lookPoint.y - .5f, lookPoint.y + .5f);
        lookPoint.y = followY + (followRagdoll ? .25f : height + .35f);

        Vector3 backwards = Quaternion.Euler(raceLookPitch, yaw + raceLookYaw, 0f) * Vector3.back;
        float availableDistance = distance + RaceSpeedEffect * .75f;
        int mask = collisionLayers.value == 0 ? Physics.DefaultRaycastLayers : collisionLayers.value;
        int hitCount = Physics.SphereCastNonAlloc(lookPoint, collisionRadius, backwards, chaseHits,
            availableDistance, mask, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < hitCount; i++)
        {
            var hit = chaseHits[i];
            if (hit.collider.GetComponentInParent<PlayerController>() != null) continue;
            availableDistance = Mathf.Min(availableDistance, Mathf.Max(minimumDistance, hit.distance - collisionOffset));
        }

        transform.SetPositionAndRotation(lookPoint + backwards * availableDistance,
            Quaternion.LookRotation(-backwards, Vector3.up));
    }

    void UpdateRaceSpeed(bool followRagdoll)
    {
        float deltaTime = Mathf.Max(Time.deltaTime, .001f);
        Vector3 displacement = target.position - previousTargetPosition;
        previousTargetPosition = target.position;
        Vector3 velocity = followedBody != null && !followedBody.isKinematic
            ? followedBody.linearVelocity
            : displacement.sqrMagnitude > 64f ? Vector3.zero : displacement / deltaTime;
        velocity.y = 0;
        float speed = followRagdoll ? 0f : Mathf.Min(velocity.magnitude, 48f);
        float blend = 1f - Mathf.Exp(-5f * Time.deltaTime);
        smoothedSpeed = Mathf.Lerp(smoothedSpeed, speed, blend);
        RaceSpeedEffect = Mathf.InverseLerp(11f, 40f, smoothedSpeed);
        if (raceCamera != null) raceCamera.fieldOfView = baseFieldOfView + RaceSpeedEffect * 19f;
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

        if (raceChase)
        {
            UpdateRaceChase();
            return;
        }

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
