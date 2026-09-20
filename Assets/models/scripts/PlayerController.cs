using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float moveSmoothTime = 0.12f;
    public float rotationSmoothTime = 0.20f;

    [Header("Skateboard")]
    public float skateForwardSpeed = 8f;
    public float skateReverseSpeed = 4f;
    public float skateAcceleration = 12f;
    public float skateBrakeSpeed = 18f;
    public float skateTurnSpeed = 100f;
    public float skateTurnSmooth = 10f;
    public GameObject skateMesh;
    public bool startInSkateMode = true;

    [Header("Skate Height")]
    public Transform skateHeightTarget;
    public CapsuleCollider capsuleCollider;
    public float skateHeightOffset = 0.12f;

    [Header("Jump")]
    public float jumpHeight = 1.5f;
    public float jumpDuration = 1f;
    public float jumpBuildUpTime = 0.1f;

    [Header("Animation")]
    public Animator animator;
    public float runAnimationSpeed = 1f;
    public float skateAnimationSpeed = 1f;
    public float blendSpeed = 5f;
    public float skateBlendSpeed = 8f;
    public string jumpState = "jump";
    public string normalMovementState = "Blend Tree";
    public string skateMovementState = "Blend Tree";

    [Header("Attack Animation")]
    public float attackDuration = 0.8f;
    public string attackIdleState = "idle";

    [Header("Model Direction")]
    public Transform visualModel;
    public float visualYawOffset = 180f;

    [Header("Camera")]
    public Transform cameraTransform;

    private Rigidbody rb;
    private Collider playerCollider;

    private Vector2 moveInput;

    private bool skateMode;
    private bool grounded;
    private bool jumping;
    private bool jumpLaunched;
    private bool attacking;

    private float currentMoveSpeed;
    private float moveSpeedVelocity;
    private float turnVelocity;

    private float skateSpeed;
    private float skateTurn;
    private float skateTurnTarget;

    private float blend;
    private float skateX;
    private float skateY;

    private float buildUpTimer;
    private float jumpTimer;
    private float jumpGravity;

    private int skatingLayer = -1;
    private int attackLayer = -1;

    private Coroutine attackRoutine;

    private GameObject[] equippedSkateObjects;
    private GameObject[] previewSkateObjects;
    private bool customizationSkatePreview;

    private Vector3 normalSkateTargetPosition;
    private float normalCapsuleHeight;
    private Vector3 normalCapsuleCenter;
    private bool skateHeightCached;

    public bool IsJumping => jumping;
    public bool IsSkateMode => skateMode;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();

        if (rb == null)
        {
            Debug.LogError("PlayerController: Rigidbody is missing.", this);
            enabled = false;
            return;
        }

        rb.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationZ;

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (visualModel != null && Mathf.Abs(visualYawOffset) > 0.01f)
        {
            visualModel.localRotation *=
                Quaternion.Euler(0f, visualYawOffset, 0f);
        }

        if (animator != null)
        {
            animator.applyRootMotion = false;
            skatingLayer = animator.GetLayerIndex("Skating Layer");
            attackLayer = animator.GetLayerIndex("Attack Layer");
        }

        if (skateHeightTarget == null)
            skateHeightTarget = visualModel;

        if (capsuleCollider == null)
            capsuleCollider = GetComponent<CapsuleCollider>();

        if (skateHeightTarget != null)
            normalSkateTargetPosition = skateHeightTarget.localPosition;

        if (capsuleCollider != null)
        {
            normalCapsuleHeight = capsuleCollider.height;
            normalCapsuleCenter = capsuleCollider.center;
        }

        skateHeightCached = true;
    }

    private void Start()
    {
        if (animator != null && attackLayer >= 0)
            animator.SetLayerWeight(attackLayer, 0f);

        SetSkateMode(startInSkateMode);
    }

    private void Update()
    {
        CheckGround();

        if (Keyboard.current != null)
        {
            if (Keyboard.current.nKey.wasPressedThisFrame)
                SetSkateMode(!skateMode);

            if (Keyboard.current.spaceKey.wasPressedThisFrame &&
                grounded &&
                !jumping)
            {
                StartJump();
            }

            if (skateMode)
                ReadSkateInput();
            else
                ReadNormalInput();
        }
        else
        {
            moveInput = Vector2.zero;
            skateTurnTarget = 0f;
        }

        ReadMouse();
        UpdateAnimator();
    }

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        rb.angularVelocity = Vector3.zero;

        if (skateMode)
            MoveSkate();
        else
            MoveNormal();

        if (jumping)
            UpdateJump();

        rb.angularVelocity = Vector3.zero;
    }

    private void ReadMouse()
    {
        if (Mouse.current == null)
            return;

        // These use SendMessage so this controller does not require
        // CombatController or ThrowableInventory to compile.
        if (Mouse.current.leftButton.wasPressedThisFrame)
            SendMessage("TrySlide", SendMessageOptions.DontRequireReceiver);

        if (Mouse.current.rightButton.wasPressedThisFrame)
            SendMessage("TryPunch", SendMessageOptions.DontRequireReceiver);

        if (Mouse.current.middleButton.wasPressedThisFrame)
            SendMessage("TryUseThrowable", SendMessageOptions.DontRequireReceiver);
    }

    private void ReadNormalInput()
    {
        float x = 0f;
        float y = 0f;

        if (Keyboard.current.wKey.isPressed) y += 1f;
        if (Keyboard.current.sKey.isPressed) y -= 1f;
        if (Keyboard.current.dKey.isPressed) x += 1f;
        if (Keyboard.current.aKey.isPressed) x -= 1f;

        moveInput = Vector2.ClampMagnitude(
            new Vector2(x, y),
            1f
        );
    }

    private void MoveNormal()
    {
        if (cameraTransform == null)
            return;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;

        forward.Normalize();
        right.Normalize();

        Vector3 desiredDirection =
            forward * moveInput.y +
            right * moveInput.x;

        if (desiredDirection.sqrMagnitude > 1f)
            desiredDirection.Normalize();

        bool moving = desiredDirection.sqrMagnitude > 0.01f;
        float targetSpeed = moving ? moveSpeed : 0f;

        currentMoveSpeed = Mathf.SmoothDamp(
            currentMoveSpeed,
            targetSpeed,
            ref moveSpeedVelocity,
            moveSmoothTime
        );

        if (moving)
        {
            float targetAngle =
                Mathf.Atan2(
                    desiredDirection.x,
                    desiredDirection.z
                ) * Mathf.Rad2Deg;

            float smoothAngle = Mathf.SmoothDampAngle(
                rb.rotation.eulerAngles.y,
                targetAngle,
                ref turnVelocity,
                rotationSmoothTime
            );

            rb.MoveRotation(
                Quaternion.Euler(0f, smoothAngle, 0f)
            );
        }

        Vector3 velocity = GetVelocity();
        Vector3 facingDirection = rb.rotation * Vector3.forward;

        velocity.x = facingDirection.x * currentMoveSpeed;
        velocity.z = facingDirection.z * currentMoveSpeed;

        SetVelocity(velocity);
    }

    private void ReadSkateInput()
    {
        skateTurnTarget = 0f;

        if (Keyboard.current.aKey.isPressed)
            skateTurnTarget = -1f;

        if (Keyboard.current.dKey.isPressed)
            skateTurnTarget = 1f;

        skateTurn = Mathf.MoveTowards(
            skateTurn,
            skateTurnTarget,
            skateTurnSmooth * Time.deltaTime
        );
    }

    private void MoveSkate()
    {
        if (Keyboard.current == null)
            return;

        bool w = Keyboard.current.wKey.isPressed;
        bool s = Keyboard.current.sKey.isPressed;

        float targetSpeed = 0f;

        if (w && !s)
            targetSpeed = skateForwardSpeed;
        else if (s && !w)
            targetSpeed = -skateReverseSpeed;

        float acceleration =
            targetSpeed == 0f
                ? skateBrakeSpeed
                : skateAcceleration;

        skateSpeed = Mathf.MoveTowards(
            skateSpeed,
            targetSpeed,
            acceleration * Time.fixedDeltaTime
        );

        if (Mathf.Abs(skateSpeed) < 0.01f)
            skateSpeed = 0f;

        if (Mathf.Abs(skateSpeed) > 0.05f)
        {
            float direction = skateSpeed >= 0f ? 1f : -1f;

            float rotationAmount =
                skateTurn *
                skateTurnSpeed *
                direction *
                Time.fixedDeltaTime;

            rb.MoveRotation(
                rb.rotation *
                Quaternion.Euler(0f, rotationAmount, 0f)
            );
        }

        Vector3 velocity = GetVelocity();
        Vector3 forward = rb.rotation * Vector3.forward;

        velocity.x = forward.x * skateSpeed;
        velocity.z = forward.z * skateSpeed;

        SetVelocity(velocity);
    }

    public void SetSkateMode(bool enabled)
    {
        skateMode = enabled;

        moveInput = Vector2.zero;

        currentMoveSpeed = 0f;
        moveSpeedVelocity = 0f;
        turnVelocity = 0f;

        skateSpeed = 0f;
        skateTurn = 0f;
        skateTurnTarget = 0f;

        blend = 0f;
        skateX = 0f;
        skateY = 0f;

        StopHorizontalVelocity();
        RefreshSkateVisuals();

        if (animator != null && skatingLayer >= 0)
        {
            animator.SetLayerWeight(
                skatingLayer,
                enabled ? 1f : 0f
            );
        }

        if (animator != null)
        {
            animator.SetFloat("SkateX", 0f);
            animator.SetFloat("SkateY", 0f);
            animator.SetFloat("Blend", 0f);
        }
    }

    public void SetEquippedSkate(GameObject[] skateObjects)
    {
        SetSkateObjects(equippedSkateObjects, false);
        equippedSkateObjects = skateObjects;
        RefreshSkateVisuals();
    }

    public void SetCustomizationSkatePreview(
        GameObject[] skateObjects,
        bool enabled)
    {
        SetSkateObjects(previewSkateObjects, false);

        previewSkateObjects = skateObjects;

        customizationSkatePreview =
            enabled &&
            HasSkateObjects(skateObjects);

        RefreshSkateVisuals();
    }

    private void RefreshSkateVisuals()
    {
        bool showPreview =
            customizationSkatePreview &&
            HasSkateObjects(previewSkateObjects);

        bool showEquipped =
            skateMode &&
            HasSkateObjects(equippedSkateObjects);

        if (skateMesh != null)
        {
            skateMesh.SetActive(
                showPreview || showEquipped
            );
        }

        SetSkateObjects(equippedSkateObjects, false);
        SetSkateObjects(previewSkateObjects, false);

        if (showPreview)
            SetSkateObjects(previewSkateObjects, true);
        else if (showEquipped)
            SetSkateObjects(equippedSkateObjects, true);

        ApplySkateHeight(showPreview || showEquipped);
    }

    private void SetSkateObjects(
        GameObject[] objects,
        bool active)
    {
        if (objects == null)
            return;

        foreach (GameObject obj in objects)
        {
            if (obj != null)
                obj.SetActive(active);
        }
    }

    private bool HasSkateObjects(GameObject[] objects)
    {
        if (objects == null || objects.Length == 0)
            return false;

        foreach (GameObject obj in objects)
        {
            if (obj != null)
                return true;
        }

        return false;
    }

    private void ApplySkateHeight(bool enabled)
    {
        if (!skateHeightCached)
            return;

        float amount = enabled ? skateHeightOffset : 0f;

        if (skateHeightTarget != null)
        {
            skateHeightTarget.localPosition =
                normalSkateTargetPosition +
                Vector3.up * amount;
        }

        if (capsuleCollider != null)
        {
            capsuleCollider.height =
                normalCapsuleHeight + amount;

            capsuleCollider.center =
                normalCapsuleCenter +
                Vector3.up * (amount * 0.5f);
        }
    }

    private void UpdateAnimator()
    {
        if (animator == null)
            return;

        if (jumping || attacking)
        {
            animator.speed = 1f;
            return;
        }

        if (!skateMode)
        {
            float targetBlend =
                moveInput.sqrMagnitude > 0.01f
                    ? 1f
                    : 0f;

            blend = Mathf.MoveTowards(
                blend,
                targetBlend,
                blendSpeed * Time.deltaTime
            );

            animator.SetFloat("Blend", blend);

            animator.speed =
                targetBlend > 0f
                    ? runAnimationSpeed
                    : 1f;

            return;
        }

        skateX = Mathf.MoveTowards(
            skateX,
            -skateTurn,
            skateBlendSpeed * Time.deltaTime
        );

        float targetY = 0f;

        if (skateSpeed < 0f)
        {
            targetY =
                -Mathf.Clamp01(
                    Mathf.Abs(skateSpeed) /
                    Mathf.Max(skateReverseSpeed, 0.01f)
                );
        }

        skateY = Mathf.MoveTowards(
            skateY,
            targetY,
            skateBlendSpeed * Time.deltaTime
        );

        animator.SetFloat("SkateX", skateX);
        animator.SetFloat("SkateY", skateY);

        animator.speed =
            Mathf.Abs(skateSpeed) > 0.05f
                ? skateAnimationSpeed
                : 1f;
    }

    public void PlayCombatAnimation(string state)
    {
        PlayAttack(state);
    }

    private void PlayAttack(string state)
    {
        if (animator == null || attackLayer < 0)
            return;

        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = StartCoroutine(
            AttackRoutine(state)
        );
    }

    private IEnumerator AttackRoutine(string state)
    {
        attacking = true;
        animator.speed = 1f;

        animator.SetLayerWeight(
            attackLayer,
            1f
        );

        animator.CrossFadeInFixedTime(
            state,
            0.05f,
            attackLayer,
            0f
        );

        yield return new WaitForSeconds(
            attackDuration
        );

        animator.CrossFadeInFixedTime(
            attackIdleState,
            0.05f,
            attackLayer,
            0f
        );

        yield return new WaitForSeconds(0.05f);

        animator.SetLayerWeight(
            attackLayer,
            0f
        );

        attacking = false;
        attackRoutine = null;
    }

    private void StartJump()
    {
        if (rb == null)
            return;

        jumping = true;
        jumpLaunched = false;

        buildUpTimer = 0f;
        jumpTimer = 0f;

        jumpGravity =
            -(8f * jumpHeight) /
            Mathf.Max(
                jumpDuration * jumpDuration,
                0.01f
            );

        if (animator != null)
        {
            animator.speed = 1f;

            animator.SetBool(
                "jump",
                true
            );

            int layer =
                skateMode && skatingLayer >= 0
                    ? skatingLayer
                    : 0;

            animator.CrossFadeInFixedTime(
                jumpState,
                0.03f,
                layer,
                0f
            );
        }
    }

    private void UpdateJump()
    {
        if (!jumpLaunched)
        {
            buildUpTimer += Time.fixedDeltaTime;

            if (buildUpTimer < jumpBuildUpTime)
                return;

            LaunchJump();
            return;
        }

        jumpTimer += Time.fixedDeltaTime;

        rb.AddForce(
            Vector3.up *
            (jumpGravity - Physics.gravity.y),
            ForceMode.Acceleration
        );

        if (jumpTimer >= jumpDuration)
            EndJump();
    }

    private void LaunchJump()
    {
        jumpLaunched = true;
        grounded = false;
        jumpTimer = 0f;

        Vector3 velocity = GetVelocity();

        velocity.y =
            (4f * jumpHeight) /
            Mathf.Max(jumpDuration, 0.01f);

        SetVelocity(velocity);
    }

    private void EndJump()
    {
        jumping = false;
        jumpLaunched = false;

        if (animator == null)
            return;

        animator.SetBool("jump", false);

        int layer =
            skateMode && skatingLayer >= 0
                ? skatingLayer
                : 0;

        animator.CrossFadeInFixedTime(
            skateMode
                ? skateMovementState
                : normalMovementState,
            0.05f,
            layer,
            0f
        );
    }

    private void CheckGround()
    {
        if (playerCollider == null)
            return;

        float distance =
            playerCollider.bounds.extents.y + 0.15f;

        bool hit = Physics.Raycast(
            playerCollider.bounds.center,
            Vector3.down,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        if (!jumping)
            grounded = hit;
    }

    public void StopImmediately()
    {
        moveInput = Vector2.zero;

        currentMoveSpeed = 0f;
        moveSpeedVelocity = 0f;
        turnVelocity = 0f;

        skateSpeed = 0f;
        skateTurn = 0f;
        skateTurnTarget = 0f;

        blend = 0f;
        skateX = 0f;
        skateY = 0f;

        StopHorizontalVelocity();

        if (animator != null)
        {
            animator.SetFloat("Blend", 0f);
            animator.SetFloat("SkateX", 0f);
            animator.SetFloat("SkateY", 0f);
        }
    }

    private void StopHorizontalVelocity()
    {
        if (rb == null)
            return;

        Vector3 velocity = GetVelocity();

        velocity.x = 0f;
        velocity.z = 0f;

        SetVelocity(velocity);
    }

    // Unity 6 uses linearVelocity.
    // Older Unity versions use velocity.
    private Vector3 GetVelocity()
    {
#if UNITY_6000_0_OR_NEWER
        return rb.linearVelocity;
#else
        return rb.velocity;
#endif
    }

    private void SetVelocity(Vector3 value)
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = value;
#else
        rb.velocity = value;
#endif
    }
}
