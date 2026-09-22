using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HitBoss.Items
{
    public sealed class ShopSpinCard : MonoBehaviour
    {
        public TMP_Text spinName;
        public TMP_Text details;
        public Button openButton;
        public TMP_Text openButtonText;

        public void Setup(ShopManager shop, int spinIndex, SpinManager.SpinDefinition definition)
        {
            if (spinName != null) spinName.text = definition.spinName;
            if (details != null)
                details.text = definition.rewards.Count + " REWARDS\n" + definition.cost.ToString("N0") + (definition.currency == ShopCurrency.Coins ? " COINS" : " GEMS");
            if (openButtonText != null) openButtonText.text = "VIEW SPIN";
            if (openButton != null)
            {
                openButton.onClick.RemoveAllListeners();
                openButton.onClick.AddListener(() => shop.ChooseSpin(spinIndex));
            }
        }
    }
}
