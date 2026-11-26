using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [Header("Normal Movement")] public float moveSpeed = 5f;
    [Tooltip("Lower = faster acceleration/deceleration.")] public float moveSmoothTime = 0.12f;
    [Tooltip("Lower = faster turning. Try 0.15 - 0.30")] public float rotationSmoothTime = 0.2f;

    [Header("Skating")] public float skateForwardSpeed = 15f;
    public float skateReverseSpeed = 7.5f;
    public float skateAcceleration = 8f;
    public float skateBrakeSpeed = 6f;
    public float skateTurnSpeed = 100f;
    public float skateTurnSmooth = 8f;
    [Min(1f)] public float steeringReturnSpeed = 14f;
    [Range(1f, 2f)] public float steeringPrecision = 1.25f;
    [Header("Banked Curves")]
    [Range(1f, 3f)] public float bankTurnMultiplier = 1.25f;
    [Min(0f)] public float skateGroundGrip = 2.5f;
    [Tooltip("Optional parent containing all skate models.")] public GameObject skateMesh;
    public bool startInSkateMode;

    [Header("Skate Height")] public Transform skateHeightTarget;
    public CapsuleCollider capsuleCollider;
    public float skateHeightOffset = 0.12f;

    [Header("Jump")] public float jumpHeight = 1.5f;
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

    [Header("Throw")]public float throwDuration = 0.8f;public float throwBlendTime = 0.15f;
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
    float buildUpTimer, speedBonus;
    readonly RaycastHit[] groundHits = new RaycastHit[12];
    int skatingLayer, attackLayer, throwLayer;
    Coroutine attackRoutine, throwRoutine;
    GameObject[] equippedSkateObjects, previewSkateObjects;
    bool customizationSkatePreview;
    Vector3 normalSkateTargetPosition;
    float normalCapsuleHeight;
    Vector3 normalCapsuleCenter;
    bool skateHeightCached;
    Vector3 groundNormal = Vector3.up;
    float steeringBank;
    float lastGroundedAt = -10f, wallContactAt = -10f;
    float groundIgnoreUntil = -1f, lastGroundCheckAt = -1f;
    bool groundAdhesionApplied;
    Vector3 wallNormal;
    readonly ContactPoint[] wallContacts = new ContactPoint[12];

    public bool IsJumping => jumping;
    public bool IsSkateMode => skateMode;
    public float SkateGroundClearance { get; private set; }
    public float SkateBoostSpeed { get; set; }
    public float SkateSlideSpeed { get; set; }
    public float SkateDraftSpeed { get; set; }
    public float SkatePackSpeed { get; set; }
    public float SteeringAmount { get; private set; }
    public Vector3 SkateGroundNormal => groundNormal;
    public bool HasGroundContact => grounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        playerCollider = GetComponent<Collider>();

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
        if (Keyboard.current != null)
        {
            if (Keyboard.current.nKey.wasPressedThisFrame)
                SetSkateMode(!skateMode);

            if (Keyboard.current.spaceKey.wasPressedThisFrame && grounded && !jumping)
                StartJump();

            if (!skateMode) ReadNormalInput();
        }

        if(skateMode)ReadSkateInput();
        if(HitBoss.Multiplayer.Skate.MobileRaceControls.TakeJump()&&grounded&&!jumping)StartJump();

        UpdateAnimator();
    }

    void FixedUpdate()
    {
        if (rb == null || rb.isKinematic) return;
        CheckGround();
        rb.angularVelocity = Vector3.zero;

        if (!jumpLaunched)
        {
            // A brief loss of bank contact must not disable steering.
            if (skateMode) MoveSkate();
            else MoveNormal();
        }

        if (jumping) UpdateJump();

        rb.angularVelocity = Vector3.zero;
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

        if (Keyboard.current?.aKey.isPressed==true) skateTurnTarget = -1f;
        if (Keyboard.current?.dKey.isPressed==true) skateTurnTarget = 1f;
        if(HitBoss.Multiplayer.Skate.MobileRaceControls.Steering!=0)skateTurnTarget=HitBoss.Multiplayer.Skate.MobileRaceControls.Steering;

        float response = skateTurnTarget == 0f || skateTurnTarget * skateTurn < 0f
            ? steeringReturnSpeed : skateTurnSmooth;
        skateTurn = Mathf.MoveTowards(skateTurn, skateTurnTarget, response * Time.deltaTime);
    }

    void MoveSkate()
    {
        if (rb == null) return;

        bool braking = Keyboard.current != null && Keyboard.current.sKey.isPressed || HitBoss.Multiplayer.Skate.MobileRaceControls.Brake;

        float targetSpeed = braking ? -skateReverseSpeed : skateForwardSpeed;
        float acceleration = braking ? skateBrakeSpeed : skateAcceleration;

        skateSpeed = Mathf.MoveTowards(
            skateSpeed,
            targetSpeed,
            acceleration * Time.fixedDeltaTime
        );

        Quaternion facing = rb.rotation;
        float yawStep = 0f;
        if (Mathf.Abs(skateSpeed) > 0.05f)
        {
            float reverse = skateSpeed >= 0f ? 1f : -1f;
            float bank = Mathf.InverseLerp(3f, 25f, Vector3.Angle(Vector3.up, groundNormal));
            steeringBank = Mathf.Lerp(steeringBank, bank, 1f - Mathf.Exp(-8f * Time.fixedDeltaTime));
            float steeringStrength = Mathf.Lerp(1f, 1f + (bankTurnMultiplier - 1f) * .25f, steeringBank);
            float preciseInput = Mathf.Sign(skateTurn) * Mathf.Pow(Mathf.Abs(skateTurn), steeringPrecision);
            float speedResponse = Mathf.Clamp((Mathf.Abs(skateSpeed) + speedBonus) / Mathf.Max(1f, skateForwardSpeed), .8f, 1.25f);
            SteeringAmount = preciseInput;
            float control = grounded || Time.fixedTime - lastGroundedAt < .2f ? 1f : .35f;
            float yawRate = preciseInput * skateTurnSpeed * steeringStrength * speedResponse * reverse * control;
            yawStep = yawRate * Time.fixedDeltaTime;
            Quaternion turn = Quaternion.Euler(
                0f,
                yawStep,
                0f
            );
            facing = rb.rotation * turn;
            rb.MoveRotation(facing);
        }

        Vector3 forward = facing * Vector3.forward;
        if (!grounded)
        {
            // Keep fall momentum and gravity. Steering rotates existing horizontal motion only.
            Vector3 velocity = rb.linearVelocity;
            Vector3 horizontal = Quaternion.Euler(0f, yawStep, 0f) * new Vector3(velocity.x, 0f, velocity.z);
            rb.linearVelocity = SlideAlongWall(new Vector3(horizontal.x, velocity.y, horizontal.z));
            return;
        }

        float targetBonus = !braking && skateSpeed >= 0f ? SkateBoostSpeed + SkateSlideSpeed + SkateDraftSpeed + SkatePackSpeed : 0f;
        float bonusRate = SkateSlideSpeed > 0f ? 45f : targetBonus < speedBonus ? 24f : 17f;
        speedBonus = Mathf.MoveTowards(speedBonus, targetBonus, bonusRate * Time.fixedDeltaTime);
        float driveSpeed = skateSpeed + speedBonus;
        rb.linearVelocity = GroundFollowingVelocity(forward, driveSpeed);
    }

    public Vector3 GroundFollowingVelocity(Vector3 direction, float speed)
    {
        if (!enabled) CheckGround();
        if (grounded && !jumpLaunched)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(direction, groundNormal).normalized;
            groundAdhesionApplied = true;
            return SlideAlongWall(tangent * speed - groundNormal * skateGroundGrip);
        }
        groundAdhesionApplied = false;
        // Bots share the same ballistic flight as the player. No air braking or forced horizontal speed.
        return SlideAlongWall(rb.linearVelocity);
    }

    Vector3 SlideAlongWall(Vector3 velocity)
    {
        if (Time.fixedTime - wallContactAt <= Time.fixedDeltaTime * 2.5f && Vector3.Dot(velocity, wallNormal) < 0f)
            return Vector3.ProjectOnPlane(velocity, wallNormal);
        return velocity;
    }

    void OnCollisionStay(Collision collision)
    {
        if (!skateMode || collision.collider.GetComponentInParent<PlayerController>() != null ||
            collision.collider.GetComponentInParent<HitBoss.Multiplayer.Skate.SkateNetworkItem>() != null) return;
        int count = collision.GetContacts(wallContacts);
        Vector3 normal = Vector3.zero;
        for (int i = 0; i < count; i++)
            if (Mathf.Abs(wallContacts[i].normal.y) < .35f) normal += wallContacts[i].normal;
        if (normal.sqrMagnitude < .01f) return;
        wallNormal = normal.normalized;
        wallContactAt = Time.fixedTime;
    }

    public void SetBotSteering(float amount)
    {
        SteeringAmount = Mathf.Clamp(amount, -1f, 1f);
        if (animator != null) animator.SetFloat("SkateX", -SteeringAmount);
    }

    public void SetSkateMode(bool enabled)
    {
        grounded = false; groundAdhesionApplied = false; lastGroundCheckAt = -1f; groundIgnoreUntil = -1f;
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
        SkateGroundClearance = amount;

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
            -SteeringAmount,
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
    IEnumerator FadeLayer(int layer, float target, float duration)
{
    float start = animator.GetLayerWeight(layer);
    float time = 0f;
    while (time < duration)
    {    time += Time.deltaTime;
         animator.SetLayerWeight(
            layer,
            Mathf.Lerp(start, target, time / duration)
        );
        yield return null;
    }

    animator.SetLayerWeight(layer, target);
}

    void StartJump()
    {
        jumping = true;
        jumpLaunched = false;
        buildUpTimer = 0f;

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

            if (grounded) LaunchJump();
            else EndJump();
            return;
        }

        if (grounded && rb.linearVelocity.y <= .1f)
            EndJump();
    }

    void LaunchJump()
    {
        jumpLaunched = true;
        grounded = false;
        float takeoff = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);
        rb.AddForce(Vector3.up * Mathf.Max(0f, takeoff - rb.linearVelocity.y), ForceMode.VelocityChange);
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

    public bool RefreshSkateContact()
    {
        CheckGround();
        return grounded;
    }

    void CheckGround()
    {
        if (Mathf.Approximately(lastGroundCheckAt, Time.fixedTime)) return;
        lastGroundCheckAt = Time.fixedTime;
        bool wasGrounded = grounded;
        Vector3 previousNormal = groundNormal;
        bool supported = SampleSkateSurface(out var normal);
        if (Time.fixedTime < groundIgnoreUntil) supported = false;
        if (supported && wasGrounded && skateMode && !jumpLaunched && !jumping && rb != null && !rb.isKinematic &&
            Vector3.Dot(rb.linearVelocity, normal) > 1.5f)
        {
            // At a crest the existing velocity points away from the next surface.
            // Let that momentum launch the body instead of snapping it back down.
            supported = false;
            groundIgnoreUntil = Time.fixedTime + .14f;
        }
        if (wasGrounded && !supported && groundAdhesionApplied && !jumpLaunched && rb != null && !rb.isKinematic)
        {
            float inwardSpeed = Mathf.Max(0f, -Vector3.Dot(rb.linearVelocity, previousNormal));
            rb.linearVelocity += previousNormal * Mathf.Min(skateGroundGrip, inwardSpeed);
        }
        if (!supported) groundAdhesionApplied = false;
        grounded = supported;
        if (grounded) { groundNormal = normal; lastGroundedAt = Time.fixedTime; }
        else groundNormal = Vector3.up;
    }

    public bool SampleSkateSurface(out Vector3 normal)
    {
        normal = Vector3.up;
        if (capsuleCollider == null || !gameObject.activeInHierarchy) return false;
        float scale = Mathf.Abs(transform.lossyScale.y);
        float halfHeight = capsuleCollider.height * scale * .5f;
        float radius = capsuleCollider.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        Vector3 center = transform.TransformPoint(capsuleCollider.center);
        float nearest = float.PositiveInfinity;
        bool found = false;
        int count = Physics.SphereCastNonAlloc(center + Vector3.up * .1f, Mathf.Max(.05f, radius * .8f), Vector3.down, groundHits, halfHeight + .7f,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            var hit = groundHits[i];
            if (hit.distance <= .001f || hit.normal.y < .35f || hit.collider.transform.root == transform.root ||
                hit.collider.GetComponentInParent<PlayerController>() != null ||
                hit.collider.GetComponentInParent<HitBoss.Multiplayer.Skate.SkateNetworkItem>() != null) continue;
            float support = radius + Mathf.Max(0f, halfHeight - radius) * hit.normal.y;
            float gap = Vector3.Dot(center - hit.point, hit.normal) - support;
            float gripRange = skateMode && !jumping ? .15f : .12f;
            if (gap > gripRange || hit.distance >= nearest) continue;
            nearest = hit.distance;
            normal = hit.normal;
            found = true;
        }
        return found;
    }

    public void StopImmediately()
    {
        moveInput = Vector2.zero;
        currentMoveSpeed = 0f;
        moveSpeedVelocity = 0f;
        turnVelocity = 0f;
        skateSpeed = 0f;
        speedBonus = 0f;
        skateTurn = 0f;
        skateTurnTarget = 0f;
        blend = 0f;
        steeringBank = 0f;
        SteeringAmount = 0f;

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
        if (jumping) EndJump();

        if (rb != null && !rb.isKinematic)
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
