using System.Collections.Generic;
using UnityEngine;

public class Mine : MonoBehaviour
{
    [Header("Mine")]
    public float armDelay = 0.75f;
    public float explosionRadius = 3f;
    public int damage = 3;

    [Header("Prefab Placement")]
    [Tooltip("Extra LOCAL position offset applied after the mine is spawned.")]
    public Vector3 positionOffset = Vector3.zero;

    [Tooltip("Extra LOCAL rotation applied after the mine is spawned.")]
    public Vector3 rotationOffset = Vector3.zero;

    [Header("Ragdoll")]
    public float ragdollTime = 2.5f;
    public float knockbackForce = 7f;
    public float upwardForce = 1.5f;

    [Header("Optional Effect")]
    public GameObject explosionEffectPrefab;

    [Header("Lifetime")]
    public float lifeTime = 15f;


    CombatStats owner;
    Collider[] mineColliders;

    bool armed;
    bool exploded;


    void Awake()
    {
        mineColliders =
            GetComponentsInChildren<Collider>(true);

        ApplyPrefabTransform();

        Destroy(
            gameObject,
            lifeTime
        );
    }


    void ApplyPrefabTransform()
    {
        transform.rotation *=
            Quaternion.Euler(
                rotationOffset
            );

        transform.position +=
            transform.TransformDirection(
                positionOffset
            );
    }


    public void SetOwner(
        CombatStats newOwner)
    {
        owner =
            newOwner;

        IgnoreOwnerCollisions();

        CancelInvoke(
            nameof(Arm)
        );

        Invoke(
            nameof(Arm),
            armDelay
        );
    }


    void IgnoreOwnerCollisions()
    {
        if (owner == null)
            return;

        Collider[] ownerColliders =
            owner.GetComponentsInChildren<Collider>(true);

        foreach (Collider mineCollider
                 in mineColliders)
        {
            if (mineCollider == null)
                continue;

            foreach (Collider ownerCollider
                     in ownerColliders)
            {
                if (ownerCollider != null)
                {
                    Physics.IgnoreCollision(
                        mineCollider,
                        ownerCollider,
                        true
                    );
                }
            }
        }
    }


    void Arm()
    {
        armed = true;
    }


    void OnTriggerEnter(
        Collider other)
    {
        if (!armed ||
            exploded)
            return;

        CombatStats target =
            other.GetComponentInParent<CombatStats>();

        if (target == null ||
            target.IsDead ||
            target == owner)
            return;

        Explode();
    }


    void Explode()
    {
        if (exploded)
            return;

        exploded = true;

        Vector3 explosionPoint =
            transform.position;

        if (explosionEffectPrefab != null)
        {
            Instantiate(
                explosionEffectPrefab,
                explosionPoint,
                Quaternion.identity
            );
        }

        Collider[] hits =
            Physics.OverlapSphere(
                explosionPoint,
                explosionRadius
            );

        HashSet<CombatStats> affected =
            new HashSet<CombatStats>();

        foreach (Collider hit in hits)
        {
            CombatStats target =
                hit.GetComponentInParent<CombatStats>();

            if (target == null ||
                target == owner ||
                target.IsDead)
                continue;

            if (!affected.Add(target))
                continue;

            RagdollController ragdoll =
                target.GetComponentInParent<RagdollController>();

            bool wasRagdolled =
                ragdoll != null &&
                ragdoll.IsRagdolled;

            target.TakeDamage(
                damage,
                owner
            );

            if (ragdoll == null ||
                wasRagdolled)
                continue;

            if (!target.IsDead)
            {
                ragdoll.EnterRagdoll(
                    ragdollTime
                );
            }

            ragdoll.ApplyForceFromPoint(
                explosionPoint,
                knockbackForce,
                upwardForce
            );
        }

        Destroy(gameObject);
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(
            transform.position,
            explosionRadius
        );
    }
}
