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
    [Tooltip("Assign ONLY the character/model object if you want it hidden while dead.")]
    public GameObject visualRoot;


    PlayerController playerController;
    SimpleAIPlayerBehaviour aiBehaviour;

    Rigidbody rb;
    Collider[] colliders;

    bool originalKinematic;


    void Awake()
    {
        if (nameText == null)
            nameText =
                GetComponentInChildren<TMP_Text>(true);


        playerController =
            GetComponent<PlayerController>();


        aiBehaviour =
            GetComponent<SimpleAIPlayerBehaviour>();


        rb =
            GetComponent<Rigidbody>();


        colliders =
            GetComponentsInChildren<Collider>(true);


        if (rb != null)
            originalKinematic = rb.isKinematic;
    }


    // =====================================================
    // SETUP
    // =====================================================

    public void Setup(
        string newName,
        bool ai)
    {
        playerName = newName;
        isAI = ai;

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
            nameText.text = playerName;
    }


    // =====================================================
    // SCORE
    // =====================================================

    public void AddKill()
    {
        kills++;

        Debug.Log(
            playerName +
            " now has " +
            kills +
            " kills."
        );
    }


    public void AddDeath()
    {
        deaths++;
    }


    // =====================================================
    // ALIVE / DEAD
    // =====================================================

    public void SetAlive(bool alive)
    {
        // -------------------------
        // HUMAN CONTROL
        // -------------------------

        if (playerController != null)
        {
            if (!alive)
                playerController.StopImmediately();

            playerController.enabled =
                alive && !isAI;
        }


        // -------------------------
        // AI CONTROL
        // -------------------------

        if (aiBehaviour != null)
        {
            aiBehaviour.enabled =
                alive && isAI;
        }


        // -------------------------
        // COLLIDERS
        // -------------------------

        if (colliders != null)
        {
            foreach (Collider col in colliders)
            {
                if (col != null)
                    col.enabled = alive;
            }
        }


        // -------------------------
        // RIGIDBODY
        // -------------------------

        if (rb != null)
        {
            rb.linearVelocity =
                Vector3.zero;

            rb.angularVelocity =
                Vector3.zero;


            if (alive)
            {
                rb.isKinematic =
                    originalKinematic;
            }
            else
            {
                rb.isKinematic = true;
            }
        }


        // -------------------------
        // OPTIONAL MODEL HIDE
        // -------------------------

        if (visualRoot != null)
            visualRoot.SetActive(alive);
    }
}