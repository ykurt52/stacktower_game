using UnityEngine;
using UnityEngine.Pool;
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

    [Header("Wave Configuration")]
    [SerializeField] private WaveConfigSO _waveConfig;
    [SerializeField] private EnemyStatsSO[] _enemyStats;

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

    // Object pool & active tracking
    private ObjectPool<ArenaEnemy> _enemyPool;
    private readonly HashSet<ArenaEnemy> _activeEnemies = new HashSet<ArenaEnemy>();

    // Scene objects destroyed on cleanup (ground, walls, light, etc.)
    private List<GameObject> spawnedObjects = new List<GameObject>();

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

        _enemyPool = new ObjectPool<ArenaEnemy>(
            createFunc:      () => new GameObject("ArenaEnemy").AddComponent<ArenaEnemy>(),
            actionOnGet:     e  => e.gameObject.SetActive(true),
            actionOnRelease: e  => e.gameObject.SetActive(false),
            actionOnDestroy: e  => { if (e != null) Destroy(e.gameObject); },
            collectionCheck: false,
            defaultCapacity: ArenaConstants.ENEMY_POOL_DEFAULT_CAPACITY,
            maxSize:         ArenaConstants.ENEMY_POOL_MAX_SIZE
        );
    }

    private void OnDestroy()
    {
        _enemyPool?.Clear();
    }

    private void Start()
    {
        GameManager.Instance.OnGameStart.AddListener(StartGame);
        GameManager.Instance.OnReturnToMenu.AddListener(Cleanup);
        GameManager.Instance.OnRevive.AddListener(OnRevive);
    }

    private void StartGame()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_waveConfig == null)
            Debug.LogError("[ArenaManager] _waveConfig is null -- assign a WaveConfigSO in the Inspector.");
        if (_enemyStats == null || _enemyStats.Length == 0)
            Debug.LogError("[ArenaManager] _enemyStats is empty -- assign EnemyStatsSO assets in the Inspector.");
