using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Runtime controller for the Talent (YETENEKLER) panel.
/// Each talent card maps to a ShopManager skill. Handles purchase and refresh.
/// Attached by SceneSetup to the TalentPanel.
/// </summary>
public class TalentUI : MonoBehaviour
{
    [System.Serializable]
    public struct TalentDef
    {
        public string skillId;
        public TextMeshProUGUI levelText;
        public TextMeshProUGUI costText;
        public Button button;
        public Image buttonBg;
    }

    [SerializeField] private TalentDef[] _talents;

    private void OnEnable()
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        if (_talents == null || ShopManager.Instance == null) return;

        for (int i = 0; i < _talents.Length; i++)
            RefreshCard(ref _talents[i]);
    }

    private void RefreshCard(ref TalentDef t)
    {
        if (string.IsNullOrEmpty(t.skillId)) return;

        int level = ShopManager.Instance.GetSkillLevel(t.skillId);
        var info = ShopManager.Instance.GetItem(t.skillId);
        if (info == null) return;

        bool maxed = level >= info.maxLevel;

        if (t.levelText != null)
        {
            t.levelText.text = maxed
                ? "Lv." + level + " MAX"
                : "Lv." + level + "/" + info.maxLevel;
            t.levelText.color = maxed
                ? new Color(0.4f, 1f, 0.5f)
                : new Color(0.75f, 0.75f, 0.85f);
        }

        if (t.costText != null)
        {
            if (maxed)
            {
                t.costText.text = "MAX";
                t.costText.color = new Color(0.4f, 1f, 0.5f);
            }
            else
            {
                int cost = ShopManager.Instance.GetNextCost(t.skillId);
                t.costText.text = "$" + cost;
                t.costText.color = new Color(0.96f, 0.82f, 0.45f);
            }
        }

        if (t.button != null)
        {
            bool canBuy = !maxed && ShopManager.Instance.CanBuy(t.skillId);
            t.button.interactable = canBuy;
        }

        if (t.buttonBg != null)
        {
            bool canBuy = !maxed && ShopManager.Instance.CanBuy(t.skillId);
            t.buttonBg.color = maxed
                ? new Color(0.35f, 0.35f, 0.45f)
                : canBuy
                    ? new Color(0.15f, 0.75f, 0.3f)
                    : new Color(0.45f, 0.2f, 0.2f);
        }
    }

    // Called by SceneSetup wiring — each button index maps to _talents[index]
    public void OnTalentBuyPressed(int index)
    {
        if (ShopManager.Instance == null) return;
        if (_talents == null || index < 0 || index >= _talents.Length) return;

        string id = _talents[index].skillId;
        if (ShopManager.Instance.TryBuy(id))
        {
            RefreshAll();
            // Update main menu currencies
            if (MainMenuManager.Instance != null)
                MainMenuManager.Instance.RefreshCurrencies();
        }
    }
}
