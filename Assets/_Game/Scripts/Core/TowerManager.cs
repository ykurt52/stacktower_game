using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tower climbing game controller.
/// Handles input, spawns platforms in themed zones, manages game flow.
/// Tap left/right side of screen to jump in that direction.
/// First tap starts the camera scrolling.
/// </summary>
public class TowerManager : MonoBehaviour
{
    [Header("Platform Settings")]
    [SerializeField] private float platformSpacing = 1.8f;
    [SerializeField] private float platformHeight = 0.2f;
    [SerializeField] private float towerWidth = 8f;

    [Header("Spawning")]
    [SerializeField] private int spawnAhead = 8;
    [SerializeField] private int cleanupBehind = 5;

    [Header("Materials")]
    [SerializeField] private Material[] platformMaterials;

    private TowerCharacter character;
    private TowerCamera towerCamera;
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private int highestSpawnedFloor;
    private bool isActive;
    private bool waitingForFirstTap;
    private HashSet<int> safeFloors = new HashSet<int>();
    private float lastPlatformX;
    private int consecutiveHazards;
    private int extraLivesRemaining;
    private int lastBossFloor;


    // Zone definitions
    private struct Zone
    {
        public string name;
        public Color color1, color2;
        public float platformWidth;
        public float movingChance;
        public float breakableChance;
        public float obstacleChance;
        public float enemyChance;
        public float coinChance;
        public float movingSpeed;
        public float springPadChance;
        public float shieldChance;
        public float soldierChance;
        public float knightChance;
        public float batChance;
        public float wizardChance;
        public float bomberChance;
        public float shieldGuardChance;
        public float iceMageChance;
        public float icyChance;
        public float upgradeStoneChance;
        public float emeraldChance;
        public int floorsInZone;
    }

    private Zone[] zones;
    private int currentZoneIndex;
    private int floorsInCurrentZone;
    private int colorIndex;

    private void Awake()
    {
        InitZones();

        // Ensure SyntyAssets is available for 3D model loading
        if (SyntyAssets.Instance == null)
        {
            var syntyObj = new GameObject("SyntyAssets");
            syntyObj.AddComponent<SyntyAssets>();
        }
    }

