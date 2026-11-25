using TMPro;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace HitBoss.Items
{
    public sealed class ShopItemRow : MonoBehaviour
    {
        public Image icon;
        public TMP_Text itemName;
        public TMP_Text priceText;
        public Button buyButton;
        public TMP_Text buyButtonText;

        public void Setup(ShopManager owner, ShopManager.ShopOffer offer, ItemManager.CatalogItem item, bool owned)
        {
            if (itemName != null) itemName.text = item.name;
            if (icon == null)
                icon = GetComponentsInChildren<Image>(true).FirstOrDefault(image =>
                    image.gameObject != gameObject &&
                    (image.name.ToLowerInvariant().Contains("icon") || image.name.ToLowerInvariant().Contains("preview")));
            if (icon != null)
            {
                icon.sprite = item.icon;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
                icon.color = Color.white;
                icon.raycastTarget = false;
                icon.gameObject.SetActive(item.icon != null);
                RuntimeItemIconRenderer.ConfigureAnimation(icon, item.id);
            }
            if (priceText != null) priceText.text = owned ? "OWNED" : offer.price.ToString("N0") + (offer.currency == ShopCurrency.Coins ? " COINS" : " GEMS");
            if (buyButtonText != null) buyButtonText.text = owned ? "OWNED" : "BUY";
            if (buyButton != null)
            {
                buyButton.interactable = !owned;
                buyButton.onClick.RemoveAllListeners();
                buyButton.onClick.AddListener(() => owner.Buy(offer));
            }
        }
    }
}
