using UnityEngine;
using TMPro;

public class MatchParticipant : MonoBehaviour
{
    public string playerName = "Player";
    public bool isAI;
    public TMP_Text nameText;
    public int kills, deaths;
    void Awake() { if (nameText == null) nameText = GetComponentInChildren<TMP_Text>(true); }
    public void RefreshName() { if (nameText != null) nameText.text = playerName; }
}
