using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Builds and manages a store panel UI at runtime.
/// Cartoon-style colorful design for kids.
/// </summary>
public class ShopUI : MonoBehaviour
{
    [SerializeField] private Transform itemContainer;
    [SerializeField] private TextMeshProUGUI coinDisplay;
    [SerializeField] private ShopManager.ItemCategory category;

    private bool isBuilt;
    private float rowHeight = 100f;
    private float rowSpacing = 14f;
    private TextMeshProUGUI stoneDisplay;

    // Alternating row colors -- fun cartoon palette
    private static readonly Color[] rowColors =
    {
        new Color(0.18f, 0.22f, 0.45f, 0.95f),
        new Color(0.22f, 0.18f, 0.40f, 0.95f),
    };

    // Icon colors per item type
    private static readonly Color iconShield = new Color(0.3f, 0.7f, 1f);
    private static readonly Color iconMagnet = new Color(0.9f, 0.3f, 0.3f);
    private static readonly Color iconSpeed = new Color(1f, 0.6f, 0.1f);
    private static readonly Color iconLaser = new Color(0.6f, 0.2f, 0.9f);
    private static readonly Color iconJump = new Color(0.2f, 0.9f, 0.4f);
    private static readonly Color iconCoin = new Color(1f, 0.85f, 0.2f);
    private static readonly Color iconLife = new Color(1f, 0.3f, 0.5f);

    private void OnEnable()
    {
        RefreshAll();
    }

    public void BuildShop()
    {
        if (isBuilt) return;
        isBuilt = true;

        var items = ShopManager.GetItemsByCategory(category);
        for (int i = 0; i < items.Length; i++)
            CreateItemRow(items[i], i);

        // Resize container to fit all items
        float totalHeight = items.Length * (rowHeight + rowSpacing);
        var cRT = itemContainer.GetComponent<RectTransform>();
        if (cRT != null)
            cRT.sizeDelta = new Vector2(cRT.sizeDelta.x, totalHeight);
    }

    public void RefreshAll()
    {
        if (!isBuilt) BuildShop();

        if (coinDisplay != null && ScoreManager.Instance != null)
            coinDisplay.text = ScoreManager.Instance.Coins.ToString();

        // Show stone count for weapons tab
        if (category == ShopManager.ItemCategory.Weapons && ScoreManager.Instance != null)
        {
            if (stoneDisplay == null) CreateStoneDisplay();
            if (stoneDisplay != null)
                stoneDisplay.text = "<sprite name=\"diamond\"> " + ScoreManager.Instance.UpgradeStones;
        }

        foreach (Transform child in itemContainer)
        {
            var row = child.GetComponent<ShopItemRow>();
            if (row != null) row.Refresh();
        }
    }

    private void CreateStoneDisplay()
    {
        if (coinDisplay == null) return;

        // Create stone display next to coin display
        var stoneObj = new GameObject("StoneDisplay");
        stoneObj.transform.SetParent(coinDisplay.transform.parent, false);
        var rt = stoneObj.AddComponent<RectTransform>();

        // Copy coin display positioning but offset to the right
        var coinRt = coinDisplay.GetComponent<RectTransform>();
        rt.anchorMin = coinRt.anchorMin;
        rt.anchorMax = coinRt.anchorMax;
        rt.anchoredPosition = coinRt.anchoredPosition + new Vector2(120f, 0);
        rt.sizeDelta = coinRt.sizeDelta;

        stoneDisplay = stoneObj.AddComponent<TextMeshProUGUI>();
        stoneDisplay.fontSize = coinDisplay.fontSize;
        stoneDisplay.color = new Color(0.7f, 0.3f, 1f); // Purple for stones
        stoneDisplay.fontStyle = FontStyles.Bold;
        stoneDisplay.alignment = coinDisplay.alignment;
        stoneDisplay.enableAutoSizing = coinDisplay.enableAutoSizing;
        stoneDisplay.fontSizeMin = coinDisplay.fontSizeMin;
        stoneDisplay.fontSizeMax = coinDisplay.fontSizeMax;
        stoneDisplay.characterSpacing = 4f;
    }

