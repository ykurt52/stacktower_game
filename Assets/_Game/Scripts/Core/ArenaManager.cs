using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages the arena: wave spawning, difficulty scaling, game flow.
/// Archero-style endless waves until death. Portrait orientation (tall arena).
/// </summary>
public class ArenaManager : MonoBehaviour
{
    public static ArenaManager Instance { get; private set; }

    [Header("Arena - Portrait (narrow & tall)")]
    [SerializeField] private float arenaHalfWidth = 5f;
    [SerializeField] private float arenaHalfDepth = 16f;

    [Header("Waves")]
    [SerializeField] private float wavePauseDuration = 2.5f;
    [SerializeField] private float spawnInterval = 0.35f;
    [SerializeField] private int maxEnemiesAlive = 12;

    private ArenaCharacter character;
    private ArenaCamera arenaCamera;

    // Wave state
    private int currentWave;
    private int totalEnemiesThisWave;
    private int enemiesSpawnedThisWave;
    private int enemiesKilledThisWave;
    private int enemiesAlive;
    private float spawnTimer;
    private float wavePauseTimer;
    private bool isActive;
    private bool waveInProgress;
    private bool waitingForFirstInput;
    private bool spawningComplete;

    // Pickup spawning
    private float pickupSpawnTimer;

    private List<GameObject> spawnedObjects = new List<GameObject>();

    // Ability system extra stat
    public float BonusMagnetRange
    {
        get
        {
            float bonus = AbilitySystem.Instance != null ? AbilitySystem.Instance.BonusMagnet : 0f;
            if (character != null) bonus += character.BonusMagnetRange;
            return bonus;
        }
    }

