using System.Collections;
using UnityEngine;

public class GameModeManager : MonoBehaviour
{
    public static GameModeManager Instance { get; private set; }


    [Header("Players")]
    public GameObject aiPlayerPrefab;
    public int playerCount = 5;


    [Header("Spawn Points")]
    public Transform[] spawnPoints;


    [Header("Respawn")]
    public float respawnDelay = 2f;


    [Header("Match")]
    public float matchDuration = 180f;


    [Header("Names")]
    public string humanPlayerName = "Neo";

    public string[] aiNames =
    {
        "Rex",
        "Max",
        "Kai",
        "Zed"
    };


    MatchParticipant[] participants;

    float remainingTime;
    bool matchRunning;


    public MatchParticipant[] Participants =>
        participants;

    public float RemainingTime =>
        remainingTime;

    public bool MatchRunning =>
        matchRunning;


    // =====================================================
    // AWAKE
    // =====================================================

    void Awake()
    {
        Instance = this;
    }


    // =====================================================
    // START
    // =====================================================

    void Start()
    {
        SetupMatch();

        remainingTime =
            matchDuration;

        matchRunning = true;
    }


    // =====================================================
    // UPDATE
    // =====================================================

    void Update()
    {
        if (!matchRunning)
            return;


        remainingTime -=
            Time.deltaTime;


        if (remainingTime <= 0f)
        {
            remainingTime = 0f;

            EndMatch();
        }
    }


    // =====================================================
    // SETUP MATCH
    // =====================================================

    void SetupMatch()
    {
        if (spawnPoints == null ||
            spawnPoints.Length < playerCount)
        {
            Debug.LogError(
                "Need at least " +
                playerCount +
                " spawn points."
            );

            return;
        }


        if (aiPlayerPrefab == null)
        {
            Debug.LogError(
                "AI Player Prefab is missing!"
            );

            return;
        }


        // =================================================
        // HUMAN
        // =================================================

        PlayerController humanController =
            FindHumanPlayer();


        if (humanController == null)
        {
            Debug.LogError(
                "Human player not found!"
            );

            return;
        }


        GameObject humanPlayer =
            humanController.gameObject;


        MatchParticipant human =
            humanPlayer.GetComponent<MatchParticipant>();


        if (human == null)
        {
            human =
                humanPlayer.AddComponent<MatchParticipant>();
        }


        human.Setup(
            humanPlayerName,
            false
        );


        MoveToSpawn(
            humanPlayer,
            spawnPoints[0]
        );


        // =================================================
        // AI
        // =================================================

        for (int i = 1;
             i < playerCount;
             i++)
        {
            Transform spawn =
                spawnPoints[i];


            if (spawn == null)
                continue;


            GameObject ai =
                Instantiate(
                    aiPlayerPrefab,
                    spawn.position,
                    spawn.rotation
                );


            ai.name =
                "AI_" + i;


            PlayerController controller =
                ai.GetComponent<PlayerController>();


            if (controller != null)
                controller.enabled = false;


            PlayerCamera camera =
                ai.GetComponentInChildren<PlayerCamera>(
                    true
                );


            if (camera != null)
                camera.enabled = false;


            MatchParticipant participant =
                ai.GetComponent<MatchParticipant>();


            if (participant == null)
            {
                participant =
                    ai.GetComponentInChildren<MatchParticipant>(
                        true
                    );
            }


            if (participant == null)
            {
                participant =
                    ai.AddComponent<MatchParticipant>();
            }


            string aiName =
                i - 1 < aiNames.Length
                    ? aiNames[i - 1]
                    : "Bot " + i;


            participant.Setup(
                aiName,
                true
            );
        }


        participants =
            FindObjectsByType<MatchParticipant>(
                FindObjectsSortMode.None
            );


        Debug.Log(
            "Match created with " +
            participants.Length +
            " players."
        );
    }


    // =====================================================
    // DEATH
    // =====================================================

    public void HandleDeath(
        CombatStats victimStats,
        CombatStats attackerStats)
    {
        if (!matchRunning ||
            victimStats == null)
            return;


        MatchParticipant victim =
            victimStats.GetComponentInParent<MatchParticipant>();


        MatchParticipant attacker =
            attackerStats != null
                ? attackerStats.GetComponentInParent<MatchParticipant>()
                : null;


        if (victim == null)
            return;


        victim.AddDeath();


        if (attacker != null &&
            attacker != victim)
        {
            attacker.AddKill();


            Debug.Log(
                attacker.playerName +
                " eliminated " +
                victim.playerName
            );
        }


        victim.SetAlive(false);


        StartCoroutine(
            RespawnRoutine(
                victim,
                victimStats
            )
        );
    }


    // =====================================================
    // RESPAWN
    // =====================================================

    IEnumerator RespawnRoutine(
        MatchParticipant participant,
        CombatStats stats)
    {
        yield return
            new WaitForSeconds(
                respawnDelay
            );


        // Match may have ended while waiting.
        if (!matchRunning)
            yield break;


        Transform spawn =
            GetRandomSpawnPoint();


        if (spawn != null)
        {
            MoveToSpawn(
                participant.gameObject,
                spawn
            );
        }


        stats.ResetStats();

        participant.SetAlive(true);
    }


    Transform GetRandomSpawnPoint()
    {
        if (spawnPoints == null ||
            spawnPoints.Length == 0)
            return null;


        return spawnPoints[
            Random.Range(
                0,
                spawnPoints.Length
            )
        ];
    }


    // =====================================================
    // END MATCH
    // =====================================================

    void EndMatch()
    {
        if (!matchRunning)
            return;


        matchRunning = false;


        if (participants == null)
            return;


        foreach (MatchParticipant player
                 in participants)
        {
            if (player != null)
                player.SetAlive(false);
        }


        Debug.Log(
            "MATCH FINISHED"
        );
    }


    // =====================================================
    // HUMAN
    // =====================================================

    PlayerController FindHumanPlayer()
    {
        PlayerController[] controllers =
            FindObjectsByType<PlayerController>(
                FindObjectsSortMode.None
            );


        foreach (PlayerController controller
                 in controllers)
        {
            if (controller != null &&
                controller.enabled)
            {
                return controller;
            }
        }


        return null;
    }


    // =====================================================
    // SPAWN
    // =====================================================

    void MoveToSpawn(
        GameObject player,
        Transform spawn)
    {
        if (player == null ||
            spawn == null)
            return;


        Rigidbody rb =
            player.GetComponent<Rigidbody>();


        if (rb != null)
        {
            rb.linearVelocity =
                Vector3.zero;

            rb.angularVelocity =
                Vector3.zero;

            rb.position =
                spawn.position;

            rb.rotation =
                spawn.rotation;
        }
        else
        {
            player.transform.SetPositionAndRotation(
                spawn.position,
                spawn.rotation
            );
        }
    }
}