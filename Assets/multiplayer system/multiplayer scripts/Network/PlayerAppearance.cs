using UnityEngine;

namespace HitBoss.Multiplayer
{
    public sealed class PlayerAppearance : MonoBehaviour
    {
        public PlayerController movement;
        public PlayerCustomizationManager.CategoryData head, body, bag, skates;
        public void Apply(NetworkLoadout value)
        {
            if (!value.initialized) return;
            ApplyCategory(head, value.head); ApplyCategory(body, value.body); ApplyCategory(bag, value.bag);
            var selected = ApplyCategory(skates, value.skates);
            movement.SetCustomizationSkatePreview(null, false);
            movement.SetEquippedSkate(selected);
        }
        public NetworkLoadout RandomLoadout()
        {
            return new NetworkLoadout
            {
                initialized = true,
                head = RandomIndex(head),
                body = RandomIndex(body),
                bag = RandomIndex(bag),
                skates = RandomIndex(skates)
            };
        }
        static int RandomIndex(PlayerCustomizationManager.CategoryData category)
        {
            return category?.items != null && category.items.Length > 0
                ? Random.Range(0, category.items.Length)
                : -1;
        }
        static GameObject[] ApplyCategory(PlayerCustomizationManager.CategoryData category, int index)
        {
            if (category?.items == null) return null;
            int fallback = -1;
            for (int i = 0; i < category.items.Length; i++) if (category.items[i].equippedByDefault) { fallback = i; break; }
            if (fallback < 0 && !category.allowNone && category.items.Length > 0) fallback = 0;
            if (index < 0 || index >= category.items.Length) index = fallback;
            GameObject[] selected = index >= 0 ? category.items[index].objectsToEnable : null;
            foreach (var item in category.items)
                foreach (var obj in item.objectsToEnable ?? System.Array.Empty<GameObject>())
                    if (obj != null) obj.SetActive(selected != null && System.Array.IndexOf(selected, obj) >= 0);
            return selected;
        }
    }
}