    public int CurrentWave => currentWave;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        GameManager.Instance.OnGameStart.AddListener(StartGame);
        GameManager.Instance.OnReturnToMenu.AddListener(Cleanup);
        GameManager.Instance.OnRevive.AddListener(OnRevive);
    }

    private void StartGame()
    {
        Cleanup();

        // Create arena ground
        CreateArena();

        // Use existing main camera or create new one
        var existingCam = Camera.main;
        GameObject camObj;
        if (existingCam != null)
        {
            camObj = existingCam.gameObject;
            arenaCamera = camObj.GetComponent<ArenaCamera>();
            if (arenaCamera == null)
                arenaCamera = camObj.AddComponent<ArenaCamera>();
            if (camObj.GetComponent<AudioListener>() == null)
                camObj.AddComponent<AudioListener>();
        }
        else
        {
            camObj = new GameObject("ArenaCamera");
            camObj.tag = "MainCamera";
            arenaCamera = camObj.AddComponent<ArenaCamera>();
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.3f, 0.5f, 0.3f);
            camObj.AddComponent<AudioListener>();
        }

        // Destroy any extra cameras
        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam.gameObject != camObj)
                Destroy(cam.gameObject);
        }

        // Create character
        var charObj = new GameObject("ArenaCharacter");
        character = charObj.AddComponent<ArenaCharacter>();
        character.Init(Vector3.zero);
        character.SetBounds(arenaHalfWidth - 0.3f, arenaHalfDepth - 0.3f);

        arenaCamera.Init(character.transform);

        // Ensure ability system exists
        if (AbilitySystem.Instance == null)
        {
            var absObj = new GameObject("AbilitySystem");
            absObj.AddComponent<AbilitySystem>();
        }
        AbilitySystem.Instance.Reset();

        // Ensure SyntyAssets exists
        if (SyntyAssets.Instance == null)
        {
            var syntyObj = new GameObject("SyntyAssets");
            syntyObj.AddComponent<SyntyAssets>();
        }

        currentWave = 0;
        enemiesAlive = 0;
        isActive = true;
        waitingForFirstInput = true;
        pickupSpawnTimer = 0;

        // Score
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetScore();
            ScoreManager.Instance.ResetXP();
        }
    }

    private void Update()
    {
        if (!isActive) return;

        // Wait for first joystick input
        if (waitingForFirstInput)
        {
            if (VirtualJoystick.Instance != null && VirtualJoystick.Instance.Magnitude > 0.1f)
            {
                waitingForFirstInput = false;
                character.Activate();
                BeginWavePause(1f); // Short initial pause
            }
            return;
        }

        if (character == null || character.IsDead) return;

        // ── Between waves: pause timer ──
        if (!waveInProgress)
        {
            wavePauseTimer -= Time.deltaTime;
            if (wavePauseTimer <= 0)
                StartNextWave();
            return;
        }

        // ── During wave: spawn enemies ──
        if (enemiesSpawnedThisWave < totalEnemiesThisWave)
        {
            // Don't exceed max alive enemies at once
            if (enemiesAlive < maxEnemiesAlive)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0)
                {
                    spawnTimer = spawnInterval;
                    SpawnOneEnemy();
                    enemiesSpawnedThisWave++;
                }
            }
        }
        else if (!spawningComplete)
        {
            spawningComplete = true;
        }

        // ── Check wave clear: all spawned AND all killed ──
        if (spawningComplete && enemiesAlive <= 0)
        {
            OnWaveCleared();
        }
    }

    private void BeginWavePause(float duration)
    {
        waveInProgress = false;
        wavePauseTimer = duration;
    }

    private void StartNextWave()
    {
        currentWave++;
        waveInProgress = true;
        spawningComplete = false;
        enemiesSpawnedThisWave = 0;
        enemiesKilledThisWave = 0;
        spawnTimer = 0.2f; // Small initial delay

        // Calculate enemy count for this wave
        totalEnemiesThisWave = CalculateEnemyCount(currentWave);

        // Wave announcement
        FloatingText.Spawn(character.transform.position + Vector3.up * 1.5f,
            "DALGA " + currentWave, new Color(1f, 0.9f, 0.2f), 1.5f);

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(currentWave);

        // Spawn pickups at wave start (from wave 2+)
        if (currentWave >= 2)
            SpawnWavePickups();
    }

    private int CalculateEnemyCount(int wave)
    {
        // Gradual scaling: starts small, grows
        // Wave 1: 3, Wave 5: 7, Wave 10: 12, Wave 20: 18, Wave 30: 22
        int count = 2 + wave;

        // Boss waves have fewer regular enemies + 1 boss
        if (wave % 10 == 0)
            count = Mathf.Max(3, count / 2) + 1;

        return Mathf.Min(count, 25);
    }

    private void OnWaveCleared()
    {
        waveInProgress = false;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySuccess();

        FloatingText.Spawn(character.transform.position + Vector3.up * 1.5f,
            "DALGA TEMİZLENDİ!", new Color(0.3f, 1f, 0.5f), 1.5f);

        // Longer pause for boss waves, shorter for early waves
        float pause = currentWave % 10 == 0 ? 3.5f : wavePauseDuration;
        BeginWavePause(pause);
    }

    public void OnEnemyKilled()
    {
        enemiesAlive--;
        enemiesKilledThisWave++;
        if (enemiesAlive < 0) enemiesAlive = 0;
    }

    private void SpawnOneEnemy()
    {
        ArenaEnemy.EnemyType type = GetEnemyTypeForWave();
        Vector3 pos = GetSpawnPosition();

        var enemyObj = new GameObject("Enemy_" + type);
        var enemy = enemyObj.AddComponent<ArenaEnemy>();
        enemy.Init(type, pos, currentWave);

        spawnedObjects.Add(enemyObj);
        enemiesAlive++;
    }

    private ArenaEnemy.EnemyType GetEnemyTypeForWave()
    {
        // Boss every 10 waves - last enemy of the wave
        if (currentWave % 10 == 0 && enemiesSpawnedThisWave == totalEnemiesThisWave - 1)
            return ArenaEnemy.EnemyType.Boss;

        float r = Random.value;

        // Early waves: simple enemies
        if (currentWave <= 2)
            return ArenaEnemy.EnemyType.Melee;

        if (currentWave <= 5)
            return r < 0.65f ? ArenaEnemy.EnemyType.Melee : ArenaEnemy.EnemyType.Ranged;

        if (currentWave <= 8)
        {
            if (r < 0.35f) return ArenaEnemy.EnemyType.Melee;
            if (r < 0.60f) return ArenaEnemy.EnemyType.Ranged;
            return ArenaEnemy.EnemyType.Heavy;
        }

        if (currentWave <= 12)
        {
            if (r < 0.20f) return ArenaEnemy.EnemyType.Melee;
            if (r < 0.40f) return ArenaEnemy.EnemyType.Ranged;
            if (r < 0.55f) return ArenaEnemy.EnemyType.Heavy;
            if (r < 0.70f) return ArenaEnemy.EnemyType.Bomber;
            if (r < 0.85f) return ArenaEnemy.EnemyType.Wizard;
            return ArenaEnemy.EnemyType.IceMage;
        }

        // Wave 13+: all types, weighted
        if (r < 0.12f) return ArenaEnemy.EnemyType.Melee;
        if (r < 0.25f) return ArenaEnemy.EnemyType.Ranged;
        if (r < 0.40f) return ArenaEnemy.EnemyType.Heavy;
        if (r < 0.55f) return ArenaEnemy.EnemyType.Bomber;
        if (r < 0.75f) return ArenaEnemy.EnemyType.Wizard;
        return ArenaEnemy.EnemyType.IceMage;
    }

    private Vector3 GetSpawnPosition()
    {
        // Spawn outside visible area at arena edges
        int edge = Random.Range(0, 4);
        float margin = 0.5f;
        float x, z;

        switch (edge)
        {
            case 0: // top
                x = Random.Range(-arenaHalfWidth + margin, arenaHalfWidth - margin);
                z = arenaHalfDepth - margin;
                break;
            case 1: // bottom
                x = Random.Range(-arenaHalfWidth + margin, arenaHalfWidth - margin);
                z = -arenaHalfDepth + margin;
                break;
            case 2: // left
                x = -arenaHalfWidth + margin;
                z = Random.Range(-arenaHalfDepth + margin, arenaHalfDepth - margin);
                break;
            default: // right
                x = arenaHalfWidth - margin;
                z = Random.Range(-arenaHalfDepth + margin, arenaHalfDepth - margin);
                break;
        }

        // Ensure not too close to player
        if (character != null)
        {
            Vector3 candidate = new Vector3(x, 0, z);
            float dist = Vector3.Distance(candidate, character.transform.position);
            if (dist < 3f)
            {
                // Push to opposite edge
                x = -x;
                z = -z;
            }
        }

        return new Vector3(x, 0, z);
    }

    // ── Pickup Spawning ──

    private void SpawnWavePickups()
    {
        // Spawn 1-3 pickups around the arena between waves
        int count = 1;
        if (currentWave >= 5) count = 2;
        if (currentWave >= 15) count = 3;

        for (int i = 0; i < count; i++)
        {
            ArenaPickup.PickupType type = GetRandomPickupType();
            Vector3 pos = GetRandomPickupPosition();

            var pickupObj = new GameObject("Pickup_" + type);
            var pickup = pickupObj.AddComponent<ArenaPickup>();
            pickup.Init(type, pos);
            spawnedObjects.Add(pickupObj);
        }
    }

    private ArenaPickup.PickupType GetRandomPickupType()
    {
        float r = Random.value;
        // Heal is most common, then shield, attack speed, magnet, bomb (rare)
        if (r < 0.35f) return ArenaPickup.PickupType.Heal;
        if (r < 0.55f) return ArenaPickup.PickupType.Shield;
        if (r < 0.75f) return ArenaPickup.PickupType.AttackSpeed;
        if (r < 0.90f) return ArenaPickup.PickupType.Magnet;
        return ArenaPickup.PickupType.Bomb;
    }

    private Vector3 GetRandomPickupPosition()
    {
        // Random position within inner area (not too close to edges)
        float margin = 1.5f;
        float x = Random.Range(-arenaHalfWidth + margin, arenaHalfWidth - margin);
        float z = Random.Range(-arenaHalfDepth + margin, arenaHalfDepth - margin);

        // Not too close to player
        if (character != null)
        {
            Vector3 candidate = new Vector3(x, 0, z);
            float dist = Vector3.Distance(candidate, character.transform.position);
            if (dist < 2f)
            {
                x = -x;
                z = -z;
            }
        }

        return new Vector3(x, 0, z);
    }

    // ── Revive ──

    private void OnRevive()
    {
        if (character != null)
        {
            character.Revive();
            isActive = true;

            // Kill all enemies on screen as mercy
            foreach (var enemy in FindObjectsByType<ArenaEnemy>(FindObjectsSortMode.None))
            {
                if (!enemy.IsDead)
                    enemy.TakeDamage(9999);
            }

            // Give a pause before next wave
            BeginWavePause(2f);
        }
    }

    // ── Arena Construction ──

    private void CreateArena()
    {
        // Ground plane (portrait: narrower X, taller Z)
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "ArenaGround";
        ground.transform.position = new Vector3(0, -0.1f, 0);
        ground.transform.localScale = new Vector3(arenaHalfWidth * 2f + 1f, 0.2f, arenaHalfDepth * 2f + 1f);

        var groundRend = ground.GetComponent<Renderer>();
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        groundMat.color = new Color(0.25f, 0.55f, 0.25f); // Grass green
        groundRend.material = groundMat;
        spawnedObjects.Add(ground);

        // Arena border walls
        Color borderColor = new Color(0.15f, 0.35f, 0.15f);
        float borderThickness = 0.3f;
        float wallH = 0.5f;

        // Top wall
        CreateWall(new Vector3(0, 0, arenaHalfDepth + borderThickness / 2f),
            new Vector3(arenaHalfWidth * 2f + 1f, wallH, borderThickness), borderColor);
        // Bottom wall
        CreateWall(new Vector3(0, 0, -arenaHalfDepth - borderThickness / 2f),
            new Vector3(arenaHalfWidth * 2f + 1f, wallH, borderThickness), borderColor);
        // Left wall
        CreateWall(new Vector3(-arenaHalfWidth - borderThickness / 2f, 0, 0),
            new Vector3(borderThickness, wallH, arenaHalfDepth * 2f + 1f), borderColor);
        // Right wall
        CreateWall(new Vector3(arenaHalfWidth + borderThickness / 2f, 0, 0),
            new Vector3(borderThickness, wallH, arenaHalfDepth * 2f + 1f), borderColor);

        // Decorative rocks/props at corners
        float dx = arenaHalfWidth - 1.5f;
        float dz = arenaHalfDepth - 1.5f;
        SpawnDecoration(new Vector3(-dx, 0, dz));
        SpawnDecoration(new Vector3(dx, 0, -dz));
        SpawnDecoration(new Vector3(dx, 0, dz));
        SpawnDecoration(new Vector3(-dx, 0, -dz));

        // Lighting
        var lightObj = new GameObject("DirectionalLight");
        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.2f;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0);
        spawnedObjects.Add(lightObj);
    }

    private void CreateWall(Vector3 pos, Vector3 scale, Color color)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Wall";
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        var rend = wall.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;
        spawnedObjects.Add(wall);
    }

    private void SpawnDecoration(Vector3 pos)
    {
        var synty = SyntyAssets.Instance;
        if (synty != null && synty.RockPrefab != null)
        {
            var rock = SyntyAssets.Spawn(synty.RockPrefab, null, pos, 0.3f, Random.Range(0f, 360f));
            if (rock != null)
            {
                SyntyAssets.FixMaterials(rock, new Color(0.5f, 0.5f, 0.45f));
                spawnedObjects.Add(rock);
            }
        }
    }

    private void Cleanup()
    {
        isActive = false;

        foreach (var obj in spawnedObjects)
        {
            if (obj != null) Destroy(obj);
        }
        spawnedObjects.Clear();

        // Cleanup remaining dynamic objects
        foreach (var e in FindObjectsByType<ArenaEnemy>(FindObjectsSortMode.None)) Destroy(e.gameObject);
        foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None)) Destroy(p.gameObject);
        foreach (var g in FindObjectsByType<XPGem>(FindObjectsSortMode.None)) Destroy(g.gameObject);
        foreach (var b in FindObjectsByType<ArenaBomb>(FindObjectsSortMode.None)) Destroy(b.gameObject);
        foreach (var pk in FindObjectsByType<ArenaPickup>(FindObjectsSortMode.None)) Destroy(pk.gameObject);

        if (character != null) { Destroy(character.gameObject); character = null; }

        // Reset camera to menu position instead of destroying it
        if (arenaCamera != null)
        {
            var cam = arenaCamera.GetComponent<Camera>();
            if (cam != null)
            {
                arenaCamera.transform.position = new Vector3(0, 8, -14);
                arenaCamera.transform.rotation = Quaternion.Euler(30, 0, 0);
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.3f, 0.5f, 0.3f);
            }
            // Remove ArenaCamera component so it stops following
            Destroy(arenaCamera);
            arenaCamera = null;
        }
    }
}
