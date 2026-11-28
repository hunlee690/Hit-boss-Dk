using HitBoss.Authentication;
using HitBoss.Currency;
using Unity.Services.LevelPlay;
using UnityEngine;

namespace HitBoss.Ads
{
    [DisallowMultipleComponent]
    public sealed class LevelPlayAdsManager : MonoBehaviour
    {
        public static LevelPlayAdsManager Instance { get; private set; }

        [Header("LevelPlay Dashboard IDs")]
        [SerializeField] string androidAppKey;
        [SerializeField] string rewardedGemAdUnitId;
        [SerializeField] bool consentGranted;
        [Header("Rewards")]
        [SerializeField, Min(1)] int gemsPerReward = 5;

        public bool Ready => rewardedAd != null && rewardedAd.IsAdReady();
        public string Status { get; private set; } = "Ads are not configured.";
        LevelPlayRewardedAd rewardedAd;
        bool rewardGrantedThisShow;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            if (string.IsNullOrWhiteSpace(androidAppKey) || string.IsNullOrWhiteSpace(rewardedGemAdUnitId)) return;
            LevelPlay.SetConsent(consentGranted);
            LevelPlay.OnInitSuccess += OnInitialized;
            LevelPlay.OnInitFailed += OnInitializationFailed;
            string playerId = AuthenticationManager.Instance?.PlayerId;
            LevelPlay.Init(androidAppKey, string.IsNullOrWhiteSpace(playerId) ? null : playerId);
            Status = "Preparing ads...";
        }

        void OnInitialized(LevelPlayConfiguration configuration)
        {
            rewardedAd = new LevelPlayRewardedAd(rewardedGemAdUnitId);
            rewardedAd.OnAdLoaded += OnLoaded;
            rewardedAd.OnAdLoadFailed += OnLoadFailed;
            rewardedAd.OnAdDisplayFailed += OnDisplayFailed;
            rewardedAd.OnAdRewarded += OnRewarded;
            rewardedAd.OnAdClosed += OnClosed;
            rewardedAd.LoadAd();
        }

        public void ShowRewardedGems()
        {
            if (rewardedAd == null || !rewardedAd.IsAdReady())
            {
                Status = "Ad is still loading. Try again shortly.";
                rewardedAd?.LoadAd();
                return;
            }
            rewardGrantedThisShow = false;
            rewardedAd.ShowAd();
        }

        public void SetConsent(bool granted)
        {
            consentGranted = granted;
            LevelPlay.SetConsent(granted);
        }

        void OnLoaded(LevelPlayAdInfo info) => Status = "Rewarded ad ready.";
        void OnLoadFailed(LevelPlayAdError error) => Status = "Ad unavailable. Please try again later.";
        void OnDisplayFailed(LevelPlayAdInfo info, LevelPlayAdError error)
        {
            Status = "Ad could not be shown.";
            rewardedAd?.LoadAd();
        }
        void OnRewarded(LevelPlayAdInfo info, LevelPlayReward reward)
        {
            if (rewardGrantedThisShow) return;
            rewardGrantedThisShow = true;
            CloudEconomyService.Instance?.GrantRewardedGems(gemsPerReward);
            Status = "+" + gemsPerReward + " gems";
        }
        void OnClosed(LevelPlayAdInfo info) => rewardedAd?.LoadAd();
        void OnInitializationFailed(LevelPlayInitError error) => Status = "Ads could not initialize.";

        void OnDestroy()
        {
            LevelPlay.OnInitSuccess -= OnInitialized;
            LevelPlay.OnInitFailed -= OnInitializationFailed;
            if (rewardedAd != null)
            {
                rewardedAd.OnAdLoaded -= OnLoaded;
                rewardedAd.OnAdLoadFailed -= OnLoadFailed;
                rewardedAd.OnAdDisplayFailed -= OnDisplayFailed;
                rewardedAd.OnAdRewarded -= OnRewarded;
                rewardedAd.OnAdClosed -= OnClosed;
                rewardedAd.Dispose();
            }
            if (Instance == this) Instance = null;
        }
    }
}