    private static readonly Color iconBow = new Color(0.6f, 0.4f, 0.2f);
    private static readonly Color iconScythe = new Color(0.6f, 0.2f, 0.8f);
    private static readonly Color iconSawblade = new Color(0.9f, 0.6f, 0.2f);
    private static readonly Color iconTornado = new Color(0.3f, 0.8f, 0.9f);
    private static readonly Color iconSpear = new Color(0.8f, 0.8f, 0.9f);
    private static readonly Color iconStaff = new Color(0.4f, 0.3f, 1f);
    private static readonly Color iconHP = new Color(1f, 0.3f, 0.3f);
    private static readonly Color iconShieldUp = new Color(0.3f, 0.6f, 1f);

    private Color GetIconColor(string id)
    {
        switch (id)
        {
            case "shield": return iconShield;
            case "magnet": return iconMagnet;
            case "headstart": return iconSpeed;
            case "slowlaser": return iconLaser;
            case "dodge": return iconJump;
            case "doublecoins": return iconCoin;
            case "extralife": return iconLife;
            case "coinrange": return iconCoin;
            case "attackspeed": return iconLaser;
            case "bow": return iconBow;
            case "scythe": return iconScythe;
            case "sawblade": return iconSawblade;
            case "tornado": return iconTornado;
            case "spear": return iconSpear;
            case "staff": return iconStaff;
            case "hp": return iconHP;
            case "shieldupgrade": return iconShieldUp;
            case "armor": return iconShieldUp;
            case "healthregen": return iconHP;
            case "armorregen": return iconShieldUp;
            default: return Color.white;
        }
    }

    private string GetIconSymbol(string id)
    {
        switch (id)
        {
            case "shield": return "<sprite name=\"diamond\">";
            case "magnet": return "U";
            case "headstart": return ">>";
            case "slowlaser": return "=";
            case "dodge": return "<sprite name=\"lightning\">";
            case "doublecoins": return "x2";
            case "extralife": return "<sprite name=\"heart\">";
            case "coinrange": return "o";
            case "attackspeed": return "<sprite name=\"lightning\">";
            case "bow": return "<sprite name=\"sword\">";
            case "scythe": return "<sprite name=\"sword\">";
            case "sawblade": return "<sprite name=\"star\">";
            case "tornado": return "~";
            case "spear": return "<sprite name=\"sword\">";
            case "staff": return "<sprite name=\"lightning\">";
            case "hp": return "<sprite name=\"heart\">";
            case "shieldupgrade": return "<sprite name=\"diamond\">";
            default: return "?";
        }
    }

