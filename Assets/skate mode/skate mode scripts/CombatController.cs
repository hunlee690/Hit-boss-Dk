using UnityEngine;

public class CombatController : MonoBehaviour
{
    [Header("References")]
    public CombatStats stats;
    public PlayerController playerController;


    [Header("Slide")]
    public int slideStaminaCost = 2;
    public int slideDamage = 1;

    public float slideRagdollTime = 2f;

    [Tooltip("Horizontal knockback from a slide.")]
    public float slideForce = 2.5f;

    [Tooltip("Small upward lift from a slide.")]
    public float slideUpwardForce = 0.2f;


    [Header("Punch")]
    public int punchStaminaCost = 3;
    public int punchDamage = 3;

    public float punchRagdollTime = 1.5f;

    [Tooltip("Horizontal knockback from a punch.")]
    public float punchForce = 4.5f;

    [Tooltip("Small upward lift from a punch.")]
    public float punchUpwardForce = 0.5f;


    void Awake()
    {
        if (stats == null)
        {
            stats =
                GetComponent<CombatStats>();
        }


        if (playerController == null)
        {
            playerController =
                GetComponent<PlayerController>();
        }
    }


    // =====================================================
    // SLIDE
    // =====================================================

    public bool TrySlide()
    {
        if (stats == null ||
            stats.IsDead)
        {
            return false;
        }


        if (!stats.UseStamina(
            slideStaminaCost))
        {
            return false;
        }


        if (playerController != null)
        {
            playerController.PlayCombatAnimation(
                "slide"
            );
        }


        return true;
    }


    // =====================================================
    // PUNCH
    // =====================================================

    public bool TryPunch()
    {
        if (stats == null ||
            stats.IsDead)
        {
            return false;
        }


        if (!stats.UseStamina(
            punchStaminaCost))
        {
            return false;
        }


        if (playerController != null)
        {
            playerController.PlayCombatAnimation(
                "punch"
            );
        }


        return true;
    }


    // =====================================================
    // SLIDE HIT
    // =====================================================

    public void DealSlideDamage(
        CombatStats target)
    {
        if (GetComponentInParent<HitBoss.Multiplayer.Skate.SkateCombat>()?.Active == true) return;
        if (target == null ||
            target == stats)
        {
            return;
        }


        RagdollController ragdoll =
            target.GetComponentInParent<RagdollController>();


        bool wasRagdolled =
            ragdoll != null &&
            ragdoll.IsRagdolled;


        target.TakeDamage(
            slideDamage,
            stats
        );


        if (ragdoll == null ||
            wasRagdolled)
        {
            return;
        }


        // If this hit killed the target, CombatStats has
        // already entered permanent ragdoll.
        if (!target.IsDead)
        {
            ragdoll.EnterRagdoll(
                slideRagdollTime
            );
        }


        ragdoll.ApplyForceFromPoint(
            transform.position,
            slideForce,
            slideUpwardForce
        );
    }


    // =====================================================
    // PUNCH HIT
    // =====================================================

    public void DealPunchDamage(
        CombatStats target)
    {
        if (GetComponentInParent<HitBoss.Multiplayer.Skate.SkateCombat>()?.Active == true) return;
        if (target == null ||
            target == stats)
        {
            return;
        }


        RagdollController ragdoll =
            target.GetComponentInParent<RagdollController>();


        bool wasRagdolled =
            ragdoll != null &&
            ragdoll.IsRagdolled;


        target.TakeDamage(
            punchDamage,
            stats
        );


        if (ragdoll == null ||
            wasRagdolled)
        {
            return;
        }


        if (!target.IsDead)
        {
            ragdoll.EnterRagdoll(
                punchRagdollTime
            );
        }


        ragdoll.ApplyForceFromPoint(
            transform.position,
            punchForce,
            punchUpwardForce
        );
    }
}
