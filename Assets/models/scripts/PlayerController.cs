using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Normal Movement")] public float moveSpeed = 5f;
    [Tooltip("Lower = faster acceleration/deceleration.")] public float moveSmoothTime = 0.12f;
    [Tooltip("Lower = faster turning. Try 0.15 - 0.30")] public float rotationSmoothTime = 0.2f;

    [Header("Skating")] public float skateForwardSpeed = 8f;
    public float skateReverseSpeed = 4f;
    public float skateAcceleration = 4f;
    public float skateBrakeSpeed = 6f;
    public float skateTurnSpeed = 100f;
    public float skateTurnSmooth = 6f;
    [Tooltip("Optional parent containing all skate models.")] public GameObject skateMesh;
    public bool startInSkateMode;

    [Header("Skate Height")] public Transform skateHeightTarget;
    public CapsuleCollider capsuleCollider;
    public float skateHeightOffset = 0.12f;

    [Header("Jump")] public float jumpHeight = 1.5f;
    public float jumpDuration = 1f;
    public float jumpBuildUpTime = 0.1f;

    [Header("Animation")] public Animator animator;
    public float runAnimationSpeed = 1f;
    public float skateAnimationSpeed = 1f;
    public float blendSpeed = 5f;
    public float skateBlendSpeed = 8f;
    public string jumpState = "jump";
    public string normalMovementState = "Blend Tree";
    public string skateMovementState = "Blend Tree";

    [Header("Attack")] public float attackDuration = 0.8f;
    public string attackIdleState = "idle";

    [Header("Throw")] public float throwDuration = 0.8f;
    public string throwIdleState = "idle";

    [Header("Model Direction")] public Transform visualModel;
    public float visualYawOffset = 180f;

    [Header("References")] public Transform cameraTransform;

    Rigidbody rb;
    Collider playerCollider;
    Vector2 moveInput;
    bool skateMode, grounded, jumping, jumpLaunched, attacking;
    float currentMoveSpeed, moveSpeedVelocity, turnVelocity;
    float skateSpeed, skateTurn, skateTurnTarget;
    float blend, skateX, skateY;
    float buildUpTimer, jumpTimer, jumpGravity;
    int skatingLayer, attackLayer, throwLayer;
    Coroutine attackRoutine, throwRoutine;
    CombatController combatController;
    ThrowableInventory throwableInventory;
    GameObject[] equippedSkateObjects, previewSkateObjects;
    bool customizationSkatePreview;
    Vector3 normalSkateTargetPosition;
    float normalCapsuleHeight;
    Vector3 normalCapsuleCenter;
    bool skateHeightCached;

    public bool IsJumping => jumping;
    public bool IsSkateMode => skateMode;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();
        combatController = GetComponent<CombatController>();
        throwableInventory = GetComponent<ThrowableInventory>();

        if (rb != null)
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        if (visualModel != null)
            visualModel.localRotation *= Quaternion.Euler(0f, visualYawOffset, 0f);

        if (animator != null)
        {
            animator.applyRootMotion = false;
            skatingLayer = animator.GetLayerIndex("Skating Layer");
            attackLayer = animator.GetLayerIndex("Attack Layer");
            throwLayer = animator.GetLayerIndex("throw Layer");
        }

        if (skateHeightTarget == null) skateHeightTarget = visualModel;
        if (capsuleCollider == null) capsuleCollider = GetComponent<CapsuleCollider>();

        if (skateHeightTarget != null)
            normalSkateTargetPosition = skateHeightTarget.localPosition;

        if (capsuleCollider != null)
        {
            normalCapsuleHeight = capsuleCollider.height;
            normalCapsuleCenter = capsuleCollider.center;
        }

        skateHeightCached = true;
    }

    void Start()
    {
        if (animator != null)
        {
            if (attackLayer >= 0) animator.SetLayerWeight(attackLayer, 0f);
            if (throwLayer >= 0) animator.SetLayerWeight(throwLayer, 0f);
        }

        SetSkateMode(startInSkateMode);
    }

    void Update()
    {
        CheckGround();

        if (Keyboard.current != null)
        {
            if (Keyboard.current.nKey.wasPressedThisFrame)
                SetSkateMode(!skateMode);

            if (Keyboard.current.spaceKey.wasPressedThisFrame && grounded && !jumping)
                StartJump();

            if (skateMode) ReadSkateInput();
            else ReadNormalInput();
        }

        ReadMouse();
        UpdateAnimator();
    }

    void FixedUpdate()
    {
        if (rb != null) rb.angularVelocity = Vector3.zero;

        if (skateMode) MoveSkate();
        else MoveNormal();

        if (jumping) UpdateJump();

        if (rb != null) rb.angularVelocity = Vector3.zero;
    }

    void ReadMouse()
    {
        if (Mouse.current == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame && combatController != null && combatController.isActiveAndEnabled)
            combatController.TrySlide();

        if (Mouse.current.rightButton.wasPressedThisFrame && combatController != null && combatController.isActiveAndEnabled)
            combatController.TryPunch();

        if (Mouse.current.middleButton.wasPressedThisFrame && throwableInventory != null && throwableInventory.isActiveAndEnabled)
            throwableInventory.TryUseThrowable();
    }

    void ReadNormalInput()
    {
        float x = 0f, y = 0f;

        if (Keyboard.current.wKey.isPressed) y += 1f;
        if (Keyboard.current.sKey.isPressed) y -= 1f;
        if (Keyboard.current.dKey.isPressed) x += 1f;
        if (Keyboard.current.aKey.isPressed) x -= 1f;

        moveInput = Vector2.ClampMagnitude(new Vector2(x, y), 1f);
    }

    void MoveNormal()
    {
        if (cameraTransform == null || rb == null) return;

        Vector3 forward = cameraTransform.forward;
        Vector3 right = cameraTransform.right;

        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        Vector3 desiredDirection = forward * moveInput.y + right * moveInput.x;

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
            float targetAngle = Mathf.Atan2(desiredDirection.x, desiredDirection.z) * Mathf.Rad2Deg;

            float smoothAngle = Mathf.SmoothDampAngle(
                rb.rotation.eulerAngles.y,
                targetAngle,
                ref turnVelocity,
                rotationSmoothTime
            );

            rb.MoveRotation(Quaternion.Euler(0f, smoothAngle, 0f));
        }

        Vector3 facingDirection = rb.rotation * Vector3.forward;
        Vector3 velocity = rb.linearVelocity;

        velocity.x = facingDirection.x * currentMoveSpeed;
        velocity.z = facingDirection.z * currentMoveSpeed;

        rb.linearVelocity = velocity;
    }

    void ReadSkateInput()
    {
        skateTurnTarget = 0f;

        if (Keyboard.current.aKey.isPressed) skateTurnTarget = -1f;
        if (Keyboard.current.dKey.isPressed) skateTurnTarget = 1f;

        skateTurn = Mathf.MoveTowards(
            skateTurn,
            skateTurnTarget,
            skateTurnSmooth * Time.deltaTime
        );
    }

    void MoveSkate()
    {
        if (rb == null) return;

        bool braking = Keyboard.current != null && Keyboard.current.sKey.isPressed;

        float targetSpeed = braking ? -skateReverseSpeed : skateForwardSpeed;
        float acceleration = braking ? skateBrakeSpeed : skateAcceleration;

        skateSpeed = Mathf.MoveTowards(
            skateSpeed,
            targetSpeed,
            acceleration * Time.fixedDeltaTime
        );

        if (Mathf.Abs(skateSpeed) > 0.05f)
        {
            float reverse = skateSpeed >= 0f ? 1f : -1f;

            Quaternion turn = Quaternion.Euler(
                0f,
                skateTurn * skateTurnSpeed * reverse * Time.fixedDeltaTime,
                0f
            );

            rb.MoveRotation(rb.rotation * turn);
        }

        Vector3 forward = rb.rotation * Vector3.forward;
        Vector3 velocity = rb.linearVelocity;

        velocity.x = forward.x * skateSpeed;
        velocity.z = forward.z * skateSpeed;

        rb.linearVelocity = velocity;
    }

    public void SetSkateMode(bool enabled)
    {
        skateMode = enabled;
        RefreshSkateVisuals();

        if (animator != null && skatingLayer >= 0)
            animator.SetLayerWeight(skatingLayer, enabled ? 1f : 0f);

        moveInput = Vector2.zero;
        currentMoveSpeed = 0f;
        moveSpeedVelocity = 0f;
        turnVelocity = 0f;
        blend = 0f;
        skateX = 0f;
        skateY = 0f;
        skateTurn = 0f;
        skateTurnTarget = 0f;

        if (enabled)
        {
            skateSpeed = skateForwardSpeed;

            if (animator != null)
            {
                animator.SetFloat("SkateX", 0f);
                animator.SetFloat("SkateY", 0f);
            }
        }
        else
        {
            skateSpeed = 0f;

            if (animator != null)
                animator.SetFloat("Blend", 0f);
        }
    }

    public void SetEquippedSkate(GameObject[] skateObjects)
    {
        SetSkateObjects(equippedSkateObjects, false);
        equippedSkateObjects = skateObjects;
        RefreshSkateVisuals();
    }

    public void SetCustomizationSkatePreview(GameObject[] skateObjects, bool enabled)
    {
        SetSkateObjects(previewSkateObjects, false);
        previewSkateObjects = skateObjects;
        customizationSkatePreview = enabled && HasSkateObjects(skateObjects);
        RefreshSkateVisuals();
    }

    void RefreshSkateVisuals()
    {
        bool showPreview = customizationSkatePreview && HasSkateObjects(previewSkateObjects);
        bool showEquipped = skateMode && HasSkateObjects(equippedSkateObjects);

        if (skateMesh != null)
            skateMesh.SetActive(showPreview || showEquipped);

        SetSkateObjects(equippedSkateObjects, false);
        SetSkateObjects(previewSkateObjects, false);

        if (showPreview) SetSkateObjects(previewSkateObjects, true);
        else if (showEquipped) SetSkateObjects(equippedSkateObjects, true);

        ApplySkateHeight(showPreview || showEquipped);
    }

    void SetSkateObjects(GameObject[] objects, bool active)
    {
        if (objects == null) return;

        foreach (GameObject obj in objects)
            if (obj != null)
                obj.SetActive(active);
    }

    bool HasSkateObjects(GameObject[] objects)
    {
        if (objects == null || objects.Length == 0) return false;

        foreach (GameObject obj in objects)
            if (obj != null)
                return true;

        return false;
    }

    void ApplySkateHeight(bool enabled)
    {
        if (!skateHeightCached) return;

        float amount = enabled ? skateHeightOffset : 0f;

        if (skateHeightTarget != null)
            skateHeightTarget.localPosition = normalSkateTargetPosition + Vector3.up * amount;

        if (capsuleCollider != null)
        {
            capsuleCollider.height = normalCapsuleHeight + amount;
            capsuleCollider.center = normalCapsuleCenter + Vector3.up * (amount * 0.5f);
        }
    }

    void UpdateAnimator()
    {
        if (animator == null) return;

        if (jumping || attacking)
        {
            animator.speed = 1f;
            return;
        }

        if (!skateMode)
        {
            float targetBlend = moveInput.sqrMagnitude > 0.01f ? 1f : 0f;

            blend = Mathf.MoveTowards(
                blend,
                targetBlend,
                blendSpeed * Time.deltaTime
            );

            animator.SetFloat("Blend", blend);
            animator.speed = targetBlend > 0f ? runAnimationSpeed : 1f;
            return;
        }

        skateX = Mathf.MoveTowards(
            skateX,
            -skateTurn,
            skateBlendSpeed * Time.deltaTime
        );

        float targetY = 0f;

        if (skateSpeed < 0f)
            targetY = -Mathf.Clamp01(
                Mathf.Abs(skateSpeed) / Mathf.Max(skateReverseSpeed, 0.01f)
            );

        skateY = Mathf.MoveTowards(
            skateY,
            targetY,
            skateBlendSpeed * Time.deltaTime
        );

        animator.SetFloat("SkateX", skateX);
        animator.SetFloat("SkateY", skateY);
        animator.speed = skateAnimationSpeed;
    }

    public void PlayCombatAnimation(string state)
    {
        if (state == "throw") PlayThrow(state);
        else PlayAttack(state);
    }

    void PlayAttack(string state)
    {
        if (animator == null || attackLayer < 0) return;

        if (attackRoutine != null)
            StopCoroutine(attackRoutine);

        attackRoutine = StartCoroutine(AttackRoutine(state));
    }

    IEnumerator AttackRoutine(string state)
    {
        attacking = true;
        animator.speed = 1f;
        animator.SetLayerWeight(attackLayer, 1f);
        animator.CrossFadeInFixedTime(state, 0.05f, attackLayer, 0f);

        yield return new WaitForSeconds(attackDuration);

        animator.CrossFadeInFixedTime(attackIdleState, 0.05f, attackLayer, 0f);
        yield return new WaitForSeconds(0.05f);

        animator.SetLayerWeight(attackLayer, 0f);
        attacking = false;
        attackRoutine = null;
    }

    void PlayThrow(string state)
    {
        if (animator == null || throwLayer < 0) return;

        if (throwRoutine != null)
            StopCoroutine(throwRoutine);

        throwRoutine = StartCoroutine(ThrowRoutine(state));
    }

    IEnumerator ThrowRoutine(string state)
    {
        animator.SetLayerWeight(throwLayer, 1f);
        animator.CrossFadeInFixedTime(state, 0.05f, throwLayer, 0f);

        yield return new WaitForSeconds(throwDuration);

        animator.CrossFadeInFixedTime(throwIdleState, 0.05f, throwLayer, 0f);
        yield return new WaitForSeconds(0.05f);

        animator.SetLayerWeight(throwLayer, 0f);
        throwRoutine = null;
    }

    void StartJump()
    {
        jumping = true;
        jumpLaunched = false;
        buildUpTimer = 0f;
        jumpTimer = 0f;

        jumpGravity = -(8f * jumpHeight) / (jumpDuration * jumpDuration);

        if (animator != null)
        {
            animator.speed = 1f;
            animator.SetBool("jump", true);

            int layer = skateMode && skatingLayer >= 0 ? skatingLayer : 0;

            animator.CrossFadeInFixedTime(
                jumpState,
                0.03f,
                layer,
                0f
            );
        }
    }

    void UpdateJump()
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

        if (rb != null)
            rb.AddForce(
                Vector3.up * (jumpGravity - Physics.gravity.y),
                ForceMode.Acceleration
            );

        if (jumpTimer >= jumpDuration)
            EndJump();
    }

    void LaunchJump()
    {
        jumpLaunched = true;
        grounded = false;
        jumpTimer = 0f;

        if (rb == null) return;

        Vector3 velocity = rb.linearVelocity;
        velocity.y = (4f * jumpHeight) / jumpDuration;
        rb.linearVelocity = velocity;
    }

    void EndJump()
    {
        jumping = false;
        jumpLaunched = false;

        if (animator == null) return;

        animator.SetBool("jump", false);

        int layer = skateMode && skatingLayer >= 0 ? skatingLayer : 0;

        animator.CrossFadeInFixedTime(
            skateMode ? skateMovementState : normalMovementState,
            0.05f,
            layer,
            0f
        );
    }

    void CheckGround()
    {
        if (playerCollider == null) return;

        float distance = playerCollider.bounds.extents.y + 0.15f;

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

        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }

        if (throwRoutine != null)
        {
            StopCoroutine(throwRoutine);
            throwRoutine = null;
        }

        attacking = false;

        if (rb != null)
        {
            Vector3 velocity = rb.linearVelocity;
            velocity.x = 0f;
            velocity.z = 0f;
            rb.linearVelocity = velocity;
        }

        if (animator != null)
        {
            animator.SetFloat("Blend", 0f);
            animator.SetFloat("SkateX", 0f);
            animator.SetFloat("SkateY", 0f);
            if (attackLayer >= 0) animator.SetLayerWeight(attackLayer, 0f);
            if (throwLayer >= 0) animator.SetLayerWeight(throwLayer, 0f);
        }
    }
}
