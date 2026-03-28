using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls a single store item row.
/// Handles consumable (Shop), permanent (Skills), and upgradeable (Weapons) items.
/// </summary>
public class ShopItemRow : MonoBehaviour
{
    private ShopManager.ItemInfo info;
    private TextMeshProUGUI levelText;
    private TextMeshProUGUI buttonText;
    private Button buyButton;
    private Image buttonImage;

    // Weapon: extra UI elements
    private Button equipButton;
    private Image equipButtonImage;
    private TextMeshProUGUI equipButtonText;

    public void Init(ShopManager.ItemInfo info, TextMeshProUGUI levelText,
                     TextMeshProUGUI buttonText, Button buyButton, Image buttonImage)
    {
        this.info = info;
        this.levelText = levelText;
        this.buttonText = buttonText;
        this.buyButton = buyButton;
        this.buttonImage = buttonImage;

        buyButton.onClick.AddListener(OnBuyClicked);

        // For weapons, create equip button
        if (info.category == ShopManager.ItemCategory.Weapons)
            CreateEquipButton();

        Refresh();
    }

    public void Refresh()
    {
        if (info == null || ShopManager.Instance == null) return;

        if (info.category == ShopManager.ItemCategory.Shop)
            RefreshConsumable();
        else if (info.category == ShopManager.ItemCategory.Weapons)
            RefreshWeapon();
        else
            RefreshSkill();
    }

    private void RefreshConsumable()
    {
        bool active = ShopManager.Instance.IsConsumableActive(info.id);
        int cost = info.costs[0];

        if (active)
        {
            levelText.text = "HAZIR!";
            levelText.color = new Color(0.4f, 1f, 0.5f);
            buttonText.text = "AKTIF";
            buyButton.interactable = false;
            buttonImage.color = new Color(0.3f, 0.55f, 0.35f);
        }
        else
        {
            levelText.text = info.description;
            levelText.color = new Color(0.75f, 0.75f, 0.85f);
            buttonText.text = cost.ToString();
            bool canAfford = ScoreManager.Instance != null && ScoreManager.Instance.Coins >= cost;
            buyButton.interactable = canAfford;
            buttonImage.color = canAfford
                ? new Color(0.15f, 0.75f, 0.3f)
                : new Color(0.45f, 0.2f, 0.2f);
        }
    }

    private void RefreshSkill()
    {
        int level = ShopManager.Instance.GetSkillLevel(info.id);
        bool maxed = ShopManager.Instance.IsSkillMaxed(info.id);

        if (maxed)
        {
            levelText.text = "Lv " + level + "/" + info.maxLevel + "  MAX!";
            levelText.color = new Color(0.4f, 1f, 0.5f);
            buttonText.text = "MAX";
            buyButton.interactable = false;
            buttonImage.color = new Color(0.35f, 0.35f, 0.45f);
        }
        else
        {
            levelText.text = "Lv " + level + "/" + info.maxLevel;
            levelText.color = new Color(0.75f, 0.75f, 0.85f);
            int cost = ShopManager.Instance.GetNextCost(info.id);
            buttonText.text = cost.ToString();
            bool canAfford = ShopManager.Instance.CanBuy(info.id);
            buyButton.interactable = canAfford;
            buttonImage.color = canAfford
                ? new Color(0.15f, 0.75f, 0.3f)
                : new Color(0.45f, 0.2f, 0.2f);
        }
    }

