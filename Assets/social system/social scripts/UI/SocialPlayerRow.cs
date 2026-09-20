using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitBoss.Social
{
    public sealed class SocialPlayerRow : MonoBehaviour
    {
        public TMP_Text username;
        public TMP_Text detail;
        public Image presenceDot;
        public Button profileButton;
        public Button primaryButton;
        public Button secondaryButton;
        public TMP_Text primaryLabel;
        public TMP_Text secondaryLabel;
        public void Bind(SocialPlayer player, string primary, string secondary, Action visit, Action action, Action other)
        {
            username.text = player.username;
            detail.text = player.presence;
            presenceDot.color = player.online ? new Color(.35f, .9f, .6f) : new Color(.5f, .55f, .62f);
            profileButton.onClick.RemoveAllListeners(); profileButton.onClick.AddListener(() => visit());
            primaryLabel.text = primary; secondaryLabel.text = secondary;
            primaryButton.gameObject.SetActive(!string.IsNullOrEmpty(primary));
            secondaryButton.gameObject.SetActive(!string.IsNullOrEmpty(secondary));
            primaryButton.interactable = action != null;
            primaryButton.onClick.RemoveAllListeners(); if (action != null) primaryButton.onClick.AddListener(() => action());
            secondaryButton.onClick.RemoveAllListeners(); if (other != null) secondaryButton.onClick.AddListener(() => other());
        }
    }
}
