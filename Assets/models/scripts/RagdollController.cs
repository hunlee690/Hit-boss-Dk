using System.Collections;
using UnityEngine;

public class RagdollController : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public Rigidbody rootRigidbody;
    public Collider rootCollider;

    [Tooltip("Assign your Generic rig pelvis/hips bone manually.")]
    public Transform hips;


    [Header("Recovery")]
    [Tooltip("How long a normal knockdown stays down before it is allowed to get up.")]
    public float getUpDelay = 0.15f;

    [Tooltip("Ragdoll must be moving slower than this before it is considered settled.")]
    public float settleVelocity = 0.5f;

    [Tooltip("Hips must be this close to the ground before the ragdoll is considered landed.")]
    public float maxHipsGroundDistance = 1f;

    [Tooltip("How long the ragdoll must remain settled continuously.")]
    public float settleHoldTime = 0.35f;

    [Tooltip("How far below the hips we search for ground.")]
    public float groundCheckDistance = 5f;

    [Tooltip("Small offset above ground when a temporary ragdoll stands back up.")]
    public float recoveryGroundOffset = 0.05f;

    [Tooltip("Prevents this character's ragdoll colliders from violently pushing each other.")]
    public bool ignoreSelfCollisions = true;


    Rigidbody[] ragdollBodies;
    Collider[] ragdollColliders;
    Rigidbody hipsRigidbody;

    PlayerController playerController;
    SimpleAIPlayerBehaviour aiBehaviour;
    CombatController combatController;
    ThrowableInventory throwableInventory;
    CombatStats stats;

    bool playerWasEnabled;
    bool aiWasEnabled;
    bool combatWasEnabled;
    bool throwableWasEnabled;

    bool ragdolled;
    bool permanentRagdoll;

    Coroutine recoverRoutine;


    public bool IsRagdolled =>
        ragdolled;


    // =====================================================
    // AWAKE
    // =====================================================

    void Awake()
    {
        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>(true);
        }


        if (rootRigidbody == null)
        {
            rootRigidbody =
                GetComponent<Rigidbody>();
        }


        if (rootCollider == null)
        {
            rootCollider =
                GetComponent<Collider>();
        }


        playerController =
            GetComponent<PlayerController>();

        aiBehaviour =
            GetComponent<SimpleAIPlayerBehaviour>();

        combatController =
            GetComponent<CombatController>();

        throwableInventory =
            GetComponent<ThrowableInventory>();

        stats =
            GetComponent<CombatStats>();


        ragdollBodies =
            GetComponentsInChildren<Rigidbody>(true);

        ragdollColliders =
            GetComponentsInChildren<Collider>(true);


        if (hips != null)
        {
            hipsRigidbody =
                hips.GetComponent<Rigidbody>();
        }


        if (ignoreSelfCollisions)
        {
            IgnoreRagdollSelfCollisions();
        }


        DisableRagdollPhysics();
    }


    // =====================================================
    // ENTER RAGDOLL
    // =====================================================

    public void EnterRagdoll(
        float duration,
        bool stayRagdolled = false)
    {
        if (permanentRagdoll &&
            !stayRagdolled)
        {
            return;
        }


        if (!ragdolled)
        {
            SaveControlStates();
            DisableControl();

            ragdolled = true;


            if (animator != null)
            {
                animator.enabled = false;
            }


            EnableRagdollPhysics();
        }


        if (stayRagdolled)
        {
            permanentRagdoll = true;
        }


        if (recoverRoutine != null)
        {
            StopCoroutine(
                recoverRoutine
            );

            recoverRoutine = null;
        }


        // Death ragdoll stays physical until
        // GameModeManager prepares the respawn.
        if (!permanentRagdoll)
        {
            recoverRoutine =
                StartCoroutine(
                    RecoverAfter(duration)
                );
        }
    }


    // =====================================================
    // RAGDOLL FORCE
    // =====================================================

    public void ApplyForceFromPoint(
        Vector3 sourcePosition,
        float horizontalForce,
        float upwardForce)
    {
        if (!ragdolled)
            return;


        Rigidbody targetBody =
            hipsRigidbody;


        if (targetBody == null)
        {
            foreach (Rigidbody body
                     in ragdollBodies)
            {
                if (body != null &&
                    body != rootRigidbody &&
                    !body.isKinematic)
                {
                    targetBody = body;
                    break;
                }
            }
        }


        if (targetBody == null)
            return;


        Vector3 direction =
            targetBody.worldCenterOfMass -
            sourcePosition;


        // Keep knockback mainly horizontal.
        direction.y = 0f;


        if (direction.sqrMagnitude <
            0.001f)
        {
            direction =
                transform.forward;
        }


        direction.Normalize();


        Vector3 impulse =
            direction *
            Mathf.Max(
                0f,
                horizontalForce
            ) +
            Vector3.up *
            Mathf.Max(
                0f,
                upwardForce
            );


        targetBody.AddForce(
            impulse,
            ForceMode.Impulse
        );
    }


    // =====================================================
    // TEMPORARY RECOVERY
    // =====================================================

    IEnumerator RecoverAfter(
        float duration)
    {
        yield return
            new WaitForSeconds(duration);


        yield return
            new WaitForSeconds(getUpDelay);


        yield return
            StartCoroutine(
                WaitUntilSettled()
            );


        if (!permanentRagdoll &&
            TryGetGroundPoint(
                out Vector3 groundPoint,
                out _))
        {
            RecoverAtGround(
                groundPoint
            );
        }
    }


    IEnumerator WaitUntilSettled()
    {
        float settledFor = 0f;


        while (ragdolled)
        {
            if (IsSettledOnGround())
            {
                settledFor +=
                    Time.deltaTime;


                if (settledFor >=
                    settleHoldTime)
                {
                    yield break;
                }
            }
            else
            {
                settledFor = 0f;
            }


            yield return null;
        }
    }


    // =====================================================
    // DEATH / RESPAWN SUPPORT
    // =====================================================

    public bool IsSettledOnGround()
    {
        if (!ragdolled)
            return true;


        if (!TryGetGroundPoint(
            out _,
            out float groundDistance))
        {
            return false;
        }


        if (groundDistance >
            maxHipsGroundDistance)
        {
            return false;
        }


        float speed =
            hipsRigidbody != null
                ? hipsRigidbody.linearVelocity.magnitude
                : GetAverageRagdollSpeed();


        return
            speed <= settleVelocity;
    }


    public IEnumerator WaitForRagdollToFinish()
    {
        if (!ragdolled)
            yield break;


        yield return
            StartCoroutine(
                WaitUntilSettled()
            );
    }


    public void PrepareForRespawn()
    {
        if (recoverRoutine != null)
        {
            StopCoroutine(
                recoverRoutine
            );

            recoverRoutine = null;
        }


        permanentRagdoll = false;


        DisableRagdollPhysics();


        if (rootRigidbody != null)
        {
            rootRigidbody.isKinematic =
                false;

            rootRigidbody.linearVelocity =
                Vector3.zero;

            rootRigidbody.angularVelocity =
                Vector3.zero;
        }


        if (animator != null)
        {
            animator.enabled = true;

            animator.Rebind();

            animator.Update(0f);
        }


        ragdolled = false;
    }


    // =====================================================
    // TEMPORARY GET-UP
    // =====================================================

    void RecoverAtGround(
        Vector3 groundPoint)
    {
        if (!ragdolled ||
            permanentRagdoll)
        {
            return;
        }


        Vector3 forward =
            GetFlatFacingDirection();


        Vector3 position =
            groundPoint +
            Vector3.up *
            recoveryGroundOffset;


        DisableRagdollPhysics();


        if (rootRigidbody != null)
        {
            rootRigidbody.isKinematic =
                false;

            rootRigidbody.position =
                position;

            rootRigidbody.rotation =
                Quaternion.LookRotation(
                    forward,
                    Vector3.up
                );

            rootRigidbody.linearVelocity =
                Vector3.zero;

            rootRigidbody.angularVelocity =
                Vector3.zero;
        }
        else
        {
            transform.position =
                position;

            transform.rotation =
                Quaternion.LookRotation(
                    forward,
                    Vector3.up
                );
        }


        if (animator != null)
        {
            animator.enabled = true;

            animator.Rebind();

            animator.Update(0f);
        }


        ragdolled = false;


        RestoreControlStates();


        recoverRoutine = null;
    }


    Vector3 GetFlatFacingDirection()
    {
        Vector3 forward =
            hips != null
                ? hips.forward
                : transform.forward;


        forward.y = 0f;


        if (forward.sqrMagnitude <
            0.001f)
        {
            forward =
                transform.forward;

            forward.y = 0f;
        }


        return
            forward.normalized;
    }


    // =====================================================
    // GROUND
    // =====================================================

    bool TryGetGroundPoint(
        out Vector3 groundPoint,
        out float groundDistance)
    {
        Vector3 origin =
            hips != null
                ? hips.position
                : transform.position +
                  Vector3.up;


        origin +=
            Vector3.up * 0.1f;


        RaycastHit[] hits =
            Physics.RaycastAll(
                origin,
                Vector3.down,
                groundCheckDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );


        bool found = false;

        float closest =
            Mathf.Infinity;

        groundPoint =
            Vector3.zero;

        groundDistance =
            Mathf.Infinity;


        foreach (RaycastHit hit
                 in hits)
        {
            if (hit.collider == null)
                continue;


            if (hit.collider.transform
                .IsChildOf(transform))
            {
                continue;
            }


            if (hit.distance <
                closest)
            {
                closest =
                    hit.distance;

                groundPoint =
                    hit.point;

                groundDistance =
                    hit.distance;

                found = true;
            }
        }


        return found;
    }


    float GetAverageRagdollSpeed()
    {
        float total = 0f;
        int count = 0;


        foreach (Rigidbody body
                 in ragdollBodies)
        {
            if (body == null ||
                body == rootRigidbody)
            {
                continue;
            }


            total +=
                body.linearVelocity.magnitude;

            count++;
        }


        if (count == 0)
            return 0f;


        return
            total / count;
    }


    // =====================================================
    // CONTROL
    // =====================================================

    void SaveControlStates()
    {
        if (playerController != null)
        {
            playerWasEnabled =
                playerController.enabled;
        }


        if (aiBehaviour != null)
        {
            aiWasEnabled =
                aiBehaviour.enabled;
        }


        if (combatController != null)
        {
            combatWasEnabled =
                combatController.enabled;
        }


        if (throwableInventory != null)
        {
            throwableWasEnabled =
                throwableInventory.enabled;
        }
    }


    void DisableControl()
    {
        if (playerController != null)
        {
            playerController.StopImmediately();

            playerController.enabled =
                false;
        }


        if (aiBehaviour != null)
        {
            aiBehaviour.enabled =
                false;
        }


        if (combatController != null)
        {
            combatController.enabled =
                false;
        }


        if (throwableInventory != null)
        {
            throwableInventory.enabled =
                false;
        }
    }


    void RestoreControlStates()
    {
        if (stats != null &&
            stats.IsDead)
        {
            return;
        }


        if (playerController != null)
        {
            playerController.enabled =
                playerWasEnabled;
        }


        if (aiBehaviour != null)
        {
            aiBehaviour.enabled =
                aiWasEnabled;
        }


        if (combatController != null)
        {
            combatController.enabled =
                combatWasEnabled;
        }


        if (throwableInventory != null)
        {
            throwableInventory.enabled =
                throwableWasEnabled;
        }
    }


    // =====================================================
    // RAGDOLL PHYSICS
    // =====================================================

    void EnableRagdollPhysics()
    {
        if (rootCollider != null)
        {
            rootCollider.enabled =
                false;
        }


        if (rootRigidbody != null)
        {
            rootRigidbody.linearVelocity =
                Vector3.zero;

            rootRigidbody.angularVelocity =
                Vector3.zero;

            rootRigidbody.isKinematic =
                true;
        }


        foreach (Rigidbody body
                 in ragdollBodies)
        {
            if (body == null ||
                body == rootRigidbody)
            {
                continue;
            }


            body.linearVelocity =
                Vector3.zero;

            body.angularVelocity =
                Vector3.zero;

            body.isKinematic =
                false;

            body.useGravity =
                true;
        }


        foreach (Collider col
                 in ragdollColliders)
        {
            if (col == null ||
                col == rootCollider)
            {
                continue;
            }


            col.enabled =
                true;
        }
    }


    void DisableRagdollPhysics()
    {
        foreach (Rigidbody body
                 in ragdollBodies)
        {
            if (body == null ||
                body == rootRigidbody)
            {
                continue;
            }


            body.linearVelocity =
                Vector3.zero;

            body.angularVelocity =
                Vector3.zero;

            body.isKinematic =
                true;

            body.useGravity =
                false;
        }


        foreach (Collider col
                 in ragdollColliders)
        {
            if (col == null ||
                col == rootCollider)
            {
                continue;
            }


            col.enabled =
                false;
        }


        if (rootCollider != null)
        {
            rootCollider.enabled =
                true;
        }
    }


    // =====================================================
    // SELF COLLISION
    // =====================================================

    void IgnoreRagdollSelfCollisions()
    {
        for (int i = 0;
             i < ragdollColliders.Length;
             i++)
        {
            Collider a =
                ragdollColliders[i];


            if (a == null ||
                a == rootCollider)
            {
                continue;
            }


            for (int j = i + 1;
                 j < ragdollColliders.Length;
                 j++)
            {
                Collider b =
                    ragdollColliders[j];


                if (b == null ||
                    b == rootCollider)
                {
                    continue;
                }


                Physics.IgnoreCollision(
                    a,
                    b,
                    true
                );
            }
        }
    }
}
