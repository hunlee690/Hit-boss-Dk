using UnityEngine;
using TMPro;

public class MatchParticipant : MonoBehaviour
{
    public string playerName = "Player";
    public bool isAI;
    public TMP_Text nameText;
    public int kills, deaths;
    public GameObject visualRoot;

    public void RefreshName()
    {
        if (nameText != null) nameText.text = playerName;
    }
}
