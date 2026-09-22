using System.Collections.Generic;
using UnityEngine;

public class CombatHitDetector : MonoBehaviour
{
    [Header("References")]
    public CombatController combatController;

    [Header("Hit Points")]
    public Transform punchHitPoint;
    public Transform slideHitPoint;

    [Header("Hit Range")]
    public float punchRadius = 0.8f;
    public float slideRadius = 1f;

    [Header("Targets")]
    public LayerMask playerLayers = ~0;


    bool slideActive;

    readonly HashSet<CombatStats> slideTargetsHit =
        new HashSet<CombatStats>();


    void Awake()
    {
        if (combatController == null)
        {
            combatController =
                GetComponentInParent<CombatController>();
        }
    }


    void Update()
    {
        // Keep checking during the entire slide attack window.
        if (slideActive)
        {
            CheckSlideHits();
        }
    }


    // =====================================================
    // PUNCH
    // =====================================================

    public void PunchHit()
    {
        if (combatController == null ||
            punchHitPoint == null)
            return;


        Collider[] hits =
            Physics.OverlapSphere(
                punchHitPoint.position,
                punchRadius,
                playerLayers,
                QueryTriggerInteraction.Collide
            );


        HashSet<CombatStats> damaged =
            new HashSet<CombatStats>();


        foreach (Collider hit in hits)
        {
            CombatStats target =
                hit.GetComponentInParent<CombatStats>();


            if (target == null)
                continue;


            if (target ==
                combatController.stats)
                continue;


            if (!damaged.Add(target))
                continue;


            combatController.DealPunchDamage(
                target
            );
        }
    }


    // =====================================================
    // SLIDE START
    // =====================================================

    public void StartSlideHit()
    {
        slideTargetsHit.Clear();

        slideActive = true;
    }


    // =====================================================
    // SLIDE END
    // =====================================================

    public void EndSlideHit()
    {
        slideActive = false;

        slideTargetsHit.Clear();
    }


    // =====================================================
    // SLIDE DAMAGE
    // =====================================================

    void CheckSlideHits()
    {
        if (combatController == null ||
            slideHitPoint == null)
            return;


        Collider[] hits =
            Physics.OverlapSphere(
                slideHitPoint.position,
                slideRadius,
                playerLayers,
                QueryTriggerInteraction.Collide
            );


        foreach (Collider hit in hits)
        {
            CombatStats target =
                hit.GetComponentInParent<CombatStats>();


            if (target == null)
                continue;


            // Don't damage ourselves.
            if (target ==
                combatController.stats)
                continue;


            // Already damaged this enemy
            // during this particular slide.
            if (slideTargetsHit.Contains(target))
                continue;


            slideTargetsHit.Add(target);


            combatController.DealSlideDamage(
                target
            );
        }
    }


    // =====================================================
    // DEBUG
    // =====================================================

    void OnDrawGizmosSelected()
    {
        if (punchHitPoint != null)
        {
            Gizmos.DrawWireSphere(
                punchHitPoint.position,
                punchRadius
            );
        }


        if (slideHitPoint != null)
        {
            Gizmos.DrawWireSphere(
                slideHitPoint.position,
                slideRadius
            );
        }
    }
}