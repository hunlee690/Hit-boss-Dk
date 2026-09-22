using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitBoss.Items
{
    public enum SpinRewardType { Item, Coins, Gems }

    public sealed class SpinManager : MonoBehaviour
    {
        [Serializable]
        public class SpinReward
        {
            public SpinRewardType rewardType;
            [Tooltip("Used when Reward Type is Item.")] public string itemId;
            [Tooltip("Used for Coin or Gem rewards.")] [Min(1)] public int amount = 10;
            [Tooltip("Coins awarded instead when this item is already owned.")] [Min(1)] public int duplicateCoins = 25;
            [Min(.01f)] public float probabilityWeight = 1;
        }

        [Serializable]
        public class SpinDefinition
        {
            public string spinName = "New Spin";
            public ShopCurrency currency;
            [Min(0)] public int cost = 50;
            public bool available = true;
            public List<SpinReward> rewards = new List<SpinReward>();
        }

        public PlayerCustomizationManager customization;
        public TMP_Text titleText;
        public TMP_Text resultText;
        public TMP_Text wheelText;
        public TMP_Text oddsText;
        public RectTransform wheel;
        public TMP_Text rewardLabelPrefab;
        public Button spinButton;
        public TMP_Text spinButtonText;
        public float animationSeconds = 2.5f;
        public List<SpinDefinition> spins = new List<SpinDefinition>();
        [Tooltip("Spin opened by the Daily Spin button. Configure the spin itself below.")]
        public int dailySpinIndex = -1;
        [Tooltip("Spin opened by the Free Gem Spin button. Configure the spin itself below.")]
        public int freeGemSpinIndex = -1;

        readonly List<GameObject> generatedWheelObjects = new List<GameObject>();
        int selectedSpin = -1;
        bool spinning;
        public int SelectedSpin => selectedSpin;

        void Awake() { if (spinButton != null) spinButton.onClick.AddListener(Spin); }
        void OnEnable() { if (ItemManager.Instance != null) ItemManager.Instance.Changed += Refresh; }
        void OnDisable() { if (ItemManager.Instance != null) ItemManager.Instance.Changed -= Refresh; }

        public int FirstAvailable(ShopCurrency currency)
        {
            for (int i = 0; i < spins.Count; i++)
                if (spins[i] != null && spins[i].available && spins[i].currency == currency) return i;
            return -1;
        }

        public void Open(string requestedGroup)
        {
            if (requestedGroup != null && requestedGroup.IndexOf("DAILY", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                OpenSpin(dailySpinIndex);
                return;
            }
            if (requestedGroup != null && requestedGroup.IndexOf("FREE GEM", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                OpenSpin(freeGemSpinIndex);
                return;
            }
            ShopCurrency currency = requestedGroup != null && requestedGroup.IndexOf("COIN", StringComparison.OrdinalIgnoreCase) >= 0
                ? ShopCurrency.Coins : ShopCurrency.Gems;
            OpenSpin(FirstAvailable(currency));
        }

        public void OpenSpin(int index)
        {
            if (spinning) return;
            selectedSpin = index >= 0 && index < spins.Count && spins[index] != null && spins[index].available ? index : -1;
            SetResult(selectedSpin < 0 ? "This spin is unavailable." : "Press SPIN to play.");
            Refresh();
        }

        public void Refresh()
        {
            if (selectedSpin < 0 || selectedSpin >= spins.Count)
            {
                if (titleText != null) titleText.text = "SPIN";
                if (wheelText != null) wheelText.text = "NO SPIN";
                if (oddsText != null) oddsText.text = "";
                if (spinButton != null) spinButton.interactable = false;
                ClearWheel();
                return;
            }

            SpinDefinition definition = spins[selectedSpin];
            List<SpinReward> rewards = EligibleRewards(definition);
            if (titleText != null) titleText.text = definition.spinName.ToUpperInvariant();
            if (!spinning && wheelText != null) wheelText.text = rewards.Count > 0 ? "" : "NO REWARDS";
            if (spinButtonText != null)
                spinButtonText.text = "SPIN  " + definition.cost.ToString("N0") + (definition.currency == ShopCurrency.Coins ? " COINS" : " GEMS");
            if (spinButton != null) spinButton.interactable = !spinning && rewards.Count > 0 && CanAfford(definition);
            if (oddsText != null) oddsText.text = BuildOdds(rewards);
            if (!spinning) BuildWheel(rewards);
            if (!spinning && rewards.Count == 0) SetResult("No rewards are available in this spin.");
        }

        public void Spin()
        {
            if (spinning || selectedSpin < 0 || selectedSpin >= spins.Count) return;
            SpinDefinition definition = spins[selectedSpin];
            List<SpinReward> rewards = EligibleRewards(definition);
            if (rewards.Count == 0) { SetResult("No rewards are available in this spin."); Refresh(); return; }
            if (!CanAfford(definition))
            {
                SetResult("Not enough " + (definition.currency == ShopCurrency.Coins ? "coins." : "gems."));
                Refresh();
                return;
            }
            StartCoroutine(AnimateSpin(definition, rewards, PickWeightedIndex(rewards)));
        }

        IEnumerator AnimateSpin(SpinDefinition definition, List<SpinReward> rewards, int winnerIndex)
        {
            spinning = true;
            if (spinButton != null) spinButton.interactable = false;
            SetResult("Spinning...");
            float duration = Mathf.Max(.5f, animationSeconds);
            float elapsed = 0;
            float startAngle = wheel != null ? Normalize(wheel.localEulerAngles.z) : 0;
            float segmentAngle = 360f / rewards.Count;
            float landingAngle = winnerIndex * segmentAngle + segmentAngle * .5f;
            float targetAngle = startAngle + 5f * 360f + Mathf.Repeat(landingAngle - startAngle, 360f);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                if (wheel != null) wheel.localRotation = Quaternion.Euler(0, 0, Mathf.LerpUnclamped(startAngle, targetAngle, eased));
                yield return null;
            }
            if (wheel != null) wheel.localRotation = Quaternion.Euler(0, 0, landingAngle);

            SpinReward winner = rewards[winnerIndex];
            string rewardName = RewardName(winner);
            bool duplicate = winner.rewardType == SpinRewardType.Item && ItemManager.Instance != null && ItemManager.Instance.IsOwned(winner.itemId);
            string itemId = winner.rewardType == SpinRewardType.Item && !duplicate ? winner.itemId : "";
            int coins = winner.rewardType == SpinRewardType.Coins ? Mathf.Max(1, winner.amount) : duplicate ? Mathf.Max(1, winner.duplicateCoins) : 0;
            int gems = winner.rewardType == SpinRewardType.Gems ? Mathf.Max(1, winner.amount) : 0;
            string message = "Reward unavailable.";
            bool granted = ItemManager.Instance != null && ItemManager.Instance.TrySpinReward(itemId, coins, gems, definition.currency, definition.cost, out message);
            spinning = false;
            Refresh();
            SetResult(granted ? duplicate ? "DUPLICATE " + rewardName + ": +" + coins.ToString("N0") + " COINS" : "YOU WON: " + rewardName : message);
        }

        public List<SpinReward> EligibleRewards(SpinDefinition definition)
        {
            var items = ItemManager.Instance;
            if (definition?.rewards == null) return new List<SpinReward>();
            return definition.rewards.Where(r =>
            {
                if (r == null || r.probabilityWeight <= 0) return false;
                if (r.rewardType == SpinRewardType.Coins || r.rewardType == SpinRewardType.Gems) return r.amount > 0;
                return items != null && items.Find(r.itemId) != null;
            }).ToList();
        }

        bool CanAfford(SpinDefinition definition)
        {
            var profile = HitBoss.Social.OnlineManager.Instance?.Profiles?.Current;
            if (profile == null) return false;
            return definition.currency == ShopCurrency.Coins ? profile.coins >= definition.cost : profile.gems >= definition.cost;
        }

        static int PickWeightedIndex(List<SpinReward> rewards)
        {
            float total = rewards.Sum(r => Mathf.Max(0, r.probabilityWeight));
            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < rewards.Count; i++)
            {
                roll -= Mathf.Max(0, rewards[i].probabilityWeight);
                if (roll <= 0) return i;
            }
            return rewards.Count - 1;
        }

        void BuildWheel(List<SpinReward> rewards)
        {
            ClearWheel();
            if (wheel == null || rewards.Count == 0) return;
            wheel.localRotation = Quaternion.identity;
            Sprite sprite = wheel.GetComponent<Image>()?.sprite;
            float angle = 360f / rewards.Count;
            for (int i = 0; i < rewards.Count; i++)
            {
                var segmentObject = new GameObject("Segment " + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                segmentObject.layer = wheel.gameObject.layer;
                segmentObject.transform.SetParent(wheel, false);
                segmentObject.transform.SetAsFirstSibling();
                var rect = segmentObject.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                rect.localRotation = Quaternion.Euler(0, 0, -i * angle);
                var image = segmentObject.GetComponent<Image>();
                image.sprite = sprite; image.type = Image.Type.Filled; image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = 2; image.fillClockwise = true; image.fillAmount = 1f / rewards.Count;
                image.color = Color.HSVToRGB(Mathf.Repeat(.58f + i * .13f, 1f), .64f, .9f);
                image.raycastTarget = false;
                generatedWheelObjects.Add(segmentObject);

                TMP_Text label = rewardLabelPrefab != null ? Instantiate(rewardLabelPrefab, wheel) : CreateRuntimeLabel();
                label.gameObject.SetActive(true);
                label.name = "Reward " + (i + 1);
                label.text = RewardName(rewards[i]);
                float center = (i + .5f) * angle * Mathf.Deg2Rad;
                label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
                label.rectTransform.sizeDelta = new Vector2(100, 42);
                label.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(center), Mathf.Cos(center)) * 72f;
                label.rectTransform.localRotation = Quaternion.Euler(0, 0, -((i + .5f) * angle));
                label.transform.SetAsLastSibling();
                generatedWheelObjects.Add(label.gameObject);
            }
            if (rewardLabelPrefab != null) rewardLabelPrefab.gameObject.SetActive(false);
        }

        TMP_Text CreateRuntimeLabel()
        {
            var go = new GameObject("Reward Label", typeof(RectTransform));
            go.layer = wheel.gameObject.layer; go.transform.SetParent(wheel, false);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.font = wheelText != null ? wheelText.font : null; label.fontSize = 13; label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white; label.enableWordWrapping = true; label.raycastTarget = false;
            return label;
        }

        void ClearWheel()
        {
            foreach (var generated in generatedWheelObjects)
                if (generated != null) { generated.SetActive(false); Destroy(generated); }
            generatedWheelObjects.Clear();
        }

        public string RewardName(SpinReward reward)
        {
            if (reward == null) return "?";
            if (reward.rewardType == SpinRewardType.Coins) return Mathf.Max(1, reward.amount).ToString("N0") + " COINS";
            if (reward.rewardType == SpinRewardType.Gems) return Mathf.Max(1, reward.amount).ToString("N0") + " GEMS";
            return ItemManager.Instance?.Find(reward.itemId)?.name?.ToUpperInvariant() ?? "ITEM";
        }

        string BuildOdds(List<SpinReward> rewards)
        {
            if (rewards.Count == 0) return "NO REWARDS";
            float total = rewards.Sum(r => Mathf.Max(0, r.probabilityWeight));
            return "REWARD CHANCES\n\n" + string.Join("\n", rewards.Select(r => RewardName(r) + "   " + (r.probabilityWeight / total * 100f).ToString("0.#") + "%"));
        }

        static float Normalize(float angle) => Mathf.Repeat(angle, 360f);
        void SetResult(string value) { if (resultText != null) resultText.text = value; }
    }
}

