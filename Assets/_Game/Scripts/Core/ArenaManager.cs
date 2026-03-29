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
    private const float ARENA_HALF_WIDTH = 6f;
    private const float ARENA_HALF_DEPTH = 15f;

    [Header("Wave Configuration")]
    [SerializeField] private WaveConfigSO _waveConfig;
    [SerializeField] private EnemyStatsSO[] _enemyStats;

    private ArenaCharacter character;
    private ArenaCamera arenaCamera;
    private FloatingJoystick _joystickRef;

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

    /// <summary>Read-only access to active enemies for targeting systems.</summary>
    public HashSet<ArenaEnemy> ActiveEnemies => _activeEnemies;

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

        foreach (var cam in FindObjectsByType<Camera>())
        {
            if (cam.gameObject != camObj) Destroy(cam.gameObject);
        }

        var charObj = new GameObject("ArenaCharacter");
        character   = charObj.AddComponent<ArenaCharacter>();
        character.Init(new Vector3(0, 0, -ARENA_HALF_DEPTH * 0.6f)); // spawn at bottom center
        character.SetBounds(ARENA_HALF_WIDTH - 0.8f, ARENA_HALF_DEPTH - 0.8f);

        // Floating Joystick UI
        _joystickRef = CreateFloatingJoystick();
        character.SetJoystick(_joystickRef);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[Arena] Joystick: {(_joystickRef != null ? "OK" : "NULL")}, " +
                  $"EventSystem: {(UnityEngine.EventSystems.EventSystem.current != null ? "OK" : "NULL")}, " +
                  $"Character pos: {charObj.transform.position}");
