using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bomb : MonoBehaviour
{
    [Header("Explosion")]
    public float impactExplosionDelay = 0.1f;
    public float explosionRadius = 3f;
    public int damage = 3;

    [Header("Optional Effect")]
    public GameObject explosionEffectPrefab;


    CombatStats owner;

    Collider[] bombColliders;

    bool exploding;


    void Awake()
    {
        bombColliders =
            GetComponentsInChildren<Collider>(true);


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


        // Completely ignore collision with the person
        // who threw the bomb.
        IgnoreOwnerCollisions();
    }


    void IgnoreOwnerCollisions()
    {
        if (owner == null)
            return;


        Collider[] ownerColliders =
            owner.GetComponentsInChildren<Collider>(true);


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


        // Safety check:
        // never explode from touching owner.
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
        if (explosionEffectPrefab != null)
        {
            Instantiate(
                explosionEffectPrefab,
                transform.position,
                Quaternion.identity
            );
        }


        Collider[] hits =
            Physics.OverlapSphere(
                transform.position,
                explosionRadius
            );


        HashSet<CombatStats> damaged =
            new HashSet<CombatStats>();


        foreach (Collider hit in hits)
        {
            CombatStats target =
                hit.GetComponentInParent<CombatStats>();


            if (target == null ||
                target == owner ||
                target.IsDead)
                continue;


            if (!damaged.Add(target))
                continue;


            target.TakeDamage(
                damage,
                owner
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