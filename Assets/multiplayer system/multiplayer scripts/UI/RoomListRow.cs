using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitBoss.Multiplayer
{
    public sealed class RoomListRow : MonoBehaviour
    {
        public TMP_Text title, detail;
        public Button action, secondary;
        public void Bind(string name, string description, string actionText = null, Action clicked = null, Action dismissed = null)
        {
            title.text = name; detail.text = description;
            action.gameObject.SetActive(clicked != null);
            action.onClick.RemoveAllListeners();
            if (clicked != null) { action.GetComponentInChildren<TMP_Text>().text = actionText; action.onClick.AddListener(() => clicked()); }
            secondary.gameObject.SetActive(dismissed != null); secondary.onClick.RemoveAllListeners();
            if (dismissed != null) secondary.onClick.AddListener(() => dismissed());
        }
    }
}
