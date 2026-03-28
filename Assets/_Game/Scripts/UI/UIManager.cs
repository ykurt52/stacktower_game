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

        // Top HUD layout — Row 1 (top): pause left, wave center, coin right
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
        int result = BonusCodeManager.Instance.TryRedeem(code);

        if (bonusCodeResult != null)
        {
            if (result > 0)
            {
                bonusCodeResult.text = "+" + result + " COIN!";
                bonusCodeResult.color = new Color(0.3f, 1f, 0.4f);
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
                // No shield purchased — gray bar, no fill
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

        // Refresh Archero-style menu currencies & XP bar
        if (MainMenuManager.Instance != null)
        {
            MainMenuManager.Instance.RefreshCurrencies();
            MainMenuManager.Instance.RefreshXpBar();
        }
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

        // Ensure VirtualJoystick Instance is set after panel activation
        if (VirtualJoystick.Instance == null)
        {
            var js = gamePanel.GetComponentInChildren<VirtualJoystick>(true);
            if (js != null)
                Debug.Log("[UIManager] VirtualJoystick found but Instance was null — forcing activation");
        }
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
        // Create as ROOT object — not child of UIManager canvas, so CanvasScaler works properly
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
        if (dailyRewardPanel == null)
            CreateDailyRewardPanel();

        // Update texts
        var dayText = dailyRewardPanel.transform.Find("DayText")?.GetComponent<TextMeshProUGUI>();
        var rewardText = dailyRewardPanel.transform.Find("RewardText")?.GetComponent<TextMeshProUGUI>();

        if (dayText != null) dayText.text = "GUN " + day + " ODUL!";
        if (rewardText != null)
        {
            string extra = (day % 3 == 0) ? " + 1 TAS" : "";
            rewardText.text = "+" + reward + " COIN" + extra;
        }

        dailyRewardPanel.SetActive(true);
        menuPanel.SetActive(false);
    }

    private void CreateDailyRewardPanel()
    {
        dailyRewardPanel = new GameObject("DailyRewardPanel");
        // Don't parent to UIManager — Canvas needs to be a root or under another Canvas
        var canvas = dailyRewardPanel.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;
        var scaler = dailyRewardPanel.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        dailyRewardPanel.AddComponent<GraphicRaycaster>();

        // Background
        var bg = CreateUIElement(dailyRewardPanel.transform, "BG", Vector2.zero, Vector2.one);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.05f, 0.15f, 0.95f);

        // Title
        var title = CreateUIElement(dailyRewardPanel.transform, "Title",
            new Vector2(0.1f, 0.7f), new Vector2(0.9f, 0.85f));
        var titleTmp = title.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "GUNLUK ODUL";
        titleTmp.fontSize = 40;
        titleTmp.color = new Color(1f, 0.85f, 0.2f);
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.enableAutoSizing = true;
        titleTmp.fontSizeMin = 20;
        titleTmp.fontSizeMax = 40;

        // Day text
        var dayObj = CreateUIElement(dailyRewardPanel.transform, "DayText",
            new Vector2(0.1f, 0.55f), new Vector2(0.9f, 0.7f));
        var dayTmp = dayObj.AddComponent<TextMeshProUGUI>();
        dayTmp.text = "GUN 1 ODUL!";
        dayTmp.fontSize = 32;
        dayTmp.color = Color.white;
        dayTmp.fontStyle = FontStyles.Bold;
        dayTmp.alignment = TextAlignmentOptions.Center;

        // Reward text
        var rewardObj = CreateUIElement(dailyRewardPanel.transform, "RewardText",
            new Vector2(0.1f, 0.42f), new Vector2(0.9f, 0.55f));
        var rewardTmp = rewardObj.AddComponent<TextMeshProUGUI>();
        rewardTmp.text = "+10 COIN";
        rewardTmp.fontSize = 36;
        rewardTmp.color = new Color(0.3f, 1f, 0.5f);
        rewardTmp.fontStyle = FontStyles.Bold;
        rewardTmp.alignment = TextAlignmentOptions.Center;

        // Collect button
        var collectBtn = CreateUIElement(dailyRewardPanel.transform, "CollectBtn",
            new Vector2(0.15f, 0.22f), new Vector2(0.85f, 0.38f));
        var collectImg = collectBtn.AddComponent<Image>();
        collectImg.color = new Color(0.15f, 0.75f, 0.3f);
        var collectButton = collectBtn.AddComponent<Button>();
        collectButton.onClick.AddListener(OnDailyRewardButton);

        var collectLabel = CreateUIElement(collectBtn.transform, "Label", Vector2.zero, Vector2.one);
        var collectTmp = collectLabel.AddComponent<TextMeshProUGUI>();
        collectTmp.text = "TOPLA";
        collectTmp.fontSize = 28;
        collectTmp.color = Color.white;
        collectTmp.fontStyle = FontStyles.Bold;
        collectTmp.alignment = TextAlignmentOptions.Center;
        collectTmp.enableAutoSizing = true;
        collectTmp.fontSizeMin = 14;
        collectTmp.fontSizeMax = 28;

        // Watch ad for 3x button
        var adBtn = CreateUIElement(dailyRewardPanel.transform, "AdBtn",
            new Vector2(0.2f, 0.08f), new Vector2(0.8f, 0.2f));
        var adBtnImg = adBtn.AddComponent<Image>();
        adBtnImg.color = new Color(0.6f, 0.2f, 0.9f);
        var adButton = adBtn.AddComponent<Button>();
        adButton.onClick.AddListener(OnDailyRewardAdButton);

        var adLabel = CreateUIElement(adBtn.transform, "Label", Vector2.zero, Vector2.one);
        var adTmp = adLabel.AddComponent<TextMeshProUGUI>();
        adTmp.text = "REKLAM IZLE = 3X ODUL";
        adTmp.fontSize = 20;
        adTmp.color = Color.white;
        adTmp.fontStyle = FontStyles.Bold;
        adTmp.alignment = TextAlignmentOptions.Center;
        adTmp.enableAutoSizing = true;
        adTmp.fontSizeMin = 10;
        adTmp.fontSizeMax = 20;
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
        uiLevelText.fontSize = 22;
        uiLevelText.fontStyle = FontStyles.Bold;
        uiLevelText.color = new Color(0.55f, 0.78f, 0.94f);
        uiLevelText.alignment = TextAlignmentOptions.Left;
        uiLevelText.enableAutoSizing = true;
        uiLevelText.fontSizeMin = 12;
        uiLevelText.fontSizeMax = 22;
        uiLevelText.text = "Lv.1";

        // ── HP Bar (below XP bar) ──
        var hpRoot = new GameObject("HpBarRoot");
        hpRoot.transform.SetParent(gamePanel.transform, false);
        var hpRootRt = hpRoot.AddComponent<RectTransform>();
        hpRootRt.anchorMin = new Vector2(0.15f, 0.90f);
        hpRootRt.anchorMax = new Vector2(0.85f, 0.925f);
        hpRootRt.offsetMin = Vector2.zero;
        hpRootRt.offsetMax = Vector2.zero;

        var hpBg = new GameObject("HpBg");
        hpBg.transform.SetParent(hpRoot.transform, false);
        var hpBgRt = hpBg.AddComponent<RectTransform>();
        hpBgRt.anchorMin = Vector2.zero; hpBgRt.anchorMax = Vector2.one;
        hpBgRt.offsetMin = Vector2.zero; hpBgRt.offsetMax = Vector2.zero;
        var hpBgImg = hpBg.AddComponent<Image>();
        hpBgImg.color = new Color(0.2f, 0.1f, 0.1f, 0.85f);

        var hpFill = new GameObject("HpFill");
        hpFill.transform.SetParent(hpRoot.transform, false);
        var hpFillRt = hpFill.AddComponent<RectTransform>();
        hpFillRt.anchorMin = Vector2.zero; hpFillRt.anchorMax = Vector2.one;
        hpFillRt.offsetMin = new Vector2(2, 2); hpFillRt.offsetMax = new Vector2(-2, -2);
        uiHpBarFill = hpFill.AddComponent<Image>();
        uiHpBarFill.sprite = CreateWhiteSprite();
        uiHpBarFill.color = new Color(0.55f, 0.85f, 0.62f);
        uiHpBarFill.type = Image.Type.Filled;
        uiHpBarFill.fillMethod = Image.FillMethod.Horizontal;
        uiHpBarFill.fillAmount = 1f;

        var hpTxt = new GameObject("HpText");
        hpTxt.transform.SetParent(hpRoot.transform, false);
        var hpTxtRt = hpTxt.AddComponent<RectTransform>();
        hpTxtRt.anchorMin = Vector2.zero; hpTxtRt.anchorMax = Vector2.one;
        hpTxtRt.offsetMin = Vector2.zero; hpTxtRt.offsetMax = new Vector2(-4, 0);
        uiHpText = hpTxt.AddComponent<TextMeshProUGUI>();
        uiHpText.fontSize = 16;
        uiHpText.fontStyle = FontStyles.Bold;
        uiHpText.color = Color.white;
        uiHpText.alignment = TextAlignmentOptions.Center;
        uiHpText.enableAutoSizing = true;
        uiHpText.fontSizeMin = 8;
        uiHpText.fontSizeMax = 16;
        uiHpText.text = "";

        // ── Shield Bar (below HP bar, thinner) ──
        var shieldRoot = new GameObject("ShieldBarRoot");
        shieldRoot.transform.SetParent(gamePanel.transform, false);
        var shieldRootRt = shieldRoot.AddComponent<RectTransform>();
        shieldRootRt.anchorMin = new Vector2(0.15f, 0.885f);
        shieldRootRt.anchorMax = new Vector2(0.85f, 0.898f);
        shieldRootRt.offsetMin = Vector2.zero;
        shieldRootRt.offsetMax = Vector2.zero;

        var shieldBg = new GameObject("ShieldBg");
        shieldBg.transform.SetParent(shieldRoot.transform, false);
        var shieldBgRt = shieldBg.AddComponent<RectTransform>();
        shieldBgRt.anchorMin = Vector2.zero; shieldBgRt.anchorMax = Vector2.one;
        shieldBgRt.offsetMin = Vector2.zero; shieldBgRt.offsetMax = Vector2.zero;
        uiShieldBarBg = shieldBg.AddComponent<Image>();
        uiShieldBarBg.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);

        var shieldFill = new GameObject("ShieldFill");
        shieldFill.transform.SetParent(shieldRoot.transform, false);
        var shieldFillRt = shieldFill.AddComponent<RectTransform>();
        shieldFillRt.anchorMin = Vector2.zero; shieldFillRt.anchorMax = Vector2.one;
        shieldFillRt.offsetMin = new Vector2(1, 1); shieldFillRt.offsetMax = new Vector2(-1, -1);
        uiShieldBarFill = shieldFill.AddComponent<Image>();
        uiShieldBarFill.sprite = CreateWhiteSprite();
        uiShieldBarFill.color = new Color(0.60f, 0.75f, 0.95f);
        uiShieldBarFill.type = Image.Type.Filled;
        uiShieldBarFill.fillMethod = Image.FillMethod.Horizontal;
        uiShieldBarFill.fillAmount = 0f;
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
        stoneHudText.fontSize = 20;
        stoneHudText.color = new Color(0.62f, 0.72f, 0.95f);
        stoneHudText.fontStyle = FontStyles.Bold;
        stoneHudText.alignment = TextAlignmentOptions.Right;
        stoneHudText.enableAutoSizing = true;
        stoneHudText.fontSizeMin = 10;
        stoneHudText.fontSizeMax = 20;
        int stones = ScoreManager.Instance != null ? ScoreManager.Instance.UpgradeStones : 0;
        stoneHudText.text = "◆ " + stones;

        // Emerald counter
        var emeraldObj = new GameObject("EmeraldHud");
        emeraldObj.transform.SetParent(gamePanel.transform, false);
        var emeraldRt = emeraldObj.AddComponent<RectTransform>();
        emeraldRt.anchorMin = new Vector2(0.65f, 0.005f);
        emeraldRt.anchorMax = new Vector2(0.98f, 0.04f);
        emeraldRt.offsetMin = Vector2.zero;
        emeraldRt.offsetMax = Vector2.zero;
        emeraldHudText = emeraldObj.AddComponent<TextMeshProUGUI>();
        emeraldHudText.fontSize = 20;
        emeraldHudText.color = new Color(0.55f, 0.85f, 0.62f);
        emeraldHudText.fontStyle = FontStyles.Bold;
        emeraldHudText.alignment = TextAlignmentOptions.Right;
        emeraldHudText.enableAutoSizing = true;
        emeraldHudText.fontSizeMin = 10;
        emeraldHudText.fontSizeMax = 20;
        int emeralds = ScoreManager.Instance != null ? ScoreManager.Instance.Emeralds : 0;
        emeraldHudText.text = "◆ " + emeralds;
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
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.7f);

        // Title
        var title = CreateUIElement(abilityPickPanel.transform, "Title",
            new Vector2(0.1f, 0.7f), new Vector2(0.9f, 0.8f));
        var titleTmp = title.AddComponent<TextMeshProUGUI>();
        titleTmp.text = "YETENek SEC";
        titleTmp.fontSize = 40;
        titleTmp.color = new Color(1f, 0.9f, 0.3f);
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.enableAutoSizing = true;
        titleTmp.fontSizeMin = 20;
        titleTmp.fontSizeMax = 40;

        // Ability cards
        for (int i = 0; i < choices.Count; i++)
        {
            var ability = choices[i];
            float yMin = 0.55f - i * 0.15f;
            float yMax = yMin + 0.12f;

            var card = CreateUIElement(abilityPickPanel.transform, "Card_" + i,
                new Vector2(0.1f, yMin), new Vector2(0.9f, yMax));
            var cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(ability.color.r * 0.3f, ability.color.g * 0.3f, ability.color.b * 0.3f, 0.9f);

            // Icon/color strip
            var strip = CreateUIElement(card.transform, "Strip",
                new Vector2(0, 0), new Vector2(0.08f, 1));
            var stripImg = strip.AddComponent<Image>();
            stripImg.color = ability.color;

            // Name
            var nameObj = CreateUIElement(card.transform, "Name",
                new Vector2(0.1f, 0.5f), new Vector2(0.6f, 1f));
            var nameTmp = nameObj.AddComponent<TextMeshProUGUI>();
            nameTmp.text = ability.name;
            nameTmp.fontSize = 26;
            nameTmp.color = Color.white;
            nameTmp.fontStyle = FontStyles.Bold;
            nameTmp.alignment = TextAlignmentOptions.Left;
            nameTmp.enableAutoSizing = true;
            nameTmp.fontSizeMin = 14;
            nameTmp.fontSizeMax = 26;

            // Description
            var descObj = CreateUIElement(card.transform, "Desc",
                new Vector2(0.1f, 0f), new Vector2(0.9f, 0.5f));
            var descTmp = descObj.AddComponent<TextMeshProUGUI>();
            descTmp.text = ability.description;
            descTmp.fontSize = 20;
            descTmp.color = new Color(0.8f, 0.8f, 0.8f);
            descTmp.alignment = TextAlignmentOptions.Left;
            descTmp.enableAutoSizing = true;
            descTmp.fontSizeMin = 10;
            descTmp.fontSizeMax = 20;

            // Button
            var btn = card.AddComponent<Button>();
            var abilityId = ability.id;
            btn.onClick.AddListener(() => OnAbilityChosen(abilityId));
        }
    }

    private void OnAbilityChosen(AbilitySystem.AbilityId id)
    {
        if (AbilitySystem.Instance != null)
            AbilitySystem.Instance.AcquireAbility(id);

        if (abilityPickPanel != null) Destroy(abilityPickPanel);
        Time.timeScale = 1f;
    }

    private void OnStoneChanged(int count)
    {
        if (stoneHudText != null) stoneHudText.text = "◆ " + count;
    }

    private void OnEmeraldChanged(int count)
    {
        if (emeraldHudText != null) emeraldHudText.text = "◆ " + count;
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
