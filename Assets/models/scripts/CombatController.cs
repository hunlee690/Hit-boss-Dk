using UnityEngine;

public class CombatController : MonoBehaviour
{
    [Header("References")]
    public CombatStats stats;
    public PlayerController playerController;


    [Header("Slide")]
    public int slideStaminaCost = 2;
    public int slideDamage = 1;


    [Header("Punch")]
    public int punchStaminaCost = 3;
    public int punchDamage = 3;


    void Awake()
    {
        if (stats == null)
            stats = GetComponent<CombatStats>();

        if (playerController == null)
            playerController =
                GetComponent<PlayerController>();
    }


    // =====================================================
    // SLIDE
    // =====================================================

    public bool TrySlide()
    {
        if (stats == null ||
            stats.IsDead)
            return false;


        if (!stats.UseStamina(
            slideStaminaCost))
        {
            Debug.Log(
                gameObject.name +
                " doesn't have enough stamina to slide."
            );

            return false;
        }


        if (playerController != null)
            playerController.PlayCombatAnimation("slide");


        return true;
    }


    // =====================================================
    // PUNCH
    // =====================================================

    public bool TryPunch()
    {
        if (stats == null ||
            stats.IsDead)
            return false;


        if (!stats.UseStamina(
            punchStaminaCost))
        {
            Debug.Log(
                gameObject.name +
                " doesn't have enough stamina to punch."
            );

            return false;
        }


        if (playerController != null)
            playerController.PlayCombatAnimation("punch");


        return true;
    }


    // =====================================================
    // DAMAGE
    // =====================================================

    public void DealSlideDamage(
        CombatStats target)
    {
        if (target == null ||
            target == stats)
            return;


        target.TakeDamage(
            slideDamage,
            stats
        );
    }


    public void DealPunchDamage(
        CombatStats target)
    {
        if (target == null ||
            target == stats)
            return;


        target.TakeDamage(
            punchDamage,
            stats
        );
    }
}