using System;
using System.Text;
using UnityEngine;
using TMPro;

public class MatchHUD : MonoBehaviour
{
    [Header("Match UI")]
    public TMP_Text timerText, rankingText;

    [Header("Throwable")]
    public TMP_Text throwableText;

    [Header("Player Stats")]
    public TMP_Text healthText, staminaText;

    [Header("End Match")]
    public GameObject endPanel;
    public TMP_Text resultText;

    bool endShown, matchStarted;
    CombatStats localStats;
    ThrowableInventory localInventory;

    void Start()
    {
        if (endPanel != null) endPanel.SetActive(false);
    }

    void Update()
    {
        var manager = GameModeManager.Instance;
        if (manager == null) return;

        UpdateTimer(manager.RemainingTime);
        UpdateRanking(manager.Participants);
        FindLocalPlayer(manager.Participants);
        UpdatePlayerStats();
        UpdateThrowable();

        if (manager.MatchRunning)
            matchStarted = true;

        if (matchStarted && !manager.MatchRunning && !endShown)
            ShowResults(manager.Participants);
    }

    void FindLocalPlayer(MatchParticipant[] players)
    {
        if (localStats != null || players == null) return;

        foreach (var player in players)
        {
            if (player == null || player.isAI) continue;

            localStats = player.GetComponent<CombatStats>();
            if (localStats == null) localStats = player.GetComponentInChildren<CombatStats>();

            localInventory = player.GetComponent<ThrowableInventory>();
            if (localInventory == null) localInventory = player.GetComponentInChildren<ThrowableInventory>();

            break;
        }
    }

    void UpdatePlayerStats()
    {
        if (localStats == null) return;

        if (healthText != null)
            healthText.text = "HEALTH: " + localStats.Health + "/" + localStats.maxHealth;

        if (staminaText != null)
            staminaText.text = "STAMINA: " + localStats.Stamina + "/" + localStats.maxStamina;
    }

    void UpdateThrowable()
    {
        if (throwableText == null) return;

        if (localInventory == null)
        {
            throwableText.text = "ITEM: EMPTY";
            return;
        }

        switch (localInventory.CurrentItem)
        {
            case ThrowableInventory.ThrowableType.Bomb:
                throwableText.text = "ITEM: BOMB";
                break;

            case ThrowableInventory.ThrowableType.Mine:
                throwableText.text = "ITEM: MINE x" + localInventory.CurrentCount;
                break;

            default:
                throwableText.text = "ITEM: EMPTY";
                break;
        }
    }

    void UpdateTimer(float time)
    {
        if (timerText == null) return;

        int totalSeconds = Mathf.CeilToInt(time);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        timerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
    }

    void UpdateRanking(MatchParticipant[] players)
    {
        if (rankingText == null || players == null) return;

        var sorted = (MatchParticipant[])players.Clone();

        Array.Sort(sorted, (a, b) =>
        {
            if (a == null) return 1;
            if (b == null) return -1;

            int kills = b.kills.CompareTo(a.kills);
            return kills != 0 ? kills : a.deaths.CompareTo(b.deaths);
        });

        var text = new StringBuilder();
        int rank = 1;

        foreach (var player in sorted)
        {
            if (player == null) continue;
            text.Append(rank++).Append(". ").Append(player.playerName).Append("     ").Append(player.kills).AppendLine();
        }

        rankingText.text = text.ToString();
    }

    void ShowResults(MatchParticipant[] players)
    {
        endShown = true;

        if (endPanel != null)
            endPanel.SetActive(true);

        if (players == null || players.Length == 0 || resultText == null)
            return;

        MatchParticipant winner = null;

        foreach (var player in players)
        {
            if (player == null) continue;

            if (winner == null ||
                player.kills > winner.kills ||
                player.kills == winner.kills && player.deaths < winner.deaths)
            {
                winner = player;
            }
        }

        if (winner != null)
            resultText.text = winner.playerName + " WINS!\n" + winner.kills + " KILLS";
    }
}