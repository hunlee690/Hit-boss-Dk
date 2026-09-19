using UnityEngine;

public class SimpleAIPlayerBehaviour : MonoBehaviour
{
    [Header("References")]
    public Rigidbody rb;
    public Animator animator;
    public PlayerController playerController;
    public CombatStats stats;
    public CombatController combat;


    [Header("Movement")]
    public float runSpeed = 5f;
    public float skateSpeed = 8f;
    public float turnSpeed = 6f;


    [Header("Combat")]
    public float attackDistance = 1.5f;
    public float attackCooldown = 1.2f;

    [Range(0f, 1f)]
    public float punchChance = 0.65f;


    [Header("Behaviour")]
    public float targetRefreshTime = 0.4f;

    [Tooltip("AI looks for stamina when at or below this amount.")]
    public int seekStaminaAt = 1;


    [Header("Obstacle Avoidance")]
    public float obstacleCheckDistance = 2f;
    public LayerMask obstacleLayers;


    [Header("Skating")]
    [Range(0f, 1f)]
    public float skateChance = 0.35f;


    MatchParticipant participant;

    MatchParticipant targetPlayer;
    StaminaPickup targetPickup;

    Vector3 moveDirection;

    float targetTimer;
    float attackTimer;

    bool skating;


    // =====================================================
    // AWAKE
    // =====================================================

    void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (stats == null)
            stats = GetComponent<CombatStats>();

        if (combat == null)
            combat = GetComponent<CombatController>();

        participant =
            GetComponent<MatchParticipant>();


        // AI never reads human keyboard input.
        if (playerController != null)
            playerController.enabled = false;