    private void CreateItemRow(ShopManager.ItemInfo info, int index)
    {
        // Row -- full width, positioned by index
        GameObject row = new GameObject(info.id + "_Row");
        row.transform.SetParent(itemContainer, false);
        var rowRT = row.AddComponent<RectTransform>();
        rowRT.anchorMin = new Vector2(0, 1);
        rowRT.anchorMax = new Vector2(1, 1);
        rowRT.pivot = new Vector2(0.5f, 1);
        rowRT.anchoredPosition = new Vector2(0, -index * (rowHeight + rowSpacing));
        rowRT.sizeDelta = new Vector2(0, rowHeight);

        // Row background -- alternating colors
        var bgImage = row.AddComponent<Image>();
        bgImage.color = rowColors[index % rowColors.Length];

        // Left accent bar -- colored stripe
        Color accent = GetIconColor(info.id);
        var accentBar = MakeChild(row.transform, "Accent",
            new Vector2(0, 0), new Vector2(0.02f, 1f),
            Vector2.zero, Vector2.zero);
        var accentImg = accentBar.AddComponent<Image>();
        accentImg.color = accent;

        // Icon area -- left side with symbol
        var iconObj = MakeChild(row.transform, "Icon",
            new Vector2(0.03f, 0.15f), new Vector2(0.14f, 0.85f),
            Vector2.zero, Vector2.zero);
        var iconBg = iconObj.AddComponent<Image>();
        iconBg.color = new Color(accent.r, accent.g, accent.b, 0.3f);

        var iconTextObj = MakeChild(iconObj.transform, "Symbol",
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var iconTmp = iconTextObj.AddComponent<TextMeshProUGUI>();
        iconTmp.text = GetIconSymbol(info.id);
        iconTmp.fontSize = 32;
        iconTmp.color = accent;
        iconTmp.fontStyle = FontStyles.Bold;
        iconTmp.alignment = TextAlignmentOptions.Center;
        iconTmp.enableAutoSizing = true;
        iconTmp.fontSizeMin = 16;
        iconTmp.fontSizeMax = 32;
        iconTmp.characterSpacing = 4f;

        // Item name -- upper text area
        var nameObj = MakeChild(row.transform, "Name",
            new Vector2(0.16f, 0.5f), new Vector2(0.64f, 0.95f),
            new Vector2(4, 0), new Vector2(0, -4));
        var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
        nameTmp.text = info.name;
        nameTmp.fontSize = 24;
        nameTmp.color = Color.white;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.enableAutoSizing = true;
        nameTmp.fontSizeMin = 14;
        nameTmp.fontSizeMax = 24;
        nameTmp.alignment = TextAlignmentOptions.BottomLeft;
        nameTmp.enableWordWrapping = false;
        nameTmp.characterSpacing = 4f;

        // Level/description -- lower text area
        var levelObj = MakeChild(row.transform, "Level",
            new Vector2(0.16f, 0.05f), new Vector2(0.64f, 0.5f),
            new Vector2(4, 4), new Vector2(0, 0));
        var levelTmp = levelObj.AddComponent<TextMeshProUGUI>();
        levelTmp.text = "";
        levelTmp.fontSize = 16;
        levelTmp.color = new Color(0.75f, 0.75f, 0.85f);
        levelTmp.enableAutoSizing = true;
        levelTmp.fontSizeMin = 10;
        levelTmp.fontSizeMax = 16;
        levelTmp.alignment = TextAlignmentOptions.TopLeft;
        levelTmp.enableWordWrapping = false;
        levelTmp.characterSpacing = 4f;

        // Buy button -- right side, chunky cartoon style
        var btnObj = MakeChild(row.transform, "BuyBtn",
            new Vector2(0.66f, 0.12f), new Vector2(0.97f, 0.88f),
            Vector2.zero, Vector2.zero);
        var btnImage = btnObj.AddComponent<Image>();
        btnImage.color = new Color(0.15f, 0.75f, 0.3f);

        // Button shadow/border effect
        var btnShadow = MakeChild(row.transform, "BtnShadow",
            new Vector2(0.665f, 0.08f), new Vector2(0.975f, 0.84f),
            Vector2.zero, Vector2.zero);
        var shadowImg = btnShadow.AddComponent<Image>();
        shadowImg.color = new Color(0.08f, 0.4f, 0.15f);
        btnShadow.transform.SetSiblingIndex(btnObj.transform.GetSiblingIndex());

        var button = btnObj.AddComponent<Button>();
        var btnColors = button.colors;
        btnColors.normalColor = new Color(0.15f, 0.75f, 0.3f);
        btnColors.highlightedColor = new Color(0.2f, 0.85f, 0.4f);
        btnColors.pressedColor = new Color(0.1f, 0.55f, 0.2f);
        btnColors.disabledColor = new Color(0.35f, 0.35f, 0.4f);
        button.colors = btnColors;

        // Button text
        var btnLabelObj = MakeChild(btnObj.transform, "Label",
            Vector2.zero, Vector2.one,
            new Vector2(4, 2), new Vector2(-4, -2));
        var btnTmp = btnLabelObj.AddComponent<TextMeshProUGUI>();
        btnTmp.text = "";
        btnTmp.fontSize = 24;
        btnTmp.color = Color.white;
        btnTmp.fontStyle = FontStyles.Bold;
        btnTmp.enableAutoSizing = true;
        btnTmp.fontSizeMin = 12;
        btnTmp.fontSizeMax = 24;
        btnTmp.alignment = TextAlignmentOptions.Center;
        btnTmp.characterSpacing = 4f;

        var itemRow = row.AddComponent<ShopItemRow>();
        itemRow.Init(info, levelTmp, btnTmp, button, btnImage);
    }

    private GameObject MakeChild(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return obj;
    }
}
