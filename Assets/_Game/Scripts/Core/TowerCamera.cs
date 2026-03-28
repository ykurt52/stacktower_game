using UnityEngine;

/// <summary>
/// Camera that follows the character upward through the tower.
/// After first tap: scrolls up at increasing speed.
/// Also follows character if they climb faster than scroll speed.
/// Character below camera view = death.
/// </summary>
public class TowerCamera : MonoBehaviour
{
    [Header("Follow")]
    [SerializeField] private float followSmooth = 4f;
    [SerializeField] private float followLeadY = 3f;

    [Header("Death Laser")]
    [SerializeField] private float laserInitialSpeed = 0.4f;
    [SerializeField] private float laserAcceleration = 0.02f;
    [SerializeField] private float laserMaxSpeed = 2.5f;
    [SerializeField] private float laserStartDelay = 3f;

    [Header("Offset")]
    [SerializeField] private Vector3 offset = new Vector3(0, 3.5f, -10f);

    private bool isScrolling;
    private TowerCharacter character;
    private float shakeTimer;
    private float shakeIntensity;
    private Camera cam;
    private GameObject laserRoot;
    private float laserPulseTimer;
    private float laserY;
    private float laserSpeed;
    private float laserDelayTimer;
    private TowerParallax parallax;

    public float LaserY => laserY;

    // Sky gradient: height thresholds and colors
    private static readonly float[] skyHeights = { 0, 15, 35, 55, 80, 120 };
    private static readonly Color[] skyColors =
    {
        new Color(0.4f, 0.6f, 0.9f),    // Lobby: bright blue
        new Color(0.3f, 0.5f, 0.85f),   // Office: deeper blue
        new Color(0.8f, 0.5f, 0.2f),    // Construction: sunset orange
        new Color(0.4f, 0.2f, 0.5f),    // Penthouse: purple dusk
        new Color(0.15f, 0.1f, 0.25f),  // Rooftop: dark night
        new Color(0.05f, 0.05f, 0.15f), // Sky: deep space
    };

    private float laserSpeedMultiplier = 1f;

    public void Init(TowerCharacter character)
    {
        this.character = character;
        isScrolling = false;
        shakeTimer = 0;
        shakeIntensity = 0;
        laserPulseTimer = 0;
        laserY = -3f;
        laserSpeed = 0;
        laserDelayTimer = 0;
        cam = GetComponent<Camera>();

        // Apply laser speed modifiers from shop
        laserSpeedMultiplier = 1f;
        if (ShopManager.Instance != null)
        {
            // Permanent skill: laser resist
            laserSpeedMultiplier *= ShopManager.Instance.GetLaserSpeedMultiplier();
            // Consumable: slow laser (50% slower this game)
            if (ShopManager.Instance.ConsumeSlowLaser())
                laserSpeedMultiplier *= 0.5f;
        }

        Vector3 pos = character.transform.position + offset;
        transform.position = pos;
        transform.rotation = Quaternion.Euler(12, 0, 0);

        // Setup scene lighting for 2.5D look
        SetupLighting();

        CreateDeathLaser();
        if (laserRoot != null) laserRoot.SetActive(false);

        // Parallax background
        if (parallax != null) parallax.Cleanup();
        parallax = gameObject.GetComponent<TowerParallax>();
        if (parallax == null) parallax = gameObject.AddComponent<TowerParallax>();
        parallax.Init();
    }

    private void SetupLighting()
    {
        // Remove old lights we created
        var oldLight = GameObject.Find("TowerDirectionalLight");
        if (oldLight != null) Destroy(oldLight);
        var oldFill = GameObject.Find("TowerFillLight");
        if (oldFill != null) Destroy(oldFill);

        // Main directional light — warm sunlight from top-left
        var lightObj = new GameObject("TowerDirectionalLight");
        lightObj.transform.rotation = Quaternion.Euler(45, -30, 0);
        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.95f, 0.85f);
        light.intensity = 1.2f;
        light.shadows = LightShadows.Soft;
        light.shadowStrength = 0.4f;

        // Fill light — cool from opposite side for depth
        var fillObj = new GameObject("TowerFillLight");
        fillObj.transform.rotation = Quaternion.Euler(30, 150, 0);
        var fill = fillObj.AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.color = new Color(0.6f, 0.7f, 0.9f);
        fill.intensity = 0.4f;
        fill.shadows = LightShadows.None;

