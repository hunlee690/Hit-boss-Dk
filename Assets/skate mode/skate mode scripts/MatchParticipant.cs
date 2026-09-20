using UnityEngine;
using TMPro;

public class MatchParticipant : MonoBehaviour
{
    [Header("Identity")]
    public string playerName = "Player";
    public bool isAI;

    [Header("Name Tag")]
    public TMP_Text nameText;

    [Header("Score")]
    public int kills;
    public int deaths;

    [Header("Optional")]
    [Tooltip("Assign ONLY the character/model object. Keep it visible during death ragdoll.")]
    public GameObject visualRoot;


    PlayerController playerController;
    SimpleAIPlayerBehaviour aiBehaviour;
    CombatController combatController;
    ThrowableInventory throwableInventory;

    Rigidbody rb;
    bool originalKinematic;


    void Awake()
    {
        if (nameText == null)
        {
            nameText =
                GetComponentInChildren<TMP_Text>(true);
        }


        playerController =
            GetComponent<PlayerController>();

        aiBehaviour =
            GetComponent<SimpleAIPlayerBehaviour>();

        combatController =
            GetComponent<CombatController>();

        throwableInventory =
            GetComponent<ThrowableInventory>();

        rb =
            GetComponent<Rigidbody>();


        if (rb != null)
        {
            originalKinematic =
                rb.isKinematic;
        }
    }


    // =====================================================
    // SETUP
    // =====================================================

    public void Setup(
        string newName,
        bool ai)
    {
        playerName =
            newName;

        isAI =
            ai;

        kills = 0;
        deaths = 0;


        RefreshName();

        SetAlive(true);
    }


    // =====================================================
    // NAME
    // =====================================================

    public void RefreshName()
    {
        if (nameText != null)
        {
            nameText.text =
                playerName;
        }
    }


    // =====================================================
    // SCORE
    // =====================================================

    public void AddKill()
    {
        kills++;
    }


    public void AddDeath()
    {
        deaths++;
    }


    // =====================================================
    // ALIVE / DEAD
    // =====================================================

    public void SetAlive(
        bool alive)
    {
        if (!alive)
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


            // IMPORTANT:
            // Do NOT disable colliders.
            // Do NOT hide the model.
            // Do NOT move the player.
            // Death ragdoll must remain real physics
            // where the player actually fell.
            return;
        }


        // =================================================
        // CALLED ONLY AFTER GAMEMODEMANAGER HAS:
        // 1. waited for ragdoll to settle
        // 2. disabled ragdoll physics
        // 3. moved root to respawn point
        // 4. reset CombatStats
        // =================================================

        if (visualRoot != null)
        {
            visualRoot.SetActive(
                true
            );
        }


        if (rb != null)
        {
            rb.isKinematic =
                originalKinematic;

            rb.linearVelocity =
                Vector3.zero;

            rb.angularVelocity =
                Vector3.zero;
        }


        if (combatController != null)
        {
            combatController.enabled =
                true;
        }


        if (throwableInventory != null)
        {
            throwableInventory.enabled =
                true;
        }


        if (playerController != null)
        {
            playerController.enabled =
                !isAI;
        }


        if (aiBehaviour != null)
        {
            aiBehaviour.enabled =
                isAI;
        }
    }
}
