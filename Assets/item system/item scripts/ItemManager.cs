using System;
using System.Collections.Generic;
using System.Linq;
using HitBoss.Social;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HitBoss.Items
{
    public enum ShopCurrency { Coins, Gems }

    [DefaultExecutionOrder(-70)]
    public sealed class ItemManager : MonoBehaviour
    {
        public sealed class CatalogItem
        {
            public string id;
            public string name;
            public Sprite icon;
            public PlayerCustomizationManager.Category category;
            public int index;
            public bool ownedByDefault;
        }

        public static ItemManager Instance { get; private set; }
        public PlayerCustomizationManager customization;
        public event Action Changed;
        readonly List<CatalogItem> catalog = new List<CatalogItem>();
        bool syncing;

        public IReadOnlyList<CatalogItem> Catalog => catalog;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            SceneManager.sceneLoaded += SceneLoaded;
        }

        void Start()
        {
            if (OnlineManager.Instance != null) OnlineManager.Instance.Changed += Sync;
            Sync();
        }

        void SceneLoaded(Scene scene, LoadSceneMode mode) => Sync();

        public void Sync()
        {
            if (syncing) return;
            syncing = true;
            try
            {
                if (customization == null)
                    customization = FindObjectsByType<PlayerCustomizationManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault();
                BuildCatalog();
                var profiles = OnlineManager.Instance?.Profiles;
                if (profiles?.Current == null || customization == null) return;
                var defaults = catalog.Where(x => x.ownedByDefault).Select(x => x.id).ToArray();
                if (!profiles.Current.inventoryInitialized)
                {
                    profiles.InitializeInventory(defaults,
                        StartingId(PlayerCustomizationManager.Category.Head),
                        StartingId(PlayerCustomizationManager.Category.Body),
                        StartingId(PlayerCustomizationManager.Category.Bag),
                        StartingId(PlayerCustomizationManager.Category.Skates));
                }
                else profiles.EnsureOwnedItems(defaults);
                customization.RefreshFromInventory();
                Changed?.Invoke();
            }
            finally { syncing = false; }
        }

        public bool IsOwned(string itemId) => OnlineManager.Instance?.Profiles?.OwnsItem(itemId) == true;

        public string EquippedId(PlayerCustomizationManager.Category category)
        {
            var p = OnlineManager.Instance?.Profiles?.Current;
            if (p == null) return "";
            switch (category)
            {
                case PlayerCustomizationManager.Category.Head: return p.equippedHead ?? "";
                case PlayerCustomizationManager.Category.Body: return p.equippedBody ?? "";
                case PlayerCustomizationManager.Category.Bag: return p.equippedBag ?? "";
                case PlayerCustomizationManager.Category.Skates: return p.equippedSkates ?? "";
                default: return "";
            }
        }

        public bool Equip(PlayerCustomizationManager.Category category, string itemId)
        {
            bool changed = OnlineManager.Instance?.Profiles?.SetEquippedItem(category.ToString(), itemId) == true;
            if (changed) OnlineManager.Instance?.Flush();
            return changed;
        }

        public bool TryPurchase(string itemId, ShopCurrency currency, int price, out string message)
        {
            var profiles = OnlineManager.Instance?.Profiles;
            if (profiles == null) { message = "Profile is still loading."; return false; }
            bool purchased = profiles.TryPurchaseItem(itemId,
                currency == ShopCurrency.Coins ? price : 0,
                currency == ShopCurrency.Gems ? price : 0,
                out message);
            if (purchased) OnlineManager.Instance.Flush();
            return purchased;
        }

        public CatalogItem Find(string itemId) => catalog.FirstOrDefault(x => x.id == itemId);

        void BuildCatalog()
        {
            catalog.Clear();
            if (customization == null) return;
            Add(PlayerCustomizationManager.Category.Head, customization.headAccessories);
            Add(PlayerCustomizationManager.Category.Body, customization.body);
            Add(PlayerCustomizationManager.Category.Bag, customization.bags);
            Add(PlayerCustomizationManager.Category.Skates, customization.skates);
        }

        void Add(PlayerCustomizationManager.Category category, PlayerCustomizationManager.CategoryData data)
        {
            if (data?.items == null) return;
            int defaultIndex = StartingIndex(data);
            for (int i = 0; i < data.items.Length; i++)
            {
                var item = data.items[i];
                if (item == null || string.IsNullOrWhiteSpace(item.itemId)) continue;
                catalog.Add(new CatalogItem
                {
                    id = item.itemId,
                    name = string.IsNullOrWhiteSpace(item.itemName) ? category + " " + (i + 1) : item.itemName,
                    icon = item.icon,
                    category = category,
                    index = i,
                    ownedByDefault = i == defaultIndex
                });
            }
        }

        string StartingId(PlayerCustomizationManager.Category category)
        {
            var item = catalog.FirstOrDefault(x => x.category == category && x.ownedByDefault);
            return item?.id ?? "";
        }

        static int StartingIndex(PlayerCustomizationManager.CategoryData data)
        {
            if (data?.items == null) return -1;
            for (int i = 0; i < data.items.Length; i++) if (data.items[i]?.equippedByDefault == true) return i;
            return !data.allowNone && data.items.Length > 0 ? 0 : -1;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= SceneLoaded;
            if (OnlineManager.Instance != null) OnlineManager.Instance.Changed -= Sync;
            if (Instance == this) Instance = null;
        }
    }
}
