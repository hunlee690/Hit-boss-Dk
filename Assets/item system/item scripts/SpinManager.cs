using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitBoss.Items
{
    public sealed class SpinManager : MonoBehaviour
    {
        [Serializable]
        public class SpinReward
        {
            [Tooltip("Choose a customization item from the Inspector dropdown.")]
            public string itemId;
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
        public Transform spinChoiceContent;
        public Button spinChoicePrefab;
        public Button spinButton;
        public TMP_Text spinButtonText;
        public float animationSeconds = 2.5f;
        public List<SpinDefinition> spins = new List<SpinDefinition>();

        readonly List<int> visibleSpins = new List<int>();
        int selectedSpin = -1;
        bool spinning;

        void Awake()
        {
            if (spinButton != null) spinButton.onClick.AddListener(Spin);
        }

        void OnEnable()
        {
            if (ItemManager.Instance != null) ItemManager.Instance.Changed += Refresh;
        }

        void OnDisable()
        {
            if (ItemManager.Instance != null) ItemManager.Instance.Changed -= Refresh;
        }

        public void Open(string requestedGroup)
        {
            ShopCurrency currency = requestedGroup != null && requestedGroup.IndexOf("COIN", StringComparison.OrdinalIgnoreCase) >= 0
                ? ShopCurrency.Coins : ShopCurrency.Gems;
            ShowGroup(currency);
        }

        public void ShowGroup(ShopCurrency currency)
        {
            visibleSpins.Clear();
            for (int i = 0; i < spins.Count; i++)
                if (spins[i] != null && spins[i].available && spins[i].currency == currency) visibleSpins.Add(i);
            BuildChoiceButtons();
            SelectSpin(visibleSpins.Count > 0 ? visibleSpins[0] : -1);
            if (selectedSpin < 0) SetResult("No " + currency.ToString().ToLowerInvariant() + " spins are configured.");
        }

        void BuildChoiceButtons()
        {
            if (spinChoiceContent == null || spinChoicePrefab == null) return;
            for (int i = spinChoiceContent.childCount - 1; i >= 0; i--)
            {
                GameObject child = spinChoiceContent.GetChild(i).gameObject;
                if (child == spinChoicePrefab.gameObject) continue;
                child.SetActive(false);
                Destroy(child);
            }
            foreach (int index in visibleSpins)
            {
                int captured = index;
                Button button = Instantiate(spinChoicePrefab, spinChoiceContent);
                button.gameObject.SetActive(true);
                button.name = spins[index].spinName;
                TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
                if (label != null) label.text = spins[index].spinName;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => SelectSpin(captured));
            }
            spinChoicePrefab.gameObject.SetActive(false);
        }

        public void SelectSpin(int index)
        {
            if (spinning) return;
            selectedSpin = index >= 0 && index < spins.Count ? index : -1;
            Refresh();
        }

        public void Refresh()
        {
            if (selectedSpin < 0 || selectedSpin >= spins.Count)
            {
                if (titleText != null) titleText.text = "SPINS";
                if (wheelText != null) wheelText.text = "NO SPIN";
                if (oddsText != null) oddsText.text = "";
                if (spinButton != null) spinButton.interactable = false;
                return;
            }

            SpinDefinition definition = spins[selectedSpin];
            if (titleText != null) titleText.text = definition.spinName.ToUpperInvariant();
            List<SpinReward> rewards = EligibleRewards(definition);
            if (!spinning && wheelText != null) wheelText.text = rewards.Count > 0 ? "READY" : "SOLD OUT";
            if (spinButtonText != null)
                spinButtonText.text = "SPIN  " + definition.cost.ToString("N0") + (definition.currency == ShopCurrency.Coins ? " COINS" : " GEMS");
            if (spinButton != null) spinButton.interactable = !spinning && rewards.Count > 0 && CanAfford(definition);
            if (oddsText != null) oddsText.text = BuildOdds(rewards);
            if (!spinning && rewards.Count == 0) SetResult("You already own every reward in this spin.");
        }

        public void Spin()
        {
            if (spinning || selectedSpin < 0 || selectedSpin >= spins.Count) return;
            SpinDefinition definition = spins[selectedSpin];
            List<SpinReward> rewards = EligibleRewards(definition);
            if (rewards.Count == 0) { SetResult("You already own every reward in this spin."); Refresh(); return; }
            if (!CanAfford(definition))
            {
                SetResult("Not enough " + (definition.currency == ShopCurrency.Coins ? "coins." : "gems."));
                Refresh();
                return;
            }
            SpinReward winner = PickWeighted(rewards);
            StartCoroutine(AnimateSpin(definition, rewards, winner));
        }

        IEnumerator AnimateSpin(SpinDefinition definition, List<SpinReward> rewards, SpinReward winner)
        {
            spinning = true;
            if (spinButton != null) spinButton.interactable = false;
            SetResult("Spinning…");
            float duration = Mathf.Max(.5f, animationSeconds);
            float elapsed = 0;
            float nextLabel = 0;
            int labelIndex = 0;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalized = Mathf.Clamp01(elapsed / duration);
                if (wheel != null) wheel.Rotate(0, 0, -(720f * (1f - normalized) + 90f) * Time.unscaledDeltaTime);
                if (elapsed >= nextLabel)
                {
                    nextLabel = elapsed + Mathf.Lerp(.06f, .22f, normalized);
                    var preview = ItemManager.Instance?.Find(rewards[labelIndex++ % rewards.Count].itemId);
                    if (wheelText != null) wheelText.text = preview?.name?.ToUpperInvariant() ?? "?";
                }
                yield return null;
            }

            var item = ItemManager.Instance?.Find(winner.itemId);
            string message = "Reward unavailable.";
            bool granted = item != null && ItemManager.Instance.TryPurchase(winner.itemId, definition.currency, definition.cost, out message);
            if (wheelText != null) wheelText.text = item?.name?.ToUpperInvariant() ?? "?";
            string finalMessage = granted ? "YOU WON: " + item.name : message;
            spinning = false;
            Refresh();
            SetResult(finalMessage);
        }

        List<SpinReward> EligibleRewards(SpinDefinition definition)
        {
            var items = ItemManager.Instance;
            if (items == null || definition?.rewards == null) return new List<SpinReward>();
            return definition.rewards.Where(r => r != null && r.probabilityWeight > 0 && items.Find(r.itemId) != null && !items.IsOwned(r.itemId)).ToList();
        }

        bool CanAfford(SpinDefinition definition)
        {
            var profile = HitBoss.Social.OnlineManager.Instance?.Profiles?.Current;
            if (profile == null) return false;
            return definition.currency == ShopCurrency.Coins ? profile.coins >= definition.cost : profile.gems >= definition.cost;
        }

        static SpinReward PickWeighted(List<SpinReward> rewards)
        {
            float total = rewards.Sum(r => Mathf.Max(0, r.probabilityWeight));
            float roll = UnityEngine.Random.value * total;
            foreach (var reward in rewards)
            {
                roll -= Mathf.Max(0, reward.probabilityWeight);
                if (roll <= 0) return reward;
            }
            return rewards[rewards.Count - 1];
        }

        string BuildOdds(List<SpinReward> rewards)
        {
            if (rewards.Count == 0) return "NO REWARDS LEFT";
            float total = rewards.Sum(r => Mathf.Max(0, r.probabilityWeight));
            return "AVAILABLE REWARDS\n\n" + string.Join("\n", rewards.Select(r =>
            {
                var item = ItemManager.Instance.Find(r.itemId);
                float chance = total > 0 ? r.probabilityWeight / total * 100f : 0;
                return item.name + "   " + chance.ToString("0.#") + "%";
            }));
        }

        void SetResult(string value) { if (resultText != null) resultText.text = value; }
    }
}
