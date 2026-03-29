using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Controls visibility of UI panels and updates score labels reactively.
/// Handles rewarded ad flows: revive, 2x coins, free stone.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Background")]
    [SerializeField] private GameObject backgroundCanvas;

    [Header("Menu Panel")]
    [SerializeField] private TextMeshProUGUI bestScoreMenuText;

    [Header("Game Panel")]
    [SerializeField] private TextMeshProUGUI currentScoreText;
    [SerializeField] private TextMeshProUGUI coinText;
    [SerializeField] private TextMeshProUGUI floorText;

    [Header("Pause Panel")]
    [SerializeField] private GameObject pausePanel;

    [Header("Store Panels")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject skillsPanel;
    [SerializeField] private GameObject weaponsPanel;

    [Header("Confirm Popup")]
    [SerializeField] private GameObject confirmResetPanel;

    [Header("Bonus Code Panel")]
    [SerializeField] private GameObject bonusCodePanel;
    [SerializeField] private TMP_InputField bonusCodeInput;
    [SerializeField] private TextMeshProUGUI bonusCodeResult;

    [Header("Game Over Panel")]
    [SerializeField] private TextMeshProUGUI gameOverScoreText;
    [SerializeField] private TextMeshProUGUI gameOverBestText;

    [Header("Weapon HUD")]
    [SerializeField] private TextMeshProUGUI weaponAmmoText;

    [Header("Settings")]
    [SerializeField] private float gameOverDelay = 0.5f;
    [SerializeField] private float reviveTimeLimit = 5f;

    // Stone HUD (created at runtime)
    private TextMeshProUGUI stoneHudText;
    private TextMeshProUGUI emeraldHudText;

    // HP, Shield & XP bars (created at runtime)
    private Image uiHpBarFill;
    private Image uiShieldBarFill;
    private Image uiShieldBarBg;
    private Image uiXpBarFill;
    private TextMeshProUGUI uiHpText;
    private TextMeshProUGUI uiLevelText;

    // Ability pick UI
    private GameObject abilityPickPanel;

    // Revive panel (created at runtime)
    private GameObject revivePanel;
    private float reviveTimer;
    private bool reviveCountdownActive;

    // Game over reward buttons (created at runtime)
    private GameObject doubleCoinsBtn;
    private GameObject freeStoneBtn;
    private int coinsEarnedThisRun;

    // Daily reward
    private GameObject dailyRewardPanel;
    private bool dailyRewardShown;


    private void Start()
    {
        // Fallback: find panels by name if SerializeField references are missing
        if (menuPanel == null) menuPanel = transform.Find("Canvas")?.Find("MenuPanel")?.gameObject
            ?? GameObject.Find("MenuPanel");
        if (gamePanel == null) gamePanel = transform.Find("Canvas")?.Find("GamePanel")?.gameObject
            ?? GameObject.Find("GamePanel");
        if (gameOverPanel == null) gameOverPanel = transform.Find("Canvas")?.Find("GameOverPanel")?.gameObject
            ?? GameObject.Find("GameOverPanel");

        GameManager.Instance.OnGameStart.AddListener(OnGameStart);
        GameManager.Instance.OnGameOver.AddListener(OnGameOver);
        GameManager.Instance.OnReturnToMenu.AddListener(OnReturnToMenu);
        GameManager.Instance.OnRevivePrompt.AddListener(OnRevivePrompt);
        GameManager.Instance.OnRevive.AddListener(OnRevive);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged.AddListener(OnScoreChanged);
            ScoreManager.Instance.OnCoinChanged.AddListener(OnCoinChanged);
            ScoreManager.Instance.OnCombo.AddListener(OnCombo);
            ScoreManager.Instance.OnStoneChanged.AddListener(OnStoneChanged);
            ScoreManager.Instance.OnEmeraldChanged.AddListener(OnEmeraldChanged);
            ScoreManager.Instance.OnLevelUp.AddListener(OnLevelUp);
        }

        if (AdManager.Instance != null)
            AdManager.Instance.OnRewardEarned.AddListener(OnRewardEarned);

        ShowMenuPanel();

        // Top HUD layout -- Row 1 (top): pause left, wave center, coin right
        // Pause button occupies ~0-12% left, so wave starts at 12%
        RepositionToBottom(floorText, 0.14f, 0.96f, 0.55f, 1.0f);           // Wave: center-left
        RepositionToBottom(currentScoreText, 0.35f, 0.96f, 0.65f, 1.0f);    // Score: center
        RepositionToBottom(coinText, 0.65f, 0.96f, 0.98f, 1.0f);            // Coins: right

        // Create HP & XP bars, stone & emerald counters
        CreateHealthBars();
        CreateStoneHud();

        CheckDailyReward();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart.RemoveListener(OnGameStart);
            GameManager.Instance.OnGameOver.RemoveListener(OnGameOver);
            GameManager.Instance.OnReturnToMenu.RemoveListener(OnReturnToMenu);
            GameManager.Instance.OnRevivePrompt.RemoveListener(OnRevivePrompt);
            GameManager.Instance.OnRevive.RemoveListener(OnRevive);
        }

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged.RemoveListener(OnScoreChanged);
            ScoreManager.Instance.OnCoinChanged.RemoveListener(OnCoinChanged);
            ScoreManager.Instance.OnCombo.RemoveListener(OnCombo);
        }

        if (AdManager.Instance != null)
            AdManager.Instance.OnRewardEarned.RemoveListener(OnRewardEarned);
    }

    // ── Button Handlers ──

    public void OnPlayButton() => GameManager.Instance.StartGame();
    public void OnRetryButton() => GameManager.Instance.StartGame();
    public void OnMenuButton() => GameManager.Instance.ReturnToMenu();

    public void OnPauseButton()
    {
        if (pausePanel == null) return;
        bool pausing = !pausePanel.activeSelf;
        pausePanel.SetActive(pausing);
        Time.timeScale = pausing ? 0f : 1f;
    }

    public void OnResumeButton()
    {
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
    }

    public void OnPauseMenuButton()
    {
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);
        GameManager.Instance.ReturnToMenu();
    }

    public void OnShopButton()
    {
        if (shopPanel != null) { shopPanel.SetActive(true); menuPanel.SetActive(false); }
    }

    public void OnSkillsButton()
    {
        if (skillsPanel != null) { skillsPanel.SetActive(true); menuPanel.SetActive(false); }
    }

    public void OnWeaponsButton()
    {
        if (weaponsPanel != null) { weaponsPanel.SetActive(true); menuPanel.SetActive(false); }
    }

    public void OnResetSkillsButton()
    {
        if (confirmResetPanel != null)
            confirmResetPanel.SetActive(true);
    }

    public void OnResetWeaponsButton()
    {
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.ResetAllWeapons();
            if (weaponsPanel != null && weaponsPanel.activeSelf)
            {
                var shopUI = weaponsPanel.GetComponent<ShopUI>();
                if (shopUI != null) shopUI.RefreshAll();
            }
        }
    }

    public void OnConfirmResetYes()
    {
        if (confirmResetPanel != null)
            confirmResetPanel.SetActive(false);

        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.ResetAllSkills();
            if (skillsPanel != null && skillsPanel.activeSelf)
            {
                var shopUI = skillsPanel.GetComponent<ShopUI>();
                if (shopUI != null) shopUI.RefreshAll();
            }
        }
    }

    public void OnConfirmResetNo()
    {
        if (confirmResetPanel != null)
            confirmResetPanel.SetActive(false);
    }

    public void OnStoreCloseButton()
    {
        if (shopPanel != null) shopPanel.SetActive(false);
        if (skillsPanel != null) skillsPanel.SetActive(false);
        if (weaponsPanel != null) weaponsPanel.SetActive(false);
        if (bonusCodePanel != null) bonusCodePanel.SetActive(false);
        ShowMenuPanel();
    }

    public void OnBonusCodeButton()
    {
        if (bonusCodePanel != null)
        {
            bonusCodePanel.SetActive(true);
            menuPanel.SetActive(false);
            if (bonusCodeInput != null) bonusCodeInput.text = "";
            if (bonusCodeResult != null) bonusCodeResult.text = "";
        }
    }

    public void OnRedeemCodeButton()
    {
        if (BonusCodeManager.Instance == null || bonusCodeInput == null) return;

        string code = bonusCodeInput.text;
        int result = BonusCodeManager.Instance.TryRedeem(code, out int coins, out int stones);

        if (bonusCodeResult != null)
        {
            if (result == 1)
            {
                bonusCodeResult.text = $"+{coins} altin  +{stones} tas!";
                bonusCodeResult.color = new Color(0.3f, 1f, 0.4f);

                // Refresh HUD currencies
                if (MainMenuManager.Instance != null)
                    MainMenuManager.Instance.RefreshCurrencies();
            }
            else if (result == -1)
            {
                bonusCodeResult.text = "KOD ZATEN KULLANILDI";
                bonusCodeResult.color = new Color(1f, 0.5f, 0.2f);
            }
            else
            {
                bonusCodeResult.text = "GECERSIZ KOD";
                bonusCodeResult.color = new Color(1f, 0.3f, 0.2f);
            }
        }
    }

    // ── Revive Flow ──

    public void OnReviveAdButton()
    {
        reviveCountdownActive = false;
        if (revivePanel != null) revivePanel.SetActive(false);
        if (AdManager.Instance != null)
            AdManager.Instance.ShowRewarded(AdManager.RewardRevive);
    }

    public void OnReviveCoinButton()
    {
        if (ScoreManager.Instance == null || ScoreManager.Instance.Coins < 100) return;

        ScoreManager.Instance.SpendCoins(100);
        reviveCountdownActive = false;
        if (revivePanel != null) revivePanel.SetActive(false);
        Time.timeScale = 1f;
        GameManager.Instance.RevivePlayer();
    }

    public void OnReviveSkipButton()
    {
        reviveCountdownActive = false;
        if (revivePanel != null) revivePanel.SetActive(false);
        Time.timeScale = 1f;
        GameManager.Instance.DeclineRevive();
    }

    // ── Game Over Reward Buttons ──

    public void OnDoubleCoinsButton()
    {
        if (AdManager.Instance != null)
            AdManager.Instance.ShowRewarded(AdManager.RewardDoubleCoins);
    }

    public void OnFreeStoneButton()
    {
        if (AdManager.Instance != null)
            AdManager.Instance.ShowRewarded(AdManager.RewardFreeStone);
    }

    // ── Reward Callback ──

    private void OnRewardEarned(string rewardType)
    {
        switch (rewardType)
        {
            case AdManager.RewardRevive:
                GameManager.Instance.RevivePlayer();
                break;

            case AdManager.RewardDoubleCoins:
                if (ScoreManager.Instance != null)
                {
                    ScoreManager.Instance.AddCoins(coinsEarnedThisRun);
                    FloatingText.Spawn(Vector3.up * 3f,
                        "+" + coinsEarnedThisRun + " BONUS COIN!", new Color(1f, 0.85f, 0.2f), 1.5f);
                }
                if (doubleCoinsBtn != null) doubleCoinsBtn.SetActive(false);
                break;

            case AdManager.RewardFreeStone:
                if (ScoreManager.Instance != null)
                {
                    ScoreManager.Instance.AddStone();
                    FloatingText.Spawn(Vector3.up * 3f,
                        "+1 GUCLENME TASI!", new Color(0.7f, 0.3f, 1f), 1.5f);
                }
                if (freeStoneBtn != null) freeStoneBtn.SetActive(false);
                break;
        }
    }

    // ── Daily Reward ──

    public void OnDailyRewardButton()
    {
        if (dailyRewardPanel != null) dailyRewardPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
        // Refresh the main menu coin display so the newly added coins are visible
        if (MainMenuManager.Instance != null) MainMenuManager.Instance.RefreshCurrencies();
    }

    public void OnDailyRewardAdButton()
    {
        // Watch ad for 3x daily reward
        int day = GetLoginStreak();
        int baseReward = GetDailyRewardAmount(day);
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddCoins(baseReward * 2); // 2x extra (3x total)

        // Give bonus stone on day 7
        if (day % 7 == 0 && ScoreManager.Instance != null)
            ScoreManager.Instance.AddStones(2); // extra stones

        if (dailyRewardPanel != null) dailyRewardPanel.SetActive(false);
        if (menuPanel != null) menuPanel.SetActive(true);
    }

    // Track game state to sync panels
    private GameManager.GameState lastKnownState = GameManager.GameState.MENU;

    // ── Update Loop ──

    private void Update()
    {
        // Auto-sync panels with GameManager state (failsafe for Android)
        if (GameManager.Instance != null)
        {
            var currentState = GameManager.Instance.CurrentState;
            if (currentState != lastKnownState)
            {
                lastKnownState = currentState;
                switch (currentState)
                {
                    case GameManager.GameState.PLAYING:
                        if (menuPanel != null) menuPanel.SetActive(false);
                        if (gamePanel != null) gamePanel.SetActive(true);
                        if (gameOverPanel != null) gameOverPanel.SetActive(false);
                        if (dailyRewardPanel != null) dailyRewardPanel.SetActive(false);
                        break;
                    case GameManager.GameState.MENU:
                        if (menuPanel != null) menuPanel.SetActive(true);
                        if (gamePanel != null) gamePanel.SetActive(false);
                        if (gameOverPanel != null) gameOverPanel.SetActive(false);
                        break;
                    case GameManager.GameState.GAME_OVER:
                        if (menuPanel != null) menuPanel.SetActive(false);
                        if (gamePanel != null) gamePanel.SetActive(false);
                        if (gameOverPanel != null) gameOverPanel.SetActive(true);
                        break;
                }
            }
        }

        // Revive countdown
        if (reviveCountdownActive)
        {
            reviveTimer -= Time.unscaledDeltaTime;
            if (reviveTimer <= 0)
            {
                reviveCountdownActive = false;
                if (revivePanel != null) revivePanel.SetActive(false);
                Time.timeScale = 1f;
                GameManager.Instance.DeclineRevive();
            }
            else
            {
                // Update countdown text
                var countdownText = revivePanel?.transform.Find("Countdown")?.GetComponent<TextMeshProUGUI>();
                if (countdownText != null)
                    countdownText.text = Mathf.CeilToInt(reviveTimer).ToString();
            }
        }

        var character = FindAnyObjectByType<ArenaCharacter>();
        if (character == null || character.IsDead) return;

        // Wave counter
        if (floorText != null && floorText.gameObject.activeInHierarchy)
        {
            int wave = ArenaManager.Instance != null ? ArenaManager.Instance.CurrentWave : 0;
            floorText.text = "DALGA " + wave;
        }

        // Update HP bar
        if (uiHpBarFill != null)
        {
            float ratio = (float)character.CurrentHP / character.MaxHP;
            uiHpBarFill.fillAmount = ratio;

            Color barColor;
            if (ratio > 0.6f)
                barColor = Color.Lerp(new Color(1f, 0.9f, 0.1f), new Color(0.2f, 0.9f, 0.3f),
                    (ratio - 0.6f) / 0.4f);
            else if (ratio > 0.3f)
                barColor = Color.Lerp(new Color(1f, 0.4f, 0.1f), new Color(1f, 0.9f, 0.1f),
                    (ratio - 0.3f) / 0.3f);
            else
                barColor = Color.Lerp(new Color(0.9f, 0.1f, 0.1f), new Color(1f, 0.4f, 0.1f),
                    ratio / 0.3f);
            uiHpBarFill.color = barColor;

            if (uiHpText != null)
            {
                uiHpText.text = character.CurrentHP + "/" + character.MaxHP;
            }
        }

        // Update Shield bar
        if (uiShieldBarFill != null)
        {
            int maxShield = character.MaxShield;
            int curShield = character.CurrentShield;

            if (maxShield > 0)
            {
                float shieldRatio = (float)curShield / maxShield;
                uiShieldBarFill.fillAmount = shieldRatio;
                uiShieldBarFill.color = new Color(0.60f, 0.75f, 0.95f);
                if (uiShieldBarBg != null)
                    uiShieldBarBg.color = new Color(0.15f, 0.15f, 0.25f, 0.85f);
            }
            else
            {
                // No shield purchased -- gray bar, no fill
                uiShieldBarFill.fillAmount = 0f;
                if (uiShieldBarBg != null)
                    uiShieldBarBg.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);
            }
        }

        // Update XP bar
        if (uiXpBarFill != null && ScoreManager.Instance != null)
        {
            float xpRatio = ScoreManager.Instance.XPToNextLevel > 0
                ? (float)ScoreManager.Instance.CurrentXP / ScoreManager.Instance.XPToNextLevel : 0f;
            uiXpBarFill.fillAmount = xpRatio;
            if (uiLevelText != null)
                uiLevelText.text = "Lv." + ScoreManager.Instance.CurrentLevel;
        }
    }

    // ── Game State Callbacks ──

    private void OnGameStart()
    {
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ResetScore();

        coinsEarnedThisRun = 0;
        ShowGamePanel();
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        UpdateScoreText(0);
        UpdateCoinText(ScoreManager.Instance != null ? ScoreManager.Instance.Coins : 0);
    }

    private void OnGameOver()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.SaveHighScore();
            coinsEarnedThisRun = ScoreManager.Instance.CurrentScore; // approximate
        }

        Invoke(nameof(ShowGameOverPanel), gameOverDelay);
    }

    private void OnRevivePrompt()
    {
        // Pause game time while showing revive prompt
        Time.timeScale = 0f;
        ShowRevivePanel();
    }

    private void OnRevive()
    {
        // Resume game after revive
        Time.timeScale = 1f;
        ShowGamePanel();
    }

    private void OnReturnToMenu()
    {
        ShowMenuPanel();

        // Refresh Archero-style menu currencies
        if (MainMenuManager.Instance != null)
            MainMenuManager.Instance.RefreshCurrencies();
    }

    private void OnScoreChanged(int score) => UpdateScoreText(score);
    private void OnCoinChanged(int coins) => UpdateCoinText(coins);

    private void OnCombo(int combo, int bonus)
    {
        var character = FindAnyObjectByType<ArenaCharacter>();
        if (character == null) return;

        Vector3 pos = character.transform.position;
        Color color = combo > 10 ? new Color(1f, 0.3f, 0.1f) :
                      combo > 5 ? new Color(1f, 0.8f, 0.1f) :
                      new Color(0.2f, 1f, 0.4f);
        float scale = Mathf.Min(1f + combo * 0.1f, 2f);
        FloatingText.Spawn(pos, $"COMBO x{combo}\n+{bonus}", color, scale);

        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayCombo(pos, combo);
    }

    // ── Panel Management ──

    private void ShowMenuPanel()
    {
        if (backgroundCanvas != null) backgroundCanvas.SetActive(true);
        if (menuPanel != null) menuPanel.SetActive(true);
        if (gamePanel != null) gamePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        if (skillsPanel != null) skillsPanel.SetActive(false);
        if (weaponsPanel != null) weaponsPanel.SetActive(false);
        if (bonusCodePanel != null) bonusCodePanel.SetActive(false);
        if (revivePanel != null) revivePanel.SetActive(false);
        if (dailyRewardPanel != null) dailyRewardPanel.SetActive(false);
        if (abilityPickPanel != null) Destroy(abilityPickPanel);
        Time.timeScale = 1f;

        if (AudioManager.Instance != null)
            AudioManager.Instance.StopMusic();

        if (ScoreManager.Instance != null && bestScoreMenuText != null)
            bestScoreMenuText.text = "BEST: " + ScoreManager.Instance.HighScore;
    }

    private void ShowGamePanel()
    {
        if (backgroundCanvas != null) backgroundCanvas.SetActive(false);
        menuPanel.SetActive(false);
        gamePanel.SetActive(true);
        gameOverPanel.SetActive(false);
        if (revivePanel != null) revivePanel.SetActive(false);

        // Joystick is now created by ArenaManager at runtime
    }

    private void ShowGameOverPanel()
    {
        menuPanel.SetActive(false);
        gamePanel.SetActive(false);
        gameOverPanel.SetActive(true);

        if (ScoreManager.Instance != null)
        {
            if (gameOverScoreText != null)
                gameOverScoreText.text = "SCORE: " + ScoreManager.Instance.CurrentScore;
            if (gameOverBestText != null)
                gameOverBestText.text = "BEST: " + ScoreManager.Instance.HighScore;
        }

        // Create reward buttons on game over panel
        CreateGameOverRewardButtons();
    }

    // ── Revive Panel (Runtime UI) ──

    private void ShowRevivePanel()
    {
        if (revivePanel == null)
            CreateRevivePanel();

        revivePanel.SetActive(true);
        gamePanel.SetActive(false);
        reviveTimer = reviveTimeLimit;
        reviveCountdownActive = true;

        // Update coin button state based on whether player can afford it
        var coinBtn = revivePanel.transform.Find("ReviveCoinBtn");
        if (coinBtn != null)
        {
            bool canAfford = ScoreManager.Instance != null && ScoreManager.Instance.Coins >= 100;
            var btnComp = coinBtn.GetComponent<Button>();
            if (btnComp != null) btnComp.interactable = canAfford;
            var img = coinBtn.GetComponent<Image>();
            if (img != null) img.color = canAfford
                ? new Color(0.85f, 0.65f, 0.1f)
                : new Color(0.4f, 0.35f, 0.3f);
        }
    }

    private void CreateRevivePanel()
    {
        // Create as ROOT object -- not child of UIManager canvas, so CanvasScaler works properly
        revivePanel = new GameObject("RevivePanel");
        var canvas = revivePanel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = revivePanel.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        revivePanel.AddComponent<GraphicRaycaster>();

        // Full-screen dim background
        var bg = CreateUIElement(revivePanel.transform, "BG", Vector2.zero, Vector2.one);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.8f);

        // Big red X icon
        var icon = CreateUIElement(revivePanel.transform, "DeathIcon",
            new Vector2(0.3f, 0.68f), new Vector2(0.7f, 0.85f));
        var iconTmp = icon.AddComponent<TextMeshProUGUI>();
        iconTmp.text = "X";
        iconTmp.fontSize = 120;
        iconTmp.color = new Color(1f, 0.2f, 0.15f);
        iconTmp.fontStyle = FontStyles.Bold;
        iconTmp.alignment = TextAlignmentOptions.Center;
        iconTmp.enableAutoSizing = true;
        iconTmp.fontSizeMin = 60;
        iconTmp.fontSizeMax = 120;
        iconTmp.characterSpacing = 4f;

        // Title - OLDUN! big and centered
        var title = CreateUIElement(revivePanel.transform, "Title",
            new Vector2(0.1f, 0.56f), new Vector2(0.9f, 0.7f));
        var titleTmp = title.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "OLDUN!";
        titleTmp.fontSize = 80;
        titleTmp.color = new Color(1f, 0.3f, 0.2f);
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.enableAutoSizing = true;
        titleTmp.fontSizeMin = 40;
        titleTmp.fontSizeMax = 80;
        titleTmp.characterSpacing = 4f;

        // Subtitle
        var subtitle = CreateUIElement(revivePanel.transform, "Subtitle",
            new Vector2(0.15f, 0.50f), new Vector2(0.85f, 0.57f));
        var subtitleTmp = subtitle.AddComponent<TextMeshProUGUI>();
        subtitleTmp.text = "Devam etmek ister misin?";
        subtitleTmp.fontSize = 28;
        subtitleTmp.color = new Color(0.8f, 0.8f, 0.8f);
        subtitleTmp.alignment = TextAlignmentOptions.Center;
        subtitleTmp.enableAutoSizing = true;
        subtitleTmp.fontSizeMin = 16;
        subtitleTmp.fontSizeMax = 28;
        subtitleTmp.characterSpacing = 4f;

        // Countdown - big yellow
        var countdown = CreateUIElement(revivePanel.transform, "Countdown",
            new Vector2(0.35f, 0.42f), new Vector2(0.65f, 0.50f));
        var countdownTmp = countdown.AddComponent<TextMeshProUGUI>();
        countdownTmp.text = "5";
        countdownTmp.fontSize = 56;
        countdownTmp.color = Color.yellow;
        countdownTmp.fontStyle = FontStyles.Bold;
        countdownTmp.alignment = TextAlignmentOptions.Center;

        // Watch ad button - large green
        var adBtn = CreateUIElement(revivePanel.transform, "ReviveAdBtn",
            new Vector2(0.1f, 0.32f), new Vector2(0.9f, 0.44f));
        var adBtnImg = adBtn.AddComponent<Image>();
        adBtnImg.color = new Color(0.1f, 0.7f, 0.25f);
        var adButton = adBtn.AddComponent<Button>();
        adButton.onClick.AddListener(OnReviveAdButton);

        var adLabel = CreateUIElement(adBtn.transform, "Label", Vector2.zero, Vector2.one);
        var adTmp = adLabel.AddComponent<TextMeshProUGUI>();
        adTmp.text = "REKLAM IZLE = DIRILIS";
        adTmp.fontSize = 36;
        adTmp.color = Color.white;
        adTmp.fontStyle = FontStyles.Bold;
        adTmp.alignment = TextAlignmentOptions.Center;
        adTmp.enableAutoSizing = true;
        adTmp.fontSizeMin = 18;
        adTmp.fontSizeMax = 36;
        adTmp.characterSpacing = 4f;

        // Coin revive button - gold/yellow
        var coinBtn = CreateUIElement(revivePanel.transform, "ReviveCoinBtn",
            new Vector2(0.1f, 0.19f), new Vector2(0.9f, 0.31f));
        var coinBtnImg = coinBtn.AddComponent<Image>();
        coinBtnImg.color = new Color(0.85f, 0.65f, 0.1f);
        var coinButton = coinBtn.AddComponent<Button>();
        coinButton.onClick.AddListener(OnReviveCoinButton);

        var coinLabel = CreateUIElement(coinBtn.transform, "Label", Vector2.zero, Vector2.one);
        var coinTmp = coinLabel.AddComponent<TextMeshProUGUI>();
        coinTmp.text = "100 ALTIN = DIRILIS";
        coinTmp.fontSize = 34;
        coinTmp.color = Color.white;
        coinTmp.fontStyle = FontStyles.Bold;
        coinTmp.alignment = TextAlignmentOptions.Center;
        coinTmp.enableAutoSizing = true;
        coinTmp.fontSizeMin = 16;
        coinTmp.fontSizeMax = 34;
        coinTmp.characterSpacing = 4f;

        // Skip button
        var skipBtn = CreateUIElement(revivePanel.transform, "SkipBtn",
            new Vector2(0.25f, 0.08f), new Vector2(0.75f, 0.17f));
        var skipBtnImg = skipBtn.AddComponent<Image>();
        skipBtnImg.color = new Color(0.35f, 0.15f, 0.15f);
        var skipButton = skipBtn.AddComponent<Button>();
        skipButton.onClick.AddListener(OnReviveSkipButton);

        var skipLabel = CreateUIElement(skipBtn.transform, "Label", Vector2.zero, Vector2.one);
        var skipTmp = skipLabel.AddComponent<TextMeshProUGUI>();
        skipTmp.text = "VAZGEC";
        skipTmp.fontSize = 28;
        skipTmp.color = new Color(0.7f, 0.7f, 0.7f);
        skipTmp.alignment = TextAlignmentOptions.Center;
        skipTmp.enableAutoSizing = true;
        skipTmp.fontSizeMin = 14;
        skipTmp.fontSizeMax = 28;
        skipTmp.characterSpacing = 4f;
    }

    // ── Game Over Reward Buttons ──

    private void CreateGameOverRewardButtons()
    {
        // Clean up old ones
        if (doubleCoinsBtn != null) Destroy(doubleCoinsBtn);
        if (freeStoneBtn != null) Destroy(freeStoneBtn);

        if (gameOverPanel == null) return;
        var parent = gameOverPanel.transform;

        // 2x Coins button
        doubleCoinsBtn = CreateRewardButton(parent, "DoubleCoinBtn",
            new Vector2(0.05f, 0.05f), new Vector2(0.48f, 0.18f),
            "REKLAM IZLE = 2X COIN", new Color(1f, 0.85f, 0.2f),
            OnDoubleCoinsButton);

        // Free stone button
        freeStoneBtn = CreateRewardButton(parent, "FreeStoneBtn",
            new Vector2(0.52f, 0.05f), new Vector2(0.95f, 0.18f),
            "REKLAM IZLE = +1 TAS", new Color(0.7f, 0.3f, 1f),
            OnFreeStoneButton);
    }

    private GameObject CreateRewardButton(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, string text, Color accentColor,
        UnityEngine.Events.UnityAction onClick)
    {
        var btnObj = CreateUIElement(parent, name, anchorMin, anchorMax);
        var btnImg = btnObj.AddComponent<Image>();
        btnImg.color = new Color(accentColor.r * 0.3f, accentColor.g * 0.3f, accentColor.b * 0.3f, 0.95f);

        var button = btnObj.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = btnImg.color;
        colors.highlightedColor = new Color(accentColor.r * 0.5f, accentColor.g * 0.5f, accentColor.b * 0.5f);
        colors.pressedColor = new Color(accentColor.r * 0.2f, accentColor.g * 0.2f, accentColor.b * 0.2f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        var label = CreateUIElement(btnObj.transform, "Label", Vector2.zero, Vector2.one);
        var tmp = label.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 18;
        tmp.color = accentColor;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 10;
        tmp.fontSizeMax = 18;
        tmp.characterSpacing = 4f;

        return btnObj;
    }

    // ── Daily Reward System ──

    private void CheckDailyReward()
    {
        string lastClaimDate = PlayerPrefs.GetString("daily_last", "");
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");

        if (lastClaimDate == today)
        {
            dailyRewardShown = true;
            return;
        }

        // Update streak
        string yesterday = System.DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
        int streak = PlayerPrefs.GetInt("daily_streak", 0);
        if (lastClaimDate == yesterday)
            streak++;
        else
            streak = 1; // Reset streak

        PlayerPrefs.SetInt("daily_streak", streak);
        PlayerPrefs.SetString("daily_last", today);
        PlayerPrefs.Save();

        // Give base daily reward
        int reward = GetDailyRewardAmount(streak);
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddCoins(reward);

        // Give stone every 3rd day
        if (streak % 3 == 0 && ScoreManager.Instance != null)
            ScoreManager.Instance.AddStone();

        // Show daily reward popup
        ShowDailyRewardPanel(streak, reward);
    }

    private int GetDailyRewardAmount(int day)
    {
        // Escalating daily rewards: 10, 15, 20, 25, 30, 40, 60 (resets after 7)
        int cycleDay = ((day - 1) % 7) + 1;
        return cycleDay switch
        {
            1 => 10,
            2 => 15,
            3 => 20,
            4 => 25,
            5 => 30,
            6 => 40,
            7 => 60,
            _ => 10
        };
    }

    private int GetLoginStreak()
    {
        return PlayerPrefs.GetInt("daily_streak", 1);
    }

    private void ShowDailyRewardPanel(int day, int reward)
    {
        // Always recreate so the 7-day grid reflects the correct streak state
        if (dailyRewardPanel != null)
        {
            Destroy(dailyRewardPanel);
            dailyRewardPanel = null;
        }
        CreateDailyRewardPanel(day, reward);
        dailyRewardPanel.SetActive(true);
        menuPanel.SetActive(false);
    }

    // ── Archero-style daily reward panel ──
    private void CreateDailyRewardPanel(int currentDay, int currentReward)
    {
        dailyRewardPanel = new GameObject("DailyRewardPanel");
        var canvas = dailyRewardPanel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        var scaler = dailyRewardPanel.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        dailyRewardPanel.AddComponent<GraphicRaycaster>();

        // ── Full-screen dark overlay ──
        var overlay = CreateUIElement(dailyRewardPanel.transform, "Overlay", Vector2.zero, Vector2.one);
        overlay.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

        // ── Floating card (centered, not full-screen) ──
        // Card occupies 92% width, 75% height, vertically centered
        var card = CreateUIElement(dailyRewardPanel.transform, "Card",
            new Vector2(0.04f, 0.13f), new Vector2(0.96f, 0.89f));

        // Card background -- deep navy
        card.AddComponent<Image>().color = new Color(0.07f, 0.09f, 0.21f, 1f);

        // Card top gold border strip
        var topBorder = CreateUIElement(card.transform, "TopBorder",
            new Vector2(0f, 0.987f), new Vector2(1f, 1f));
        topBorder.AddComponent<Image>().color = new Color(1f, 0.76f, 0.05f, 1f);

        // Card bottom gold border strip
        var botBorder = CreateUIElement(card.transform, "BotBorder",
            new Vector2(0f, 0f), new Vector2(1f, 0.013f));
        botBorder.AddComponent<Image>().color = new Color(1f, 0.76f, 0.05f, 1f);

        // ── Title section ──
        var lineL = CreateUIElement(card.transform, "LineL",
            new Vector2(0.03f, 0.935f), new Vector2(0.24f, 0.948f));
        lineL.AddComponent<Image>().color = new Color(1f, 0.76f, 0.05f, 0.85f);

        var title = CreateUIElement(card.transform, "Title",
            new Vector2(0.24f, 0.925f), new Vector2(0.76f, 0.977f));
        var titleTmp = title.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "GUNLUK ODUL";
        titleTmp.fontSize = 38;
        titleTmp.color = new Color(1f, 0.83f, 0.08f);
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.enableAutoSizing = true;
        titleTmp.fontSizeMin = 18; titleTmp.fontSizeMax = 38;
        titleTmp.characterSpacing = 4f;

        var lineR = CreateUIElement(card.transform, "LineR",
            new Vector2(0.76f, 0.935f), new Vector2(0.97f, 0.948f));
        lineR.AddComponent<Image>().color = new Color(1f, 0.76f, 0.05f, 0.85f);

        // Divider below title
        var div1 = CreateUIElement(card.transform, "Div1",
            new Vector2(0.04f, 0.916f), new Vector2(0.96f, 0.920f));
        div1.AddComponent<Image>().color = new Color(1f, 0.76f, 0.05f, 0.30f);

        // ── 7-Day streak grid ──
        int[] dayRewards = { 10, 15, 20, 25, 30, 40, 60 };
        int cycleDay = ((currentDay - 1) % 7) + 1;

        float stripYMin = 0.720f, stripYMax = 0.908f;
        float startX = 0.025f;
        float cellGap = 0.007f;
        float cellW = (0.95f - cellGap * 6f) / 7f; // 0.95 total strip width

        for (int i = 0; i < 7; i++)
        {
            int d = i + 1;
            float xMin = startX + i * (cellW + cellGap);
            float xMax = xMin + cellW;
            bool isCurr = (d == cycleDay);
            bool isPast = (d < cycleDay);

            var cell = CreateUIElement(card.transform, "Day" + d,
                new Vector2(xMin, stripYMin), new Vector2(xMax, stripYMax));

            // Cell BG
            Color bgCol = isPast
                ? new Color(0.06f, 0.07f, 0.14f, 1f)
                : isCurr ? new Color(0.20f, 0.14f, 0.02f, 1f)
                : new Color(0.10f, 0.13f, 0.28f, 1f);
            cell.AddComponent<Image>().color = bgCol;

            // Gold border frame for current day (4 strips)
            if (isCurr)
            {
                Color gold = new Color(1f, 0.80f, 0f, 1f);
                float b = 0.03f;
                var bT = CreateUIElement(cell.transform, "BT", new Vector2(0, 1f - b), new Vector2(1f, 1f));
                bT.AddComponent<Image>().color = gold;
                var bB = CreateUIElement(cell.transform, "BB", new Vector2(0, 0), new Vector2(1f, b));
                bB.AddComponent<Image>().color = gold;
                var bLe = CreateUIElement(cell.transform, "BL", new Vector2(0, b), new Vector2(b * 0.45f, 1f - b));
                bLe.AddComponent<Image>().color = gold;
                var bRi = CreateUIElement(cell.transform, "BR", new Vector2(1f - b * 0.45f, b), new Vector2(1f, 1f - b));
                bRi.AddComponent<Image>().color = gold;
            }

            // Day label ("G1", "G2" …)
            var dayLabel = CreateUIElement(cell.transform, "DayLbl",
                new Vector2(0f, 0.77f), new Vector2(1f, 1f));
            var dayLblTmp = dayLabel.AddComponent<TextMeshProUGUI>();
            dayLblTmp.text = "G" + d;
            dayLblTmp.color = isCurr ? new Color(1f, 0.88f, 0.2f)
                : isPast ? new Color(0.40f, 0.40f, 0.50f)
                : new Color(0.70f, 0.70f, 0.88f);
            dayLblTmp.fontStyle = FontStyles.Bold;
            dayLblTmp.alignment = TextAlignmentOptions.Center;
            dayLblTmp.enableAutoSizing = true;
            dayLblTmp.fontSizeMin = 8; dayLblTmp.fontSizeMax = 20;
            dayLblTmp.characterSpacing = 4f;

            // Coin icon (outer circle)
            var coin = CreateUIElement(cell.transform, "Coin",
                new Vector2(0.12f, 0.33f), new Vector2(0.88f, 0.77f));
            coin.AddComponent<Image>().color = isPast
                ? new Color(0.22f, 0.22f, 0.28f)
                : isCurr ? new Color(1f, 0.74f, 0f)
                : new Color(0.62f, 0.52f, 0.08f);

            // Coin inner highlight
            if (!isPast)
            {
                var coinIn = CreateUIElement(coin.transform, "Inner",
                    new Vector2(0.14f, 0.14f), new Vector2(0.86f, 0.86f));
                coinIn.AddComponent<Image>().color = isCurr
                    ? new Color(1f, 0.93f, 0.42f)
                    : new Color(0.74f, 0.63f, 0.12f);
            }

            // Amount text
            var amtLabel = CreateUIElement(cell.transform, "Amt",
                new Vector2(0f, 0.02f), new Vector2(1f, 0.33f));
            var amtTmp = amtLabel.AddComponent<TextMeshProUGUI>();
            amtTmp.text = dayRewards[i].ToString();
            amtTmp.color = isCurr ? Color.white
                : isPast ? new Color(0.38f, 0.38f, 0.46f)
                : new Color(0.78f, 0.78f, 0.88f);
            amtTmp.fontStyle = FontStyles.Bold;
            amtTmp.alignment = TextAlignmentOptions.Center;
            amtTmp.enableAutoSizing = true;
            amtTmp.fontSizeMin = 8; amtTmp.fontSizeMax = 18;
            amtTmp.characterSpacing = 4f;
        }

        // ── Main reward display ──
        // Large coin icon
        var bigCoin = CreateUIElement(card.transform, "BigCoin",
            new Vector2(0.36f, 0.500f), new Vector2(0.64f, 0.700f));
        bigCoin.AddComponent<Image>().color = new Color(1f, 0.74f, 0f);
        var bigCoinIn = CreateUIElement(bigCoin.transform, "Inner",
            new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.92f));
        bigCoinIn.AddComponent<Image>().color = new Color(1f, 0.92f, 0.40f);

        // Reward amount (green, large)
        string bonusText = (cycleDay % 3 == 0) ? " + 1 TAS" : "";
        var rewardAmtObj = CreateUIElement(card.transform, "RewardAmt",
            new Vector2(0.05f, 0.425f), new Vector2(0.95f, 0.492f));
        var rewardAmtTmp = rewardAmtObj.AddComponent<TextMeshProUGUI>();
        rewardAmtTmp.text = "+" + currentReward + " COIN" + bonusText;
        rewardAmtTmp.fontSize = 42;
        rewardAmtTmp.color = new Color(0.22f, 0.98f, 0.50f);
        rewardAmtTmp.fontStyle = FontStyles.Bold;
        rewardAmtTmp.alignment = TextAlignmentOptions.Center;
        rewardAmtTmp.enableAutoSizing = true;
        rewardAmtTmp.fontSizeMin = 20; rewardAmtTmp.fontSizeMax = 42;
        rewardAmtTmp.characterSpacing = 4f;

        // Day subtitle ("GUN 3 ODULU")
        var daySubObj = CreateUIElement(card.transform, "DaySub",
            new Vector2(0.05f, 0.375f), new Vector2(0.95f, 0.420f));
        var daySubTmp = daySubObj.AddComponent<TextMeshProUGUI>();
        daySubTmp.text = "GUN " + currentDay + " ODULU";
        daySubTmp.fontSize = 26;
        daySubTmp.color = new Color(0.70f, 0.70f, 0.88f);
        daySubTmp.fontStyle = FontStyles.Bold;
        daySubTmp.alignment = TextAlignmentOptions.Center;
        daySubTmp.enableAutoSizing = true;
        daySubTmp.fontSizeMin = 13; daySubTmp.fontSizeMax = 26;
        daySubTmp.characterSpacing = 4f;

        // Divider
        var div2 = CreateUIElement(card.transform, "Div2",
            new Vector2(0.04f, 0.360f), new Vector2(0.96f, 0.364f));
        div2.AddComponent<Image>().color = new Color(1f, 0.76f, 0.05f, 0.28f);

        // ── TOPLA button (Archero orange) ──
        var collectBtn = CreateUIElement(card.transform, "CollectBtn",
            new Vector2(0.10f, 0.222f), new Vector2(0.90f, 0.340f));
        collectBtn.AddComponent<Image>().color = new Color(0.95f, 0.50f, 0.05f);
        var collectButton = collectBtn.AddComponent<Button>();
        collectButton.onClick.AddListener(OnDailyRewardButton);

        // Subtle top highlight for pseudo-gradient
        var collectHL = CreateUIElement(collectBtn.transform, "HL",
            new Vector2(0.02f, 0.52f), new Vector2(0.98f, 0.90f));
        collectHL.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.11f);

        var collectLbl = CreateUIElement(collectBtn.transform, "Lbl", Vector2.zero, Vector2.one);
        var collectTmp = collectLbl.AddComponent<TextMeshProUGUI>();
        collectTmp.text = "TOPLA";
        collectTmp.fontSize = 34;
        collectTmp.color = Color.white;
        collectTmp.fontStyle = FontStyles.Bold;
        collectTmp.alignment = TextAlignmentOptions.Center;
        collectTmp.enableAutoSizing = true;
        collectTmp.fontSizeMin = 16; collectTmp.fontSizeMax = 34;
        collectTmp.characterSpacing = 4f;

        // ── 3x Ad button (blue) ──
        var adBtn = CreateUIElement(card.transform, "AdBtn",
            new Vector2(0.12f, 0.068f), new Vector2(0.88f, 0.200f));
        adBtn.AddComponent<Image>().color = new Color(0.14f, 0.34f, 0.84f);
        var adButton = adBtn.AddComponent<Button>();
        adButton.onClick.AddListener(OnDailyRewardAdButton);

        var adLbl = CreateUIElement(adBtn.transform, "Lbl", Vector2.zero, Vector2.one);
        var adTmp = adLbl.AddComponent<TextMeshProUGUI>();
        adTmp.text = "\u25B6  REKLAM IZLE = 3X ODUL";
        adTmp.fontSize = 22;
        adTmp.color = Color.white;
        adTmp.fontStyle = FontStyles.Bold;
        adTmp.alignment = TextAlignmentOptions.Center;
        adTmp.enableAutoSizing = true;
        adTmp.fontSizeMin = 11; adTmp.fontSizeMax = 22;
        adTmp.characterSpacing = 4f;
    }

    // ── Helpers ──

    private static Sprite _whiteSprite;
    private static Sprite CreateWhiteSprite()
    {
        if (_whiteSprite != null) return _whiteSprite;
        var tex = new Texture2D(4, 4);
        var pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        _whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        return _whiteSprite;
    }

    private GameObject CreateUIElement(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return obj;
    }

    private void CreateHealthBars()
    {
        if (gamePanel == null) return;

        // ── XP Bar (below top row, thin, full width) ──
        var xpRoot = new GameObject("XpBarRoot");
        xpRoot.transform.SetParent(gamePanel.transform, false);
        var xpRootRt = xpRoot.AddComponent<RectTransform>();
        xpRootRt.anchorMin = new Vector2(0.05f, 0.93f);
        xpRootRt.anchorMax = new Vector2(0.95f, 0.955f);
        xpRootRt.offsetMin = Vector2.zero;
        xpRootRt.offsetMax = Vector2.zero;

        var xpBg = new GameObject("XpBg");
        xpBg.transform.SetParent(xpRoot.transform, false);
        var xpBgRt = xpBg.AddComponent<RectTransform>();
        xpBgRt.anchorMin = Vector2.zero; xpBgRt.anchorMax = Vector2.one;
        xpBgRt.offsetMin = Vector2.zero; xpBgRt.offsetMax = Vector2.zero;
        var xpBgImg = xpBg.AddComponent<Image>();
        xpBgImg.color = new Color(0.15f, 0.15f, 0.2f, 0.8f);

        var xpFill = new GameObject("XpFill");
        xpFill.transform.SetParent(xpRoot.transform, false);
        var xpFillRt = xpFill.AddComponent<RectTransform>();
        xpFillRt.anchorMin = Vector2.zero; xpFillRt.anchorMax = Vector2.one;
        xpFillRt.offsetMin = new Vector2(1, 1); xpFillRt.offsetMax = new Vector2(-1, -1);
        uiXpBarFill = xpFill.AddComponent<Image>();
        uiXpBarFill.sprite = CreateWhiteSprite();
        uiXpBarFill.color = new Color(0.55f, 0.78f, 0.94f);
        uiXpBarFill.type = Image.Type.Filled;
        uiXpBarFill.fillMethod = Image.FillMethod.Horizontal;
        uiXpBarFill.fillAmount = 0f;

        // Level text (left, between top row and XP bar)
        var lvlObj = new GameObject("LevelText");
        lvlObj.transform.SetParent(gamePanel.transform, false);
        var lvlRt = lvlObj.AddComponent<RectTransform>();
        lvlRt.anchorMin = new Vector2(0.05f, 0.955f);
        lvlRt.anchorMax = new Vector2(0.20f, 0.96f);
        lvlRt.offsetMin = Vector2.zero; lvlRt.offsetMax = Vector2.zero;
        uiLevelText = lvlObj.AddComponent<TextMeshProUGUI>();
        uiLevelText.fontSize = 30;
        uiLevelText.fontStyle = FontStyles.Bold;
        uiLevelText.color = new Color(0.55f, 0.78f, 0.94f);
        uiLevelText.alignment = TextAlignmentOptions.Left;
        uiLevelText.enableAutoSizing = true;
        uiLevelText.fontSizeMin = 16;
        uiLevelText.fontSizeMax = 30;
        uiLevelText.characterSpacing = 4f;
        uiLevelText.text = "Lv.1";

        // HP/Shield bars removed from HUD — now shown as overhead bars on ArenaCharacter
    }

    private void CreateStoneHud()
    {
        if (gamePanel == null) return;

        // Blue stone counter
        var stoneObj = new GameObject("StoneHud");
        stoneObj.transform.SetParent(gamePanel.transform, false);
        var stoneRt = stoneObj.AddComponent<RectTransform>();
        stoneRt.anchorMin = new Vector2(0.65f, 0.04f);
        stoneRt.anchorMax = new Vector2(0.98f, 0.08f);
        stoneRt.offsetMin = Vector2.zero;
        stoneRt.offsetMax = Vector2.zero;
        stoneHudText = stoneObj.AddComponent<TextMeshProUGUI>();
        stoneHudText.fontSize = 28;
        stoneHudText.color = new Color(0.62f, 0.72f, 0.95f);
        stoneHudText.fontStyle = FontStyles.Bold;
        stoneHudText.alignment = TextAlignmentOptions.Right;
        stoneHudText.enableAutoSizing = true;
        stoneHudText.fontSizeMin = 14;
        stoneHudText.fontSizeMax = 28;
        stoneHudText.characterSpacing = 4f;
        int stones = ScoreManager.Instance != null ? ScoreManager.Instance.UpgradeStones : 0;
        stoneHudText.text = "<sprite name=\"diamond\"> " + stones;

        // Emerald counter
        var emeraldObj = new GameObject("EmeraldHud");
        emeraldObj.transform.SetParent(gamePanel.transform, false);
        var emeraldRt = emeraldObj.AddComponent<RectTransform>();
        emeraldRt.anchorMin = new Vector2(0.65f, 0.005f);
        emeraldRt.anchorMax = new Vector2(0.98f, 0.04f);
        emeraldRt.offsetMin = Vector2.zero;
        emeraldRt.offsetMax = Vector2.zero;
        emeraldHudText = emeraldObj.AddComponent<TextMeshProUGUI>();
        emeraldHudText.fontSize = 28;
        emeraldHudText.color = new Color(0.55f, 0.85f, 0.62f);
        emeraldHudText.fontStyle = FontStyles.Bold;
        emeraldHudText.alignment = TextAlignmentOptions.Right;
        emeraldHudText.enableAutoSizing = true;
        emeraldHudText.fontSizeMin = 14;
        emeraldHudText.fontSizeMax = 28;
        emeraldHudText.characterSpacing = 4f;
        int emeralds = ScoreManager.Instance != null ? ScoreManager.Instance.Emeralds : 0;
        emeraldHudText.text = "<sprite name=\"diamond\"> " + emeralds;
    }

    private void OnLevelUp(int level)
    {
        Time.timeScale = 0f;
        ShowAbilityPick();
    }

    private void ShowAbilityPick()
    {
        if (AbilitySystem.Instance == null) return;

        var choices = AbilitySystem.Instance.GetRandomChoices(3);
        if (choices.Count == 0) { Time.timeScale = 1f; return; }

        if (abilityPickPanel != null) Destroy(abilityPickPanel);

        abilityPickPanel = new GameObject("AbilityPickPanel");
        var canvas = abilityPickPanel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 90;
        var scaler = abilityPickPanel.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        abilityPickPanel.AddComponent<GraphicRaycaster>();

        // Dim BG
        var bg = CreateUIElement(abilityPickPanel.transform, "BG", Vector2.zero, Vector2.one);
        bg.AddComponent<Image>().color = new Color(0, 0, 0, 0.8f);

        // Title banner
        var titleBg = CreateUIElement(abilityPickPanel.transform, "TitleBg",
            new Vector2(0.1f, 0.68f), new Vector2(0.9f, 0.76f));
        titleBg.AddComponent<Image>().color = new Color(0.85f, 0.7f, 0.1f, 0.9f);

        var titleObj = CreateUIElement(titleBg.transform, "TitleText", Vector2.zero, Vector2.one);
        var titleTmp = titleObj.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "YETENEK SEC";
        titleTmp.fontSize = 42;
        titleTmp.color = Color.white;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.enableAutoSizing = true;
        titleTmp.fontSizeMin = 20; titleTmp.fontSizeMax = 42;
        titleTmp.characterSpacing = 6f;
        titleTmp.raycastTarget = false;

        // Subtitle
        var subtitleObj = CreateUIElement(abilityPickPanel.transform, "Subtitle",
            new Vector2(0.1f, 0.64f), new Vector2(0.9f, 0.68f));
        var subtitleTmp = subtitleObj.AddComponent<TextMeshProUGUI>();
        subtitleTmp.text = "Bir yetenek sec!";
        subtitleTmp.fontSize = 22;
        subtitleTmp.color = new Color(0.8f, 0.8f, 0.8f);
        subtitleTmp.alignment = TextAlignmentOptions.Center;
        subtitleTmp.enableAutoSizing = true;
        subtitleTmp.fontSizeMin = 12; subtitleTmp.fontSizeMax = 22;
        subtitleTmp.characterSpacing = 4f;
        subtitleTmp.raycastTarget = false;

        // 3 cards side by side — Archero style vertical cards
        float cardWidth = 0.29f;
        float cardGap = 0.02f;
        float startX = 0.055f;
        float cardYMin = 0.28f;
        float cardYMax = 0.62f;

        for (int i = 0; i < choices.Count; i++)
        {
            var (abilityDef, tier) = choices[i];
            Color rarityColor = AbilitySystem.GetRarityColor(tier.rarity);
            Color rarityDark = new Color(rarityColor.r * 0.25f, rarityColor.g * 0.25f, rarityColor.b * 0.25f, 0.95f);
            string rarityName = AbilitySystem.GetRarityName(tier.rarity);

            float xMin = startX + i * (cardWidth + cardGap);
            float xMax = xMin + cardWidth;

            // Card outer border (rarity color)
            var cardBorder = CreateUIElement(abilityPickPanel.transform, "CardBorder_" + i,
                new Vector2(xMin, cardYMin), new Vector2(xMax, cardYMax));
            cardBorder.AddComponent<Image>().color = rarityColor;

            // Card inner bg (dark)
            var card = CreateUIElement(cardBorder.transform, "Card",
                new Vector2(0.02f, 0.01f), new Vector2(0.98f, 0.99f));
            card.AddComponent<Image>().color = rarityDark;

            // Rarity badge (top center)
            var badgeBg = CreateUIElement(card.transform, "BadgeBg",
                new Vector2(0.15f, 0.88f), new Vector2(0.85f, 0.98f));
            badgeBg.AddComponent<Image>().color = new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.4f);
            var badgeText = CreateUIElement(badgeBg.transform, "Text", Vector2.zero, Vector2.one);
            var badgeTmp = badgeText.AddComponent<TextMeshProUGUI>();
            badgeTmp.text = rarityName;
            badgeTmp.fontSize = 14;
            badgeTmp.color = Color.white;
            badgeTmp.fontStyle = FontStyles.Bold;
            badgeTmp.alignment = TextAlignmentOptions.Center;
            badgeTmp.enableAutoSizing = true;
            badgeTmp.fontSizeMin = 8; badgeTmp.fontSizeMax = 14;
            badgeTmp.characterSpacing = 3f;
            badgeTmp.raycastTarget = false;

            // Icon area (center, large)
            var iconBg = CreateUIElement(card.transform, "IconBg",
                new Vector2(0.15f, 0.50f), new Vector2(0.85f, 0.86f));
            iconBg.AddComponent<Image>().color = new Color(rarityColor.r * 0.4f, rarityColor.g * 0.4f, rarityColor.b * 0.4f, 0.8f);
            var iconText = CreateUIElement(iconBg.transform, "Icon", Vector2.zero, Vector2.one);
            var iconTmp = iconText.AddComponent<TextMeshProUGUI>();
            iconTmp.text = GetAbilityIcon(abilityDef.id);
            iconTmp.fontSize = 40;
            iconTmp.color = rarityColor;
            iconTmp.fontStyle = FontStyles.Bold;
            iconTmp.alignment = TextAlignmentOptions.Center;
            iconTmp.enableAutoSizing = true;
            iconTmp.fontSizeMin = 20; iconTmp.fontSizeMax = 40;
            iconTmp.raycastTarget = false;

            // Name (below icon)
            var nameObj = CreateUIElement(card.transform, "Name",
                new Vector2(0.05f, 0.32f), new Vector2(0.95f, 0.50f));
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = abilityDef.name;
            nameTmp.fontSize = 20;
            nameTmp.color = Color.white;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.enableAutoSizing = true;
            nameTmp.fontSizeMin = 10; nameTmp.fontSizeMax = 20;
            nameTmp.characterSpacing = 3f;
            nameTmp.raycastTarget = false;

            // Description (bottom)
            var descObj = CreateUIElement(card.transform, "Desc",
                new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.32f));
            var descTmp = descObj.AddComponent<TextMeshProUGUI>();
            descTmp.text = tier.description;
            descTmp.fontSize = 14;
            descTmp.color = new Color(0.8f, 0.8f, 0.8f);
            descTmp.alignment = TextAlignmentOptions.Center;
            descTmp.enableAutoSizing = true;
            descTmp.fontSizeMin = 8; descTmp.fontSizeMax = 14;
            descTmp.characterSpacing = 3f;
            descTmp.raycastTarget = false;

            // Button (entire card border is clickable)
            var btn = cardBorder.AddComponent<Button>();
            var capturedId = abilityDef.id;
            var capturedValue = tier.value;
            btn.onClick.AddListener(() => OnAbilityChosen(capturedId, capturedValue));
        }
    }

    private string GetAbilityIcon(AbilitySystem.AbilityId id)
    {
        return id switch
        {
            AbilitySystem.AbilityId.MultiHit       => "II",
            AbilitySystem.AbilityId.AttackSpeedUp   => ">>",
            AbilitySystem.AbilityId.AttackDamageUp  => "ATK",
            AbilitySystem.AbilityId.MoveSpeedUp     => "SPD",
            AbilitySystem.AbilityId.HPBoost         => "+HP",
            AbilitySystem.AbilityId.Heal            => "+",
            AbilitySystem.AbilityId.SlowOnHit       => "*",
            AbilitySystem.AbilityId.MagnetRange     => "U",
            AbilitySystem.AbilityId.Armor           => "DEF",
            AbilitySystem.AbilityId.Vampirism       => "V",
            AbilitySystem.AbilityId.Reflect         => "<>",
            AbilitySystem.AbilityId.CriticalHit     => "!!",
            AbilitySystem.AbilityId.Dodge           => "~",
            AbilitySystem.AbilityId.Pierce          => "-->",
            AbilitySystem.AbilityId.BounceWall      => "/\\",
            AbilitySystem.AbilityId.RearArrow       => "<-",
            AbilitySystem.AbilityId.DiagonalArrow   => "X",
            AbilitySystem.AbilityId.Cleave          => ")(",
            AbilitySystem.AbilityId.Stun            => "ZZ",
            _ => "?"
        };
    }

    private void OnAbilityChosen(AbilitySystem.AbilityId id, float tierValue)
    {
        if (AbilitySystem.Instance != null)
            AbilitySystem.Instance.AcquireAbility(id, tierValue);

        if (abilityPickPanel != null) Destroy(abilityPickPanel);
        Time.timeScale = 1f;
    }

    private void OnStoneChanged(int count)
    {
        if (stoneHudText != null) stoneHudText.text = "<sprite name=\"diamond\"> " + count;
    }

    private void OnEmeraldChanged(int count)
    {
        if (emeraldHudText != null) emeraldHudText.text = "<sprite name=\"diamond\"> " + count;
    }

    private void UpdateScoreText(int score)
    {
        if (currentScoreText != null)
            currentScoreText.text = score.ToString();
    }

    private void UpdateCoinText(int coins)
    {
        if (coinText != null)
            coinText.text = "$" + coins;
    }

    private void RepositionToBottom(Component comp, float ancMinX, float ancMinY, float ancMaxX, float ancMaxY)
    {
        if (comp == null) return;
        var rt = comp.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = new Vector2(ancMinX, ancMinY);
        rt.anchorMax = new Vector2(ancMaxX, ancMaxY);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
