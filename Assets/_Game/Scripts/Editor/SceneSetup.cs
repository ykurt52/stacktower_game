#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

/// <summary>
/// Unified editor utility -- builds the entire game into a single scene:
/// Archero-style main menu (SafeArea-constrained) + gameplay HUD + overlays.
/// Menu item: Stack Tower ▸ Setup Scene
/// </summary>
public static class SceneSetup
{
    // ── Pastel palette ──
    static readonly Color DarkBg       = new Color(0.11f, 0.11f, 0.18f);       // soft dark navy
    static readonly Color PanelBg      = new Color(0.14f, 0.14f, 0.22f);       // slightly lighter navy
    static readonly Color BarBg        = new Color(0.10f, 0.10f, 0.16f, 0.96f);// top/bottom bar
    static readonly Color CardBg       = new Color(0.17f, 0.17f, 0.26f, 0.92f);// card surfaces
    static readonly Color Gold         = new Color(0.96f, 0.82f, 0.45f);       // warm cream gold
    static readonly Color GoldDark     = new Color(0.72f, 0.58f, 0.28f);       // muted gold shadow
    static readonly Color GemBlue      = new Color(0.55f, 0.78f, 0.94f);       // soft sky blue
    static readonly Color EmeraldGreen = new Color(0.55f, 0.85f, 0.65f);       // soft mint
    static readonly Color EnergyPurple = new Color(0.72f, 0.62f, 0.92f);       // soft lavender
    static readonly Color AccentRed    = new Color(0.90f, 0.55f, 0.55f);       // soft coral
    static readonly Color TextMuted    = new Color(0.55f, 0.55f, 0.68f);       // muted blue-gray
    static readonly Color TextBright   = new Color(0.93f, 0.93f, 0.96f);       // warm off-white

    static readonly Color[] TabColors = {
        new Color(0.55f, 0.72f, 0.95f), // Shop -- soft blue
        new Color(0.95f, 0.72f, 0.50f), // Equipment -- soft peach
        Gold,                             // Battle -- cream gold
        EnergyPurple,                    // Talent -- lavender
        new Color(0.58f, 0.60f, 0.70f)  // Settings -- warm gray
    };
    static readonly string[] TabNames = { "SHOP", "EKIPMAN", "SAVAS", "YETENEK", "AYARLAR" };

    // Button sizes
    static readonly Vector2 BTN_LARGE  = new Vector2(320, 70);
    static readonly Vector2 BTN_MEDIUM = new Vector2(240, 60);
    static readonly Vector2 BTN_SMALL  = new Vector2(160, 55);

    // Cached resources
    static Sprite _roundedSprite;
    static TMP_FontAsset _font;

    // ══════════════════════════════════════════════════════════════
    // ENTRY POINT
    // ══════════════════════════════════════════════════════════════

    [MenuItem("Stack Tower/Setup Scene")]
    public static void CreateScene()
    {
        _roundedSprite = null;
        _font = null;

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ── Scene infrastructure ──
        BuildSceneInfrastructure();

        // ── Event System ──
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

        // ── Background Canvas (full bleed, sort 0) ──
        // Only visible during menu state; hidden during gameplay so the 3D arena is visible.
        GameObject bgCanvas = CreateCanvasRoot("BackgroundCanvas", 0);
        var bgPanel = CreatePanel(bgCanvas.transform, "Background");
        var bgImg = bgPanel.AddComponent<Image>();
        bgImg.color = DarkBg;
        bgImg.raycastTarget = false;

        // ── UI Canvas (interactive, sort 1) ──
        GameObject uiCanvas = CreateCanvasRoot("UICanvas", 1);
        UIManager uiManager = uiCanvas.AddComponent<UIManager>();
        uiCanvas.AddComponent<UIPopupManager>();

        // SafeArea container -- all interactive UI
        var safeArea = CreatePanel(uiCanvas.transform, "SafeArea");
        safeArea.AddComponent<SafeArea>();

        // ══════════════════════════════════════════
        // A) MENU ROOT -- Archero-style main menu
        // ══════════════════════════════════════════
        var menuRoot = CreatePanel(safeArea.transform, "MenuRoot");
        var menuRootBg = menuRoot.AddComponent<Image>();
        menuRootBg.color = PanelBg;
        menuRootBg.raycastTarget = true;

        MainMenuManager menuManager = menuRoot.AddComponent<MainMenuManager>();

        // Top HUD
        var topHudRefs = BuildTopHUD(menuRoot.transform);

        // Center Area (between HUD and nav)
        var centerArea = CreateAnchoredPanel(menuRoot.transform, "CenterArea",
            new Vector2(0, 0.10f), new Vector2(1, 0.92f));

        var battlePanel   = BuildBattlePanel(centerArea.transform);
        var equipPanel    = BuildEquipmentPanel(centerArea.transform);
        var talentPanel   = BuildTalentPanel(centerArea.transform);
        var menuShopPanel = BuildMenuShopPanel(centerArea.transform);
        var settingsPanel = BuildSettingsPanel(centerArea.transform);

        // Bottom Nav
        var navRefs = BuildBottomNav(menuRoot.transform, menuManager);

        // Wire MainMenuManager
        WireMainMenuManager(menuManager, topHudRefs, navRefs,
            battlePanel, equipPanel, talentPanel, menuShopPanel, settingsPanel);

        // Wire bonus code button in settings panel (nested inside scroll content)
        var settingsBonusBtn = FindDeep(settingsPanel.transform, "BonusCodeBtn");
        if (settingsBonusBtn != null)
            WireButton(settingsBonusBtn.gameObject, uiManager, "OnBonusCodeButton");

        // Wire settings gear to tab 4
        var settingsBtn = topHudRefs.settingsIcon.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddIntPersistentListener(
            settingsBtn.onClick, menuManager.OnTabPressed, 4);

        // Wire PLAY button
        var playBtnObj = battlePanel.transform.Find("PlayButton");
        if (playBtnObj != null)
        {
            var playBtn = playBtnObj.GetComponent<Button>();
            UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
                playBtn.onClick,
                new UnityEngine.Events.UnityAction(menuManager.OnPlayButton));
        }

        // ══════════════════════════════════════════
        // B) GAME PANEL -- gameplay HUD
        // ══════════════════════════════════════════
        var gameRefs = BuildGamePanel(safeArea.transform);

        // ══════════════════════════════════════════
        // C) GAME OVER PANEL
        // ══════════════════════════════════════════
        var goRefs = BuildGameOverPanel(safeArea.transform);

        // ══════════════════════════════════════════
        // D) PAUSE PANEL
        // ══════════════════════════════════════════
        var pauseRefs = BuildPausePanel(safeArea.transform);

        // ══════════════════════════════════════════
        // E) STORE OVERLAYS (ShopUI-based)
        // ══════════════════════════════════════════
        GameObject shopStorePanel = CreateStorePanel(safeArea.transform, "ShopPanel", "SHOP",
            ShopManager.ItemCategory.Shop);
        GameObject shopCloseBtn = shopStorePanel.transform.Find("CloseBtn").gameObject;

        GameObject skillsStorePanel = CreateStorePanel(safeArea.transform, "SkillsPanel", "SKILLS",
            ShopManager.ItemCategory.Skills);
        GameObject skillsCloseBtn = skillsStorePanel.transform.Find("CloseBtn").gameObject;

        // Reset Skills button
        GameObject resetSkillsBtn = CreateButton(skillsStorePanel.transform, "ResetSkillsBtn", "SIFIRLA",
            new Vector2(0, 0), BTN_MEDIUM, new Color(0.85f, 0.52f, 0.52f));
        RectTransform resetBtnRT = resetSkillsBtn.GetComponent<RectTransform>();
        resetBtnRT.anchorMin = new Vector2(0.5f, 0);
        resetBtnRT.anchorMax = new Vector2(0.5f, 0);
        resetBtnRT.pivot = new Vector2(0.5f, 0);
        resetBtnRT.anchoredPosition = new Vector2(0, 95);

        GameObject weaponsStorePanel = CreateStorePanel(safeArea.transform, "WeaponsPanel", "WEAPONS",
            ShopManager.ItemCategory.Weapons);
        GameObject weaponsCloseBtn = weaponsStorePanel.transform.Find("CloseBtn").gameObject;
        GameObject weaponsResetBtn = CreateButton(weaponsStorePanel.transform, "ResetWeaponsBtn", "SIFIRLA",
            new Vector2(0, 0), BTN_SMALL, new Color(0.85f, 0.52f, 0.52f));
        RectTransform wResetRT = weaponsResetBtn.GetComponent<RectTransform>();
        wResetRT.anchorMin = new Vector2(0.5f, 0);
        wResetRT.anchorMax = new Vector2(0.5f, 0);
        wResetRT.pivot = new Vector2(0.5f, 0);
        wResetRT.anchoredPosition = new Vector2(0, 130);

        // ══════════════════════════════════════════
        // F) BONUS CODE PANEL
        // ══════════════════════════════════════════
        var bonusRefs = BuildBonusCodePanel(safeArea.transform);

        // ══════════════════════════════════════════
        // G) CONFIRM POPUP
        // ══════════════════════════════════════════
        var confirmRefs = BuildConfirmPanel(safeArea.transform);

        // ══════════════════════════════════════════
        // WIRE UI MANAGER
        // ══════════════════════════════════════════
        SerializedObject uiSO = new SerializedObject(uiManager);
        uiSO.FindProperty("backgroundCanvas").objectReferenceValue  = bgCanvas;
        uiSO.FindProperty("menuPanel").objectReferenceValue         = menuRoot;
        uiSO.FindProperty("gamePanel").objectReferenceValue         = gameRefs.panel;
        uiSO.FindProperty("gameOverPanel").objectReferenceValue     = goRefs.panel;
        uiSO.FindProperty("bestScoreMenuText").objectReferenceValue = null;
        uiSO.FindProperty("currentScoreText").objectReferenceValue  = gameRefs.scoreText;
        uiSO.FindProperty("coinText").objectReferenceValue          = gameRefs.coinText;
        uiSO.FindProperty("floorText").objectReferenceValue         = gameRefs.floorText;
        uiSO.FindProperty("pausePanel").objectReferenceValue        = pauseRefs.panel;
        uiSO.FindProperty("shopPanel").objectReferenceValue         = shopStorePanel;
        uiSO.FindProperty("skillsPanel").objectReferenceValue       = skillsStorePanel;
        uiSO.FindProperty("weaponsPanel").objectReferenceValue      = weaponsStorePanel;
        uiSO.FindProperty("bonusCodePanel").objectReferenceValue    = bonusRefs.panel;
        uiSO.FindProperty("bonusCodeInput").objectReferenceValue    = bonusRefs.inputField;
        uiSO.FindProperty("bonusCodeResult").objectReferenceValue   = bonusRefs.resultText;
        uiSO.FindProperty("gameOverScoreText").objectReferenceValue = goRefs.scoreText;
        uiSO.FindProperty("gameOverBestText").objectReferenceValue  = goRefs.bestText;
        uiSO.FindProperty("confirmResetPanel").objectReferenceValue = confirmRefs.panel;
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        // ══════════════════════════════════════════
        // WIRE BUTTONS
        // ══════════════════════════════════════════
        WireButton(goRefs.retryBtn,     uiManager, "OnRetryButton");
        WireButton(goRefs.menuBtn,      uiManager, "OnMenuButton");
        WireButton(gameRefs.pauseBtn,   uiManager, "OnPauseButton");
        WireButton(pauseRefs.resumeBtn, uiManager, "OnResumeButton");
        WireButton(pauseRefs.menuBtn,   uiManager, "OnPauseMenuButton");