#endif

        arenaCamera.Init(character.transform, ARENA_HALF_WIDTH * 2f);

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
            if (_joystickRef != null && _joystickRef.Direction.sqrMagnitude > 0.01f)
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

        // Collect all matching stats for this type, pick one at random
        int matchCount = 0;
        foreach (var s in _enemyStats)
            if (s != null && s.type == type) matchCount++;

        if (matchCount == 0)
            return _enemyStats.Length > 0 ? _enemyStats[0] : null;

        int pick = Random.Range(0, matchCount);
        int idx = 0;
        foreach (var s in _enemyStats)
        {
            if (s != null && s.type == type)
            {
                if (idx == pick) return s;
                idx++;
            }
        }

        return _enemyStats[0];
    }

    private Vector3 GetSpawnPosition()
    {
        float margin = 2.5f;
        float safeW = ARENA_HALF_WIDTH - margin;
        float safeD = ARENA_HALF_DEPTH - margin;

        int obsMask = 0;
        int obsLayer = LayerMask.NameToLayer("Obstacle");
        if (obsLayer >= 0) obsMask = 1 << obsLayer;

        for (int attempt = 0; attempt < 20; attempt++)
        {
            float x = Random.Range(-safeW, safeW);
            float z = Random.Range(2f, safeD);
            Vector3 candidate = new Vector3(x, 0, z);

            if (character != null && Vector3.Distance(candidate, character.transform.position) < ArenaConstants.SPAWN_MIN_PLAYER_DIST)
                continue;

            if (obsMask != 0 && Physics.CheckSphere(candidate + Vector3.up * 0.5f, 1.2f, obsMask))
                continue;

            return candidate;
        }

        return new Vector3(Random.Range(-safeW * 0.5f, safeW * 0.5f), 0, safeD * 0.7f);
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
        float x = Random.Range(-ARENA_HALF_WIDTH + m, ARENA_HALF_WIDTH - m);
        float z = Random.Range(-ARENA_HALF_DEPTH + m, ARENA_HALF_DEPTH - m);

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

    // ── Arena Construction (Halloween Theme) ────────────────────────────────

    private void CreateArena()
    {
        // ── Phase 1: Ground ──
        CreateGround();

        // ── Phase 2: Walls (fence border) ──
        CreateFenceBorder();

        // ── Phase 3: Obstacles (Obstacle layer, LoS blockers) ──
        CreateObstacles();

        // ── Phase 4: Decoration + Lighting ──
        CreateDecorations();
        CreateLighting();
    }

    private void CreateGround()
    {
        // Load floor tile
        var floorPrefab = Resources.Load<GameObject>("Models/Environment/floor_dirt");
        if (floorPrefab == null)
        {
            // Fallback: plain dark cube
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "ArenaGround";
            ground.transform.position = new Vector3(0, -0.1f, 0);
            ground.transform.localScale = new Vector3(ARENA_HALF_WIDTH * 2f + 2f, 0.2f, ARENA_HALF_DEPTH * 2f + 2f);
            ground.GetComponent<Renderer>().material = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { color = new Color(0.2f, 0.18f, 0.15f) };
            spawnedObjects.Add(ground);
            return;
        }

        // Tile the floor — determine tile size from prefab bounds
        var tempTile = Instantiate(floorPrefab);
        var bounds = GetCompositeBounds(tempTile);
        float tileW = Mathf.Max(bounds.size.x, 1f);
        float tileD = Mathf.Max(bounds.size.z, 1f);
        Destroy(tempTile);

        float totalW = ARENA_HALF_WIDTH * 2f + 2f;
        float totalD = ARENA_HALF_DEPTH * 2f + 2f;
        int tilesX = Mathf.CeilToInt(totalW / tileW);
        int tilesZ = Mathf.CeilToInt(totalD / tileD);

        var tileParent = new GameObject("GroundTiles");
        spawnedObjects.Add(tileParent);

        for (int x = 0; x < tilesX; x++)
        {
            for (int z = 0; z < tilesZ; z++)
            {
                var tile = Instantiate(floorPrefab, tileParent.transform);
                tile.transform.position = new Vector3(
                    -totalW / 2f + tileW * 0.5f + x * tileW,
                    0,
                    -totalD / 2f + tileD * 0.5f + z * tileD);
                // Remove colliders from floor tiles
                foreach (var col in tile.GetComponentsInChildren<Collider>())
                { col.enabled = false; Destroy(col); }
            }
        }

        // Invisible ground collider for physics
        var groundCol = GameObject.CreatePrimitive(PrimitiveType.Cube);
        groundCol.name = "GroundCollider";
        groundCol.transform.position = new Vector3(0, -0.15f, 0);
        groundCol.transform.localScale = new Vector3(totalW, 0.3f, totalD);
        groundCol.GetComponent<Renderer>().enabled = false;
        spawnedObjects.Add(groundCol);
    }

    private void CreateFenceBorder()
    {
        var fencePrefab = Resources.Load<GameObject>("Models/Environment/fence");
        var pillarPrefab = Resources.Load<GameObject>("Models/Environment/fence_pillar");
        var treeLargePrefab = Resources.Load<GameObject>("Models/Environment/tree_pine_orange_large");
        var treeDeadPrefab = Resources.Load<GameObject>("Models/Environment/tree_dead_large");

        float w = ARENA_HALF_WIDTH - 0.2f;
        float d = ARENA_HALF_DEPTH - 0.2f;
        float fenceSpacing = 2f;

        // Fence along X edges (top and bottom walls) — fence faces +Z by default
        for (float x = -w; x <= w; x += fenceSpacing)
        {
            SpawnEnvProp(fencePrefab, new Vector3(x, 0, d), 0f);
            SpawnEnvProp(fencePrefab, new Vector3(x, 0, -d), 180f);
        }

        // Fence along Z edges (left and right walls) — rotated 90°
        for (float z = -d; z <= d; z += fenceSpacing)
        {
            SpawnEnvProp(fencePrefab, new Vector3(-w, 0, z), 90f);
            SpawnEnvProp(fencePrefab, new Vector3(w, 0, z), -90f);
        }

        // Corner pillars
        SpawnEnvProp(pillarPrefab, new Vector3(-w, 0, d), 0f);
        SpawnEnvProp(pillarPrefab, new Vector3(w, 0, d), 0f);
        SpawnEnvProp(pillarPrefab, new Vector3(-w, 0, -d), 0f);
        SpawnEnvProp(pillarPrefab, new Vector3(w, 0, -d), 0f);

        // Trees at corners (outside fence, decorative)
        float treeOff = 1.5f;
        SpawnEnvProp(treeLargePrefab, new Vector3(-w - treeOff, 0, d + treeOff), Random.Range(0f, 360f));
        SpawnEnvProp(treeLargePrefab, new Vector3(w + treeOff, 0, d + treeOff), Random.Range(0f, 360f));
        SpawnEnvProp(treeDeadPrefab, new Vector3(-w - treeOff, 0, -d - treeOff), Random.Range(0f, 360f));
        SpawnEnvProp(treeDeadPrefab, new Vector3(w + treeOff, 0, -d - treeOff), Random.Range(0f, 360f));

        // Invisible wall colliders (physics boundary — keeps players/enemies inside)
        float wallH = 3f;
        float thick = 0.5f;
        CreateInvisibleWall(new Vector3(0, wallH / 2f, d + thick / 2f), new Vector3(w * 2f + 2f, wallH, thick));
        CreateInvisibleWall(new Vector3(0, wallH / 2f, -d - thick / 2f), new Vector3(w * 2f + 2f, wallH, thick));
        CreateInvisibleWall(new Vector3(-w - thick / 2f, wallH / 2f, 0), new Vector3(thick, wallH, d * 2f + 2f));
        CreateInvisibleWall(new Vector3(w + thick / 2f, wallH / 2f, 0), new Vector3(thick, wallH, d * 2f + 2f));
    }

    private void CreateObstacles()
    {
        int obstacleLayer = LayerMask.NameToLayer("Obstacle");

        // Fixed map layout — 3 obstacles only
        // 1) Pumpkin: 3 adım oyuncunun önünde (oyuncu Z=-9.6, pumpkin Z=-6)
        SpawnObstacle("pumpkin_orange", new Vector3(0, 0, -6f), 0f, obstacleLayer);

        // 2) Coffin: sol üst bölge
        SpawnObstacle("coffin", new Vector3(-2.5f, 0, 7f), 15f, obstacleLayer);

        // 3) Shrine/pillar: haritanın tam ortası
        SpawnObstacle("shrine", new Vector3(0, 0, 0), 0f, obstacleLayer);
    }

    private void SpawnObstacle(string assetName, Vector3 pos, float yRot, int obstacleLayer)
    {
        var prefab = Resources.Load<GameObject>($"Models/Environment/{assetName}");
        if (prefab == null) return;

        var obj = Instantiate(prefab);
        obj.name = "Obstacle_" + assetName;
        obj.transform.position = pos;
        obj.transform.rotation = Quaternion.Euler(0, yRot, 0);

        // Get bounds then replace colliders
        var bounds = GetCompositeBounds(obj);
        foreach (var c in obj.GetComponentsInChildren<Collider>())
            DestroyImmediate(c);

        var box = obj.AddComponent<BoxCollider>();
        box.center = obj.transform.InverseTransformPoint(bounds.center);
        box.size = new Vector3(
            Mathf.Max(bounds.size.x, 0.6f),
            Mathf.Max(bounds.size.y, 1.5f),
            Mathf.Max(bounds.size.z, 0.6f));
        box.isTrigger = false;

        var rb = obj.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        if (obstacleLayer >= 0)
            SetLayerRecursive(obj, obstacleLayer);

        spawnedObjects.Add(obj);
    }

    private void CreateDecorations()
    {
        var pumpkinSmall = Resources.Load<GameObject>("Models/Environment/pumpkin_orange");
        var skullPrefab = Resources.Load<GameObject>("Models/Environment/skull_candle");
        var postLantern = Resources.Load<GameObject>("Models/Environment/post_lantern");

        float w = ARENA_HALF_WIDTH - 0.2f;
        float d = ARENA_HALF_DEPTH - 0.2f;

        // Small pumpkins scattered as decoration (not obstacles)
        SpawnEnvProp(pumpkinSmall, new Vector3(3f, 0, -3f), 30f);
        SpawnEnvProp(pumpkinSmall, new Vector3(-3.5f, 0, 4f), 120f);
        SpawnEnvProp(pumpkinSmall, new Vector3(2f, 0, 12f), 200f);

        // Skulls
        SpawnEnvProp(skullPrefab, new Vector3(-1.5f, 0, 5f), 45f);
        SpawnEnvProp(skullPrefab, new Vector3(2f, 0, -10f), 90f);

        // Lanterns on fence posts (elevated, attached to fence line)
        float lanternY = 0.8f;  // raised up on fence
        SpawnEnvProp(postLantern, new Vector3(-w, 0, d * 0.3f), 0f);
        SpawnEnvProp(postLantern, new Vector3(w, 0, d * 0.3f), 180f);
        SpawnEnvProp(postLantern, new Vector3(-w, 0, -d * 0.3f), 0f);
        SpawnEnvProp(postLantern, new Vector3(w, 0, -d * 0.3f), 180f);

        // Point lights at lantern positions
        CreatePointLight(new Vector3(-w, lanternY, d * 0.3f), new Color(1f, 0.6f, 0.2f), 4f, 0.7f);
        CreatePointLight(new Vector3(w, lanternY, d * 0.3f), new Color(1f, 0.6f, 0.2f), 4f, 0.7f);
        CreatePointLight(new Vector3(-w, lanternY, -d * 0.3f), new Color(1f, 0.6f, 0.2f), 4f, 0.7f);
        CreatePointLight(new Vector3(w, lanternY, -d * 0.3f), new Color(1f, 0.6f, 0.2f), 4f, 0.7f);

    }

    private void CreateLighting()
    {
        // Main directional light — dim, cool moonlight
        var lightObj = new GameObject("MoonLight");
        var light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = new Color(0.6f, 0.65f, 0.8f); // cool blue moonlight
        light.intensity = 0.8f;
        lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0);
        spawnedObjects.Add(lightObj);

        // Ambient: dark warm
        RenderSettings.ambientLight = new Color(0.15f, 0.1f, 0.08f);
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
    }

    // ── Arena Helpers ────────────────────────────────────────────────────────

    private GameObject SpawnEnvProp(GameObject prefab, Vector3 pos, float yRotation, bool keepCollider = false)
    {
        if (prefab == null) return null;
        var obj = Instantiate(prefab);
        obj.transform.position = pos;
        obj.transform.rotation = Quaternion.Euler(0, yRotation, 0);
        if (!keepCollider)
        {
            foreach (var col in obj.GetComponentsInChildren<Collider>())
            { col.enabled = false; Destroy(col); }
        }
        spawnedObjects.Add(obj);
        return obj;
    }

    private void CreateInvisibleWall(Vector3 pos, Vector3 scale)
    {
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "InvisibleWall";
        wall.transform.position = pos;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().enabled = false;
        spawnedObjects.Add(wall);
    }

    private void CreatePointLight(Vector3 pos, Color color, float range, float intensity)
    {
        var obj = new GameObject("PointLight");
        obj.transform.position = pos;
        var light = obj.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;
        spawnedObjects.Add(obj);
    }

    private static Bounds GetCompositeBounds(GameObject obj)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(obj.transform.position, Vector3.one * 0.5f);
        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    private static void SetLayerRecursive(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform)
            SetLayerRecursive(child.gameObject, layer);
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

        foreach (var p in FindObjectsByType<Projectile>())    Destroy(p.gameObject);
        foreach (var g in FindObjectsByType<XPGem>())         Destroy(g.gameObject);
        foreach (var b in FindObjectsByType<ArenaBomb>())     Destroy(b.gameObject);
        foreach (var pk in FindObjectsByType<ArenaPickup>())  Destroy(pk.gameObject);

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

    private FloatingJoystick CreateFloatingJoystick()
    {
        // Ensure EventSystem exists for UI touch input
        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            var esObj = new GameObject("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            spawnedObjects.Add(esObj);
        }

        // Load prefab from Resources
        var prefab = Resources.Load<GameObject>("Prefabs/FloatingJoystick");
        if (prefab == null)
        {
            Debug.LogError("[ArenaManager] FloatingJoystick prefab not found in Resources/Prefabs/");
            return null;
        }

        // Canvas for joystick
        var canvasObj = new GameObject("JoystickCanvas");
        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080, 1920);
        scaler.matchWidthOrHeight = 0.5f;
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        spawnedObjects.Add(canvasObj);

        // Instantiate prefab
        var instance = Instantiate(prefab, canvasObj.transform);
        var joystick = instance.GetComponent<FloatingJoystick>();

        // Make it cover bottom 75% of screen for touch area
        var rt = instance.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = new Vector2(1f, 0.75f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return joystick;
    }

#if DEVELOPMENT_BUILD
    private void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            normal = { textColor = Color.yellow }
        };
        float y = 200;
        GUI.Label(new Rect(20, y, 800, 40), $"Joystick: {(_joystickRef != null ? "OK" : "NULL")}", style);
        y += 40;
        GUI.Label(new Rect(20, y, 800, 40), $"EventSystem: {(UnityEngine.EventSystems.EventSystem.current != null ? "OK" : "NULL")}", style);
        y += 40;
        GUI.Label(new Rect(20, y, 800, 40), $"Character: {(character != null ? character.transform.position.ToString() : "NULL")}", style);
        y += 40;
        if (_joystickRef != null)
            GUI.Label(new Rect(20, y, 800, 40), $"Joy Dir: {_joystickRef.Direction}", style);
        y += 40;
        GUI.Label(new Rect(20, y, 800, 40), $"Touch: {Input.touchCount}", style);
    }
#endif
}