    private void RefreshWeapon()
    {
        int level = ShopManager.Instance.GetWeaponLevel(info.id);
        bool owned = level >= 0;
        string equippedId = ShopManager.Instance.GetEquippedWeaponId();
        bool isEquipped = equippedId == info.id;

        if (!owned)
        {
            // Not owned — show buy price
            levelText.text = info.description;
            levelText.color = new Color(0.75f, 0.75f, 0.85f);
            int cost = info.costs[0];
            buttonText.text = cost.ToString();
            bool canAfford = ScoreManager.Instance != null && ScoreManager.Instance.Coins >= cost;
            buyButton.interactable = canAfford;
            buttonImage.color = canAfford
                ? new Color(0.15f, 0.75f, 0.3f)
                : new Color(0.45f, 0.2f, 0.2f);

            // Hide equip button
            if (equipButton != null)
                equipButton.gameObject.SetActive(false);
        }
        else
        {
            bool maxed = level >= info.maxLevel;

            // Show weapon level and stats
            ShopManager.Instance.GetWeaponStats(info.id, out int dmg, out float rate, out float spd, out string special);

            string rateStr = rate.ToString("F2");
            if (maxed)
            {
                levelText.text = "+" + level + " MAX  DMG:" + dmg + " HIZ:" + rateStr + "s";
                levelText.color = new Color(1f, 0.85f, 0.2f);
                buttonText.text = "MAX";
                buyButton.interactable = false;
                buttonImage.color = new Color(0.35f, 0.35f, 0.45f);
            }
            else
            {
                int coinCost = ShopManager.Instance.GetWeaponUpgradeCoinCost(info.id);
                int stoneCost = ShopManager.Instance.GetWeaponUpgradeStoneCost(info.id);

                levelText.text = "+" + level + "  DMG:" + dmg + " HIZ:" + rateStr + "s";
                levelText.color = new Color(0.75f, 0.75f, 0.85f);

                // Button shows: coin cost + stone cost
                buttonText.text = coinCost + " + " + stoneCost + "tas";

                bool canUpgrade = ShopManager.Instance.CanBuy(info.id);
                buyButton.interactable = canUpgrade;
                buttonImage.color = canUpgrade
                    ? new Color(0.6f, 0.2f, 0.9f) // Purple for upgrade
                    : new Color(0.45f, 0.2f, 0.2f);
            }

            // Show equip button
            if (equipButton != null)
            {
                equipButton.gameObject.SetActive(true);
                if (isEquipped)
                {
                    equipButtonText.text = "SECILI";
                    equipButton.interactable = false;
                    equipButtonImage.color = new Color(0.3f, 0.55f, 0.35f);
                }
                else
                {
                    equipButtonText.text = "SEC";
                    equipButton.interactable = true;
                    equipButtonImage.color = new Color(0.2f, 0.5f, 0.8f);
                }
            }
        }
    }

    private void OnBuyClicked()
    {
        if (ShopManager.Instance == null) return;
        if (ShopManager.Instance.TryBuy(info.id))
        {
            var shopUI = GetComponentInParent<ShopUI>();
            if (shopUI != null) shopUI.RefreshAll();
        }
    }

    private void OnEquipClicked()
    {
        if (ShopManager.Instance == null) return;
        if (ShopManager.Instance.EquipWeapon(info.id))
        {
            var shopUI = GetComponentInParent<ShopUI>();
            if (shopUI != null) shopUI.RefreshAll();
        }
    }

    private void CreateEquipButton()
    {
        // Create a small equip/select button below the buy/upgrade button
        var parent = buyButton.transform.parent;

        var btnObj = new GameObject("EquipBtn");
        btnObj.transform.SetParent(parent, false);
        var rt = btnObj.AddComponent<RectTransform>();
        // Position below the buy button area
        rt.anchorMin = new Vector2(0.66f, 0.02f);
        rt.anchorMax = new Vector2(0.97f, 0.35f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        equipButtonImage = btnObj.AddComponent<Image>();
        equipButtonImage.color = new Color(0.2f, 0.5f, 0.8f);

        equipButton = btnObj.AddComponent<Button>();
        var colors = equipButton.colors;
        colors.normalColor = new Color(0.2f, 0.5f, 0.8f);
        colors.highlightedColor = new Color(0.3f, 0.6f, 0.9f);
        colors.pressedColor = new Color(0.15f, 0.35f, 0.6f);
        colors.disabledColor = new Color(0.3f, 0.55f, 0.35f);
        equipButton.colors = colors;
        equipButton.onClick.AddListener(OnEquipClicked);

        var labelObj = new GameObject("Label");
        labelObj.transform.SetParent(btnObj.transform, false);
        var labelRt = labelObj.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(2, 1);
        labelRt.offsetMax = new Vector2(-2, -1);

        equipButtonText = labelObj.AddComponent<TextMeshProUGUI>();
        equipButtonText.text = "SEC";
        equipButtonText.fontSize = 14;
        equipButtonText.color = Color.white;
        equipButtonText.fontStyle = FontStyles.Bold;
        equipButtonText.enableAutoSizing = true;
        equipButtonText.fontSizeMin = 8;
        equipButtonText.fontSizeMax = 14;
        equipButtonText.alignment = TextAlignmentOptions.Center;

        // Adjust buy button to be smaller (upper portion)
        var buyRt = buyButton.GetComponent<RectTransform>();
        buyRt.anchorMin = new Vector2(0.66f, 0.38f);
        buyRt.anchorMax = new Vector2(0.97f, 0.88f);
    }
}