        // Store buttons
        WireButton(shopCloseBtn,     uiManager, "OnStoreCloseButton");
        WireButton(skillsCloseBtn,   uiManager, "OnStoreCloseButton");
        WireButton(weaponsCloseBtn,  uiManager, "OnStoreCloseButton");
        WireButton(resetSkillsBtn,   uiManager, "OnResetSkillsButton");
        WireButton(weaponsResetBtn,  uiManager, "OnResetWeaponsButton");

        // Bonus code buttons
        WireButton(bonusRefs.redeemBtn, uiManager, "OnRedeemCodeButton");
        WireButton(bonusRefs.closeBtn,  uiManager, "OnStoreCloseButton");

        // Confirm popup buttons
        WireButton(confirmRefs.yesBtn, uiManager, "OnConfirmResetYes");
        WireButton(confirmRefs.noBtn,  uiManager, "OnConfirmResetNo");

        // ── Save ──
        EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/Game.unity");
        Debug.Log("[OK] Unified scene created: Assets/_Game/Scenes/Game.unity");
    }

    // ══════════════════════════════════════════════════════════════
    // SCENE INFRASTRUCTURE
    // ══════════════════════════════════════════════════════════════

    static void BuildSceneInfrastructure()
    {
        // Camera
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 40f;
        cam.transform.position = new Vector3(0, 8, -14);
        cam.transform.rotation = Quaternion.Euler(30, 0, 0);
        camObj.tag = "MainCamera";
        camObj.AddComponent<AudioListener>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = DarkBg;

        var camData = camObj.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null) camData = camObj.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;

        // Ambient
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor    = new Color(0.6f, 0.7f, 0.9f);
        RenderSettings.ambientEquatorColor = new Color(0.5f, 0.55f, 0.65f);
        RenderSettings.ambientGroundColor  = new Color(0.3f, 0.3f, 0.35f);

        // Fog
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.005f;
        RenderSettings.fogColor = new Color(0.6f, 0.75f, 0.9f);

        // Post-Processing
        GameObject ppObj = new GameObject("PostProcessVolume");
        Volume volume = ppObj.AddComponent<Volume>();
        volume.isGlobal = true;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.6f);
        bloom.intensity.Override(2f);
        bloom.scatter.Override(0.75f);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.3f);
        vignette.smoothness.Override(0.5f);

        ColorAdjustments colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.Override(0.6f);
        colorAdj.contrast.Override(25f);
        colorAdj.saturation.Override(35f);

        AssetDatabase.CreateAsset(profile, "Assets/_Game/PostProcessProfile.asset");
        volume.profile = profile;

        // Directional light
        GameObject lightObj = new GameObject("DirectionalLight");
        Light dirLight = lightObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(1f, 0.95f, 0.85f);
        dirLight.intensity = 2.5f;
        dirLight.shadows = LightShadows.Soft;
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Managers
        CreateManager<GameManager>("GameManager");
        var arenaManagerObj = CreateManager<ArenaManager>("ArenaManager");
        AssignArenaManagerAssets(arenaManagerObj.GetComponent<ArenaManager>());
        CreateManager<ScoreManager>("ScoreManager");
        CreateManager<ShopManager>("ShopManager");
        CreateManager<BonusCodeManager>("BonusCodeManager");
        CreateManager<AdManager>("AdManager");
        CreateManager<AudioManager>("AudioManager");
        CreateManager<VFXManager>("VFXManager");
        CreateManager<SyntyAssets>("SyntyAssets");
        CreateManager<ModelManager>("ModelManager");
        CreateManager<AbilitySystem>("AbilitySystem");
    }

    // ══════════════════════════════════════════════════════════════
    // TOP HUD (8% of safe area)
    // ══════════════════════════════════════════════════════════════

    struct TopHudRefs
    {
        public TextMeshProUGUI gemText, goldText, energyText, goldAmountText;
        public GameObject settingsIcon;
    }

    static TopHudRefs BuildTopHUD(Transform parent)
    {
        var refs = new TopHudRefs();
        var spr = GetRoundedSprite();

        var topHud = CreateAnchoredPanel(parent, "TopHUD",
            new Vector2(0, 0.92f), new Vector2(1, 1));
        topHud.AddComponent<Image>().color = BarBg;

        // Settings gear (top-left)
        refs.settingsIcon = CreateAnchoredPanel(topHud.transform, "SettingsIcon",
            new Vector2(0.01f, 0.12f), new Vector2(0.09f, 0.88f));
        var sBg = refs.settingsIcon.AddComponent<Image>();
        sBg.sprite = spr; sBg.type = Image.Type.Sliced;
        sBg.color = new Color(0.15f, 0.15f, 0.25f);
        refs.settingsIcon.AddComponent<Button>();
        CreateTMP(refs.settingsIcon.transform, "Icon", Vector2.zero, Vector2.one,
            "<sprite name=\"gear\">", 28, TextMuted, TextAlignmentOptions.Center);

        // Energy display (lightning icon)
        var energyPill = CreateCurrencyPill(topHud.transform, "EnergyDisplay",
            new Vector2(0.10f, 0.15f), new Vector2(0.33f, 0.85f),
            "<sprite name=\"lightning\">", EnergyPurple, "5/5");
        refs.energyText = energyPill.transform.Find("Amount").GetComponent<TextMeshProUGUI>();

        // Gem display
        var gemPill = CreateCurrencyPill(topHud.transform, "GemDisplay",
            new Vector2(0.35f, 0.15f), new Vector2(0.58f, 0.85f),
            "<sprite name=\"diamond\">", GemBlue, "0");
        refs.gemText = gemPill.transform.Find("Amount").GetComponent<TextMeshProUGUI>();

        // Gold display
        var goldPill = CreateCurrencyPill(topHud.transform, "GoldDisplay",
            new Vector2(0.60f, 0.15f), new Vector2(0.99f, 0.85f), "$", Gold, "0");
        refs.goldText = goldPill.transform.Find("Amount").GetComponent<TextMeshProUGUI>();
        refs.goldAmountText = refs.goldText;

        return refs;
    }

    // ══════════════════════════════════════════════════════════════
    // BOTTOM NAVIGATION BAR (10% of safe area)
    // ══════════════════════════════════════════════════════════════

    struct NavRefs
    {
        public Image[] tabBgs;
        public TextMeshProUGUI[] tabLabels;
    }

    static NavRefs BuildBottomNav(Transform parent, MainMenuManager mgr)
    {
        var refs = new NavRefs { tabBgs = new Image[5], tabLabels = new TextMeshProUGUI[5] };
        var spr = GetRoundedSprite();

        var bottomNav = CreateAnchoredPanel(parent, "BottomNav",
            new Vector2(0, 0), new Vector2(1, 0.10f));
        bottomNav.AddComponent<Image>().color = BarBg;

        // Separator line
        var sep = CreateAnchoredPanel(bottomNav.transform, "Sep",
            new Vector2(0, 0.96f), new Vector2(1, 1));
        sep.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.35f, 0.5f);

        float tabW = 0.2f;
        for (int i = 0; i < 5; i++)
        {
            float x0 = i * tabW;
            bool isCenter = (i == 2);

            var tab = CreateAnchoredPanel(bottomNav.transform, "Tab_" + TabNames[i],
                new Vector2(x0 + 0.006f, isCenter ? 0.06f : 0.10f),
                new Vector2(x0 + tabW - 0.006f, isCenter ? 0.94f : 0.90f));
            var tabBg = tab.AddComponent<Image>();
            tabBg.sprite = spr; tabBg.type = Image.Type.Sliced;
            tabBg.color = isCenter ? Gold : new Color(0.1f, 0.1f, 0.18f, 0.9f);
            refs.tabBgs[i] = tabBg;

            var btn = tab.AddComponent<Button>();
            var nav = btn.navigation;
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            // Indicator dot (non-center tabs)
            if (!isCenter)
            {
                var dot = CreateAnchoredPanel(tab.transform, "Dot",
                    new Vector2(0.35f, 0.82f), new Vector2(0.65f, 0.96f));
                var dotImg = dot.AddComponent<Image>();
                dotImg.sprite = spr; dotImg.type = Image.Type.Sliced;
                dotImg.color = TabColors[i];
            }

            var label = CreateTMP(tab.transform, "Label",
                new Vector2(0.02f, 0.02f), new Vector2(0.98f, isCenter ? 1f : 0.78f),
                TabNames[i], isCenter ? 26 : 18,
                isCenter ? new Color(0.15f, 0.1f, 0f) : TextMuted,
                TextAlignmentOptions.Center, FontStyles.Bold);
            refs.tabLabels[i] = label;

            // Wire tab press
            int idx = i;
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(
                btn.onClick, mgr.OnTabPressed, idx);
        }

        return refs;
    }

    // ══════════════════════════════════════════════════════════════
    // CENTER PANELS
    // ══════════════════════════════════════════════════════════════

    static GameObject BuildBattlePanel(Transform parent)
    {
        var panel = CreatePanel(parent, "BattlePanel");
        var spr = GetRoundedSprite();

        // Stage info
        CreateTMP(panel.transform, "StageInfo",
            new Vector2(0.1f, 0.90f), new Vector2(0.9f, 0.98f),
            "Chapter 1 - Stage 1", 28, TextMuted, TextAlignmentOptions.Center, FontStyles.Bold);

        // Character preview area
        var charArea = CreateAnchoredPanel(panel.transform, "CharPreview",
            new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.88f));
        var charBg = charArea.AddComponent<Image>();
        charBg.sprite = spr; charBg.type = Image.Type.Sliced;
        charBg.color = new Color(0.08f, 0.08f, 0.18f, 0.6f);

        // Character name badge
        var nameBadge = CreateAnchoredPanel(charArea.transform, "NameBadge",
            new Vector2(0.25f, 0.88f), new Vector2(0.75f, 0.99f));
        var nbBg = nameBadge.AddComponent<Image>();
        nbBg.sprite = spr; nbBg.type = Image.Type.Sliced; nbBg.color = Gold;
        CreateTMP(nameBadge.transform, "Name", Vector2.zero, Vector2.one,
            "KAHRAMAN", 24, new Color(0.15f, 0.1f, 0f), TextAlignmentOptions.Center, FontStyles.Bold);

        // Silhouette placeholder
        var sil = CreateAnchoredPanel(charArea.transform, "Silhouette",
            new Vector2(0.3f, 0.2f), new Vector2(0.7f, 0.85f));
        sil.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.35f, 0.5f);
        CreateTMP(sil.transform, "Hint", Vector2.zero, Vector2.one,
            "3D\nKARAKTER", 28, TextMuted, TextAlignmentOptions.Center);

        // Platform
        var plat = CreateAnchoredPanel(charArea.transform, "Platform",
            new Vector2(0.15f, 0.05f), new Vector2(0.85f, 0.15f));
        var platBg = plat.AddComponent<Image>();
        platBg.sprite = spr; platBg.type = Image.Type.Sliced;
        platBg.color = new Color(0.12f, 0.12f, 0.25f, 0.6f);

        // Stats row (ATK + HP)
        var statsRow = CreateAnchoredPanel(charArea.transform, "StatsRow",
            new Vector2(0.05f, 0.15f), new Vector2(0.95f, 0.25f));

        var atkBox = CreateAnchoredPanel(statsRow.transform, "AtkBox",
            new Vector2(0.02f, 0), new Vector2(0.48f, 1));
        var atkBg = atkBox.AddComponent<Image>();
        atkBg.sprite = spr; atkBg.type = Image.Type.Sliced;
        atkBg.color = new Color(0.8f, 0.25f, 0.2f, 0.7f);
        CreateTMP(atkBox.transform, "AtkText", Vector2.zero, Vector2.one,
            "<sprite name=\"sword\"> ATK 10", 20, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        var hpBox = CreateAnchoredPanel(statsRow.transform, "HpBox",
            new Vector2(0.52f, 0), new Vector2(0.98f, 1));
        var hpBg = hpBox.AddComponent<Image>();
        hpBg.sprite = spr; hpBg.type = Image.Type.Sliced;
        hpBg.color = new Color(0.2f, 0.7f, 0.3f, 0.7f);
        CreateTMP(hpBox.transform, "HpText", Vector2.zero, Vector2.one,
            "<sprite name=\"heart\"> HP 100", 20, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // PLAY button (golden, with shadow)
        var playShadow = CreateAnchoredPanel(panel.transform, "PlayShadow",
            new Vector2(0.1f, 0.06f), new Vector2(0.9f, 0.195f));
        var psBg = playShadow.AddComponent<Image>();
        psBg.sprite = spr; psBg.type = Image.Type.Sliced; psBg.color = GoldDark;

        var playBtn = CreateAnchoredPanel(panel.transform, "PlayButton",
            new Vector2(0.1f, 0.075f), new Vector2(0.9f, 0.24f));
        var pbBg = playBtn.AddComponent<Image>();
        pbBg.sprite = spr; pbBg.type = Image.Type.Sliced; pbBg.color = Gold;
        playBtn.AddComponent<Button>();

        CreateTMP(playBtn.transform, "PlayText",
            new Vector2(0, 0.15f), new Vector2(1, 0.95f),
            "BASLA", 64, new Color(0.2f, 0.12f, 0f), TextAlignmentOptions.Center, FontStyles.Bold);
        CreateTMP(playBtn.transform, "PlaySub",
            new Vector2(0, 0), new Vector2(1, 0.3f),
            "Dalga 1", 22, new Color(0.4f, 0.3f, 0f), TextAlignmentOptions.Center);

        return panel;
    }

    static GameObject BuildEquipmentPanel(Transform parent)
    {
        var panel = CreatePanel(parent, "EquipmentPanel");
        panel.SetActive(false);
        var spr = GetRoundedSprite();

        CreateTMP(panel.transform, "Title",
            new Vector2(0.05f, 0.92f), new Vector2(0.95f, 1f),
            "EKIPMAN", 36, TextBright, TextAlignmentOptions.Center, FontStyles.Bold);

        // ── ScrollRect ──
        var scrollArea = CreateAnchoredPanel(panel.transform, "ScrollArea",
            new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.92f));
        var scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 30f;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        var vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = Color.clear;
        scrollRect.viewport = vpRT;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var cRT = content.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0, 1); cRT.anchorMax = new Vector2(1, 1);
        cRT.pivot = new Vector2(0.5f, 1);
        scrollRect.content = cRT;

        float cursorY = 0f;

        // ── Section 1: Mevcut Ekipman (equipped gear slots) ──
        cursorY += 8f;
        var gearHeader = CreateContentChild(content.transform, "GearHeader", spr,
            cursorY, 40f, BarBg);
        CreateContentTMP(gearHeader.transform, "Mevcut Ekipman", 22, Gold, FontStyles.Bold);
        cursorY += 48f;

        string[] slotNames  = { "SILAH", "ZIRH", "KASK" };
        string[] slotIcons  = { "<sprite name=\"sword\">", "DEF", "<sprite name=\"heart\">" };
        Color[]  slotColors = { AccentRed, GemBlue, EmeraldGreen };

        float slotRowH = 160f;
        var slotRow = CreateContentChild(content.transform, "GearSlots", null,
            cursorY, slotRowH, Color.clear);
        for (int i = 0; i < 3; i++)
        {
            float x0 = 0.03f + i * 0.32f;
            var slot = CreateAnchoredPanel(slotRow.transform, "Slot_" + slotNames[i],
                new Vector2(x0, 0.0f), new Vector2(x0 + 0.30f, 1.0f));
            var slotBg = slot.AddComponent<Image>();
            slotBg.sprite = spr; slotBg.type = Image.Type.Sliced;
            slotBg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);

            var strip = CreateAnchoredPanel(slot.transform, "Strip",
                new Vector2(0, 0.92f), new Vector2(1, 1));
            strip.AddComponent<Image>().color = slotColors[i];

            CreateTMP(slot.transform, "Icon",
                new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.85f),
                slotIcons[i], 40, new Color(0.4f, 0.4f, 0.55f), TextAlignmentOptions.Center);

            CreateTMP(slot.transform, "Label",
                new Vector2(0.05f, 0.02f), new Vector2(0.95f, 0.3f),
                slotNames[i], 16, TextMuted, TextAlignmentOptions.Center, FontStyles.Bold);
        }
        cursorY += slotRowH + 8f;

        // ── Section 2: Silahlar ──
        cursorY += 8f;
        var weaponHeader = CreateContentChild(content.transform, "WeaponHeader", spr,
            cursorY, 40f, BarBg);
        CreateContentTMP(weaponHeader.transform, "<sprite name=\"sword\"> Silahlar", 22,
            AccentRed, FontStyles.Bold);
        cursorY += 48f;

        string[] wNames = { "Yay", "Tirpan", "Testere", "Kasirga", "Mizrak", "Asa" };
        string[] wIcons = { "<sprite name=\"sword\">", "<sprite name=\"sword\">",
            "<sprite name=\"star\">", "~", "<sprite name=\"sword\">", "<sprite name=\"lightning\">" };
        string[] wDescs = { "Dengeli baslangic", "Delici, yuksek hasar",
            "Bumerang geri doner", "Her seyi deler", "Uzun menzil, hizli", "Gudumlu mermiler" };
        Color[] wColors = { new Color(0.6f, 0.4f, 0.2f), new Color(0.6f, 0.2f, 0.8f),
            new Color(0.9f, 0.6f, 0.2f), new Color(0.3f, 0.8f, 0.9f),
            new Color(0.8f, 0.8f, 0.9f), new Color(0.4f, 0.3f, 1f) };

        for (int i = 0; i < wNames.Length; i++)
        {
            var wCard = CreateContentChild(content.transform, "Weapon_" + i, spr,
                cursorY, 80f, CardBg);

            var strip = CreateAnchoredPanel(wCard.transform, "Strip",
                new Vector2(0, 0), new Vector2(0.02f, 1));
            strip.AddComponent<Image>().color = wColors[i];

            CreateTMP(wCard.transform, "Icon",
                new Vector2(0.03f, 0.1f), new Vector2(0.14f, 0.9f),
                wIcons[i], 28, wColors[i], TextAlignmentOptions.Center);

            CreateTMP(wCard.transform, "Name",
                new Vector2(0.16f, 0.5f), new Vector2(0.6f, 0.95f),
                wNames[i], 20, TextBright, TextAlignmentOptions.Left, FontStyles.Bold);

            CreateTMP(wCard.transform, "Desc",
                new Vector2(0.16f, 0.05f), new Vector2(0.6f, 0.5f),
                wDescs[i], 14, TextMuted, TextAlignmentOptions.Left);

            var eqBtn = CreateAnchoredPanel(wCard.transform, "EquipBtn",
                new Vector2(0.62f, 0.15f), new Vector2(0.96f, 0.85f));
            var eqBg = eqBtn.AddComponent<Image>();
            eqBg.sprite = spr; eqBg.type = Image.Type.Sliced;
            eqBg.color = GemBlue;
            eqBtn.AddComponent<Button>();
            CreateTMP(eqBtn.transform, "Label", Vector2.zero, Vector2.one,
                "SEC", 16, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            cursorY += 88f;
        }

        // ── Section 3: Koleksiyon ──
        cursorY += 8f;
        var collHeader = CreateContentChild(content.transform, "CollHeader", spr,
            cursorY, 40f, BarBg);
        CreateContentTMP(collHeader.transform, "Koleksiyon", 22, Gold, FontStyles.Bold);
        cursorY += 48f;

        int gridCols = 4;
        int gridRows = 4;
        float cellH = 80f;
        float cellGap = 8f;
        float gridRowH = cellH + cellGap;

        for (int row = 0; row < gridRows; row++)
        {
            var gridRow = CreateContentChild(content.transform, "GridRow_" + row, null,
                cursorY, cellH, Color.clear);

            for (int col = 0; col < gridCols; col++)
            {
                float cx = 0.02f + col * 0.245f;
                var cell = CreateAnchoredPanel(gridRow.transform, $"Cell_{row}_{col}",
                    new Vector2(cx, 0.0f), new Vector2(cx + 0.23f, 1.0f));
                var cellBg = cell.AddComponent<Image>();
                cellBg.sprite = spr; cellBg.type = Image.Type.Sliced;
                cellBg.color = new Color(0.1f, 0.1f, 0.2f, 0.7f);
                CreateTMP(cell.transform, "Placeholder", Vector2.zero, Vector2.one,
                    "?", 32, new Color(0.25f, 0.25f, 0.35f), TextAlignmentOptions.Center);
            }
            cursorY += gridRowH;
        }

        cursorY += 20f;
        cRT.sizeDelta = new Vector2(0, cursorY);

        return panel;
    }

    static GameObject BuildTalentPanel(Transform parent)
    {
        var panel = CreatePanel(parent, "TalentPanel");
        panel.SetActive(false);
        var spr = GetRoundedSprite();

        CreateTMP(panel.transform, "Title",
            new Vector2(0.05f, 0.92f), new Vector2(0.95f, 1f),
            "YETENEKLER", 36, TextBright, TextAlignmentOptions.Center, FontStyles.Bold);

        // ── ScrollRect ──
        var scrollArea = CreateAnchoredPanel(panel.transform, "ScrollArea",
            new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.92f));
        var scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 30f;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        var vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = Color.clear;
        scrollRect.viewport = vpRT;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var cRT = content.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0, 1); cRT.anchorMax = new Vector2(1, 1);
        cRT.pivot = new Vector2(0.5f, 1);
        scrollRect.content = cRT;

        // Talent definitions: name, skillId, icon, description
        string[,] talents = {
            { "Guc",       "attack",      "<sprite name=\"sword\">",     "Saldiri gucu arttirir" },
            { "Can",       "hp",          "<sprite name=\"heart\">",     "Maksimum HP arttirir" },
            { "Hiz",       "speed",       ">>",                          "Hareket hizi arttirir" },
            { "Zirh",      "armor",       "DEF",                         "Hasar azaltma" },
            { "Kritik",    "critical",    "<sprite name=\"star\">",      "Kritik sans arttirir" },
            { "Vampir",    "vampirism",   "<sprite name=\"heart\">",     "Oldururken can kazanir" },
            { "Dodge",     "dodge",       "<sprite name=\"lightning\">", "Kacinma sansi (%0->%50)" },
            { "Deneyim",   "xpboost",     "<sprite name=\"lightning\">", "XP kazanimi arttirir" },
            { "Altin",     "goldboost",   "$",                           "Altin kazanimi arttirir" },
            { "Can Yen.",  "healthregen", "<sprite name=\"heart\">",     "Zamanla can yenile" },
            { "Sld. Hizi", "attackspeed", "<sprite name=\"lightning\">", "Saldiri hizi (1.0->7.0)" },
        };
        int talentCount = talents.GetLength(0);

        // Card dimensions — 2 columns for better readability
        float cardW = 0.46f; // anchor fraction
        float cardH = 150f;  // pixels
        float gapX = 0.02f;
        float gapY = 12f;
        float startX = 0.02f;
        int cols = 2;

        // Collect references for TalentUI wiring
        var levelTexts = new TextMeshProUGUI[talentCount];
        var costTexts = new TextMeshProUGUI[talentCount];
        var buttons = new Button[talentCount];
        var buttonBgs = new Image[talentCount];
        string[] skillIds = new string[talentCount];

        for (int i = 0; i < talentCount; i++)
        {
            int r = i / cols, c = i % cols;
            float x0 = startX + c * (cardW + gapX);
            float yTop = 8f + r * (cardH + gapY);

            var card = new GameObject("Talent_" + talents[i, 0]);
            card.transform.SetParent(content.transform, false);
            var cardRT = card.AddComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(x0, 1);
            cardRT.anchorMax = new Vector2(x0 + cardW, 1);
            cardRT.pivot = new Vector2(0.5f, 1);
            cardRT.anchoredPosition = new Vector2(0, -yTop);
            cardRT.sizeDelta = new Vector2(0, cardH);

            var cardBgImg = card.AddComponent<Image>();
            cardBgImg.sprite = spr; cardBgImg.type = Image.Type.Sliced;
            cardBgImg.color = CardBg;

            Color accent = TabColors[c % TabColors.Length];

            // Icon area (top portion)
            var iconArea = CreateAnchoredPanel(card.transform, "IconArea",
                new Vector2(0.08f, 0.55f), new Vector2(0.42f, 0.95f));
            var iconBg = iconArea.AddComponent<Image>();
            iconBg.sprite = spr; iconBg.type = Image.Type.Sliced;
            iconBg.color = new Color(accent.r * 0.3f, accent.g * 0.3f, accent.b * 0.3f);
            CreateTMP(iconArea.transform, "Icon", Vector2.zero, Vector2.one,
                talents[i, 2], 28, accent, TextAlignmentOptions.Center);

            // Name
            CreateTMP(card.transform, "Name",
                new Vector2(0.44f, 0.7f), new Vector2(0.98f, 0.98f),
                talents[i, 0], 16, TextBright, TextAlignmentOptions.Left, FontStyles.Bold);

            // Level text (dynamic)
            var levelObj = CreateTMP(card.transform, "Level",
                new Vector2(0.44f, 0.5f), new Vector2(0.98f, 0.72f),
                "Lv. 0", 13, TextMuted, TextAlignmentOptions.Left);
            levelTexts[i] = levelObj.GetComponent<TextMeshProUGUI>();

            // Description
            CreateTMP(card.transform, "Desc",
                new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.5f),
                talents[i, 3], 11, TextMuted, TextAlignmentOptions.Center);

            // Buy button (bottom)
            var btnArea = CreateAnchoredPanel(card.transform, "BuyBtn",
                new Vector2(0.08f, 0.03f), new Vector2(0.92f, 0.27f));
            var btnBg = btnArea.AddComponent<Image>();
            btnBg.sprite = spr; btnBg.type = Image.Type.Sliced;
            btnBg.color = new Color(0.15f, 0.75f, 0.3f);
            var btn = btnArea.AddComponent<Button>();

            var costObj = CreateTMP(btnArea.transform, "Cost", Vector2.zero, Vector2.one,
                "$100", 14, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            costTexts[i] = costObj.GetComponent<TextMeshProUGUI>();
            buttons[i] = btn;
            buttonBgs[i] = btnBg;
            skillIds[i] = talents[i, 1];
        }

        // Total content height
        int rows = (talentCount + cols - 1) / cols;
        float totalH = 8f + rows * (cardH + gapY) + 20f;
        cRT.sizeDelta = new Vector2(0, totalH);

        // ── Attach TalentUI and wire via SerializedObject ──
        var talentUI = panel.AddComponent<TalentUI>();
        var so = new SerializedObject(talentUI);
        var talentsProp = so.FindProperty("_talents");
        talentsProp.ClearArray();

        for (int i = 0; i < talentCount; i++)
        {
            talentsProp.InsertArrayElementAtIndex(i);
            var elem = talentsProp.GetArrayElementAtIndex(i);
            elem.FindPropertyRelative("skillId").stringValue = skillIds[i];
            elem.FindPropertyRelative("levelText").objectReferenceValue = levelTexts[i];
            elem.FindPropertyRelative("costText").objectReferenceValue = costTexts[i];
            elem.FindPropertyRelative("button").objectReferenceValue = buttons[i];
            elem.FindPropertyRelative("buttonBg").objectReferenceValue = buttonBgs[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        // Wire button onClick -> TalentUI.OnTalentBuyPressed(index)
        for (int i = 0; i < talentCount; i++)
        {
            UnityEditor.Events.UnityEventTools.AddIntPersistentListener(
                buttons[i].onClick, talentUI.OnTalentBuyPressed, i);
        }

        return panel;
    }

    static GameObject BuildMenuShopPanel(Transform parent)
    {
        var panel = CreatePanel(parent, "ShopPanel");
        panel.SetActive(false);
        var spr = GetRoundedSprite();

        CreateTMP(panel.transform, "Title",
            new Vector2(0.05f, 0.92f), new Vector2(0.95f, 1f),
            "MARKET", 36, TextBright, TextAlignmentOptions.Center, FontStyles.Bold);

        // ── ScrollRect structure ──
        var scrollArea = CreateAnchoredPanel(panel.transform, "ScrollArea",
            new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.92f));
        var scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 30f;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        var vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = Color.clear;

        scrollRect.viewport = vpRT;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var cRT = content.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0, 1); cRT.anchorMax = new Vector2(1, 1);
        cRT.pivot = new Vector2(0.5f, 1);
        // Total height calculated at the end
        scrollRect.content = cRT;

        // Helper: pixel-based child inside content (yPos from top)
        float cursorY = 0f; // tracks current Y position from top

        // ── Section 1: Gunluk Teklif ──
        cursorY += 8f;
        var dailyHeader = CreateContentChild(content.transform, "DailyHeader", spr,
            cursorY, 40f, BarBg);
        CreateContentTMP(dailyHeader.transform, "Gunluk Teklif", 22, Gold, FontStyles.Bold);
        cursorY += 48f;

        var dailyCard = CreateContentChild(content.transform, "DailyDeal", spr,
            cursorY, 90f, CardBg);
        CreateTMP(dailyCard.transform, "Icon",
            new Vector2(0.03f, 0.1f), new Vector2(0.18f, 0.9f),
            "<sprite name=\"star\">", 36, Gold, TextAlignmentOptions.Center);
        CreateTMP(dailyCard.transform, "Name",
            new Vector2(0.20f, 0.5f), new Vector2(0.65f, 0.95f),
            "Gizemli Paket", 20, TextBright, TextAlignmentOptions.Left, FontStyles.Bold);
        CreateTMP(dailyCard.transform, "Desc",
            new Vector2(0.20f, 0.05f), new Vector2(0.65f, 0.5f),
            "500 Altin + 10 Tas", 16, EmeraldGreen, TextAlignmentOptions.Left);
        var dailyBtn = CreateAnchoredPanel(dailyCard.transform, "Btn",
            new Vector2(0.68f, 0.15f), new Vector2(0.96f, 0.85f));
        dailyBtn.AddComponent<Image>().color = EmeraldGreen;
        dailyBtn.AddComponent<Button>();
        CreateTMP(dailyBtn.transform, "Label", Vector2.zero, Vector2.one,
            "Reklam", 16, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        cursorY += 98f;

        // ── Section 2: Altin Paketleri ──
        cursorY += 8f;
        var goldHeader = CreateContentChild(content.transform, "GoldHeader", spr,
            cursorY, 40f, BarBg);
        CreateContentTMP(goldHeader.transform, "Altin Paketleri", 22, Gold, FontStyles.Bold);
        cursorY += 48f;

        string[] bPrices  = { "Reklam", "$500", "$2000" };
        string[] bAmounts = { "+100", "+600", "+2500" };
        Color[]  bColors  = { EmeraldGreen, GemBlue, EnergyPurple };

        float bundleH = 180f;
        float bundleW = 0.30f;
        var bundleRow = CreateContentChild(content.transform, "BundleRow", null,
            cursorY, bundleH, Color.clear);
        for (int i = 0; i < 3; i++)
        {
            float x0 = 0.03f + i * 0.32f;
            var bundle = CreateAnchoredPanel(bundleRow.transform, "Bundle_" + i,
                new Vector2(x0, 0.0f), new Vector2(x0 + bundleW, 1.0f));
            var bBg = bundle.AddComponent<Image>();
            bBg.sprite = spr; bBg.type = Image.Type.Sliced; bBg.color = CardBg;
            bundle.AddComponent<Button>();

            var strip = CreateAnchoredPanel(bundle.transform, "Strip",
                new Vector2(0, 0.92f), new Vector2(1, 1));
            strip.AddComponent<Image>().color = bColors[i];

            CreateTMP(bundle.transform, "Icon",
                new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.88f),
                "$", 48, Gold, TextAlignmentOptions.Center);

            CreateTMP(bundle.transform, "Amount",
                new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.5f),
                bAmounts[i], 20, EmeraldGreen, TextAlignmentOptions.Center, FontStyles.Bold);

            var priceArea = CreateAnchoredPanel(bundle.transform, "Price",
                new Vector2(0.1f, 0.03f), new Vector2(0.9f, 0.25f));
            var pBg = priceArea.AddComponent<Image>();
            pBg.sprite = spr; pBg.type = Image.Type.Sliced; pBg.color = bColors[i];
            CreateTMP(priceArea.transform, "PriceText", Vector2.zero, Vector2.one,
                bPrices[i], 16, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        }
        cursorY += bundleH + 8f;

        // ── Section 3: Tas Paketleri ──
        cursorY += 8f;
        var stoneHeader = CreateContentChild(content.transform, "StoneHeader", spr,
            cursorY, 40f, BarBg);
        CreateContentTMP(stoneHeader.transform, "<sprite name=\"diamond\"> Tas Paketleri", 22,
            new Color(0.7f, 0.3f, 1f), FontStyles.Bold);
        cursorY += 48f;

        string[] sPrices  = { "$200", "$800", "$2500" };
        string[] sAmounts = { "+5", "+25", "+80" };
        Color[]  sColors  = { new Color(0.6f, 0.4f, 0.9f), new Color(0.7f, 0.3f, 1f), new Color(0.9f, 0.5f, 1f) };

        var stoneRow = CreateContentChild(content.transform, "StoneRow", null,
            cursorY, bundleH, Color.clear);
        for (int i = 0; i < 3; i++)
        {
            float x0 = 0.03f + i * 0.32f;
            var bundle = CreateAnchoredPanel(stoneRow.transform, "StonePack_" + i,
                new Vector2(x0, 0.0f), new Vector2(x0 + bundleW, 1.0f));
            var bBg = bundle.AddComponent<Image>();
            bBg.sprite = spr; bBg.type = Image.Type.Sliced; bBg.color = CardBg;
            bundle.AddComponent<Button>();

            var strip = CreateAnchoredPanel(bundle.transform, "Strip",
                new Vector2(0, 0.92f), new Vector2(1, 1));
            strip.AddComponent<Image>().color = sColors[i];

            CreateTMP(bundle.transform, "Icon",
                new Vector2(0.1f, 0.45f), new Vector2(0.9f, 0.88f),
                "<sprite name=\"diamond\">", 48, sColors[i], TextAlignmentOptions.Center);

            CreateTMP(bundle.transform, "Amount",
                new Vector2(0.05f, 0.25f), new Vector2(0.95f, 0.5f),
                sAmounts[i], 20, EmeraldGreen, TextAlignmentOptions.Center, FontStyles.Bold);

            var priceArea = CreateAnchoredPanel(bundle.transform, "Price",
                new Vector2(0.1f, 0.03f), new Vector2(0.9f, 0.25f));
            var pBg = priceArea.AddComponent<Image>();
            pBg.sprite = spr; pBg.type = Image.Type.Sliced; pBg.color = sColors[i];
            CreateTMP(priceArea.transform, "PriceText", Vector2.zero, Vector2.one,
                sPrices[i], 16, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        }
        cursorY += bundleH + 8f;

        // ── Section 4: Sandiklar ──
        cursorY += 8f;
        var chestHeader = CreateContentChild(content.transform, "ChestHeader", spr,
            cursorY, 40f, BarBg);
        CreateContentTMP(chestHeader.transform, "Sandiklar", 22, EnergyPurple, FontStyles.Bold);
        cursorY += 48f;

        string[] cNames  = { "Normal Sandik", "Nadir Sandik", "Efsanevi Sandik" };
        Color[]  cColors = { new Color(0.4f, 0.5f, 0.6f), GemBlue, Gold };
        string[] cPrices = { "100 <sprite name=\"diamond\">", "300 <sprite name=\"diamond\">", "800 <sprite name=\"diamond\">" };

        for (int i = 0; i < 3; i++)
        {
            var chest = CreateContentChild(content.transform, "Chest_" + i, spr,
                cursorY, 80f, CardBg);
            chest.AddComponent<Button>();

            var strip = CreateAnchoredPanel(chest.transform, "Strip",
                new Vector2(0, 0), new Vector2(0.02f, 1));
            strip.AddComponent<Image>().color = cColors[i];

            CreateTMP(chest.transform, "Icon",
                new Vector2(0.03f, 0.1f), new Vector2(0.12f, 0.9f),
                "BOX", 28, cColors[i], TextAlignmentOptions.Center);

            CreateTMP(chest.transform, "Name",
                new Vector2(0.13f, 0.1f), new Vector2(0.65f, 0.9f),
                cNames[i], 20, TextBright, TextAlignmentOptions.Left, FontStyles.Bold);

            CreateTMP(chest.transform, "Price",
                new Vector2(0.65f, 0.1f), new Vector2(0.97f, 0.9f),
                cPrices[i], 20, cColors[i], TextAlignmentOptions.Right, FontStyles.Bold);

            cursorY += 88f;
        }

        // ── Section 5: Ozel Teklifler ──
        cursorY += 8f;
        var specialHeader = CreateContentChild(content.transform, "SpecialHeader", spr,
            cursorY, 40f, BarBg);
        CreateContentTMP(specialHeader.transform, "Ozel Teklifler", 22,
            new Color(1f, 0.5f, 0.2f), FontStyles.Bold);
        cursorY += 48f;

        string[] spNames = { "Baslangic Paketi", "Savasci Paketi", "Efsane Paketi" };
        string[] spDescs = { "1000 Altin + 20 Tas", "3000 Altin + 50 Tas + Tirpan", "10000 Altin + 200 Tas + Asa" };
        string[] spPrcs  = { "$1.99", "$4.99", "$9.99" };
        Color[]  spColors = { EmeraldGreen, GemBlue, Gold };

        for (int i = 0; i < 3; i++)
        {
            var offer = CreateContentChild(content.transform, "Offer_" + i, spr,
                cursorY, 90f, CardBg);
            offer.AddComponent<Button>();

            var strip = CreateAnchoredPanel(offer.transform, "Strip",
                new Vector2(0, 0), new Vector2(0.02f, 1));
            strip.AddComponent<Image>().color = spColors[i];

            CreateTMP(offer.transform, "Icon",
                new Vector2(0.03f, 0.1f), new Vector2(0.14f, 0.9f),
                "<sprite name=\"star\">", 32, spColors[i], TextAlignmentOptions.Center);

            CreateTMP(offer.transform, "Name",
                new Vector2(0.15f, 0.5f), new Vector2(0.65f, 0.95f),
                spNames[i], 20, TextBright, TextAlignmentOptions.Left, FontStyles.Bold);

            CreateTMP(offer.transform, "Desc",
                new Vector2(0.15f, 0.05f), new Vector2(0.65f, 0.5f),
                spDescs[i], 14, TextMuted, TextAlignmentOptions.Left);

            var priceBtn = CreateAnchoredPanel(offer.transform, "PriceBtn",
                new Vector2(0.68f, 0.15f), new Vector2(0.96f, 0.85f));
            priceBtn.AddComponent<Image>().color = spColors[i];
            CreateTMP(priceBtn.transform, "Label", Vector2.zero, Vector2.one,
                spPrcs[i], 18, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            cursorY += 98f;
        }

        cursorY += 20f; // bottom padding

        // Set content height
        cRT.sizeDelta = new Vector2(0, cursorY);

        return panel;
    }

    // Helper: creates a pixel-height child inside scroll content
    static GameObject CreateContentChild(Transform contentParent, string name, Sprite spr,
        float yFromTop, float height, Color bgColor)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(contentParent, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.03f, 1); rt.anchorMax = new Vector2(0.97f, 1);
        rt.pivot = new Vector2(0.5f, 1);
        rt.anchoredPosition = new Vector2(0, -yFromTop);
        rt.sizeDelta = new Vector2(0, height);

        if (bgColor.a > 0.01f)
        {
            var img = obj.AddComponent<Image>();
            if (spr != null) { img.sprite = spr; img.type = Image.Type.Sliced; }
            img.color = bgColor;
        }

        return obj;
    }

    static void CreateContentTMP(Transform parent, string text, int fontSize, Color color, FontStyles style)
    {
        CreateTMP(parent, "Label", new Vector2(0.05f, 0f), new Vector2(0.95f, 1f),
            text, fontSize, color, TextAlignmentOptions.Left, style);
    }

    static GameObject BuildSettingsPanel(Transform parent)
    {
        var panel = CreatePanel(parent, "SettingsPanel");
        panel.SetActive(false);
        var spr = GetRoundedSprite();

        CreateTMP(panel.transform, "Title",
            new Vector2(0.05f, 0.92f), new Vector2(0.95f, 1f),
            "AYARLAR", 36, TextBright, TextAlignmentOptions.Center, FontStyles.Bold);

        // ── ScrollRect ──
        var scrollArea = CreateAnchoredPanel(panel.transform, "ScrollArea",
            new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.92f));
        var scrollRect = scrollArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 30f;

        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollArea.transform, false);
        var vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = Color.clear;
        scrollRect.viewport = vpRT;

        var content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        var cRT = content.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0, 1); cRT.anchorMax = new Vector2(1, 1);
        cRT.pivot = new Vector2(0.5f, 1);
        scrollRect.content = cRT;

        float cursorY = 0f;

        // ── Section 1: Ses Ayarlari ──
        cursorY += 8f;
        var audioHeader = CreateContentChild(content.transform, "AudioHeader", spr,
            cursorY, 40f, BarBg);
        CreateContentTMP(audioHeader.transform, "<sprite name=\"gear\"> Ses Ayarlari", 22,
            Gold, FontStyles.Bold);
        cursorY += 48f;

        string[] audioNames  = { "Muzik", "Ses Efekti" };
        string[] audioValues = { "ACIK", "ACIK" };
        for (int i = 0; i < audioNames.Length; i++)
        {
            var row = CreateContentChild(content.transform, "Audio_" + i, spr,
                cursorY, 70f, CardBg);

            CreateTMP(row.transform, "Label",
                new Vector2(0.05f, 0), new Vector2(0.6f, 1),
                audioNames[i], 24, TextBright, TextAlignmentOptions.Left);

            var toggle = CreateAnchoredPanel(row.transform, "Toggle",
                new Vector2(0.65f, 0.15f), new Vector2(0.95f, 0.85f));
            var tBg = toggle.AddComponent<Image>();
            tBg.sprite = spr; tBg.type = Image.Type.Sliced; tBg.color = EmeraldGreen;
            toggle.AddComponent<Button>();
            CreateTMP(toggle.transform, "Value", Vector2.zero, Vector2.one,
                audioValues[i], 18, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            cursorY += 78f;
        }

        // ── Section 2: Genel Ayarlar ──
        cursorY += 8f;
        var generalHeader = CreateContentChild(content.transform, "GeneralHeader", spr,
            cursorY, 40f, BarBg);
        CreateContentTMP(generalHeader.transform, "<sprite name=\"gear\"> Genel", 22,
            GemBlue, FontStyles.Bold);
        cursorY += 48f;

        string[] genNames  = { "Bildirimler", "Titresim", "Dil" };
        string[] genValues = { "ACIK", "ACIK", "TR" };
        for (int i = 0; i < genNames.Length; i++)
        {
            var row = CreateContentChild(content.transform, "General_" + i, spr,
                cursorY, 70f, CardBg);

            CreateTMP(row.transform, "Label",
                new Vector2(0.05f, 0), new Vector2(0.6f, 1),
                genNames[i], 24, TextBright, TextAlignmentOptions.Left);

            var toggle = CreateAnchoredPanel(row.transform, "Toggle",
                new Vector2(0.65f, 0.15f), new Vector2(0.95f, 0.85f));
            var tBg = toggle.AddComponent<Image>();
            tBg.sprite = spr; tBg.type = Image.Type.Sliced;
            tBg.color = i == 2 ? GemBlue : EmeraldGreen;
            toggle.AddComponent<Button>();
            CreateTMP(toggle.transform, "Value", Vector2.zero, Vector2.one,
                genValues[i], 18, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            cursorY += 78f;
        }

        // ── Section 3: Grafik ──
        cursorY += 8f;
        var gfxHeader = CreateContentChild(content.transform, "GfxHeader", spr,
            cursorY, 40f, BarBg);
        CreateContentTMP(gfxHeader.transform, "<sprite name=\"lightning\"> Grafik", 22,
            EnergyPurple, FontStyles.Bold);
        cursorY += 48f;

        string[] gfxNames  = { "Kalite", "FPS Goster", "Golge" };
        string[] gfxValues = { "ORTA", "KAPALI", "ACIK" };
        Color[]  gfxColors = { EnergyPurple, new Color(0.45f, 0.2f, 0.2f), EmeraldGreen };
        for (int i = 0; i < gfxNames.Length; i++)
        {
            var row = CreateContentChild(content.transform, "Gfx_" + i, spr,
                cursorY, 70f, CardBg);

            CreateTMP(row.transform, "Label",
                new Vector2(0.05f, 0), new Vector2(0.6f, 1),
                gfxNames[i], 24, TextBright, TextAlignmentOptions.Left);

            var toggle = CreateAnchoredPanel(row.transform, "Toggle",
                new Vector2(0.65f, 0.15f), new Vector2(0.95f, 0.85f));
            var tBg = toggle.AddComponent<Image>();
            tBg.sprite = spr; tBg.type = Image.Type.Sliced; tBg.color = gfxColors[i];
            toggle.AddComponent<Button>();
            CreateTMP(toggle.transform, "Value", Vector2.zero, Vector2.one,
                gfxValues[i], 18, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

            cursorY += 78f;
        }

        // ── Section 4: Hesap & Destek ──
        cursorY += 8f;
        var accountHeader = CreateContentChild(content.transform, "AccountHeader", spr,
            cursorY, 40f, BarBg);
        CreateContentTMP(accountHeader.transform, "<sprite name=\"star\"> Hesap & Destek", 22,
            AccentRed, FontStyles.Bold);
        cursorY += 48f;

        // Bonus Code button
        var bonusBtn = CreateContentChild(content.transform, "BonusCodeBtn", spr,
            cursorY, 70f, new Color(0.85f, 0.65f, 0.2f));
        bonusBtn.AddComponent<Button>();
        CreateTMP(bonusBtn.transform, "Label", Vector2.zero, Vector2.one,
            "BONUS KOD", 28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        cursorY += 78f;

        // Data management buttons
        string[] actionNames  = { "Ilerlemeyi Sifirla", "Gizlilik Politikasi", "Bize Ulasin" };
        Color[]  actionColors = { new Color(0.7f, 0.25f, 0.25f), GemBlue, EmeraldGreen };
        for (int i = 0; i < actionNames.Length; i++)
        {
            var actionBtn = CreateContentChild(content.transform, "Action_" + i, spr,
                cursorY, 60f, actionColors[i]);
            actionBtn.AddComponent<Button>();
            CreateTMP(actionBtn.transform, "Label", Vector2.zero, Vector2.one,
                actionNames[i], 22, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            cursorY += 68f;
        }

        // Version info
        cursorY += 12f;
        var versionArea = CreateContentChild(content.transform, "Version", null,
            cursorY, 30f, Color.clear);
        CreateContentTMP(versionArea.transform, "v1.0.0", 16, TextMuted, FontStyles.Normal);
        cursorY += 40f;

        cursorY += 20f;
        cRT.sizeDelta = new Vector2(0, cursorY);

        return panel;
    }

    // ══════════════════════════════════════════════════════════════
    // GAME PANEL -- gameplay HUD
    // ══════════════════════════════════════════════════════════════

    struct GamePanelRefs
    {
        public GameObject panel, pauseBtn;
        public TextMeshProUGUI scoreText, coinText, floorText;
    }

    static GamePanelRefs BuildGamePanel(Transform parent)
    {
        var refs = new GamePanelRefs();
        refs.panel = CreatePanel(parent, "GamePanel");
        refs.panel.SetActive(false);

        // Score (top center, same row as pause and coin)
        var scoreObj = CreateAnchoredPanel(refs.panel.transform, "ScoreText",
            new Vector2(0.35f, 0.96f), new Vector2(0.65f, 1.0f));
        refs.scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
        refs.scoreText.text = "0";
        refs.scoreText.fontSize = 72;
        refs.scoreText.color = Color.white;
        refs.scoreText.alignment = TextAlignmentOptions.Center;
        refs.scoreText.fontStyle = FontStyles.Bold;
        refs.scoreText.characterSpacing = 4f;
        var sf = GetFont(); if (sf != null) refs.scoreText.font = sf;

        // Coin counter (top-right, same row as pause btn)
        var coinObj = CreateAnchoredPanel(refs.panel.transform, "CoinText",
            new Vector2(0.65f, 0.96f), new Vector2(0.98f, 1.0f));
        refs.coinText = coinObj.AddComponent<TextMeshProUGUI>();
        refs.coinText.text = "$0";
        refs.coinText.fontSize = 36;
        refs.coinText.color = new Color(1f, 0.9f, 0.2f);
        refs.coinText.alignment = TextAlignmentOptions.MidlineRight;
        refs.coinText.fontStyle = FontStyles.Bold;
        refs.coinText.characterSpacing = 4f;
        if (sf != null) refs.coinText.font = sf;

        // Floor/wave counter (repositioned at runtime by UIManager)
        var floorObj = CreateAnchoredPanel(refs.panel.transform, "FloorText",
            new Vector2(0.14f, 0.96f), new Vector2(0.55f, 1.0f));
        refs.floorText = floorObj.AddComponent<TextMeshProUGUI>();
        refs.floorText.text = "DALGA 1";
        refs.floorText.fontSize = 28;
        refs.floorText.color = new Color(1f, 1f, 1f, 0.7f);
        refs.floorText.alignment = TextAlignmentOptions.MidlineLeft;
        refs.floorText.fontStyle = FontStyles.Bold;
        refs.floorText.characterSpacing = 4f;
        if (sf != null) refs.floorText.font = sf;

        // Weapon ammo display (center)
        var ammoObj = CreateAnchoredPanel(refs.panel.transform, "WeaponAmmoText",
            new Vector2(0.35f, 0.90f), new Vector2(0.65f, 0.93f));
        var ammoTmp = ammoObj.AddComponent<TextMeshProUGUI>();
        ammoTmp.text = "";
        ammoTmp.fontSize = 22;
        ammoTmp.color = new Color(1f, 0.7f, 0.3f);
        ammoTmp.fontStyle = FontStyles.Bold;
        ammoTmp.alignment = TextAlignmentOptions.Center;
        ammoTmp.characterSpacing = 4f;
        if (sf != null) ammoTmp.font = sf;
        ammoObj.SetActive(false);

        // Pause button (top-left, aligned with top row 96%-100%)
        refs.pauseBtn = CreateButton(refs.panel.transform, "PauseButton", "||",
            new Vector2(0, 0), new Vector2(55, 55), new Color(0.45f, 0.45f, 0.58f));
        RectTransform pbRT = refs.pauseBtn.GetComponent<RectTransform>();
        pbRT.anchorMin = new Vector2(0.02f, 0.96f);
        pbRT.anchorMax = new Vector2(0.12f, 1.0f);
        pbRT.pivot = new Vector2(0, 1);
        pbRT.sizeDelta = Vector2.zero;
        pbRT.anchoredPosition = Vector2.zero;

        // Virtual joystick
        GameObject joystickArea = new GameObject("JoystickArea");
        joystickArea.transform.SetParent(refs.panel.transform, false);
        RectTransform jaRT = joystickArea.AddComponent<RectTransform>();
        jaRT.anchorMin = new Vector2(0f, 0.05f);
        jaRT.anchorMax = new Vector2(1f, 0.80f);
        jaRT.offsetMin = Vector2.zero;
        jaRT.offsetMax = Vector2.zero;

        joystickArea.AddComponent<VirtualJoystick>();

        // Joystick BG
        GameObject jsBg = new GameObject("JoystickBG");
        jsBg.transform.SetParent(joystickArea.transform, false);
        RectTransform jsBgRT = jsBg.AddComponent<RectTransform>();
        jsBgRT.sizeDelta = new Vector2(180, 180);
        jsBg.AddComponent<Image>().color = new Color(1, 1, 1, 0.25f);

        // Joystick Knob
        GameObject jsKnob = new GameObject("JoystickKnob");
        jsKnob.transform.SetParent(jsBg.transform, false);
        RectTransform jsKnobRT = jsKnob.AddComponent<RectTransform>();
        jsKnobRT.sizeDelta = new Vector2(70, 70);
        jsKnob.AddComponent<Image>().color = new Color(1, 1, 1, 0.6f);

        // Move hint
        CreateTMP(refs.panel.transform, "MoveHint",
            new Vector2(0.2f, 0.02f), new Vector2(0.8f, 0.06f),
            "Hareket ettir", 20, new Color(1, 1, 1, 0.4f), TextAlignmentOptions.Center);

        return refs;
    }

    // ══════════════════════════════════════════════════════════════
    // GAME OVER PANEL
    // ══════════════════════════════════════════════════════════════

    struct GameOverRefs
    {
        public GameObject panel, retryBtn, menuBtn;
        public TextMeshProUGUI scoreText, bestText;
    }

    static GameOverRefs BuildGameOverPanel(Transform parent)
    {
        var refs = new GameOverRefs();
        refs.panel = CreatePanel(parent, "GameOverPanel");
        refs.panel.SetActive(false);

        var header = CreateTextElement(refs.panel.transform, "GameOverHeader", "GAME OVER", 60,
            TextAlignmentOptions.Center, new Vector2(0, 100), new Vector2(600, 100));
        refs.scoreText = CreateTextElement(refs.panel.transform, "ScoreText", "SCORE: 0", 40,
            TextAlignmentOptions.Center, new Vector2(0, 20), new Vector2(400, 60)).GetComponent<TextMeshProUGUI>();
        refs.bestText = CreateTextElement(refs.panel.transform, "BestText", "BEST: 0", 36,
            TextAlignmentOptions.Center, new Vector2(0, -40), new Vector2(400, 60)).GetComponent<TextMeshProUGUI>();
        refs.retryBtn = CreateButton(refs.panel.transform, "RetryButton", "TEKRAR DENE",
            new Vector2(0, -130), BTN_LARGE, new Color(0.55f, 0.82f, 0.62f));
        refs.menuBtn = CreateButton(refs.panel.transform, "MenuButton", "ANA MENU",
            new Vector2(0, -220), BTN_LARGE, new Color(0.88f, 0.55f, 0.55f));

        return refs;
    }

    // ══════════════════════════════════════════════════════════════
    // PAUSE PANEL
    // ══════════════════════════════════════════════════════════════

    struct PausePanelRefs
    {
        public GameObject panel, resumeBtn, menuBtn;
    }

    static PausePanelRefs BuildPausePanel(Transform parent)
    {
        var refs = new PausePanelRefs();
        refs.panel = CreatePanel(parent, "PausePanel");
        refs.panel.SetActive(false);
        Image bg = refs.panel.AddComponent<Image>();
        bg.color = new Color(0, 0, 0, 0.7f);
        bg.raycastTarget = true;

        CreateTextElement(refs.panel.transform, "PauseTitle", "DURDURULDU", 56,
            TextAlignmentOptions.Center, new Vector2(0, 120), new Vector2(500, 100));
        refs.resumeBtn = CreateButton(refs.panel.transform, "ResumeButton", "DEVAM ET",
            new Vector2(0, 0), BTN_LARGE, new Color(0.55f, 0.82f, 0.62f));
        refs.menuBtn = CreateButton(refs.panel.transform, "PauseMenuButton", "ANA MENU",
            new Vector2(0, -100), BTN_LARGE, new Color(0.88f, 0.55f, 0.55f));

        return refs;
    }

    // ══════════════════════════════════════════════════════════════
    // BONUS CODE PANEL
    // ══════════════════════════════════════════════════════════════

    struct BonusCodeRefs
    {
        public GameObject panel, redeemBtn, closeBtn;
        public TMP_InputField inputField;
        public TextMeshProUGUI resultText;
    }

    static BonusCodeRefs BuildBonusCodePanel(Transform parent)
    {
        var refs = new BonusCodeRefs();
        var spr = GetRoundedSprite();

        refs.panel = CreatePanel(parent, "BonusCodePanel");
        refs.panel.SetActive(false);
        Image bg = refs.panel.AddComponent<Image>();
        bg.color = PanelBg;
        bg.raycastTarget = true;

        // Top bar
        var topBar = CreateAnchoredPanel(refs.panel.transform, "TopBar",
            new Vector2(0, 0.88f), new Vector2(1, 1));
        topBar.AddComponent<Image>().color = BarBg;

        var titleBadge = CreateAnchoredPanel(topBar.transform, "TitleBadge",
            new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.9f));
        var tbBg = titleBadge.AddComponent<Image>();
        tbBg.sprite = spr; tbBg.type = Image.Type.Sliced;
        tbBg.color = new Color(0.85f, 0.65f, 0.2f);
        CreateTMP(titleBadge.transform, "Title", Vector2.zero, Vector2.one,
            "BONUS KOD", 40, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // Card area
        var card = CreateAnchoredPanel(refs.panel.transform, "Card",
            new Vector2(0.08f, 0.45f), new Vector2(0.92f, 0.85f));
        var cardBg = card.AddComponent<Image>();
        cardBg.sprite = spr; cardBg.type = Image.Type.Sliced;
        cardBg.color = CardBg;

        CreateTMP(card.transform, "Subtitle",
            new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.95f),
            "Bonus kodunu gir", 24, TextMuted, TextAlignmentOptions.Center);

        // Input field
        var inputBg = CreateAnchoredPanel(card.transform, "CodeInput",
            new Vector2(0.08f, 0.42f), new Vector2(0.92f, 0.72f));
        inputBg.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.16f);

        GameObject inputTextArea = new GameObject("Text Area");
        inputTextArea.transform.SetParent(inputBg.transform, false);
        RectTransform taRT = inputTextArea.AddComponent<RectTransform>();
        taRT.anchorMin = Vector2.zero; taRT.anchorMax = Vector2.one;
        taRT.offsetMin = new Vector2(10, 5); taRT.offsetMax = new Vector2(-10, -5);

        GameObject inputTextObj = new GameObject("Text");
        inputTextObj.transform.SetParent(inputTextArea.transform, false);
        RectTransform itRT = inputTextObj.AddComponent<RectTransform>();
        itRT.anchorMin = Vector2.zero; itRT.anchorMax = Vector2.one;
        itRT.offsetMin = Vector2.zero; itRT.offsetMax = Vector2.zero;
        TextMeshProUGUI inputTMP = inputTextObj.AddComponent<TextMeshProUGUI>();
        inputTMP.fontSize = 30; inputTMP.color = Color.white;
        inputTMP.alignment = TextAlignmentOptions.Center;
        inputTMP.fontStyle = FontStyles.Bold;
        inputTMP.characterSpacing = 4f;

        GameObject placeholderObj = new GameObject("Placeholder");
        placeholderObj.transform.SetParent(inputTextArea.transform, false);
        RectTransform phRT = placeholderObj.AddComponent<RectTransform>();
        phRT.anchorMin = Vector2.zero; phRT.anchorMax = Vector2.one;
        phRT.offsetMin = Vector2.zero; phRT.offsetMax = Vector2.zero;
        TextMeshProUGUI phTMP = placeholderObj.AddComponent<TextMeshProUGUI>();
        phTMP.text = "Kodu girin..."; phTMP.fontSize = 28;
        phTMP.color = new Color(0.45f, 0.45f, 0.55f);
        phTMP.alignment = TextAlignmentOptions.Center;
        phTMP.fontStyle = FontStyles.Italic;
        phTMP.characterSpacing = 4f;

        refs.inputField = inputBg.AddComponent<TMP_InputField>();
        refs.inputField.textViewport = taRT;
        refs.inputField.textComponent = inputTMP;
        refs.inputField.placeholder = phTMP;
        refs.inputField.characterLimit = 20;
        refs.inputField.contentType = TMP_InputField.ContentType.Alphanumeric;

        // KULLAN button
        var redeemBtn = CreateAnchoredPanel(card.transform, "RedeemButton",
            new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.35f));
        var rbBg = redeemBtn.AddComponent<Image>();
        rbBg.sprite = spr; rbBg.type = Image.Type.Sliced;
        rbBg.color = EmeraldGreen;
        redeemBtn.AddComponent<Button>();
        CreateTMP(redeemBtn.transform, "Label", Vector2.zero, Vector2.one,
            "KULLAN", 28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        refs.redeemBtn = redeemBtn;

        // Result text
        var resultObj = CreateAnchoredPanel(refs.panel.transform, "ResultText",
            new Vector2(0.08f, 0.35f), new Vector2(0.92f, 0.44f));
        refs.resultText = resultObj.AddComponent<TextMeshProUGUI>();
        refs.resultText.fontSize = 26;
        refs.resultText.alignment = TextAlignmentOptions.Center;
        refs.resultText.fontStyle = FontStyles.Bold;
        refs.resultText.color = Color.white;
        refs.resultText.characterSpacing = 4f;

        // KAPAT button
        var closeBtn = CreateAnchoredPanel(refs.panel.transform, "BonusCloseButton",
            new Vector2(0.2f, 0.18f), new Vector2(0.8f, 0.28f));
        var clBg = closeBtn.AddComponent<Image>();
        clBg.sprite = spr; clBg.type = Image.Type.Sliced;
        clBg.color = new Color(0.85f, 0.52f, 0.52f);
        closeBtn.AddComponent<Button>();
        CreateTMP(closeBtn.transform, "Label", Vector2.zero, Vector2.one,
            "KAPAT", 28, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        refs.closeBtn = closeBtn;

        return refs;
    }

    // ══════════════════════════════════════════════════════════════
    // CONFIRM POPUP
    // ══════════════════════════════════════════════════════════════

    struct ConfirmRefs
    {
        public GameObject panel, yesBtn, noBtn;
    }

    static ConfirmRefs BuildConfirmPanel(Transform parent)
    {
        var refs = new ConfirmRefs();

        refs.panel = new GameObject("ConfirmPanel");
        refs.panel.transform.SetParent(parent, false);
        RectTransform cRT = refs.panel.AddComponent<RectTransform>();
        cRT.anchorMin = Vector2.zero; cRT.anchorMax = Vector2.one;
        cRT.offsetMin = Vector2.zero; cRT.offsetMax = Vector2.zero;
        Image overlay = refs.panel.AddComponent<Image>();
        overlay.color = new Color(0, 0, 0, 0.75f);
        overlay.raycastTarget = true;
        refs.panel.SetActive(false);

        GameObject box = new GameObject("ConfirmBox");
        box.transform.SetParent(refs.panel.transform, false);
        RectTransform boxRT = box.AddComponent<RectTransform>();
        boxRT.anchoredPosition = Vector2.zero;
        boxRT.sizeDelta = new Vector2(420, 260);
        box.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.22f);

        // Top accent
        GameObject accent = new GameObject("Accent");
        accent.transform.SetParent(box.transform, false);
        RectTransform aRT = accent.AddComponent<RectTransform>();
        aRT.anchorMin = new Vector2(0, 1); aRT.anchorMax = new Vector2(1, 1);
        aRT.pivot = new Vector2(0.5f, 1);
        aRT.sizeDelta = new Vector2(0, 8);
        aRT.anchoredPosition = Vector2.zero;
        accent.AddComponent<Image>().color = AccentRed;

        CreateTextElement(box.transform, "ConfirmText", "Emin misin?\nTum skilller sifirlanacak!",
            28, TextAlignmentOptions.Center, new Vector2(0, 40), new Vector2(380, 120));

        refs.yesBtn = CreateButton(box.transform, "ConfirmYes", "EVET",
            new Vector2(-80, -80), BTN_SMALL, new Color(0.85f, 0.50f, 0.50f));
        refs.noBtn = CreateButton(box.transform, "ConfirmNo", "HAYIR",
            new Vector2(80, -80), BTN_SMALL, new Color(0.55f, 0.78f, 0.60f));

        return refs;
    }

    // ══════════════════════════════════════════════════════════════
    // STORE PANEL (ShopUI-based overlay)
    // ══════════════════════════════════════════════════════════════

    static GameObject CreateStorePanel(Transform parent, string name, string title,
        ShopManager.ItemCategory category)
    {
        var spr = GetRoundedSprite();
        var panel = CreatePanel(parent, name);
        panel.SetActive(false);

        Image bg = panel.AddComponent<Image>();
        bg.color = PanelBg;
        bg.raycastTarget = true;

        Color bannerColor = category == ShopManager.ItemCategory.Shop
            ? new Color(0.55f, 0.72f, 0.92f)
            : category == ShopManager.ItemCategory.Weapons
            ? new Color(0.90f, 0.58f, 0.55f)
            : new Color(0.72f, 0.58f, 0.88f);

        var topBar = CreateAnchoredPanel(panel.transform, "TopBar",
            new Vector2(0, 0.92f), new Vector2(1, 1));
        topBar.AddComponent<Image>().color = BarBg;

        var titleBadge = CreateAnchoredPanel(topBar.transform, "TitleBadge",
            new Vector2(0.15f, 0.1f), new Vector2(0.85f, 0.9f));
        var tbBg = titleBadge.AddComponent<Image>();
        tbBg.sprite = spr; tbBg.type = Image.Type.Sliced; tbBg.color = bannerColor;
        CreateTMP(titleBadge.transform, "Title", Vector2.zero, Vector2.one,
            title, 48, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // Coin display
        var coinBar = CreateAnchoredPanel(panel.transform, "CoinBar",
            new Vector2(0.2f, 0.87f), new Vector2(0.8f, 0.915f));
        var cbBg = coinBar.AddComponent<Image>();
        cbBg.sprite = spr; cbBg.type = Image.Type.Sliced;
        cbBg.color = new Color(0.1f, 0.1f, 0.2f, 0.9f);
        CreateTMP(coinBar.transform, "CoinIcon",
            new Vector2(0.02f, 0), new Vector2(0.15f, 1),
            "<color=#FFD54F>$</color>", 28, Color.white, TextAlignmentOptions.Center);
        var coinTmp = CreateTMP(coinBar.transform, "CoinText",
            new Vector2(0.15f, 0), new Vector2(0.98f, 1),
            "0", 28, new Color(1f, 0.9f, 0.3f), TextAlignmentOptions.Center, FontStyles.Bold);

        // Item container
        var containerArea = CreateAnchoredPanel(panel.transform, "ContainerArea",
            new Vector2(0.03f, 0.12f), new Vector2(0.97f, 0.86f));
        var caBg = containerArea.AddComponent<Image>();
        caBg.sprite = spr; caBg.type = Image.Type.Sliced;
        caBg.color = new Color(0.08f, 0.08f, 0.16f, 0.5f);

        // ScrollRect with separate viewport child
        var scrollRect = containerArea.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 30f;

        // Viewport — child of containerArea, clips content
        var viewport = new GameObject("Viewport");
        viewport.transform.SetParent(containerArea.transform, false);
        var vpRT = viewport.AddComponent<RectTransform>();
        vpRT.anchorMin = Vector2.zero;
        vpRT.anchorMax = Vector2.one;
        vpRT.offsetMin = Vector2.zero;
        vpRT.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();
        viewport.AddComponent<Image>().color = Color.clear; // Raycast target for drag detection

        scrollRect.viewport = vpRT;

        // Content — child of viewport
        GameObject container = new GameObject("ItemContainer");
        container.transform.SetParent(viewport.transform, false);
        RectTransform cRT = container.AddComponent<RectTransform>();
        cRT.anchorMin = new Vector2(0, 1); cRT.anchorMax = new Vector2(1, 1);
        cRT.pivot = new Vector2(0.5f, 1);
        cRT.sizeDelta = new Vector2(0, 2000);

        scrollRect.content = cRT;

        // Close button
        var closeBtn = CreateAnchoredPanel(panel.transform, "CloseBtn",
            new Vector2(0.2f, 0.02f), new Vector2(0.8f, 0.1f));
        var clBg = closeBtn.AddComponent<Image>();
        clBg.sprite = spr; clBg.type = Image.Type.Sliced;
        clBg.color = new Color(0.85f, 0.52f, 0.52f);
        closeBtn.AddComponent<Button>();
        CreateTMP(closeBtn.transform, "Label", Vector2.zero, Vector2.one,
            "KAPAT", 32, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        ShopUI shopUI = panel.AddComponent<ShopUI>();
        SerializedObject so = new SerializedObject(shopUI);
        so.FindProperty("itemContainer").objectReferenceValue = container.transform;
        so.FindProperty("coinDisplay").objectReferenceValue = coinTmp;
        so.FindProperty("category").enumValueIndex = (int)category;
        so.ApplyModifiedPropertiesWithoutUndo();

        return panel;
    }

    // ══════════════════════════════════════════════════════════════
    // WIRING HELPERS
    // ══════════════════════════════════════════════════════════════

    static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindDeep(child, name);
            if (found != null) return found;
        }
        return null;
    }

    static void WireMainMenuManager(MainMenuManager mgr, TopHudRefs hud, NavRefs nav,
        GameObject battle, GameObject equip, GameObject talent, GameObject shop, GameObject settings)
    {
        SerializedObject so = new SerializedObject(mgr);
        so.FindProperty("gemText").objectReferenceValue      = hud.gemText;
        so.FindProperty("goldText").objectReferenceValue     = hud.goldText;
        so.FindProperty("energyText").objectReferenceValue   = hud.energyText;
        so.FindProperty("battlePanel").objectReferenceValue  = battle;
        so.FindProperty("equipmentPanel").objectReferenceValue = equip;
        so.FindProperty("talentPanel").objectReferenceValue  = talent;
        so.FindProperty("shopPanel").objectReferenceValue    = shop;
        so.FindProperty("settingsPanel").objectReferenceValue = settings;
        so.FindProperty("stageInfoText").objectReferenceValue =
            battle.transform.Find("StageInfo")?.GetComponent<TextMeshProUGUI>();

        var tabBgsProp = so.FindProperty("tabBackgrounds");
        tabBgsProp.arraySize = 5;
        for (int i = 0; i < 5; i++)
            tabBgsProp.GetArrayElementAtIndex(i).objectReferenceValue = nav.tabBgs[i];

        var tabLabelsProp = so.FindProperty("tabLabels");
        tabLabelsProp.arraySize = 5;
        for (int i = 0; i < 5; i++)
            tabLabelsProp.GetArrayElementAtIndex(i).objectReferenceValue = nav.tabLabels[i];

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireButton(GameObject btnObj, UIManager target, string methodName)
    {
        Button btn = btnObj.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
            btn.onClick,
            System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, methodName)
                as UnityEngine.Events.UnityAction
        );
    }

    // ══════════════════════════════════════════════════════════════
    // GENERIC UI HELPERS
    // ══════════════════════════════════════════════════════════════

    static GameObject CreateCanvasRoot(string name, int sortOrder)
    {
        GameObject obj = new GameObject(name);
        Canvas canvas = obj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortOrder;

        CanvasScaler scaler = obj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        obj.AddComponent<GraphicRaycaster>();
        return obj;
    }

    static GameObject CreatePanel(Transform parent, string name)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return obj;
    }

    static GameObject CreateAnchoredPanel(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax)
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

    static TextMeshProUGUI CreateTMP(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, string text, int fontSize, Color color,
        TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
    {
        var obj = CreateAnchoredPanel(parent, name, anchorMin, anchorMax);
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = Mathf.Max(10, fontSize / 2);
        tmp.fontSizeMax = fontSize;
        tmp.characterSpacing = 4f;
        tmp.raycastTarget = false;
        tmp.overflowMode = TextOverflowModes.Overflow;

        var font = GetFont();
        if (font != null) tmp.font = font;

        return tmp;
    }

    static GameObject CreateCurrencyPill(Transform parent, string name,
        Vector2 anchorMin, Vector2 anchorMax, string icon, Color color, string amount)
    {
        var pill = CreateAnchoredPanel(parent, name, anchorMin, anchorMax);
        var bg = pill.AddComponent<Image>();
        bg.sprite = GetRoundedSprite();
        bg.type = Image.Type.Sliced;
        bg.color = new Color(0.1f, 0.1f, 0.2f, 0.9f);

        CreateTMP(pill.transform, "Icon",
            new Vector2(0.02f, 0), new Vector2(0.28f, 1),
            $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{icon}</color>", 20,
            Color.white, TextAlignmentOptions.Center);

        CreateTMP(pill.transform, "Amount",
            new Vector2(0.28f, 0), new Vector2(0.98f, 1),
            amount, 18, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        return pill;
    }

    static GameObject CreateTextElement(Transform parent, string name, string text, float fontSize,
        TextAlignmentOptions alignment, Vector2 anchoredPos, Vector2 size)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = alignment;
        tmp.fontStyle = FontStyles.Bold;
        tmp.characterSpacing = 4f;

        var font = GetFont();
        if (font != null) tmp.font = font;

        return obj;
    }

    static GameObject CreateButton(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 size, Color faceColor)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);
        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        // Shadow
        GameObject shadow = new GameObject("Shadow");
        shadow.transform.SetParent(btnObj.transform, false);
        RectTransform shadowRT = shadow.AddComponent<RectTransform>();
        shadowRT.anchorMin = Vector2.zero; shadowRT.anchorMax = Vector2.one;
        shadowRT.offsetMin = new Vector2(0, -5); shadowRT.offsetMax = new Vector2(0, -5);
        float h, s, v;
        Color.RGBToHSV(faceColor, out h, out s, out v);
        shadow.AddComponent<Image>().color = Color.HSVToRGB(h, Mathf.Min(1f, s * 1.2f), v * 0.45f);

        // Face
        GameObject face = new GameObject("Face");
        face.transform.SetParent(btnObj.transform, false);
        RectTransform faceRT = face.AddComponent<RectTransform>();
        faceRT.anchorMin = Vector2.zero; faceRT.anchorMax = Vector2.one;
        faceRT.offsetMin = new Vector2(0, 3); faceRT.offsetMax = Vector2.zero;
        face.AddComponent<Image>().color = faceColor;

        // Shine
        GameObject shine = new GameObject("Shine");
        shine.transform.SetParent(face.transform, false);
        RectTransform shineRT = shine.AddComponent<RectTransform>();
        shineRT.anchorMin = new Vector2(0.05f, 0.6f); shineRT.anchorMax = new Vector2(0.95f, 0.95f);
        shineRT.offsetMin = Vector2.zero; shineRT.offsetMax = Vector2.zero;
        shine.AddComponent<Image>().color = new Color(1, 1, 1, 0.15f);
        shine.GetComponent<Image>().raycastTarget = false;

        // Invisible raycast target
        Image mainImg = btnObj.AddComponent<Image>();
        mainImg.color = new Color(0, 0, 0, 0);
        mainImg.raycastTarget = true;
        btnObj.AddComponent<Button>();

        // Label
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(face.transform, false);
        RectTransform labelRT = labelObj.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = new Vector2(8, 0); labelRT.offsetMax = new Vector2(-8, -2);
        TextMeshProUGUI tmp = labelObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label; tmp.fontSize = 30; tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center; tmp.fontStyle = FontStyles.Bold;
        tmp.enableAutoSizing = true; tmp.fontSizeMin = 14; tmp.fontSizeMax = 30;
        tmp.characterSpacing = 4f;

        // Text shadow
        GameObject tShadow = new GameObject("TextShadow");
        tShadow.transform.SetParent(face.transform, false);
        RectTransform tsRT = tShadow.AddComponent<RectTransform>();
        tsRT.anchorMin = Vector2.zero; tsRT.anchorMax = Vector2.one;
        tsRT.offsetMin = new Vector2(10, -2); tsRT.offsetMax = new Vector2(-6, -4);
        TextMeshProUGUI tsTmp = tShadow.AddComponent<TextMeshProUGUI>();
        tsTmp.text = label; tsTmp.fontSize = 30; tsTmp.color = new Color(0, 0, 0, 0.35f);
        tsTmp.alignment = TextAlignmentOptions.Center; tsTmp.fontStyle = FontStyles.Bold;
        tsTmp.enableAutoSizing = true; tsTmp.fontSizeMin = 14; tsTmp.fontSizeMax = 30;
        tsTmp.characterSpacing = 4f;
        tsTmp.raycastTarget = false;
        tShadow.transform.SetSiblingIndex(0);

        return btnObj;
    }

    // ── Resource helpers ──

    static Sprite GetRoundedSprite()
    {
        if (_roundedSprite != null) return _roundedSprite;
        int size = 32, radius = 8;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(radius - x, x - (size - 1 - radius)));
                float dy = Mathf.Max(0, Mathf.Max(radius - y, y - (size - 1 - radius)));
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - dist + 0.5f));
            }
        tex.SetPixels(pixels);
        tex.Apply();
        _roundedSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        return _roundedSprite;
    }

    static TMP_FontAsset GetFont()
    {
        if (_font != null) return _font;
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/Fonts/LuckiestGuy.asset");
        if (_font == null)
            _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Game/Fonts/LuckiestGuy SDF.asset");
        return _font;
    }

    static GameObject CreateManager<T>(string name) where T : Component
    {
        var obj = new GameObject(name);
        obj.AddComponent<T>();
        return obj;
    }

    static void AssignArenaManagerAssets(ArenaManager arenaManager)
    {
        if (arenaManager == null) return;

        var so = new UnityEditor.SerializedObject(arenaManager);

        // Assign WaveConfigSO
        var waveConfig = AssetDatabase.LoadAssetAtPath<WaveConfigSO>(
            "Assets/_Game/Data/Waves/ArenaWaveConfig.asset");
        if (waveConfig != null)
            so.FindProperty("_waveConfig").objectReferenceValue = waveConfig;

        // Assign all EnemyStatsSO assets
        var guids = AssetDatabase.FindAssets("t:EnemyStatsSO", new[] { "Assets/_Game/Data/Enemies" });
        var statsProp = so.FindProperty("_enemyStats");
        statsProp.arraySize = guids.Length;
        for (int i = 0; i < guids.Length; i++)
        {
            var path  = AssetDatabase.GUIDToAssetPath(guids[i]);
            var asset = AssetDatabase.LoadAssetAtPath<EnemyStatsSO>(path);
            statsProp.GetArrayElementAtIndex(i).objectReferenceValue = asset;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