        // Ambient light
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.35f, 0.35f, 0.4f);
    }

    public void StartScrolling()
    {
        isScrolling = true;
    }

    public void StopScrolling()
    {
        isScrolling = false;
        if (laserRoot != null) laserRoot.SetActive(false);
    }

    public void Shake(float intensity = 0.3f, float duration = 0.3f)
    {
        shakeIntensity = intensity;
        shakeTimer = duration;
    }

    public bool IsCharacterHitByLaser()
    {
        if (character == null) return false;
        return character.CurrentY < laserY;
    }

    public bool IsCharacterBelowCamera()
    {
        if (character == null || cam == null) return false;
        // Character below the bottom edge of the camera view
        float camBottomY = transform.position.y - cam.orthographicSize;
        if (!cam.orthographic)
        {
            // Perspective: calculate visible height at character's Z distance
            float dist = Mathf.Abs(transform.position.z - character.transform.position.z);
            float halfHeight = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            camBottomY = transform.position.y - halfHeight;
        }
        return character.CurrentY < camBottomY - 1f;
    }

    private void LateUpdate()
    {
        if (character == null) return;

        if (!isScrolling)
        {
            // Before first tap: static, just look at character area
            return;
        }

        // Follow character — use highest landed Y so camera doesn't drop back down
        float landedTargetY = character.HighestLandedY + followLeadY;

        // During spring pad launch, also track character's live position
        float liveTargetY = character.CurrentY + followLeadY;
        float targetY = Mathf.Max(landedTargetY, liveTargetY - 2f);

        // Never move camera downward
        float currentY = transform.position.y;
        if (targetY < currentY) targetY = currentY;

        // Smooth follow
        Vector3 pos = transform.position;
        pos.y = Mathf.Lerp(pos.y, targetY, followSmooth * Time.deltaTime);
        pos.x = offset.x;

        // Screen shake
        if (shakeTimer > 0)
        {
            shakeTimer -= Time.unscaledDeltaTime;
            float t = shakeTimer > 0 ? shakeIntensity : 0;
            pos.x += Random.Range(-t, t);
            pos.y += Random.Range(-t, t);
        }

        transform.position = pos;

        // Dynamic sky color
        UpdateSkyColor();

        // Parallax background
        if (parallax != null)
            parallax.UpdateParallax(character != null ? character.HighestLandedY : 0);

        // Death laser follows camera
        UpdateDeathLaser();
    }

    private void UpdateSkyColor()
    {
        if (cam == null) return;
        float h = character != null ? character.HighestLandedY : 0;

        // Find which two colors to lerp between
        for (int i = 0; i < skyHeights.Length - 1; i++)
        {
            if (h <= skyHeights[i + 1] || i == skyHeights.Length - 2)
            {
                float t = Mathf.InverseLerp(skyHeights[i], skyHeights[i + 1], h);
                cam.backgroundColor = Color.Lerp(skyColors[i], skyColors[i + 1], t);

                // Fog color follows sky
                RenderSettings.fogColor = cam.backgroundColor;
                break;
            }
        }
    }

    private void CreateDeathLaser()
    {
        if (laserRoot != null) Destroy(laserRoot);

        laserRoot = new GameObject("DeathLaser");
        // Don't parent to camera — we position it manually in world space each frame

        float laserWidth = 20f;

        // Outer glow (wide, faint red)
        CreateLaserLayer(laserRoot, "Glow3", new Vector3(laserWidth, 0.6f, 0.6f),
            new Color(0.8f, 0.05f, 0.05f));
        // Mid glow
        CreateLaserLayer(laserRoot, "Glow2", new Vector3(laserWidth, 0.3f, 0.4f),
            new Color(0.9f, 0.1f, 0.08f));
        // Inner glow
        CreateLaserLayer(laserRoot, "Glow1", new Vector3(laserWidth, 0.15f, 0.3f),
            new Color(1f, 0.2f, 0.15f));
        // Core beam (bright, thin)
        CreateLaserLayer(laserRoot, "Core", new Vector3(laserWidth, 0.05f, 0.2f),
            new Color(1f, 0.5f, 0.4f));
    }

    private void CreateLaserLayer(GameObject parent, string name, Vector3 scale, Color color)
    {
        var layer = GameObject.CreatePrimitive(PrimitiveType.Cube);
        layer.name = name;
        layer.transform.SetParent(parent.transform, false);
        layer.transform.localPosition = Vector3.zero;
        layer.transform.localScale = scale;
        var col = layer.GetComponent<Collider>();
        if (col != null) { col.enabled = false; Destroy(col); }
        var rend = layer.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;
    }

    private void UpdateDeathLaser()
    {
        if (laserRoot == null || !isScrolling) return;

        // Delay before laser starts moving
        laserDelayTimer += Time.deltaTime;
        if (laserDelayTimer < laserStartDelay)
        {
            laserRoot.SetActive(false);
            return;
        }

        laserRoot.SetActive(true);

        // Init speed on first active frame
        if (laserSpeed == 0) laserSpeed = laserInitialSpeed * laserSpeedMultiplier;

        // Accelerate laser speed over time
        laserSpeed = Mathf.Min(laserSpeed + laserAcceleration * laserSpeedMultiplier * Time.deltaTime,
                               laserMaxSpeed * laserSpeedMultiplier);

        // Move laser up at its own pace
        laserY += laserSpeed * Time.deltaTime;

        // Position laser in world space
        laserRoot.transform.position = new Vector3(0, laserY, 0);

        // Pulse animation — subtle scale breathing
        laserPulseTimer += Time.deltaTime * 3f;
        float pulse = 1f + Mathf.Sin(laserPulseTimer) * 0.15f;
        laserRoot.transform.localScale = new Vector3(1f, pulse, 1f);
    }
}
