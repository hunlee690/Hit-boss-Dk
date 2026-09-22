using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    [Header("Explosion")]
    public float impactExplosionDelay = 0.1f;
    public float explosionRadius = 3f;
    public int damage = 3;


    [Header("Ragdoll")]
    public float ragdollTime = 2.5f;

    [Tooltip("Horizontal blast knockback.")]
    public float knockbackForce = 8.5f;

    [Tooltip("Upward part of the blast.")]
    public float upwardForce = 2f;


    [Header("Optional Effect")]
    public GameObject explosionEffectPrefab;


    CombatStats owner;
    Collider[] bombColliders;

    bool exploding;


    void Awake()
    {
        bombColliders =
            GetComponentsInChildren<Collider>(
                true
            );


        Rigidbody rb =
            GetComponent<Rigidbody>();


        if (rb != null)
        {
            rb.collisionDetectionMode =
                CollisionDetectionMode.ContinuousDynamic;

            rb.interpolation =
                RigidbodyInterpolation.Interpolate;
        }
    }


    // =====================================================
    // OWNER
    // =====================================================

    public void SetOwner(
        CombatStats newOwner)
    {
        owner =
            newOwner;


        IgnoreOwnerCollisions();
    }


    void IgnoreOwnerCollisions()
    {
        if (owner == null)
            return;


        Collider[] ownerColliders =
            owner.GetComponentsInChildren<Collider>(
                true
            );


        foreach (Collider bombCollider
                 in bombColliders)
        {
            if (bombCollider == null)
                continue;


            foreach (Collider ownerCollider
                     in ownerColliders)
            {
                if (ownerCollider != null)
                {
                    Physics.IgnoreCollision(
                        bombCollider,
                        ownerCollider,
                        true
                    );
                }
            }
        }
    }


    // =====================================================
    // IMPACT
    // =====================================================

    void OnCollisionEnter(
        Collision collision)
    {
        if (exploding)
            return;


        CombatStats hitPlayer =
            collision.collider
                .GetComponentInParent<CombatStats>();


        if (hitPlayer == owner)
            return;


        exploding = true;


        StartCoroutine(
            ExplodeAfterImpact()
        );
    }


    IEnumerator ExplodeAfterImpact()
    {
        yield return
            new WaitForSeconds(
                impactExplosionDelay
            );


        Explode();
    }


    // =====================================================
    // EXPLOSION
    // =====================================================

    void Explode()
    {
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
            {
                continue;
            }


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
            {
                continue;
            }


            if (!target.IsDead)
            {
                ragdoll.EnterRagdoll(
                    ragdollTime
                );
            }


            // Killing hits also receive the blast force
            // because CombatStats already enabled death ragdoll.
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
