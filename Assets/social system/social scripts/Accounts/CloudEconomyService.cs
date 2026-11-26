using System;
using HitBoss.Social;
using UnityEngine;

namespace HitBoss.Currency
{
    // Account-scoped economy facade used by gameplay, shop, spins, and rewarded ads.
    [DefaultExecutionOrder(-75)]
    [DisallowMultipleComponent]
    public sealed class CloudEconomyService : MonoBehaviour
    {
        public static CloudEconomyService Instance { get; private set; }
        public bool Ready => OnlineManager.Instance?.Ready == true;
        public int Coins => OnlineManager.Instance?.Profiles?.Current?.coins ?? 0;
        public int Gems => OnlineManager.Instance?.Profiles?.Current?.gems ?? 0;
        public event Action Changed;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (OnlineManager.Instance != null) OnlineManager.Instance.Changed += Notify;
        }

        public bool TryPurchase(string itemId, int coinCost, int gemCost, out string message)
        {
            if (!Ready) { message = "Account is still loading."; return false; }
            bool result = OnlineManager.Instance.Profiles.TryPurchaseItem(itemId, coinCost, gemCost, out message);
            if (result) OnlineManager.Instance.Flush();
            return result;
        }

        public void GrantMatchCoins(int amount)
        {
            if (!Ready || amount <= 0) return;
            OnlineManager.Instance.Profiles.RecordCurrency(amount, 0);
            OnlineManager.Instance.Flush();
        }

        public void GrantRewardedGems(int amount)
        {
            if (!Ready || amount <= 0) return;
            OnlineManager.Instance.Profiles.RecordCurrency(0, amount);
            OnlineManager.Instance.Flush();
        }

        void Notify() => Changed?.Invoke();

        void OnDestroy()
        {
            if (OnlineManager.Instance != null) OnlineManager.Instance.Changed -= Notify;
            if (Instance == this) Instance = null;
        }
    }
}
