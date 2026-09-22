using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace HitBoss.Items
{
    public sealed class ShopManager : MonoBehaviour
    {
        [Serializable]
        public class ShopOffer
        {
            [Tooltip("Choose this item from the Shop Manager inspector dropdown.")]
            public string itemId;
            public ShopCurrency currency;
            [Min(0)] public int price = 100;
            public bool available = true;
        }

        public PlayerCustomizationManager customization;
        public Transform content;
        public ShopItemRow rowPrefab;
        public ShopSpinCard spinCardPrefab;
        public SpinManager spinManager;
        public MainMenu mainMenu;
        public TMP_Text statusText;
        public List<ShopOffer> offers = new List<ShopOffer>();
        bool open;
        bool showingSpins;
        ShopCurrency spinCurrency;

        void OnEnable()
        {
            if (ItemManager.Instance != null) ItemManager.Instance.Changed += Refresh;
            if (open) Refresh();
        }

        void OnDisable()
        {
            if (ItemManager.Instance != null) ItemManager.Instance.Changed -= Refresh;
        }

        public void Open()
        {
            open = true;
            showingSpins = false;
            if (content != null) content.gameObject.SetActive(true);
            Refresh();
        }

        public void OpenSpins(ShopCurrency currency)
        {
            open = true;
            showingSpins = true;
            spinCurrency = currency;
            if (content != null) content.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            open = false;
            if (content != null) content.gameObject.SetActive(false);
        }

        public void Refresh()
        {
            if (!open || content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                GameObject child = content.GetChild(i).gameObject;
                if ((rowPrefab != null && child == rowPrefab.gameObject) || (spinCardPrefab != null && child == spinCardPrefab.gameObject)) continue;
                child.SetActive(false);
                Destroy(child);
            }

            if (showingSpins) { BuildSpinCards(); return; }
            if (rowPrefab == null) return;

            var items = ItemManager.Instance;
            if (items == null || items.Catalog.Count == 0)
            {
                SetStatus("Loading items…");
                return;
            }

            int visible = 0;
            foreach (var offer in offers)
            {
                if (offer == null || !offer.available) continue;
                var item = items.Find(offer.itemId);
                if (item == null) continue;
                var row = Instantiate(rowPrefab, content);
                row.gameObject.SetActive(true);
                row.name = "Offer - " + item.name;
                row.Setup(this, offer, item, items.IsOwned(item.id));
                visible++;
            }
            rowPrefab.gameObject.SetActive(false);
            SetStatus(visible == 0 ? "No items are on sale. Add offers in Shop Manager." : "ITEM SHOP");
        }

        void BuildSpinCards()
        {
            if (spinManager == null || spinCardPrefab == null) { SetStatus("No spins are configured."); return; }
            int visible = 0;
            for (int i = 0; i < spinManager.spins.Count; i++)
            {
                var definition = spinManager.spins[i];
                if (definition == null || !definition.available || definition.currency != spinCurrency) continue;
                var card = Instantiate(spinCardPrefab, content);
                card.gameObject.SetActive(true);
                card.name = "Spin - " + definition.spinName;
                card.Setup(this, i, definition);
                visible++;
            }
            spinCardPrefab.gameObject.SetActive(false);
            if (rowPrefab != null) rowPrefab.gameObject.SetActive(false);
            SetStatus(visible == 0 ? "No spins are available." : (spinCurrency == ShopCurrency.Coins ? "COIN SPINS" : "GEM SPINS"));
        }

        public void ChooseSpin(int spinIndex)
        {
            if (mainMenu != null) mainMenu.OpenSpin(spinIndex);
        }

        public void Buy(ShopOffer offer)
        {
            if (offer == null || ItemManager.Instance == null) return;
            bool bought = ItemManager.Instance.TryPurchase(offer.itemId, offer.currency, Mathf.Max(0, offer.price), out string message);
            SetStatus(message);
            if (bought) Refresh();
        }

        void SetStatus(string message) { if (statusText != null) statusText.text = message; }
    }
}