    private void Start()
    {
        GameManager.Instance.OnGameStart.AddListener(OnGameStart);
        GameManager.Instance.OnGameOver.AddListener(OnGameOver);
        GameManager.Instance.OnReturnToMenu.AddListener(OnReturnToMenu);
        GameManager.Instance.OnRevive.AddListener(OnRevive);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart.RemoveListener(OnGameStart);
            GameManager.Instance.OnGameOver.RemoveListener(OnGameOver);
            GameManager.Instance.OnReturnToMenu.RemoveListener(OnReturnToMenu);
            GameManager.Instance.OnRevive.RemoveListener(OnRevive);
        }
    }

    private void InitZones()
    {
        zones = new Zone[]
        {
            new Zone {
                name = "Lobby",
                color1 = HexColor("#4CAF50"), color2 = HexColor("#66BB6A"),
                platformWidth = 2.0f,
                movingChance = 0f, breakableChance = 0f,
                obstacleChance = 0f, enemyChance = 0f, coinChance = 0.3f,
                movingSpeed = 0f, springPadChance = 0.05f, shieldChance = 0f,
                soldierChance = 0f, knightChance = 0f,
                batChance = 0f, wizardChance = 0f, bomberChance = 0f,
                shieldGuardChance = 0f, iceMageChance = 0f, icyChance = 0f,
                upgradeStoneChance = 0.05f,
                emeraldChance = 0f,
                floorsInZone = 8
            },
            new Zone {
                name = "Office",
                color1 = HexColor("#2196F3"), color2 = HexColor("#42A5F5"),
                platformWidth = 1.8f,
                movingChance = 0.1f, breakableChance = 0f,
                obstacleChance = 0.15f, enemyChance = 0f, coinChance = 0.25f,
                movingSpeed = 0.8f, springPadChance = 0.08f, shieldChance = 0.05f,
                soldierChance = 0f, knightChance = 0f,
                batChance = 0.05f, wizardChance = 0f, bomberChance = 0f,
                shieldGuardChance = 0f, iceMageChance = 0f, icyChance = 0.05f,
                upgradeStoneChance = 0.08f,
                emeraldChance = 0.02f,
                floorsInZone = 12
            },
            new Zone {
                name = "Construction",
                color1 = HexColor("#FF9800"), color2 = HexColor("#FFC107"),
                platformWidth = 2.2f,
                movingChance = 0.3f, breakableChance = 0.1f,
                obstacleChance = 0.2f, enemyChance = 0.1f, coinChance = 0.2f,
                movingSpeed = 1.1f, springPadChance = 0.1f, shieldChance = 0.08f,
                soldierChance = 0.08f, knightChance = 0.06f,
                batChance = 0.08f, wizardChance = 0.05f, bomberChance = 0.05f,
                shieldGuardChance = 0.06f, iceMageChance = 0f, icyChance = 0.1f,
                upgradeStoneChance = 0.10f,
                emeraldChance = 0.04f,
                floorsInZone = 15
            },
            new Zone {
                name = "Penthouse",
                color1 = HexColor("#9C27B0"), color2 = HexColor("#BA68C8"),
                platformWidth = 1.8f,
                movingChance = 0.35f, breakableChance = 0.15f,
                obstacleChance = 0.2f, enemyChance = 0.15f, coinChance = 0.2f,
                movingSpeed = 1.4f, springPadChance = 0.1f, shieldChance = 0.1f,
                soldierChance = 0.12f, knightChance = 0.1f,
                batChance = 0.1f, wizardChance = 0.08f, bomberChance = 0.08f,
                shieldGuardChance = 0.08f, iceMageChance = 0.06f, icyChance = 0.15f,
                upgradeStoneChance = 0.12f,
                emeraldChance = 0.06f,
                floorsInZone = 15
            },
            new Zone {
                name = "Rooftop",
                color1 = HexColor("#E63946"), color2 = HexColor("#FF6B6B"),
                platformWidth = 1.5f,
                movingChance = 0.4f, breakableChance = 0.2f,
                obstacleChance = 0.25f, enemyChance = 0.2f, coinChance = 0.15f,
                movingSpeed = 1.7f, springPadChance = 0.12f, shieldChance = 0.12f,
                soldierChance = 0.15f, knightChance = 0.12f,
                batChance = 0.12f, wizardChance = 0.1f, bomberChance = 0.1f,
                shieldGuardChance = 0.1f, iceMageChance = 0.08f, icyChance = 0.2f,
                upgradeStoneChance = 0.15f,
                emeraldChance = 0.08f,
                floorsInZone = 20
            },
            new Zone {
                name = "Sky",
                color1 = HexColor("#00BCD4"), color2 = HexColor("#26C6DA"),
                platformWidth = 1.2f,
                movingChance = 0.45f, breakableChance = 0.2f,
                obstacleChance = 0.3f, enemyChance = 0.25f, coinChance = 0.15f,
                movingSpeed = 2f, springPadChance = 0.15f, shieldChance = 0.15f,
                soldierChance = 0.2f, knightChance = 0.15f,
                batChance = 0.15f, wizardChance = 0.12f, bomberChance = 0.12f,
                shieldGuardChance = 0.12f, iceMageChance = 0.1f, icyChance = 0.25f,
                upgradeStoneChance = 0.18f,
                emeraldChance = 0.10f,
                floorsInZone = 999
            },
        };
    }

    // ── Public input methods (called from UI buttons) ──

    public void OnTapJumpUp()
    {
        if (!isActive) return;

        if (waitingForFirstTap)
        {
            waitingForFirstTap = false;
            character.Activate();
            character.TryJumpUp();
            towerCamera.StartScrolling();
            return;
        }

        if (character != null && !character.IsDead)
            character.TryJumpUp();
    }

    public void OnTapHopLeft()
    {
        if (!isActive) return;

        // Allow side hops before first jump (on starting platform)
        if (waitingForFirstTap) character?.Activate();

        if (character != null && !character.IsDead)
            character.TryHopSide(-1);
    }

    public void OnTapHopRight()
    {
        if (!isActive) return;

        if (waitingForFirstTap) character?.Activate();

        if (character != null && !character.IsDead)
            character.TryHopSide(1);
    }

    public void OnTapAttack()
    {
        if (!isActive) return;
        if (character != null && !character.IsDead)
            character.Attack();
    }

    private void Update()
    {
        if (!isActive) return;

        // Keyboard input
        if (Keyboard.current != null)
        {
            if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)
                OnTapJumpUp();
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
                OnTapHopLeft();
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
                OnTapHopRight();
            if (Keyboard.current.fKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame)
                OnTapAttack();
        }

        // Death checks
        if (character != null && !character.IsDead && towerCamera != null)
        {
            if (towerCamera.IsCharacterHitByLaser() || towerCamera.IsCharacterBelowCamera())
            {
                if (extraLivesRemaining > 0)
                {
                    // Revive: teleport character above laser
                    extraLivesRemaining--;
                    float safeY = towerCamera.LaserY + 5f;
                    character.Revive(new Vector3(0, safeY, 0));
                    if (VFXManager.Instance != null)
                        VFXManager.Instance.PlayJump(character.transform.position);
                }
                else
                {
                    // Don't Kill yet -- GameManager may offer revive
                    character.SetPendingDeath(TowerCharacter.DeathType.FellOff);
                    GameManager.Instance.TriggerGameOver();
                }
                return;
            }
        }

        SpawnPlatformsAhead();
        CleanupObjects();
    }

    // ── Game Events ──

    private void OnGameStart()
    {
        ClearAll();
        isActive = true;
        waitingForFirstTap = true;
        highestSpawnedFloor = -1;
        colorIndex = 0;
        currentZoneIndex = 0;
        floorsInCurrentZone = 0;
        safeFloors.Clear();
        lastPlatformX = 0;
        consecutiveHazards = 0;
        lastBossFloor = 0;

        // Extra lives from skill
        extraLivesRemaining = ShopManager.Instance != null ? ShopManager.Instance.GetExtraLives() : 0;

        // Starting platform -- slightly wider than normal
        SpawnPlatform(0, 0, 2.5f, TowerPlatform.PlatformType.Normal, GetZone());
        var startingPlatform = spawnedObjects[spawnedObjects.Count - 1].GetComponent<TowerPlatform>();

        // Spawn initial platforms ahead
        for (int i = 1; i <= spawnAhead; i++)
            SpawnFloor(i);
        highestSpawnedFloor = spawnAhead;

        SpawnCharacter();
        if (startingPlatform != null)
            character.SetStartingPlatform(startingPlatform);

        // Shop consumable: start with shield (one-time use)
        if (ShopManager.Instance != null && ShopManager.Instance.ConsumeShield())
            character.ActivateShield();

        // Weapon: equip if owned and selected (permanent, not consumed)
        if (ShopManager.Instance != null)
        {
            var weaponInfo = ShopManager.Instance.GetWeaponForGame();
            if (weaponInfo != null)
                character.EquipWeapon(weaponInfo);
        }

        // Shop consumable: headstart -- start from floor 5
        if (ShopManager.Instance != null && ShopManager.Instance.ConsumeHeadstart())
        {
            float headstartY = 5 * platformSpacing + platformHeight + 0.01f;
            character.transform.position = new Vector3(0, headstartY, 0);
            character.SetHighestLandedY(headstartY);
        }

        SetupCamera();
    }

    private void OnGameOver()
    {
        isActive = false;
        if (towerCamera != null)
            towerCamera.StopScrolling();

        // Execute the actual death animation (was deferred for revive chance)
        if (character != null && !character.IsDead)
            character.ExecutePendingDeath();

        // Consume magnet at game end so it's used up for this game
        if (ShopManager.Instance != null)
            ShopManager.Instance.ConsumeMagnet();
    }

    private void OnRevive()
    {
        if (character == null || towerCamera == null) return;

        isActive = true;

        // Kill all enemies on screen so the player has a clean restart
        ClearAllEnemies();

        // Spawn a guaranteed safe platform at character's last known height
        float safeY = character.CurrentY;
        if (safeY < towerCamera.LaserY + 2f)
            safeY = towerCamera.LaserY + 4f;

        // Always create a fresh wide platform -- don't rely on existing ones
        Zone zone = GetZone();
        SpawnPlatform(safeY, 0f, 3.0f, TowerPlatform.PlatformType.Normal, zone);

        // Place character on the safe platform
        character.Revive(new Vector3(0f, safeY + 1f, 0));

        // Restore some HP
        character.Heal(2);

        // Make sure platforms ahead are generated
        SpawnPlatformsAhead();

        // Resume camera
        towerCamera.StartScrolling();

        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayJump(character.transform.position);

        FloatingText.Spawn(character.transform.position + Vector3.up * 0.8f,
            "DIRILIS!", new Color(0.3f, 1f, 0.5f), 1.5f);
    }

    /// <summary>
    /// Destroys all enemies, projectiles, traps, and hazards currently in the scene.
    /// </summary>
    private void ClearAllEnemies()
    {
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            var obj = spawnedObjects[i];
            if (obj == null) { spawnedObjects.RemoveAt(i); continue; }

            // Check if it's an enemy, trap, projectile, or hazard
            if (obj.GetComponent<TowerEnemy>() != null ||
                obj.GetComponent<TowerSoldier>() != null ||
                obj.GetComponent<TowerKnight>() != null ||
                obj.GetComponent<TowerWizard>() != null ||
                obj.GetComponent<TowerBomber>() != null ||
                obj.GetComponent<TowerIceMage>() != null ||
                obj.GetComponent<TowerShieldGuard>() != null ||
                obj.GetComponent<TowerBat>() != null ||
                obj.GetComponent<TowerBoss>() != null ||
                obj.GetComponent<TowerObstacle>() != null)
            {
                spawnedObjects.RemoveAt(i);
                Destroy(obj);
            }
        }

        // Also destroy wizard traps and projectiles (not in spawnedObjects list)
        foreach (var trap in FindObjectsByType<TowerWizardTrap>())
            Destroy(trap.gameObject);
        foreach (var proj in FindObjectsByType<TowerWizardProjectile>())
            Destroy(proj.gameObject);
    }

    private void OnReturnToMenu()
    {
        isActive = false;
        ClearAll();
    }

    // ── Character & Camera ──

    private void SpawnCharacter()
    {
        if (character != null) { Destroy(character.gameObject); character = null; }

        GameObject charObj = new GameObject("TowerCharacter");
        character = charObj.AddComponent<TowerCharacter>();
        character.Init(new Vector3(0, platformHeight + 0.01f, 0));
    }

    private void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        towerCamera = cam.GetComponent<TowerCamera>();
        if (towerCamera == null)
            towerCamera = cam.gameObject.AddComponent<TowerCamera>();

        towerCamera.Init(character);

        // Calculate tower width from camera's visible area
        UpdateTowerWidth(cam);
    }

    private void UpdateTowerWidth(Camera cam)
    {
        if (cam == null) return;
        float dist = Mathf.Abs(cam.transform.position.z);
        float halfHeight = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfWidth = halfHeight * cam.aspect;
        towerWidth = halfWidth * 2f - 1f; // slight margin
    }

    // ── Level Generation ──

    private Zone GetZone()
    {
        if (currentZoneIndex >= zones.Length)
            return zones[zones.Length - 1];
        return zones[currentZoneIndex];
    }

    private void AdvanceZone()
    {
        floorsInCurrentZone++;
        Zone z = GetZone();
        if (floorsInCurrentZone >= z.floorsInZone && currentZoneIndex < zones.Length - 1)
        {
            int oldZone = currentZoneIndex;
            currentZoneIndex++;
            floorsInCurrentZone = 0;

            // Zone transition effect
            if (character != null)
            {
                Zone newZone = GetZone();
                SpawnZoneBanner(newZone.name, newZone.color1, character.CurrentY + 4f);
            }
        }
    }

    private void SpawnZoneBanner(string zoneName, Color zoneColor, float y)
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayPlace();
    }

    private void SpawnPlatformsAhead()
    {
        if (character == null) return;

        float charY = character.CurrentY;
        int targetFloor = Mathf.CeilToInt((charY + spawnAhead * platformSpacing) / platformSpacing);

        while (highestSpawnedFloor < targetFloor)
        {
            highestSpawnedFloor++;
            if (highestSpawnedFloor > 0)
                SpawnFloor(highestSpawnedFloor);
        }
    }

    private void SpawnFloor(int floor)
    {
        Zone zone = GetZone();
        AdvanceZone();

        float y = floor * platformSpacing;
        float halfArea = towerWidth / 2f;
        float width = zone.platformWidth;
        float maxOffset = halfArea - width / 2f;

        // ── First 5 floors: 100% clean, wider, no items, no special types ──
        bool isEarlyFloor = floor <= 5;
        if (isEarlyFloor)
            width = Mathf.Max(width, 3.0f);

        // ── Reachability: platform X must be within jump range of previous ──
        float maxJumpReach = 3.5f;
        float minXShift = 0.8f; // Minimum horizontal shift so platforms don't stack vertically
        float x;
        int pattern = floor % 5;
        switch (pattern)
        {
            case 0: x = 0; break;
            case 1: x = -Random.Range(1.0f, Mathf.Min(maxOffset, 2.5f)); break;
            case 2: x = Random.Range(-0.5f, 0.5f); break;
            case 3: x = Random.Range(1.0f, Mathf.Min(maxOffset, 2.5f)); break;
            default: x = Random.Range(-1.5f, 1.5f); break;
        }

        // Clamp so player can always reach from previous platform
        float reachMin = lastPlatformX - maxJumpReach;
        float reachMax = lastPlatformX + maxJumpReach;
        x = Mathf.Clamp(x, Mathf.Max(reachMin, -maxOffset), Mathf.Min(reachMax, maxOffset));

        // Ensure minimum horizontal distance from previous platform to avoid visual overlap
        if (Mathf.Abs(x - lastPlatformX) < minXShift && floor > 1)
        {
            float dir = (Random.value > 0.5f) ? 1f : -1f;
            x = lastPlatformX + dir * minXShift;
            x = Mathf.Clamp(x, -maxOffset, maxOffset);
        }

        // Safe floors (spring pad landing) are always normal and wider
        bool isSafeFloor = safeFloors.Contains(floor);
        if (isSafeFloor)
            width = Mathf.Max(width, 3.0f);

        // ── Max 2 consecutive hazards, then force a clean platform ──
        bool forceClean = isEarlyFloor || consecutiveHazards >= 2;

        // Platform type -- early floors always Normal
        TowerPlatform.PlatformType platType = TowerPlatform.PlatformType.Normal;
        float moveSpeed = 0;
        float moveLeft = 0, moveRight = 0;

        if (!isEarlyFloor)
        {
            float roll = Random.value;
            if (isSafeFloor || forceClean)
            {
                // Stay Normal
            }
            else if (roll < zone.movingChance)
            {
                platType = TowerPlatform.PlatformType.Moving;
                moveSpeed = zone.movingSpeed;
                float movingWidth = width * 0.6f;
                moveLeft = x - 2.5f;
                moveRight = x + 2.5f;
                moveLeft = Mathf.Max(moveLeft, -halfArea + movingWidth / 2f);
                moveRight = Mathf.Min(moveRight, halfArea - movingWidth / 2f);
            }
            else if (roll < zone.movingChance + zone.breakableChance)
            {
                platType = TowerPlatform.PlatformType.Breakable;
            }
            else if (roll < zone.movingChance + zone.breakableChance + zone.icyChance)
            {
                platType = TowerPlatform.PlatformType.Icy;
            }
        }

        SpawnPlatform(y, x, width, platType, zone);
        GameObject platObj = spawnedObjects[spawnedObjects.Count - 1];

        // Setup moving/breakable
        if (platType == TowerPlatform.PlatformType.Moving)
        {
            var plat = platObj.GetComponent<TowerPlatform>();
            if (plat != null) plat.Setup(platType, moveSpeed, moveLeft, moveRight);
        }
        else if (platType == TowerPlatform.PlatformType.Breakable)
        {
            var plat = platObj.GetComponent<TowerPlatform>();
            if (plat != null) plat.Setup(platType);
        }

        // ── Early floors: absolutely NO items ──
        if (isEarlyFloor)
        {
            lastPlatformX = x;
            return;
        }

        // ── Pick ONE item per platform (mutually exclusive) ──
        // Items on moving platforms become children so they move together
        bool hasHazard = false;
        Transform itemParent = (platType == TowerPlatform.PlatformType.Moving) ? platObj.transform : null;

        // Actual platform width (moving platforms are half-width)
        float actualW = (platType == TowerPlatform.PlatformType.Moving) ? width * 0.5f : width;
        // Safe inner zone: keep enemies well inside platform edges
        float safeHalfW = actualW * 0.3f;

        if (!isSafeFloor && !forceClean && Random.value < zone.springPadChance && floor > 8)
        {
            int jumpFloors = Random.Range(3, 16);
            safeFloors.Add(floor + jumpFloors);
            SpawnSpringPad(new Vector3(x, y + platformHeight / 2f, 0), jumpFloors, itemParent);
        }
        else if (!isSafeFloor && !forceClean && Random.value < zone.shieldChance && floor > 10)
        {
            float shieldX = x + Random.Range(-safeHalfW, safeHalfW);
            SpawnShield(new Vector3(shieldX, y + 0.8f, 0), itemParent);
        }
        else if (!isSafeFloor && !forceClean && Random.value < zone.obstacleChance && floor > 6 && actualW >= 1.5f)
        {
            float landingSide = lastPlatformX - x;
            float obsSide = (landingSide >= 0) ? -1f : 1f;
            if (Mathf.Abs(landingSide) < 0.3f) obsSide = (Random.value > 0.5f) ? 1f : -1f;
            float obsX = x + obsSide * Random.Range(safeHalfW * 0.5f, safeHalfW);
            SpawnObstacle(new Vector3(obsX, y + platformHeight / 2f, 0), itemParent);
            hasHazard = true;
        }
        else if (!isSafeFloor && !forceClean && Random.value < zone.enemyChance && floor > 8 && actualW >= 1.2f)
        {
            SpawnEnemy(x, y + platformHeight / 2f, actualW, itemParent);
            hasHazard = true;
        }
        else if (!isSafeFloor && !forceClean && Random.value < zone.soldierChance && floor > 12 && actualW >= 1.2f)
        {
            float soldierX = x + (Random.value > 0.5f ? 1 : -1) * Random.Range(0, safeHalfW);
            SpawnSoldier(new Vector3(soldierX, y + platformHeight / 2f, 0), itemParent);
            hasHazard = true;
        }
        else if (!isSafeFloor && !forceClean && Random.value < zone.knightChance && floor > 10 && actualW >= 1.5f)
        {
            SpawnKnight(x, y + platformHeight / 2f, actualW, itemParent);
            hasHazard = true;
        }
        else if (!isSafeFloor && !forceClean && Random.value < zone.batChance && floor > 8)
        {
            SpawnBat(new Vector3(x, y + 1.5f, 0), actualW, itemParent);
            hasHazard = true;
        }
        else if (!isSafeFloor && !forceClean && Random.value < zone.wizardChance && floor > 12 && actualW >= 1.2f)
        {
            float wizX = x + (Random.value > 0.5f ? 1 : -1) * Random.Range(0, safeHalfW);
            SpawnWizard(new Vector3(wizX, y + platformHeight / 2f, 0), itemParent);
            hasHazard = true;
        }
        else if (!isSafeFloor && !forceClean && Random.value < zone.bomberChance && floor > 10 && actualW >= 1.5f)
        {
            SpawnBomber(x, y + platformHeight / 2f, actualW, itemParent);
            hasHazard = true;
        }
        else if (!isSafeFloor && !forceClean && Random.value < zone.shieldGuardChance && floor > 10 && actualW >= 1.5f)
        {
            SpawnShieldGuard(x, y + platformHeight / 2f, actualW, itemParent);
            hasHazard = true;
        }
        else if (!isSafeFloor && !forceClean && Random.value < zone.iceMageChance && floor > 14 && actualW >= 1.2f)
        {
            float iceX = x + (Random.value > 0.5f ? 1 : -1) * Random.Range(0, safeHalfW);
            SpawnIceMage(new Vector3(iceX, y + platformHeight / 2f, 0), itemParent);
            hasHazard = true;
        }
        else if (!isSafeFloor && !forceClean && floor - lastBossFloor >= 25 && floor > 20 && actualW >= 2.0f)
        {
            lastBossFloor = floor;
            SpawnBoss(x, y + platformHeight / 2f, actualW, itemParent);
            hasHazard = true;
        }
        else if (Random.value < zone.emeraldChance)
        {
            float emeraldX = x + Random.Range(-safeHalfW, safeHalfW);
            float emeraldY = y + 1.2f;
            SpawnEmeraldStone(new Vector3(emeraldX, emeraldY, 0), itemParent);
        }
        else if (Random.value < zone.upgradeStoneChance)
        {
            float stoneX = x + Random.Range(-safeHalfW, safeHalfW);
            float stoneY = y + 1.2f;
            SpawnUpgradeStone(new Vector3(stoneX, stoneY, 0), itemParent);
        }
        else if (Random.value < zone.coinChance)
        {
            float coinX = x + Random.Range(-safeHalfW, safeHalfW);
            float coinY = y + 1.0f;
            SpawnCoin(new Vector3(coinX, coinY, 0), itemParent);
        }

        // Track consecutive hazards
        if (hasHazard)
            consecutiveHazards++;
        else
            consecutiveHazards = 0;

        lastPlatformX = x;

    }

    // ── Spawners ──

    private void SpawnPlatform(float y, float x, float width, TowerPlatform.PlatformType type, Zone zone)
    {
        GameObject platObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        platObj.name = "Platform";

        // Size per type
        float actualWidth = width;
        float actualHeight = platformHeight;
        float actualDepth = 0.6f;
        if (type == TowerPlatform.PlatformType.Moving)
        {
            actualWidth = width * 0.5f;
            actualHeight = platformHeight * 0.6f;
            actualDepth = 0.4f;
        }
        else if (type == TowerPlatform.PlatformType.Breakable)
        {
            actualHeight = platformHeight * 0.85f;
        }

        platObj.transform.position = new Vector3(x, y, 0);
        platObj.transform.localScale = new Vector3(actualWidth, actualHeight, actualDepth);

        // ALL platforms use Unlit -- no lighting color shifts
        Renderer rend = platObj.GetComponent<Renderer>();
        colorIndex++;

        // Fixed colors per type
        Color c;
        switch (type)
        {
            case TowerPlatform.PlatformType.Moving:
                c = new Color(0.85f, 0.75f, 0.25f); // Sarimtrak (yellowish)
                break;
            case TowerPlatform.PlatformType.Breakable:
                c = new Color(0.8f, 0.15f, 0.15f); // Kirmizi (red)
                break;
            case TowerPlatform.PlatformType.Icy:
                c = new Color(0.6f, 0.85f, 0.95f); // Acik buz mavisi
                break;
            default:
                c = new Color(0.1f, 0.35f, 0.15f); // Koyu yesil (dark green)
                break;
        }

        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = c;
        rend.material = mat;

        // ── Decorations ──

        // Breakable: crack lines
        if (type == TowerPlatform.PlatformType.Breakable)
        {
            Color crackColor = new Color(0.5f, 0.08f, 0.08f);
            AddDecor(platObj, "Crack", new Vector3(0, 0.5f, 0),
                new Vector3(0.6f, 0.01f, 0.015f), crackColor);
            AddDecor(platObj, "Crack2", new Vector3(0.1f, 0.5f, 0),
                new Vector3(0.015f, 0.01f, 0.4f), crackColor);
        }

        // Moving: arrows on top
        if (type == TowerPlatform.PlatformType.Moving)
        {
            Color arrowColor = new Color(1f, 0.95f, 0.5f);
            for (int side = -1; side <= 1; side += 2)
            {
                var arrow = AddDecor(platObj, "Arrow", new Vector3(side * 0.3f, 0.5f, 0),
                    new Vector3(0.1f, 0.01f, 0.05f), arrowColor);
                arrow.transform.localRotation = Quaternion.Euler(0, 0, side * -45f);
            }
        }

        // Icy: frost crystals on surface
        if (type == TowerPlatform.PlatformType.Icy)
        {
            Color frost = new Color(0.8f, 0.95f, 1f);
            AddDecor(platObj, "Frost1", new Vector3(-0.25f, 0.5f, 0),
                new Vector3(0.15f, 0.01f, 0.6f), frost);
            AddDecor(platObj, "Frost2", new Vector3(0.2f, 0.5f, 0),
                new Vector3(0.2f, 0.01f, 0.5f), frost);
            // Small ice crystals
            for (int i = -1; i <= 1; i += 2)
            {
                var crystal = AddDecor(platObj, "Crystal", new Vector3(i * 0.35f, 0.55f, 0),
                    new Vector3(0.03f, 0.06f, 0.03f), new Color(0.5f, 0.9f, 1f));
                crystal.transform.localRotation = Quaternion.Euler(0, 0, i * 15f);
            }
        }

        // Top edge highlight
        Color edgeColor = Color.Lerp(c, Color.white, 0.35f);
        AddDecor(platObj, "EdgeFront", new Vector3(0, 0.5f, -0.49f),
            new Vector3(0.98f, 0.01f, 0.02f), edgeColor);
        AddDecor(platObj, "EdgeBack", new Vector3(0, 0.5f, 0.49f),
            new Vector3(0.98f, 0.01f, 0.02f), edgeColor);

        var platform = platObj.AddComponent<TowerPlatform>();
        platform.Setup(type);
        platform.SetFloor(Mathf.RoundToInt(y / platformSpacing));

        spawnedObjects.Add(platObj);
    }

    private GameObject AddDecor(GameObject parent, string name, Vector3 localPos, Vector3 localScale, Color color)
    {
        GameObject decor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        decor.name = name;
        decor.transform.SetParent(parent.transform, false);
        decor.transform.localPosition = localPos;
        decor.transform.localScale = localScale;
        var dc = decor.GetComponent<Collider>(); if (dc != null) { dc.enabled = false; Destroy(dc); }
        ApplyColor(decor, color);
        return decor;
    }

    private void SpawnObstacle(Vector3 position, Transform parent = null)
    {
        GameObject obsObj = new GameObject("Obstacle");
        obsObj.transform.position = position;
        if (parent != null) obsObj.transform.SetParent(parent, true);
        var obstacle = obsObj.AddComponent<TowerObstacle>();
        obstacle.Init();
        spawnedObjects.Add(obsObj);
    }

    /// <summary>
    /// Returns bonus HP for enemies based on current zone. Higher zones = tougher enemies.
    /// </summary>
    private int GetZoneHPBonus()
    {
        return currentZoneIndex * 2; // +0, +2, +4, +6, +8, +10
    }

    private void SpawnEnemy(float platformX, float y, float platformWidth, Transform parent = null)
    {
        GameObject enemyObj = new GameObject("Enemy");
        enemyObj.transform.position = new Vector3(platformX, y, 0);
        if (parent != null) enemyObj.transform.SetParent(parent, true);
        var enemy = enemyObj.AddComponent<TowerEnemy>();
        enemy.Init(platformX, platformWidth);
        enemy.SetHP(2 + GetZoneHPBonus());
        spawnedObjects.Add(enemyObj);
    }

    private void SpawnCoin(Vector3 position, Transform parent = null)
    {
        GameObject coinObj = new GameObject("Coin");
        var coin = coinObj.AddComponent<TowerCoin>();
        coin.Init(position);
        if (parent != null) coinObj.transform.SetParent(parent, true);

        Rigidbody rb = coinObj.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        spawnedObjects.Add(coinObj);
    }

    private void SpawnUpgradeStone(Vector3 position, Transform parent = null)
    {
        GameObject stoneObj = new GameObject("UpgradeStone");
        var stone = stoneObj.AddComponent<TowerUpgradeStone>();
        stone.Init(position);
        if (parent != null) stoneObj.transform.SetParent(parent, true);

        Rigidbody rb = stoneObj.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        spawnedObjects.Add(stoneObj);
    }

    private void SpawnEmeraldStone(Vector3 position, Transform parent = null)
    {
        GameObject emeraldObj = new GameObject("EmeraldStone");
        var emerald = emeraldObj.AddComponent<TowerEmeraldStone>();
        emerald.Init(position);
        if (parent != null) emeraldObj.transform.SetParent(parent, true);

        Rigidbody rb = emeraldObj.AddComponent<Rigidbody>();
        rb.isKinematic = true;

        spawnedObjects.Add(emeraldObj);
    }

    private void SpawnSpringPad(Vector3 position, int floors, Transform parent = null)
    {
        GameObject padObj = new GameObject("SpringPad");
        var pad = padObj.AddComponent<TowerSpringPad>();
        pad.Init(position, floors);
        if (parent != null) padObj.transform.SetParent(parent, true);
        spawnedObjects.Add(padObj);
    }

    private void SpawnSoldier(Vector3 position, Transform parent = null)
    {
        GameObject soldierObj = new GameObject("Soldier");
        var soldier = soldierObj.AddComponent<TowerSoldier>();
        soldier.Init(position);
        soldier.SetHP(2 + GetZoneHPBonus());
        if (parent != null) soldierObj.transform.SetParent(parent, true);
        spawnedObjects.Add(soldierObj);
    }

    private void SpawnKnight(float platformX, float y, float platformWidth, Transform parent = null)
    {
        GameObject knightObj = new GameObject("Knight");
        knightObj.transform.position = new Vector3(platformX, y, 0);
        if (parent != null) knightObj.transform.SetParent(parent, true);
        var knight = knightObj.AddComponent<TowerKnight>();
        knight.Init(platformX, platformWidth, y);
        knight.SetHP(3 + GetZoneHPBonus());
        spawnedObjects.Add(knightObj);
    }

    private void SpawnShield(Vector3 position, Transform parent = null)
    {
        GameObject shieldObj = new GameObject("Shield");
        var shield = shieldObj.AddComponent<TowerShield>();
        shield.Init(position);
        if (parent != null) shieldObj.transform.SetParent(parent, true);
        spawnedObjects.Add(shieldObj);
    }

    private void SpawnBat(Vector3 position, float areaWidth, Transform parent = null)
    {
        GameObject batObj = new GameObject("Bat");
        batObj.transform.position = position;
        if (parent != null) batObj.transform.SetParent(parent, true);
        var bat = batObj.AddComponent<TowerBat>();
        bat.Init(position, areaWidth);
        spawnedObjects.Add(batObj);
    }

    private void SpawnWizard(Vector3 position, Transform parent = null)
    {
        GameObject wizObj = new GameObject("Wizard");
        var wiz = wizObj.AddComponent<TowerWizard>();
        wiz.Init(position);
        wiz.SetHP(2 + GetZoneHPBonus());
        if (parent != null) wizObj.transform.SetParent(parent, true);
        spawnedObjects.Add(wizObj);
    }

    private void SpawnBomber(float platformX, float y, float platformWidth, Transform parent = null)
    {
        GameObject bomberObj = new GameObject("Bomber");
        bomberObj.transform.position = new Vector3(platformX, y, 0);
        if (parent != null) bomberObj.transform.SetParent(parent, true);
        var bomber = bomberObj.AddComponent<TowerBomber>();
        bomber.Init(platformX, platformWidth);
        bomber.SetHP(2 + GetZoneHPBonus());
        spawnedObjects.Add(bomberObj);
    }

    private void SpawnShieldGuard(float platformX, float y, float platformWidth, Transform parent = null)
    {
        GameObject guardObj = new GameObject("ShieldGuard");
        guardObj.transform.position = new Vector3(platformX, y, 0);
        if (parent != null) guardObj.transform.SetParent(parent, true);
        var guard = guardObj.AddComponent<TowerShieldGuard>();
        guard.Init(platformX, platformWidth);
        spawnedObjects.Add(guardObj);
    }

    private void SpawnIceMage(Vector3 position, Transform parent = null)
    {
        GameObject iceObj = new GameObject("IceMage");
        var ice = iceObj.AddComponent<TowerIceMage>();
        ice.Init(position);
        ice.SetHP(2 + GetZoneHPBonus());
        if (parent != null) iceObj.transform.SetParent(parent, true);
        spawnedObjects.Add(iceObj);
    }

    private void SpawnBoss(float platformX, float y, float platformWidth, Transform parent = null)
    {
        GameObject bossObj = new GameObject("Boss");
        bossObj.transform.position = new Vector3(platformX, y, 0);
        if (parent != null) bossObj.transform.SetParent(parent, true);
        var boss = bossObj.AddComponent<TowerBoss>();
        boss.Init(platformX, platformWidth, y);
        boss.SetMaxHits(5 + currentZoneIndex * 2);
        spawnedObjects.Add(bossObj);
    }


    // ── Cleanup ──

    private void CleanupObjects()
    {
        if (character == null) return;
        float cleanupY = character.CurrentY - cleanupBehind * platformSpacing;

        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] == null)
            {
                spawnedObjects.RemoveAt(i);
                continue;
            }
            if (spawnedObjects[i].transform.position.y < cleanupY)
            {
                Destroy(spawnedObjects[i]);
                spawnedObjects.RemoveAt(i);
            }
        }
    }

    private void ClearAll()
    {
        // Immediately disable all colliders BEFORE deferred Destroy
        // This prevents old objects from triggering on newly spawned character
        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null)
            {
                DisableAllColliders(spawnedObjects[i]);
                Destroy(spawnedObjects[i]);
            }
        }
        spawnedObjects.Clear();

        foreach (var enemy in FindObjectsByType<TowerEnemy>(FindObjectsInactive.Exclude))
        {
            if (enemy != null) { DisableAllColliders(enemy.gameObject); Destroy(enemy.gameObject); }
        }
        foreach (var coin in FindObjectsByType<TowerCoin>(FindObjectsInactive.Exclude))
        {
            if (coin != null) { DisableAllColliders(coin.gameObject); Destroy(coin.gameObject); }
        }
        foreach (var pad in FindObjectsByType<TowerSpringPad>(FindObjectsInactive.Exclude))
        {
            if (pad != null) { DisableAllColliders(pad.gameObject); Destroy(pad.gameObject); }
        }
        foreach (var shield in FindObjectsByType<TowerShield>(FindObjectsInactive.Exclude))
        {
            if (shield != null) { DisableAllColliders(shield.gameObject); Destroy(shield.gameObject); }
        }
        foreach (var soldier in FindObjectsByType<TowerSoldier>(FindObjectsInactive.Exclude))
        {
            if (soldier != null) { DisableAllColliders(soldier.gameObject); Destroy(soldier.gameObject); }
        }
        foreach (var bullet in FindObjectsByType<TowerBullet>(FindObjectsInactive.Exclude))
        {
            if (bullet != null) { DisableAllColliders(bullet.gameObject); Destroy(bullet.gameObject); }
        }
        foreach (var knight in FindObjectsByType<TowerKnight>(FindObjectsInactive.Exclude))
        {
            if (knight != null) { DisableAllColliders(knight.gameObject); Destroy(knight.gameObject); }
        }
        foreach (var bat in FindObjectsByType<TowerBat>(FindObjectsInactive.Exclude))
        {
            if (bat != null) { DisableAllColliders(bat.gameObject); Destroy(bat.gameObject); }
        }
        foreach (var wiz in FindObjectsByType<TowerWizard>(FindObjectsInactive.Exclude))
        {
            if (wiz != null) { DisableAllColliders(wiz.gameObject); Destroy(wiz.gameObject); }
        }
        foreach (var trap in FindObjectsByType<TowerWizardTrap>(FindObjectsInactive.Exclude))
        {
            if (trap != null) { DisableAllColliders(trap.gameObject); Destroy(trap.gameObject); }
        }
        foreach (var bomber in FindObjectsByType<TowerBomber>(FindObjectsInactive.Exclude))
        {
            if (bomber != null) { DisableAllColliders(bomber.gameObject); Destroy(bomber.gameObject); }
        }
        foreach (var bomb in FindObjectsByType<TowerBomb>(FindObjectsInactive.Exclude))
        {
            if (bomb != null) { DisableAllColliders(bomb.gameObject); Destroy(bomb.gameObject); }
        }
        foreach (var guard in FindObjectsByType<TowerShieldGuard>(FindObjectsInactive.Exclude))
        {
            if (guard != null) { DisableAllColliders(guard.gameObject); Destroy(guard.gameObject); }
        }
        foreach (var ice in FindObjectsByType<TowerIceMage>(FindObjectsInactive.Exclude))
        {
            if (ice != null) { DisableAllColliders(ice.gameObject); Destroy(ice.gameObject); }
        }
        foreach (var proj in FindObjectsByType<TowerIceProjectile>(FindObjectsInactive.Exclude))
        {
            if (proj != null) { DisableAllColliders(proj.gameObject); Destroy(proj.gameObject); }
        }
        foreach (var boss in FindObjectsByType<TowerBoss>(FindObjectsInactive.Exclude))
        {
            if (boss != null) { DisableAllColliders(boss.gameObject); Destroy(boss.gameObject); }
        }
        foreach (var pProj in FindObjectsByType<TowerPlayerProjectile>(FindObjectsInactive.Exclude))
        {
            if (pProj != null) { DisableAllColliders(pProj.gameObject); Destroy(pProj.gameObject); }
        }

        safeFloors.Clear();

        if (character != null)
        {
            DisableAllColliders(character.gameObject);
            Destroy(character.gameObject);
            character = null;
        }
    }

    private void DisableAllColliders(GameObject obj)
    {
        foreach (var col in obj.GetComponentsInChildren<Collider>(true))
        {
            if (col != null) col.enabled = false;
        }
    }

    // ── Utility ──

    private void ApplyColor(GameObject obj, Color color)
    {
        var rend = obj.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;
    }

    private static Color HexColor(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }
}
