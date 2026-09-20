using UnityEngine;
using UnityEngine.InputSystem;

public class CoopPlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float moveSmoothTime = 0.12f;
    public float rotationSmoothTime = 0.12f;

    [Header("Jump")]
    public float jumpHeight = 1.5f;
    public float jumpDuration = 1f;
    public float jumpBuildUp = 0.1f;

    [Header("References")]
    public Rigidbody rb;
    public Animator animator;
    public Transform cameraTransform;
    public Transform visualModel;

    [Header("Model Direction")]
    public float visualYawOffset = 180f;

    [Header("Animation")]
    public float runAnimationSpeed = 1f;


    Vector3 moveDirection;
    Vector3 moveVelocity;

    float rotationVelocity;

    bool jumping;
    float jumpTimer;
    float buildUpTimer;

    float jumpStartY;
    float jumpVelocity;
    float gravity;

    bool jumpLaunched;


    public bool IsJumping =>
        jumping;


    // =====================================================
    // AWAKE
    // =====================================================

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (animator == null)
            animator =
                GetComponentInChildren<Animator>();

        if (cameraTransform == null &&
            Camera.main != null)
        {
            cameraTransform =
                Camera.main.transform;
        }


        CalculateJumpPhysics();
    }


    // =====================================================
    // UPDATE
    // =====================================================

    void Update()
    {
        ReadMovement();

        HandleRotation();

        HandleJumpInput();

        UpdateAnimation();
    }


    // =====================================================
    // FIXED UPDATE
    // =====================================================

    void FixedUpdate()
    {
        HandleMovement();

        HandleJump();
    }


    // =====================================================
    // INPUT
    // =====================================================

    void ReadMovement()
    {
        moveDirection =
            Vector3.zero;


        if (Keyboard.current == null)
            return;


        Vector2 input =
            Vector2.zero;


        if (Keyboard.current.wKey.isPressed)
            input.y += 1f;

        if (Keyboard.current.sKey.isPressed)
            input.y -= 1f;

        if (Keyboard.current.aKey.isPressed)
            input.x -= 1f;

        if (Keyboard.current.dKey.isPressed)
            input.x += 1f;


        input =
            Vector2.ClampMagnitude(
                input,
                1f
            );


        if (cameraTransform == null)
        {
            moveDirection =
                new Vector3(
                    input.x,
                    0f,
                    input.y
                );

            return;
        }


        Vector3 forward =
            cameraTransform.forward;

        Vector3 right =
            cameraTransform.right;


        forward.y = 0f;
        right.y = 0f;


        forward.Normalize();
        right.Normalize();


        moveDirection =
            forward * input.y +
            right * input.x;


        if (moveDirection.sqrMagnitude > 1f)
            moveDirection.Normalize();
    }


    // =====================================================
    // MOVEMENT
    // =====================================================

    void HandleMovement()
    {
        if (rb == null)
            return;


        Vector3 targetVelocity =
            moveDirection *
            moveSpeed;


        Vector3 currentHorizontal =
            new Vector3(
                rb.linearVelocity.x,
                0f,
                rb.linearVelocity.z
            );


        Vector3 smoothed =
            Vector3.SmoothDamp(
                currentHorizontal,
                targetVelocity,
                ref moveVelocity,
                moveSmoothTime
            );


        Vector3 velocity =
            rb.linearVelocity;


        velocity.x =
            smoothed.x;

        velocity.z =
            smoothed.z;


        rb.linearVelocity =
            velocity;
    }


    // =====================================================
    // ROTATION
    // =====================================================

    void HandleRotation()
    {
        if (moveDirection.sqrMagnitude <
            0.01f)
        {
            return;
        }


        float targetAngle =
            Mathf.Atan2(
                moveDirection.x,
                moveDirection.z
            ) *
            Mathf.Rad2Deg;


        float angle =
            Mathf.SmoothDampAngle(
                transform.eulerAngles.y,
                targetAngle,
                ref rotationVelocity,
                rotationSmoothTime
            );


        transform.rotation =
            Quaternion.Euler(
                0f,
                angle,
                0f
            );


        if (visualModel != null)
        {
            visualModel.localRotation =
                Quaternion.Euler(
                    0f,
                    visualYawOffset,
                    0f
                );
        }
    }


    // =====================================================
    // JUMP INPUT
    // =====================================================

    void HandleJumpInput()
    {
        if (Keyboard.current == null)
            return;


        if (Keyboard.current.spaceKey.wasPressedThisFrame &&
            !jumping)
        {
            StartJump();
        }
    }


    void StartJump()
    {
        jumping = true;
        jumpLaunched = false;

        jumpTimer = 0f;
        buildUpTimer = 0f;

        jumpStartY =
            transform.position.y;


        if (animator != null)
        {
            animator.SetBool(
                "jump",
                true
            );
        }
    }


    // =====================================================
    // JUMP PHYSICS
    // =====================================================

    void HandleJump()
    {
        if (!jumping ||
            rb == null)
        {
            return;
        }


        if (!jumpLaunched)
        {
            buildUpTimer +=
                Time.fixedDeltaTime;


            if (buildUpTimer <
                jumpBuildUp)
            {
                return;
            }


            jumpLaunched =
                true;


            Vector3 velocity =
                rb.linearVelocity;


            velocity.y =
                jumpVelocity;


            rb.linearVelocity =
                velocity;


            jumpTimer =
                0f;
        }


        jumpTimer +=
            Time.fixedDeltaTime;


        Vector3 currentVelocity =
            rb.linearVelocity;


        currentVelocity.y +=
            gravity *
            Time.fixedDeltaTime;


        rb.linearVelocity =
            currentVelocity;


        if (jumpTimer >=
            jumpDuration)
        {
            EndJump();
        }
    }


    void CalculateJumpPhysics()
    {
        float halfTime =
            Mathf.Max(
                0.05f,
                jumpDuration * 0.5f
            );


        gravity =
            (-2f * jumpHeight) /
            (halfTime * halfTime);


        jumpVelocity =
            (2f * jumpHeight) /
            halfTime;
    }


    void EndJump()
    {
        jumping = false;
        jumpLaunched = false;


        if (animator != null)
        {
            animator.SetBool(
                "jump",
                false
            );
        }
    }


    // =====================================================
    // ANIMATION
    // =====================================================

    void UpdateAnimation()
    {
        if (animator == null)
            return;


        float movement =
            Mathf.Clamp01(
                moveDirection.magnitude
            );


        animator.SetFloat(
            "Blend",
            movement
        );


        animator.speed =
            movement > 0.01f
                ? runAnimationSpeed
                : 1f;
    }


    // =====================================================
    // COMBAT ANIMATION
    // =====================================================

    public void PlayCombatAnimation(
        string animationName)
    {
        if (animator == null ||
            string.IsNullOrEmpty(
                animationName))
        {
            return;
        }


        animator.CrossFade(
            animationName,
            0.1f
        );
    }


    // =====================================================
    // STOP
    // =====================================================

    public void StopImmediately()
    {
        moveDirection =
            Vector3.zero;

        moveVelocity =
            Vector3.zero;


        if (rb != null)
        {
            Vector3 velocity =
                rb.linearVelocity;


            velocity.x = 0f;
            velocity.z = 0f;


            rb.linearVelocity =
                velocity;

            rb.angularVelocity =
                Vector3.zero;
        }


        if (animator != null)
        {
            animator.SetFloat(
                "Blend",
                0f
            );
        }
    }
}