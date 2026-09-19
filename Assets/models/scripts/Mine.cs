using System.Collections.Generic;
using UnityEngine;

public class Mine : MonoBehaviour
{
    [Header("Mine")]
    public float armDelay = 0.75f;
    public float explosionRadius = 3f;
    public int damage = 3;

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
            Destroy(gameObject, lifeTime);
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
        {
            Debug.LogWarning(
                "Mine has no owner assigned!"
            );

            return;
        }


        Collider[] ownerColliders =
            owner.GetComponentsInChildren<Collider>(
                true
            );


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


    // =====================================================
    // ARM
    // =====================================================

    void Arm()
    {
        armed = true;
    }


    // =====================================================
    // TRIGGER
    // =====================================================

    void OnTriggerEnter(
        Collider other)
    {
        if (!armed ||
            exploded)
            return;


        CombatStats target =
            other.GetComponentInParent<CombatStats>();


        if (target == null ||
            target.IsDead)
            return;


        // Owner can NEVER trigger own mine.
        if (target == owner)
            return;


        Explode();
    }


    // =====================================================
    // EXPLOSION
    // =====================================================

    void Explode()
    {
        if (exploded)
            return;


        exploded = true;


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