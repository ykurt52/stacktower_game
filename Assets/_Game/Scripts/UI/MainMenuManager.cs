using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Archero-style main menu controller. Manages tab switching between center panels,
/// updates currency displays, and smoothly lerps the XP bar.
/// Attached to the SafeArea container by MainMenuSetup.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    public enum MenuTab { Shop, Equipment, Battle, Talent, Settings }

    [Header("Top HUD")]
    [SerializeField] private TextMeshProUGUI gemText;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI energyText;

    [Header("Center Panels")]
    [SerializeField] private GameObject battlePanel;
    [SerializeField] private GameObject equipmentPanel;
    [SerializeField] private GameObject talentPanel;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Bottom Nav")]
    [SerializeField] private Image[] tabBackgrounds;
    [SerializeField] private TextMeshProUGUI[] tabLabels;

    [Header("Battle Panel")]
    [SerializeField] private TextMeshProUGUI stageInfoText;

    public static MainMenuManager Instance { get; private set; }

    private MenuTab currentTab = MenuTab.Battle;
    private GameObject[] allPanels;

    // Tab colors (pastel)
    private static readonly Color TabActive = new Color(0.96f, 0.82f, 0.45f);
    private static readonly Color TabInactive = new Color(0.17f, 0.17f, 0.26f, 0.9f);
    private static readonly Color LabelActive = new Color(0.98f, 0.95f, 0.88f);
    private static readonly Color LabelInactive = new Color(0.55f, 0.55f, 0.68f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        allPanels = new[] { shopPanel, equipmentPanel, battlePanel, talentPanel, settingsPanel };
        SwitchTab(MenuTab.Battle);
        RefreshCurrencies();
    }

    // Called by tab buttons (index 0-4 maps to MenuTab enum)
    public void OnTabPressed(int tabIndex)
    {
        if (tabIndex < 0 || tabIndex > 4) return;
        SwitchTab((MenuTab)tabIndex);
    }

    public void SwitchTab(MenuTab tab)
    {
        currentTab = tab;

        // Hide all, show selected
        for (int i = 0; i < allPanels.Length; i++)
        {
            if (allPanels[i] != null)
                allPanels[i].SetActive(i == (int)tab);
        }

        // Update tab visuals
        for (int i = 0; i < 5; i++)
        {
            bool active = i == (int)tab;
            if (tabBackgrounds != null && i < tabBackgrounds.Length && tabBackgrounds[i] != null)
                tabBackgrounds[i].color = active ? TabActive : TabInactive;
            if (tabLabels != null && i < tabLabels.Length && tabLabels[i] != null)
                tabLabels[i].color = active ? LabelActive : LabelInactive;
        }
    }

    public void OnPlayButton()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.StartGame();
    }

    public void RefreshCurrencies()
    {
        if (ScoreManager.Instance == null) return;

        if (goldText != null)
            goldText.text = FormatNumber(ScoreManager.Instance.Coins);
        if (gemText != null)
            gemText.text = FormatNumber(ScoreManager.Instance.UpgradeStones);
        if (energyText != null)
            energyText.text = "5/5"; // Placeholder -- hook into energy system when available
    }

    public void SetStageInfo(int chapter, int stage)
    {
        if (stageInfoText != null)
            stageInfoText.text = $"Chapter {chapter} - Stage {stage}";
    }

    private static string FormatNumber(int n)
    {
        if (n >= 1000000) return (n / 1000000f).ToString("0.#") + "M";
        if (n >= 1000) return (n / 1000f).ToString("0.#") + "K";
        return n.ToString();
    }
}
