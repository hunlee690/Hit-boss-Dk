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
    [Tooltip("Time used to blend from the physical ragdoll pose into the animator pose.")]
    public float recoveryBlendTime = 0.35f;
    [Tooltip("Prevents this character's ragdoll colliders from violently pushing each other.")]
    public bool ignoreSelfCollisions = true;

    Rigidbody[] ragdollBodies;
    Collider[] ragdollColliders;
    Rigidbody hipsRigidbody;
    Transform[] recoveryBones;
    Vector3[] startPositions;
    Quaternion[] startRotations;
    Vector3[] targetPositions;
    Quaternion[] targetRotations;

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

    public bool IsRagdolled => ragdolled;

    void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (rootRigidbody == null)
            rootRigidbody = GetComponent<Rigidbody>();

        if (rootCollider == null)
            rootCollider = GetComponent<Collider>();

        playerController = GetComponent<PlayerController>();
        aiBehaviour = GetComponent<SimpleAIPlayerBehaviour>();
        combatController = GetComponent<CombatController>();
        throwableInventory = GetComponent<ThrowableInventory>();
        stats = GetComponent<CombatStats>();

        ragdollBodies = GetComponentsInChildren<Rigidbody>(true);
        ragdollColliders = GetComponentsInChildren<Collider>(true);

        if (hips != null)
            hipsRigidbody = hips.GetComponent<Rigidbody>();

        BuildRecoveryBones();

        if (ignoreSelfCollisions)
            IgnoreRagdollSelfCollisions();

        DisableRagdollPhysics();
    }

    void BuildRecoveryBones()
    {
        int count = 0;

        foreach (Rigidbody body in ragdollBodies)
            if (body != null && body != rootRigidbody)
                count++;

        recoveryBones = new Transform[count];
        startPositions = new Vector3[count];
        startRotations = new Quaternion[count];
        targetPositions = new Vector3[count];
        targetRotations = new Quaternion[count];

        int index = 0;

        foreach (Rigidbody body in ragdollBodies)
        {
            if (body == null || body == rootRigidbody)
                continue;

            recoveryBones[index++] = body.transform;
        }
    }

    public void EnterRagdoll(float duration, bool stayRagdolled = false)
    {
        if (permanentRagdoll && !stayRagdolled)
            return;

        if (!ragdolled)
        {
            SaveControlStates();
            DisableControl();
            ragdolled = true;

            if (animator != null)
                animator.enabled = false;

            EnableRagdollPhysics();
        }

        if (stayRagdolled)
            permanentRagdoll = true;

        if (recoverRoutine != null)
        {
            StopCoroutine(recoverRoutine);
            recoverRoutine = null;
        }

        if (!permanentRagdoll)
            recoverRoutine = StartCoroutine(RecoverAfter(duration));
    }

    public void ApplyForceFromPoint(Vector3 sourcePosition, float horizontalForce, float upwardForce)
    {
        if (!ragdolled)
            return;

        Rigidbody targetBody = hipsRigidbody;

        if (targetBody == null)
        {
            foreach (Rigidbody body in ragdollBodies)
            {
                if (body != null && body != rootRigidbody && !body.isKinematic)
                {
                    targetBody = body;
                    break;
                }
            }
        }

        if (targetBody == null)
            return;

        Vector3 direction = targetBody.worldCenterOfMass - sourcePosition;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.001f)
            direction = transform.forward;

        direction.Normalize();

        Vector3 impulse =
            direction * Mathf.Max(0f, horizontalForce) +
            Vector3.up * Mathf.Max(0f, upwardForce);

        targetBody.AddForce(impulse, ForceMode.Impulse);
    }

    IEnumerator RecoverAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        yield return new WaitForSeconds(getUpDelay);
        yield return StartCoroutine(WaitUntilSettled());

        if (!permanentRagdoll &&
            TryGetGroundPoint(out Vector3 groundPoint, out _))
        {
            yield return StartCoroutine(RecoverAtGround(groundPoint));
        }
    }

    IEnumerator WaitUntilSettled()
    {
        float settledFor = 0f;

        while (ragdolled)
        {
            if (IsSettledOnGround())
            {
                settledFor += Time.deltaTime;

                if (settledFor >= settleHoldTime)
                    yield break;
            }
            else
            {
                settledFor = 0f;
            }

            yield return null;
        }
    }

    public bool IsSettledOnGround()
    {
        if (!ragdolled)
            return true;

        if (!TryGetGroundPoint(out _, out float groundDistance))
            return false;

        if (groundDistance > maxHipsGroundDistance)
            return false;

        float speed =
            hipsRigidbody != null
                ? hipsRigidbody.linearVelocity.magnitude
                : GetAverageRagdollSpeed();

        return speed <= settleVelocity;
    }

    public IEnumerator WaitForRagdollToFinish()
    {
        if (!ragdolled)
            yield break;

        yield return StartCoroutine(WaitUntilSettled());
    }

    public void PrepareForRespawn()
    {
        if (recoverRoutine != null)
        {
            StopCoroutine(recoverRoutine);
            recoverRoutine = null;
        }

        permanentRagdoll = false;
        DisableRagdollPhysics();

        if (rootRigidbody != null)
        {
            rootRigidbody.isKinematic = false;
            ResetVelocity(rootRigidbody);
        }

        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }

        ragdolled = false;
    }

    IEnumerator RecoverAtGround(Vector3 groundPoint)
    {
        if (!ragdolled || permanentRagdoll)
            yield break;

        CaptureWorldPose(startPositions, startRotations);

        Vector3 forward = GetFlatFacingDirection();
        Vector3 position =
            groundPoint +
            Vector3.up * recoveryGroundOffset;

        DisableRagdollPhysics();

        if (rootCollider != null)
            rootCollider.enabled = false;

        if (rootRigidbody != null)
        {
            rootRigidbody.isKinematic = true;
            rootRigidbody.position = position;
            rootRigidbody.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }
        else
        {
            transform.position = position;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        RestoreWorldPose(startPositions, startRotations);

        if (animator != null)
        {
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);

            CaptureWorldPose(targetPositions, targetRotations);

            animator.enabled = false;
            RestoreWorldPose(startPositions, startRotations);

            float duration = Mathf.Max(0f, recoveryBlendTime);

            if (duration > 0f)
            {
                float time = 0f;

                while (time < duration)
                {
                    if (permanentRagdoll)
                        yield break;

                    time += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));

                    for (int i = 0; i < recoveryBones.Length; i++)
                    {
                        Transform bone = recoveryBones[i];

                        if (bone == null)
                            continue;

                        bone.position = Vector3.Lerp(startPositions[i], targetPositions[i], t);
                        bone.rotation = Quaternion.Slerp(startRotations[i], targetRotations[i], t);
                    }

                    yield return null;
                }
            }

            RestoreWorldPose(targetPositions, targetRotations);
            animator.enabled = true;
            animator.Update(0f);
        }

        if (rootCollider != null)
            rootCollider.enabled = true;

        if (rootRigidbody != null)
        {
            ResetVelocity(rootRigidbody);
            rootRigidbody.isKinematic = false;
        }

        ragdolled = false;
        RestoreControlStates();
        recoverRoutine = null;
    }

    void CaptureWorldPose(Vector3[] positions, Quaternion[] rotations)
    {
        for (int i = 0; i < recoveryBones.Length; i++)
        {
            Transform bone = recoveryBones[i];

            if (bone == null)
                continue;

            positions[i] = bone.position;
            rotations[i] = bone.rotation;
        }
    }

    void RestoreWorldPose(Vector3[] positions, Quaternion[] rotations)
    {
        for (int i = 0; i < recoveryBones.Length; i++)
        {
            Transform bone = recoveryBones[i];

            if (bone == null)
                continue;

            bone.position = positions[i];
            bone.rotation = rotations[i];
        }
    }

    Vector3 GetFlatFacingDirection()
    {
        Vector3 forward =
            hips != null
                ? hips.forward
                : transform.forward;

        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = transform.forward;
            forward.y = 0f;
        }

        return forward.normalized;
    }

    bool TryGetGroundPoint(out Vector3 groundPoint, out float groundDistance)
    {
        Vector3 origin =
            hips != null
                ? hips.position
                : transform.position + Vector3.up;

        origin += Vector3.up * 0.1f;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            Vector3.down,
            groundCheckDistance,
            ~0,
            QueryTriggerInteraction.Ignore
        );

        bool found = false;
        float closest = Mathf.Infinity;

        groundPoint = Vector3.zero;
        groundDistance = Mathf.Infinity;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider.transform.IsChildOf(transform))
                continue;

            if (hit.distance < closest)
            {
                closest = hit.distance;
                groundPoint = hit.point;
                groundDistance = hit.distance;
                found = true;
            }
        }

        return found;
    }

    float GetAverageRagdollSpeed()
    {
        float total = 0f;
        int count = 0;

        foreach (Rigidbody body in ragdollBodies)
        {
            if (body == null || body == rootRigidbody)
                continue;

            total += body.linearVelocity.magnitude;
            count++;
        }

        return count == 0 ? 0f : total / count;
    }

    void SaveControlStates()
    {
        if (playerController != null)
            playerWasEnabled = playerController.enabled;

        if (aiBehaviour != null)
            aiWasEnabled = aiBehaviour.enabled;

        if (combatController != null)
            combatWasEnabled = combatController.enabled;

        if (throwableInventory != null)
            throwableWasEnabled = throwableInventory.enabled;
    }

    void DisableControl()
    {
        if (playerController != null)
        {
            playerController.StopImmediately();
            playerController.enabled = false;
        }

        if (aiBehaviour != null)
            aiBehaviour.enabled = false;

        if (combatController != null)
            combatController.enabled = false;

        if (throwableInventory != null)
            throwableInventory.enabled = false;
    }

    void RestoreControlStates()
    {
        if (stats != null && stats.IsDead)
            return;

        if (playerController != null)
            playerController.enabled = playerWasEnabled;

        if (aiBehaviour != null)
            aiBehaviour.enabled = aiWasEnabled;

        if (combatController != null)
            combatController.enabled = combatWasEnabled;

        if (throwableInventory != null)
            throwableInventory.enabled = throwableWasEnabled;
    }

    void EnableRagdollPhysics()
    {
        if (rootCollider != null)
            rootCollider.enabled = false;

        if (rootRigidbody != null)
        {
            ResetVelocity(rootRigidbody);
            rootRigidbody.isKinematic = true;
        }

        foreach (Rigidbody body in ragdollBodies)
        {
            if (body == null || body == rootRigidbody)
                continue;

            ResetVelocity(body);
            body.isKinematic = false;
            body.useGravity = true;
        }

        foreach (Collider col in ragdollColliders)
        {
            if (col == null || col == rootCollider)
                continue;

            col.enabled = true;
        }
    }

    static void ResetVelocity(Rigidbody body)
    {
        if (body == null || body.isKinematic)
            return;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    void DisableRagdollPhysics()
    {
        foreach (Rigidbody body in ragdollBodies)
        {
            if (body == null || body == rootRigidbody)
                continue;

            ResetVelocity(body);
            body.isKinematic = true;
            body.useGravity = false;
        }

        foreach (Collider col in ragdollColliders)
        {
            if (col == null || col == rootCollider)
                continue;

            col.enabled = false;
        }

        if (rootCollider != null)
            rootCollider.enabled = true;
    }

    void IgnoreRagdollSelfCollisions()
    {
        for (int i = 0; i < ragdollColliders.Length; i++)
        {
            Collider a = ragdollColliders[i];

            if (a == null || a == rootCollider)
                continue;

            for (int j = i + 1; j < ragdollColliders.Length; j++)
            {
                Collider b = ragdollColliders[j];

                if (b == null || b == rootCollider)
                    continue;

                Physics.IgnoreCollision(a, b, true);
            }
        }
    }
}
