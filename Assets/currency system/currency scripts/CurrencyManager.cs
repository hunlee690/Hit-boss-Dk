using System;
using HitBoss.Social;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HitBoss.Currency
{
    [DefaultExecutionOrder(-80)]
    public sealed class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }
        [Min(0)] public int coinsPerKill = 10;
        public int Coins => OnlineManager.Instance?.Profiles?.Current?.coins ?? 0;
        public int Gems => OnlineManager.Instance?.Profiles?.Current?.gems ?? 0;
        public event Action Changed;
        TMP_Text coinText, gemText;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            SceneManager.sceneLoaded += SceneLoaded;
        }
        void Start()
        {
            if (OnlineManager.Instance != null) OnlineManager.Instance.Changed += Refresh;
            BindDisplay();
            Refresh();
        }
        public void AwardKillCoins(int killCount = 1)
        {
            if (killCount <= 0 || OnlineManager.Instance?.Profiles == null) return;
            OnlineManager.Instance.Profiles.RecordCurrency(killCount * coinsPerKill, 0);
        }
        void SceneLoaded(Scene scene, LoadSceneMode mode) { BindDisplay(); Refresh(); }
        void BindDisplay()
        {
            coinText = FindLabel("coin text");
            gemText = FindLabel("gem text");
        }
        static TMP_Text FindLabel(string objectName)
        {
            foreach (var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (text.name == objectName) return text;
            return null;
        }
        void Refresh()
        {
            if (coinText != null) coinText.text = Coins.ToString("N0");
            if (gemText != null) gemText.text = Gems.ToString("N0");
            Changed?.Invoke();
        }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (OnlineManager.Instance != null) OnlineManager.Instance.Changed -= Refresh;
            if (Instance == this) Instance = null;
        }
    }
}