#endif
        Cleanup();

        CreateArena();

        var existingCam = Camera.main;
        GameObject camObj;
        if (existingCam != null)
        {
            camObj       = existingCam.gameObject;
            arenaCamera  = camObj.GetComponent<ArenaCamera>();
            if (arenaCamera == null) arenaCamera = camObj.AddComponent<ArenaCamera>();
            if (camObj.GetComponent<AudioListener>() == null) camObj.AddComponent<AudioListener>();
        }
        else
        {
            camObj      = new GameObject("ArenaCamera");
            camObj.tag  = "MainCamera";
            arenaCamera = camObj.AddComponent<ArenaCamera>();
            var cam     = camObj.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.3f, 0.5f, 0.3f);
            camObj.AddComponent<AudioListener>();
        }

        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (cam.gameObject != camObj) Destroy(cam.gameObject);
        }

        var charObj = new GameObject("ArenaCharacter");
        character   = charObj.AddComponent<ArenaCharacter>();
        character.Init(Vector3.zero);
        character.SetBounds(arenaHalfWidth - 0.3f, arenaHalfDepth - 0.3f);

        arenaCamera.Init(character.transform);

        if (AbilitySystem.Instance == null)
            new GameObject("AbilitySystem").AddComponent<AbilitySystem>();
        AbilitySystem.Instance.Reset();

        if (SyntyAssets.Instance == null)
            new GameObject("SyntyAssets").AddComponent<SyntyAssets>();

        currentWave        = 0;
        enemiesAlive       = 0;
        isActive           = true;
        waitingForFirstInput = true;

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetScore();
            ScoreManager.Instance.ResetXP();
        }
    }

    private void Update()
    {
        if (!isActive) return;

        if (waitingForFirstInput)
        {
            if (VirtualJoystick.Instance != null
                && VirtualJoystick.Instance.Magnitude > ArenaConstants.JOYSTICK_FIRST_INPUT_THRESHOLD)
            {
                waitingForFirstInput = false;
                character.Activate();
                BeginWavePause(_waveConfig.initialWavePause);
            }
            return;
        }

        if (character == null || character.IsDead) return;

        if (!waveInProgress)
        {
            wavePauseTimer -= Time.deltaTime;
            if (wavePauseTimer <= 0) StartNextWave();
            return;
        }

        if (enemiesSpawnedThisWave < totalEnemiesThisWave)
        {
            if (enemiesAlive < _waveConfig.maxEnemiesAlive)
            {
                spawnTimer -= Time.deltaTime;
                if (spawnTimer <= 0)
                {
                    spawnTimer = _waveConfig.spawnInterval;
                    if (SpawnOneEnemy())
                        enemiesSpawnedThisWave++;
                }
            }
        }
        else if (!spawningComplete)
        {
            spawningComplete = true;
        }

        if (spawningComplete && enemiesAlive <= 0)
            OnWaveCleared();
    }

    private void BeginWavePause(float duration)
    {
        waveInProgress = false;
        wavePauseTimer = duration;
    }

    private void StartNextWave()
    {
        currentWave++;
        waveInProgress         = true;
        spawningComplete       = false;
        enemiesSpawnedThisWave = 0;
        enemiesKilledThisWave  = 0;
        spawnTimer             = _waveConfig.initialSpawnDelay;

        totalEnemiesThisWave = _waveConfig.GetEnemyCount(currentWave);

        FloatingText.Spawn(character.transform.position + Vector3.up * 1.5f,
            "DALGA " + currentWave, new Color(1f, 0.9f, 0.2f), 1.5f);

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddScore(currentWave);

        if (currentWave >= 2)
            SpawnWavePickups();
    }

    private void OnWaveCleared()
    {
        waveInProgress = false;

        if (AudioManager.Instance != null) AudioManager.Instance.PlaySuccess();

        FloatingText.Spawn(character.transform.position + Vector3.up * 1.5f,
            "DALGA TEMIZLENDI!", new Color(0.3f, 1f, 0.5f), 1.5f);

        float pause = currentWave % _waveConfig.bossWaveInterval == 0
            ? _waveConfig.bossPauseDuration
            : _waveConfig.wavePauseDuration;
        BeginWavePause(pause);
    }

    public void OnEnemyKilled()
    {
        enemiesAlive--;
        enemiesKilledThisWave++;
        if (enemiesAlive < 0) enemiesAlive = 0;
    }

    /// <summary>Returns a dead enemy back to the pool. Called by ArenaEnemy.ReleaseAfterDelay.</summary>
    public void ReleaseEnemy(ArenaEnemy enemy)
    {
        if (enemy == null || !_activeEnemies.Remove(enemy)) return;
        _enemyPool.Release(enemy);
    }

    // Returns true if an enemy was successfully spawned.
    private bool SpawnOneEnemy()
    {
        ArenaEnemy.EnemyType enemyType = GetEnemyTypeForWave();
        EnemyStatsSO stats = GetEnemyStats(enemyType);

        if (stats == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[ArenaManager] No EnemyStatsSO for type {enemyType}. Assign _enemyStats in Inspector.");
#endif
            return false;
        }

        float waveMult = 1f + currentWave * _waveConfig.statScalePerWave;
        Vector3 pos    = GetSpawnPosition();

        var enemy = _enemyPool.Get();
        enemy.Init(stats, pos, waveMult);
        _activeEnemies.Add(enemy);
        enemiesAlive++;
        return true;
    }

    private ArenaEnemy.EnemyType GetEnemyTypeForWave()
    {
        if (currentWave % _waveConfig.bossWaveInterval == 0
            && enemiesSpawnedThisWave == totalEnemiesThisWave - 1)
            return ArenaEnemy.EnemyType.Boss;

        return _waveConfig.GetTierForWave(currentWave).GetRandomType();
    }

    private EnemyStatsSO GetEnemyStats(ArenaEnemy.EnemyType type)
    {
        if (_enemyStats == null) return null;
        foreach (var s in _enemyStats)
            if (s != null && s.type == type) return s;
        return _enemyStats.Length > 0 ? _enemyStats[0] : null;
    }

    private Vector3 GetSpawnPosition()
    {
        float m = ArenaConstants.SPAWN_EDGE_MARGIN;
        int   edge = Random.Range(0, 4);
        float x, z;

        switch (edge)
        {
            case 0:  x = Random.Range(-arenaHalfWidth + m, arenaHalfWidth - m); z =  arenaHalfDepth - m; break;
            case 1:  x = Random.Range(-arenaHalfWidth + m, arenaHalfWidth - m); z = -arenaHalfDepth + m; break;
            case 2:  x = -arenaHalfWidth + m; z = Random.Range(-arenaHalfDepth + m, arenaHalfDepth - m); break;
            default: x =  arenaHalfWidth - m; z = Random.Range(-arenaHalfDepth + m, arenaHalfDepth - m); break;
        }

        if (character != null)
        {
            var candidate = new Vector3(x, 0, z);
            if (Vector3.Distance(candidate, character.transform.position) < ArenaConstants.SPAWN_MIN_PLAYER_DIST)
            {
                x = -x;
                z = -z;
            }
        }

        return new Vector3(x, 0, z);
    }

    // ── Pickup Spawning ──────────────────────────────────────────────────────

    private void SpawnWavePickups()
    {
        int count = _waveConfig.GetPickupCount(currentWave);
        for (int i = 0; i < count; i++)
        {
            ArenaPickup.PickupType pickupType = _waveConfig.GetRandomPickupType();
            Vector3 pos = GetRandomPickupPosition();

            var pickupObj = new GameObject("Pickup_" + pickupType);
            var pickup    = pickupObj.AddComponent<ArenaPickup>();
            pickup.Init(pickupType, pos);
            spawnedObjects.Add(pickupObj);
        }
    }

    private Vector3 GetRandomPickupPosition()
    {
        float m = ArenaConstants.PICKUP_INNER_MARGIN;
        float x = Random.Range(-arenaHalfWidth + m, arenaHalfWidth - m);
        float z = Random.Range(-arenaHalfDepth + m, arenaHalfDepth - m);

        if (character != null)
        {
            var candidate = new Vector3(x, 0, z);
            if (Vector3.Distance(candidate, character.transform.position) < ArenaConstants.PICKUP_MIN_PLAYER_DIST)
            {
                x = -x;
                z = -z;
            }
        }

        return new Vector3(x, 0, z);
    }

    // ── Revive ───────────────────────────────────────────────────────────────

    private void OnRevive()
    {
        if (character == null) return;

        character.Revive();
        isActive = true;

        // Mercy kill all active enemies
        foreach (var enemy in _activeEnemies)
        {
            if (enemy != null && !enemy.IsDead)
                enemy.TakeDamage(ArenaConstants.INSTANT_KILL_DAMAGE);
        }

        BeginWavePause(ArenaConstants.REVIVE_WAVE_PAUSE);
    }

    // ── Arena Construction ───────────────────────────────────────────────────

    private void CreateArena()
    {
        var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name                  = "ArenaGround";
        ground.transform.position    = new Vector3(0, -0.1f, 0);
        ground.transform.localScale  = new Vector3(arenaHalfWidth * 2f + 1f, 0.2f, arenaHalfDepth * 2f + 1f);
        var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        groundMat.color = new Color(0.25f, 0.55f, 0.25f);
        ground.GetComponent<Renderer>().material = groundMat;
        spawnedObjects.Add(ground);

        Color borderColor     = new Color(0.15f, 0.35f, 0.15f);
        float borderThickness = 0.3f;
        float wallH           = 0.5f;

        CreateWall(new Vector3(0,  0,  arenaHalfDepth + borderThickness / 2f), new Vector3(arenaHalfWidth * 2f + 1f, wallH, borderThickness), borderColor);
        CreateWall(new Vector3(0,  0, -arenaHalfDepth - borderThickness / 2f), new Vector3(arenaHalfWidth * 2f + 1f, wallH, borderThickness), borderColor);
        CreateWall(new Vector3(-arenaHalfWidth - borderThickness / 2f, 0, 0),  new Vector3(borderThickness, wallH, arenaHalfDepth * 2f + 1f), borderColor);
        CreateWall(new Vector3( arenaHalfWidth + borderThickness / 2f, 0, 0),  new Vector3(borderThickness, wallH, arenaHalfDepth * 2f + 1f), borderColor);

        float dx = arenaHalfWidth  - 1.5f;
        float dz = arenaHalfDepth  - 1.5f;
        SpawnDecoration(new Vector3(-dx, 0,  dz));
        SpawnDecoration(new Vector3( dx, 0, -dz));
        SpawnDecoration(new Vector3( dx, 0,  dz));
        SpawnDecoration(new Vector3(-dx, 0, -dz));

        var lightObj = new GameObject("DirectionalLight");
        var light    = lightObj.AddComponent<Light>();
        light.type      = LightType.Directional;
        light.color     = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1.2f;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0);
        spawnedObjects.Add(lightObj);
    }

    private void CreateWall(Vector3 pos, Vector3 scale, Color color)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name                 = "Wall";
        wall.transform.position   = pos;
        wall.transform.localScale = scale;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        wall.GetComponent<Renderer>().material = mat;
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

    // ── Cleanup ──────────────────────────────────────────────────────────────

    private void Cleanup()
    {
        isActive = false;

        // Destroy all currently active (checked-out) enemies immediately
        foreach (var e in _activeEnemies)
        {
            if (e != null)
            {
                e.StopAllCoroutines();
                Destroy(e.gameObject);
            }
        }
        _activeEnemies.Clear();

        // Destroy all inactive (pooled) enemies
        _enemyPool?.Clear();

        foreach (var obj in spawnedObjects)
            if (obj != null) Destroy(obj);
        spawnedObjects.Clear();

        foreach (var p in FindObjectsByType<Projectile>(FindObjectsSortMode.None))    Destroy(p.gameObject);
        foreach (var g in FindObjectsByType<XPGem>(FindObjectsSortMode.None))         Destroy(g.gameObject);
        foreach (var b in FindObjectsByType<ArenaBomb>(FindObjectsSortMode.None))     Destroy(b.gameObject);
        foreach (var pk in FindObjectsByType<ArenaPickup>(FindObjectsSortMode.None))  Destroy(pk.gameObject);

        if (character != null) { Destroy(character.gameObject); character = null; }

        if (arenaCamera != null)
        {
            var cam = arenaCamera.GetComponent<Camera>();
            if (cam != null)
            {
                arenaCamera.transform.position = new Vector3(0, 8, -14);
                arenaCamera.transform.rotation = Quaternion.Euler(30, 0, 0);
                cam.clearFlags      = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.3f, 0.5f, 0.3f);
            }
            Destroy(arenaCamera);
            arenaCamera = null;
        }
    }
}
