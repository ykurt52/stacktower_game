#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

/// <summary>
/// Editor utility to generate the MainMenu and Game scenes with all required objects.
/// </summary>
public static class SceneSetup
{
    private static readonly Color BackgroundColor = new Color(0.102f, 0.102f, 0.180f, 1f); // #1A1A2E

    [MenuItem("Stack Tower/Setup Scenes")]
    public static void SetupAllScenes()
    {
        CreateMainMenuScene();
        CreateGameScene();
        Debug.Log("Stack Tower scenes created successfully in Assets/_Game/Scenes/");
    }

    [MenuItem("Stack Tower/Setup MainMenu Scene")]
    public static void CreateMainMenuScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.backgroundColor = BackgroundColor;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.orthographic = false;
        cam.transform.position = new Vector3(0, 6, -10);
        camObj.tag = "MainCamera";

        // Managers
        CreateManager<GameManager>("GameManager");
        CreateManager<AdManager>("AdManager");
        CreateManager<AudioManager>("AudioManager");

        // Canvas
        GameObject canvas = CreateCanvas();
        UIManager uiManager = canvas.AddComponent<UIManager>();

        // Menu Panel
        GameObject menuPanel = CreatePanel(canvas.transform, "MenuPanel");
        CreateTextElement(menuPanel.transform, "TitleText", "STACK", 72, TextAlignmentOptions.Center,
            new Vector2(0, 100), new Vector2(800, 120));
        GameObject bestText = CreateTextElement(menuPanel.transform, "BestScoreText", "BEST: 0", 36,
            TextAlignmentOptions.Center, new Vector2(0, 20), new Vector2(400, 60));
        GameObject playBtn = CreateButton(menuPanel.transform, "PlayButton", "TAP TO PLAY",
            new Vector2(0, -80), new Vector2(400, 120));

        // Game Panel (hidden in menu scene but needed by UIManager)
        GameObject gamePanel = CreatePanel(canvas.transform, "GamePanel");
        gamePanel.SetActive(false);
        GameObject scoreText = CreateTextElement(gamePanel.transform, "ScoreText", "0", 72,
            TextAlignmentOptions.Center, new Vector2(0, 350), new Vector2(400, 120));

        // Game Over Panel (hidden)
        GameObject gameOverPanel = CreatePanel(canvas.transform, "GameOverPanel");
        gameOverPanel.SetActive(false);
        CreateTextElement(gameOverPanel.transform, "GameOverHeader", "GAME OVER", 60,
            TextAlignmentOptions.Center, new Vector2(0, 100), new Vector2(600, 100));
        GameObject goScoreText = CreateTextElement(gameOverPanel.transform, "ScoreText", "SCORE: 0", 40,
            TextAlignmentOptions.Center, new Vector2(0, 20), new Vector2(400, 60));
        GameObject goBestText = CreateTextElement(gameOverPanel.transform, "BestText", "BEST: 0", 36,
            TextAlignmentOptions.Center, new Vector2(0, -40), new Vector2(400, 60));
        GameObject retryBtn = CreateButton(gameOverPanel.transform, "RetryButton", "RETRY",
            new Vector2(0, -130), new Vector2(300, 80));
        GameObject menuBtn = CreateButton(gameOverPanel.transform, "MenuButton", "MENU",
            new Vector2(0, -220), new Vector2(200, 60));

        // Wire UIManager serialized fields via SerializedObject
        SerializedObject so = new SerializedObject(uiManager);
        so.FindProperty("menuPanel").objectReferenceValue = menuPanel;
        so.FindProperty("gamePanel").objectReferenceValue = gamePanel;
        so.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
        so.FindProperty("bestScoreMenuText").objectReferenceValue = bestText.GetComponent<TextMeshProUGUI>();
        so.FindProperty("currentScoreText").objectReferenceValue = scoreText.GetComponent<TextMeshProUGUI>();
        so.FindProperty("gameOverScoreText").objectReferenceValue = goScoreText.GetComponent<TextMeshProUGUI>();
        so.FindProperty("gameOverBestText").objectReferenceValue = goBestText.GetComponent<TextMeshProUGUI>();
        so.ApplyModifiedPropertiesWithoutUndo();

        // Wire button events
        WireButton(playBtn, uiManager, "OnPlayButton");
        WireButton(retryBtn, uiManager, "OnRetryButton");
        WireButton(menuBtn, uiManager, "OnMenuButton");

        EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/MainMenu.unity");
        Debug.Log("MainMenu scene created.");
    }

    [MenuItem("Stack Tower/Setup Game Scene")]
    public static void CreateGameScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Camera
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.orthographic = false;
        cam.fieldOfView = 60f;
        cam.transform.position = new Vector3(0, 3, -6);
        cam.transform.rotation = Quaternion.Euler(15, 0, 0);
        camObj.tag = "MainCamera";
        camObj.AddComponent<AudioListener>();

        // Skybox
        Material skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Fantasy Skybox FREE/Cubemaps/Classic/FS000_Night_01.mat");
        if (skyboxMat != null)
        {
            cam.clearFlags = CameraClearFlags.Skybox;
            RenderSettings.skybox = skyboxMat;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.1f, 0.1f, 0.2f);
            RenderSettings.ambientEquatorColor = new Color(0.08f, 0.06f, 0.12f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.04f, 0.06f);
        }
        else
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BackgroundColor;
        }

        // Fog for horizon blending
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.015f;
        RenderSettings.fogColor = new Color(0.35f, 0.45f, 0.6f);

        // Post-Processing Volume
        GameObject ppObj = new GameObject("PostProcessVolume");
        Volume volume = ppObj.AddComponent<Volume>();
        volume.isGlobal = true;
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();

        // Bloom
        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.8f);
        bloom.intensity.Override(1.5f);
        bloom.scatter.Override(0.7f);

        // Vignette
        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.35f);
        vignette.smoothness.Override(0.4f);
        vignette.color.Override(new Color(0.05f, 0.02f, 0.1f));

        // Color Adjustments
        ColorAdjustments colorAdj = profile.Add<ColorAdjustments>(true);
        colorAdj.postExposure.Override(0.3f);
        colorAdj.contrast.Override(15f);
        colorAdj.saturation.Override(20f);

        AssetDatabase.CreateAsset(profile, "Assets/_Game/PostProcessProfile.asset");
        volume.profile = profile;

        // Enable post-processing on camera
        var camData = camObj.GetComponent<UniversalAdditionalCameraData>();
        if (camData == null) camData = camObj.AddComponent<UniversalAdditionalCameraData>();
        camData.renderPostProcessing = true;

        // Managers
        CreateManager<GameManager>("GameManager");
        GameObject spawnerObj = CreateManager<BlockSpawner>("BlockSpawner");
        CreateManager<ScoreManager>("ScoreManager");
        CreateManager<AdManager>("AdManager");
        CreateManager<AudioManager>("AudioManager");

        // City background
        GameObject cityObj = new GameObject("CityBackground");
        cityObj.AddComponent<CityBackground>();

        // Directional light — moonlight feel
        GameObject lightObj = new GameObject("DirectionalLight");
        Light dirLight = lightObj.AddComponent<Light>();
        dirLight.type = LightType.Directional;
        dirLight.color = new Color(0.5f, 0.5f, 0.8f);
        dirLight.intensity = 0.8f;
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Base block
        GameObject baseBlock = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseBlock.name = "BaseBlock";
        baseBlock.transform.position = Vector3.zero;
        baseBlock.transform.localScale = new Vector3(3f, 0.25f, 3f);
        Rigidbody baseRb = baseBlock.AddComponent<Rigidbody>();
        baseRb.isKinematic = true;

        // Apply base block material
        Renderer baseRend = baseBlock.GetComponent<Renderer>();
        Material baseMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        baseMat.SetFloat("_Smoothness", 0.7f);
        baseMat.SetFloat("_Metallic", 0.1f);
        Color coralColor = new Color(0.969f, 0.361f, 0.361f);
        baseMat.color = coralColor;
        baseMat.SetColor("_EmissionColor", coralColor * 0.15f);
        baseMat.EnableKeyword("_EMISSION");
        baseRend.sharedMaterial = baseMat;
        baseBlock.AddComponent<OutlineEffect>();

        // Wire BlockSpawner
        BlockSpawner spawner = spawnerObj.GetComponent<BlockSpawner>();
        SerializedObject spawnerSO = new SerializedObject(spawner);
        spawnerSO.FindProperty("baseBlock").objectReferenceValue = baseBlock.transform;

        // Wire particle effect prefabs
        GameObject placeFx = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit A (Blue).prefab");
        if (placeFx == null)
            placeFx = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit A (Red).prefab");
        GameObject perfectFx = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Explosions/CFXR Explosion 1.prefab");

        if (placeFx != null)
            spawnerSO.FindProperty("placeEffectPrefab").objectReferenceValue = placeFx;
        if (perfectFx != null)
            spawnerSO.FindProperty("perfectEffectPrefab").objectReferenceValue = perfectFx;

        spawnerSO.ApplyModifiedPropertiesWithoutUndo();

        // CameraFollow on camera
        CameraFollow camFollow = camObj.AddComponent<CameraFollow>();
        SerializedObject camSO = new SerializedObject(camFollow);
        camSO.FindProperty("blockSpawner").objectReferenceValue = spawner;
        camSO.ApplyModifiedPropertiesWithoutUndo();

        // Canvas + UIManager
        GameObject canvas = CreateCanvas();
        UIManager uiManager = canvas.AddComponent<UIManager>();

        // Menu Panel (hidden in game scene)
        GameObject menuPanel = CreatePanel(canvas.transform, "MenuPanel");
        menuPanel.SetActive(false);
        CreateTextElement(menuPanel.transform, "TitleText", "STACK", 72, TextAlignmentOptions.Center,
            new Vector2(0, 100), new Vector2(800, 120));
        GameObject bestTextMenu = CreateTextElement(menuPanel.transform, "BestScoreText", "BEST: 0", 36,
            TextAlignmentOptions.Center, new Vector2(0, 20), new Vector2(400, 60));
        GameObject playBtn = CreateButton(menuPanel.transform, "PlayButton", "TAP TO PLAY",
            new Vector2(0, -80), new Vector2(400, 120));

        // Game Panel (visible on start)
        GameObject gamePanel = CreatePanel(canvas.transform, "GamePanel");
        GameObject scoreText = CreateTextElement(gamePanel.transform, "ScoreText", "0", 72,
            TextAlignmentOptions.Center, new Vector2(0, 350), new Vector2(400, 120));

        // Game Over Panel (hidden)
        GameObject gameOverPanel = CreatePanel(canvas.transform, "GameOverPanel");
        gameOverPanel.SetActive(false);
        CreateTextElement(gameOverPanel.transform, "GameOverHeader", "GAME OVER", 60,
            TextAlignmentOptions.Center, new Vector2(0, 100), new Vector2(600, 100));
        GameObject goScoreText = CreateTextElement(gameOverPanel.transform, "ScoreText", "SCORE: 0", 40,
            TextAlignmentOptions.Center, new Vector2(0, 20), new Vector2(400, 60));
        GameObject goBestText = CreateTextElement(gameOverPanel.transform, "BestText", "BEST: 0", 36,
            TextAlignmentOptions.Center, new Vector2(0, -40), new Vector2(400, 60));
        GameObject retryBtn = CreateButton(gameOverPanel.transform, "RetryButton", "RETRY",
            new Vector2(0, -130), new Vector2(300, 80));
        GameObject menuBtn = CreateButton(gameOverPanel.transform, "MenuButton", "MENU",
            new Vector2(0, -220), new Vector2(200, 60));

        // Wire UIManager
        SerializedObject uiSO = new SerializedObject(uiManager);
        uiSO.FindProperty("menuPanel").objectReferenceValue = menuPanel;
        uiSO.FindProperty("gamePanel").objectReferenceValue = gamePanel;
        uiSO.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
        uiSO.FindProperty("bestScoreMenuText").objectReferenceValue = bestTextMenu.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("currentScoreText").objectReferenceValue = scoreText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("gameOverScoreText").objectReferenceValue = goScoreText.GetComponent<TextMeshProUGUI>();
        uiSO.FindProperty("gameOverBestText").objectReferenceValue = goBestText.GetComponent<TextMeshProUGUI>();
        uiSO.ApplyModifiedPropertiesWithoutUndo();

        // Wire button events
        WireButton(playBtn, uiManager, "OnPlayButton");
        WireButton(retryBtn, uiManager, "OnRetryButton");
        WireButton(menuBtn, uiManager, "OnMenuButton");

        EditorSceneManager.SaveScene(scene, "Assets/_Game/Scenes/Game.unity");
        Debug.Log("Game scene created.");
    }

    private static GameObject CreateManager<T>(string name) where T : Component
    {
        GameObject obj = new GameObject(name);
        obj.AddComponent<T>();
        return obj;
    }

    private static GameObject CreateCanvas()
    {
        GameObject canvasObj = new GameObject("Canvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // EventSystem
        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();

        return canvasObj;
    }

    private static GameObject CreatePanel(Transform parent, string name)
    {
        GameObject panel = new GameObject(name);
        panel.transform.SetParent(parent, false);

        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return panel;
    }

    private static GameObject CreateTextElement(Transform parent, string name, string text, float fontSize,
        TextAlignmentOptions alignment, Vector2 anchoredPos, Vector2 size)
    {
        GameObject textObj = new GameObject(name);
        textObj.transform.SetParent(parent, false);

        RectTransform rt = textObj.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = alignment;
        tmp.fontStyle = FontStyles.Bold;

        return textObj;
    }

    private static GameObject CreateButton(Transform parent, string name, string label,
        Vector2 anchoredPos, Vector2 size)
    {
        GameObject btnObj = new GameObject(name);
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.AddComponent<RectTransform>();
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(1, 1, 1, 0.05f); // Nearly transparent background for tap target

        Button btn = btnObj.AddComponent<Button>();

        // Label child
        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(btnObj.transform, false);

        RectTransform labelRT = labelObj.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = labelObj.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 36;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;

        return btnObj;
    }

    private static void WireButton(GameObject btnObj, UIManager target, string methodName)
    {
        Button btn = btnObj.GetComponent<Button>();
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
            btn.onClick,
            System.Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction), target, methodName)
                as UnityEngine.Events.UnityAction
        );
    }
}
#endif