        if (rb != null)
        {
            rb.constraints =
                RigidbodyConstraints.FreezeRotationX |
                RigidbodyConstraints.FreezeRotationZ;
        }
    }


    // =====================================================
    // START
    // =====================================================

    void Start()
    {
        ChooseMovementMode();

        RefreshTarget();
    }


    // =====================================================
    // UPDATE
    // =====================================================

    void Update()
    {
        if (stats == null ||
            stats.IsDead)
            return;


        attackTimer -= Time.deltaTime;
        targetTimer -= Time.deltaTime;


        if (targetTimer <= 0f)
        {
            RefreshTarget();

            targetTimer =
                targetRefreshTime;
        }


        DecideBehaviour();

        AvoidObstacles();

        UpdateAnimation();
    }


    // =====================================================
    // FIXED UPDATE
    // =====================================================

    void FixedUpdate()
{
    if (stats == null ||
        stats.IsDead)
        return;


    if (rb != null)
        rb.angularVelocity = Vector3.zero;


    Move();


    if (rb != null)
        rb.angularVelocity = Vector3.zero;
}


    // =====================================================
    // DECISION
    // =====================================================

    void DecideBehaviour()
    {
        // -------------------------------------------------
        // LOW STAMINA = FIND STAMINA BOX
        // -------------------------------------------------

        if (stats.Stamina <= seekStaminaAt)
        {
            if (targetPickup != null)
            {
                MoveTowards(
                    targetPickup.transform.position
                );
            }
            else
            {
                Wander();
            }

            return;
        }


        // -------------------------------------------------
        // HAVE STAMINA = HUNT PLAYER
        // -------------------------------------------------

        if (targetPlayer == null)
        {
            Wander();
            return;
        }


        CombatStats targetStats =
            targetPlayer.GetComponent<CombatStats>();


        if (targetStats == null ||
            targetStats.IsDead)
        {
            targetPlayer = null;
            return;
        }


        float distance =
            Vector3.Distance(
                transform.position,
                targetPlayer.transform.position
            );


        // -------------------------------------------------
        // ATTACK
        // -------------------------------------------------

        if (distance <= attackDistance)
        {
            FacePosition(
                targetPlayer.transform.position
            );


            moveDirection =
                Vector3.zero;


            if (attackTimer <= 0f)
                TryAttack();


            return;
        }


        // -------------------------------------------------
        // CHASE
        // -------------------------------------------------

        MoveTowards(
            targetPlayer.transform.position
        );
    }


    // =====================================================
    // ATTACK
    // =====================================================

    void TryAttack()
    {
        if (combat == null ||
            stats == null)
            return;


        bool attacked = false;


        // Full stamina:
        // usually punch, sometimes slide.
        if (stats.Stamina >= 3)
        {
            if (Random.value <= punchChance)
            {
                attacked =
                    combat.TryPunch();
            }
            else
            {
                attacked =
                    combat.TrySlide();
            }
        }

        // 2 stamina = slide.
        else if (stats.Stamina >= 2)
        {
            attacked =
                combat.TrySlide();
        }


        if (attacked)
        {
            attackTimer =
                attackCooldown;
        }
    }


    // =====================================================
    // FIND TARGET
    // =====================================================

    void RefreshTarget()
    {
        if (stats == null)
            return;


        // Low stamina = pickup target.
        if (stats.Stamina <= seekStaminaAt)
        {
            targetPlayer = null;

            FindNearestStamina();

            return;
        }


        // Enough stamina = enemy target.
        targetPickup = null;

        FindNearestPlayer();
    }


    // =====================================================
    // NEAREST PLAYER
    // =====================================================

    void FindNearestPlayer()
    {
        if (GameModeManager.Instance == null ||
            GameModeManager.Instance.Participants == null)
            return;


        MatchParticipant nearest = null;

        float nearestDistance =
            Mathf.Infinity;


        foreach (MatchParticipant other
                 in GameModeManager.Instance.Participants)
        {
            if (other == null ||
                other == participant)
                continue;


            CombatStats otherStats =
                other.GetComponent<CombatStats>();


            if (otherStats == null ||
                otherStats.IsDead)
                continue;


            float distance =
                (
                    other.transform.position -
                    transform.position
                ).sqrMagnitude;


            if (distance <
                nearestDistance)
            {
                nearestDistance =
                    distance;

                nearest =
                    other;
            }
        }


        targetPlayer =
            nearest;
    }


    // =====================================================
    // NEAREST STAMINA
    // =====================================================

    void FindNearestStamina()
    {
        StaminaPickup[] pickups =
            FindObjectsByType<StaminaPickup>(
                FindObjectsSortMode.None
            );


        StaminaPickup nearest = null;

        float nearestDistance =
            Mathf.Infinity;


        foreach (StaminaPickup pickup
                 in pickups)
        {
            if (pickup == null)
                continue;


            float distance =
                (
                    pickup.transform.position -
                    transform.position
                ).sqrMagnitude;


            if (distance <
                nearestDistance)
            {
                nearestDistance =
                    distance;

                nearest =
                    pickup;
            }
        }


        targetPickup =
            nearest;
    }


    // =====================================================
    // MOVE TOWARD POSITION
    // =====================================================

    void MoveTowards(Vector3 position)
    {
        Vector3 direction =
            position -
            transform.position;


        direction.y = 0f;


        if (direction.sqrMagnitude >
            0.01f)
        {
            moveDirection =
                direction.normalized;
        }
    }


    // =====================================================
    // WANDER
    // =====================================================

    void Wander()
    {
        if (moveDirection.sqrMagnitude >
            0.01f)
            return;


        Vector2 random =
            Random.insideUnitCircle.normalized;


        moveDirection =
            new Vector3(
                random.x,
                0f,
                random.y
            );
    }


    // =====================================================
    // MOVE
    // =====================================================

    void Move()
    {
        if (rb == null)
            return;


        Vector3 direction =
            moveDirection;


        direction.y = 0f;


        if (direction.sqrMagnitude <
            0.01f)
        {
            Vector3 stopVelocity =
                rb.linearVelocity;


            stopVelocity.x = 0f;
            stopVelocity.z = 0f;


            rb.linearVelocity =
                stopVelocity;

            return;
        }


        direction.Normalize();


        float speed =
            skating
                ? skateSpeed
                : runSpeed;


        Vector3 velocity =
            rb.linearVelocity;


        velocity.x =
            direction.x * speed;

        velocity.z =
            direction.z * speed;


        rb.linearVelocity =
            velocity;


        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction,
                Vector3.up
            );


        rb.MoveRotation(
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                turnSpeed *
                Time.fixedDeltaTime
            )
        );
    }


    // =====================================================
    // FACE TARGET
    // =====================================================

    void FacePosition(Vector3 position)
    {
        Vector3 direction =
            position -
            transform.position;


        direction.y = 0f;


        if (direction.sqrMagnitude <
            0.01f)
            return;


        Quaternion targetRotation =
            Quaternion.LookRotation(
                direction.normalized
            );


        transform.rotation =
            Quaternion.Slerp(
                transform.rotation,
                targetRotation,
                turnSpeed *
                Time.deltaTime
            );
    }


    // =====================================================
    // OBSTACLE AVOIDANCE
    // =====================================================

    void AvoidObstacles()
    {
        if (moveDirection.sqrMagnitude <
            0.01f)
            return;


        Vector3 origin =
            transform.position +
            Vector3.up * 0.7f;


        Vector3 direction =
            moveDirection.normalized;


        if (!Physics.Raycast(
            origin,
            direction,
            obstacleCheckDistance,
            obstacleLayers,
            QueryTriggerInteraction.Ignore))
        {
            return;
        }


        Vector3 left =
            Quaternion.Euler(
                0f,
                -70f,
                0f
            ) * direction;


        Vector3 right =
            Quaternion.Euler(
                0f,
                70f,
                0f
            ) * direction;


        bool leftBlocked =
            Physics.Raycast(
                origin,
                left,
                obstacleCheckDistance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore
            );


        bool rightBlocked =
            Physics.Raycast(
                origin,
                right,
                obstacleCheckDistance,
                obstacleLayers,
                QueryTriggerInteraction.Ignore
            );


        if (!leftBlocked)
        {
            moveDirection =
                left.normalized;
        }
        else if (!rightBlocked)
        {
            moveDirection =
                right.normalized;
        }
        else
        {
            moveDirection =
                -direction;
        }
    }


    // =====================================================
    // SKATING
    // =====================================================

    void ChooseMovementMode()
    {
        skating =
            Random.value <
            skateChance;


        if (playerController != null)
        {
            playerController.SetSkateMode(
                skating
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


        bool moving =
            moveDirection.sqrMagnitude >
            0.01f;


        if (!skating)
        {
            animator.SetFloat(
                "Blend",
                moving ? 1f : 0f
            );

            return;
        }


        animator.SetFloat(
            "SkateX",
            0f
        );

        animator.SetFloat(
            "SkateY",
            0f
        );
    }
}