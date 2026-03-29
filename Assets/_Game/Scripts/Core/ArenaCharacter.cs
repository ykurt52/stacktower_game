using UnityEngine;

/// <summary>
/// Archero-style player character. Joystick movement, auto-attack when stationary.
/// HP and Shield scale via ShopManager upgrades.
/// Base HP: 100, Max HP: 5000 (198 upgrades)
/// Base Shield: 50 (when purchased), Max Shield: 15000 (198 upgrades)
/// </summary>
public class ArenaCharacter : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float baseSpeed = 3f;

    [Header("Attack")]
    [SerializeField] private float baseAttackRate = 0.6f;
    [SerializeField] private float projectileSpeed = 10f;
    [SerializeField] private int baseDamage = 10;
    private string weaponSpecial = "";

    // HP/Shield constants
    private const int BASE_HP = 100;
    private const int MAX_HP = 5000;
    private const int MAX_HP_LEVELS = 198;
    private const int BASE_SHIELD = 50;
    private const int MAX_SHIELD = 15000;
    private const int MAX_SHIELD_LEVELS = 198;

    private int maxHP;
    private int currentHP;
    private int maxShield;
    private int currentShield;
    private bool isActive;
    private bool isDead;
    private float attackTimer;
    private float damageImmunity;
    private float damageFlashTimer;

    // Punch alternation (left/right)
    private bool punchLeft;

    // Temporary buffs
    private float attackSpeedBuffTimer;
    private float attackSpeedBuffMult;
    private float magnetBuffTimer;
    private float magnetBuffRange;

    // Joystick reference (set by ArenaManager at runtime)
    private Joystick _joystick;
    private int _obstacleMask;

    // Auto-backstep after melee attack
    private Vector3 backstepDir;
    private float backstepTimer;
    private const float BACKSTEP_DURATION = 0.2f;
    private const float BACKSTEP_SPEED = 8f;

    // Dodge disabled — no dodge animation in KayKit Adventurers pack

    private GameObject modelRoot;
    private GanzSeAnimator ganzSeAnim;
    private Animator heroAnimator;

    // Overhead HP/Shield bar (Archero style)
    private Transform barRoot;
    private Transform hpBarFill;
    private Transform hpGhostFill;  // white ghost bar for damage animation
    private Transform shieldBarFill;
    private Transform shieldBarBg;
    private TextMesh hpValueText;
    private TextMesh[] hpShadowTexts;
    private float barWidth = 0.72f;
    private float _ghostHP;         // ghost bar tracks delayed HP

    // Arena bounds
    private float boundsX = 5f;
    private float boundsZ = 8f;

    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public int CurrentShield => currentShield;
    public int MaxShield => maxShield;
    public bool IsDead => isDead;
    public bool IsActive => isActive;
    public float BonusMagnetRange => magnetBuffTimer > 0 ? magnetBuffRange : 0f;

    public void SetJoystick(Joystick joystick) => _joystick = joystick;

    public void Init(Vector3 position)
    {
        transform.position = position;
        int layer = LayerMask.NameToLayer("Obstacle");
        _obstacleMask = layer >= 0 ? (1 << layer) : 0;

        CalculateStats();
        LoadWeaponStats();
        currentHP = maxHP;
        currentShield = maxShield;

        isDead = false;
        isActive = false;
        damageImmunity = 0;
        attackTimer = 0;

        var rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.mass = 2f;
        rb.linearDamping = 10f;
        rb.isKinematic = true;

        // Solid collider for enemy collision separation
        var col = gameObject.AddComponent<CapsuleCollider>();
        col.isTrigger = false;
        col.radius = 0.25f;
        col.height = 0.8f;
        col.center = new Vector3(0, 0.4f, 0);

        // Trigger collider for pickups/projectiles/gems (tight to body)
        var triggerObj = new GameObject("PlayerTrigger");
        triggerObj.transform.SetParent(transform, false);
        triggerObj.layer = gameObject.layer;
        var triggerCol = triggerObj.AddComponent<SphereCollider>();
        triggerCol.isTrigger = true;
        triggerCol.radius = 0.3f;
        triggerCol.center = new Vector3(0, 0.4f, 0);
        triggerObj.AddComponent<ArenaCharacterTriggerProxy>().owner = this;

        CreateCharacterModel();
        CreateOverheadBar();
    }

    /// <summary>
    /// Calculate max HP and max Shield from ShopManager upgrade levels.
    /// </summary>
    private void CalculateStats()
    {
        int hpLevel = 0;
        int shieldLevel = -1; // -1 = not purchased

        if (ShopManager.Instance != null)
        {
            hpLevel = ShopManager.Instance.GetSkillLevel("hp");
            shieldLevel = ShopManager.Instance.GetSkillLevel("shieldupgrade");
            // shieldLevel 0 = not purchased (default), 1 = first purchase (base), 2+ = upgrades
        }

        // HP: linear interpolation from BASE_HP to MAX_HP across 198 levels
        maxHP = BASE_HP + Mathf.RoundToInt((float)(MAX_HP - BASE_HP) / MAX_HP_LEVELS * hpLevel);

        // Ability system bonus
        if (AbilitySystem.Instance != null)
            maxHP += AbilitySystem.Instance.BonusHP * 20; // Each ability HP boost = +20

        // Shield: 0 if not purchased, otherwise linear from BASE_SHIELD to MAX_SHIELD
        if (shieldLevel <= 0)
        {
            maxShield = 0;
        }
        else
        {
            // Level 1 = base shield (50), levels 2-198 scale to max
            int upgradeSteps = shieldLevel - 1; // 0 to 197
            maxShield = BASE_SHIELD + Mathf.RoundToInt((float)(MAX_SHIELD - BASE_SHIELD) / (MAX_SHIELD_LEVELS - 1) * upgradeSteps);
        }
    }

    private void LoadWeaponStats()
    {
        // Default: unarmed
        baseDamage = 5;
        baseAttackRate = 0.8f;
        projectileSpeed = 0f;
        weaponSpecial = "";

        if (ShopManager.Instance == null)
        {
            Debug.Log("[ArenaCharacter] Weapon: UNARMED (no ShopManager)");
            UpdateWeaponAnimType();
            return;
        }
        string wpnId = ShopManager.Instance.GetEquippedWeaponId();
        if (string.IsNullOrEmpty(wpnId) || !ShopManager.Instance.OwnsWeapon(wpnId))
        {
            Debug.Log($"[ArenaCharacter] Weapon: UNARMED (wpnId={wpnId ?? "null"}, owned={(!string.IsNullOrEmpty(wpnId) && ShopManager.Instance.OwnsWeapon(wpnId))})");
            UpdateWeaponAnimType();
            return;
        }

        ShopManager.Instance.GetWeaponStats(wpnId, out int dmg, out float rate, out float spd, out string special);
        baseDamage = dmg;
        baseAttackRate = rate;
        projectileSpeed = spd;
        weaponSpecial = special ?? "";

        UpdateWeaponAnimType();
        Debug.Log($"[ArenaCharacter] Weapon: id={wpnId}, dmg={baseDamage}, rate={baseAttackRate}, speed={projectileSpeed}, special={weaponSpecial}, animType={currentWeaponAnimType}");
    }

    public void Activate()
    {
        isActive = true;
    }

    public void RecalculateMaxHP()
    {
        int oldMax = maxHP;
        int oldMaxShield = maxShield;
        CalculateStats();
        if (maxHP > oldMax)
            currentHP += (maxHP - oldMax);
        if (maxShield > oldMaxShield)
            currentShield += (maxShield - oldMaxShield);
    }

    public void Heal(int amount)
    {
        currentHP = Mathf.Min(currentHP + amount, maxHP);
    }

    public void HealPercent(float percent)
    {
        int amount = Mathf.CeilToInt(maxHP * percent);
        currentHP = Mathf.Min(currentHP + amount, maxHP);
    }

    public void ApplyAttackSpeedBuff(float duration, float mult)
    {
        attackSpeedBuffTimer = duration;
        attackSpeedBuffMult = mult;
    }

    public void ApplyMagnetBuff(float duration, float range)
    {
        magnetBuffTimer = duration;
        magnetBuffRange = range;
    }

    public void ApplyShield(int amount)
    {
        currentShield = Mathf.Min(currentShield + amount, Mathf.Max(maxShield, currentShield + amount));
    }

    public void TakeDamage(int damage)
    {
        if (isDead || damageImmunity > 0) return;

        // Shield absorbs damage first
        if (currentShield > 0)
        {
            int absorbed = Mathf.Min(currentShield, damage);
            currentShield -= absorbed;
            damage -= absorbed;
            FloatingText.Spawn(transform.position + Vector3.up * 1.5f,
                "-" + absorbed, new Color(0.4f, 0.7f, 1f), 1f);
            if (damage <= 0)
            {
                damageImmunity = 0.3f;
                return;
            }
        }

        currentHP -= damage;
        damageImmunity = 0.8f;
        damageFlashTimer = 0.3f;

        FloatingText.Spawn(transform.position + Vector3.up * 1.5f,
            "-" + damage, new Color(1f, 0.2f, 0.15f), 1.2f);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayHurt();

        var cam = FindAnyObjectByType<ArenaCamera>();
        if (cam != null) cam.Shake();

        if (currentHP <= 0)
        {
            currentHP = 0;
            Die();
        }
    }

    private void Die()
    {
        isDead = true;
        isActive = false;
        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayDeath(transform.position);
        GameManager.Instance.TriggerGameOver();
    }

    public void Revive()
    {
        isDead = false;
        isActive = true;
        currentHP = maxHP;
        currentShield = maxShield;
        damageImmunity = 2f;
    }

    public void SetBounds(float x, float z)
    {
        boundsX = x;
        boundsZ = z;
    }

    private void Update()
    {
        if (!isActive || isDead) return;

        damageImmunity -= Time.deltaTime;
        if (attackSpeedBuffTimer > 0) attackSpeedBuffTimer -= Time.deltaTime;
        if (magnetBuffTimer > 0) magnetBuffTimer -= Time.deltaTime;

        // Damage flash
        if (damageFlashTimer > 0)
        {
            damageFlashTimer -= Time.deltaTime;
            if (modelRoot != null)
            {
                // Scale pulse instead of color change -- no material damage
                float pulse = 1f + Mathf.Sin(damageFlashTimer * 30f) * 0.1f;
                modelRoot.transform.localScale = Vector3.one * pulse;
                if (damageFlashTimer <= 0)
                    modelRoot.transform.localScale = Vector3.one;
            }
        }

        // Auto-backstep after melee attack
        if (backstepTimer > 0)
        {
            backstepTimer -= Time.deltaTime;
            Vector3 stepMove = backstepDir * BACKSTEP_SPEED * Time.deltaTime;
            Vector3 newPos = transform.position + stepMove;
            newPos.x = Mathf.Clamp(newPos.x, -boundsX, boundsX);
            newPos.z = Mathf.Clamp(newPos.z, -boundsZ, boundsZ);
            newPos.y = 0;
            transform.position = newPos;
        }

        Vector2 input = GetMovementInput();
        bool isMoving = input.magnitude > 0.1f;

        if (isMoving)
        {
            float speed = baseSpeed;
            if (AbilitySystem.Instance != null)
                speed *= AbilitySystem.Instance.MoveSpeedMult;

            Vector3 move = new Vector3(input.x, 0, input.y) * speed * Time.deltaTime;
            Vector3 newPos = transform.position + move;

            newPos.x = Mathf.Clamp(newPos.x, -boundsX, boundsX);
            newPos.z = Mathf.Clamp(newPos.z, -boundsZ, boundsZ);
            newPos.y = 0;

            // Obstacle collision check — don't move into obstacles
            Vector3 origin = transform.position + Vector3.up * 0.3f;
            Vector3 dir = (newPos - transform.position);
            float dist = dir.magnitude;
            if (dist > 0.001f && Physics.SphereCast(origin, 0.2f, dir.normalized, out _, dist + 0.05f, _obstacleMask))
            {
                // Blocked — try sliding along X and Z separately
                Vector3 slideX = new Vector3(newPos.x, 0, transform.position.z);
                Vector3 slideZ = new Vector3(transform.position.x, 0, newPos.z);

                Vector3 dirX = slideX - transform.position;
                Vector3 dirZ = slideZ - transform.position;

                bool blockedX = dirX.magnitude > 0.001f && Physics.SphereCast(origin, 0.2f, dirX.normalized, out _, dirX.magnitude + 0.05f, _obstacleMask);
                bool blockedZ = dirZ.magnitude > 0.001f && Physics.SphereCast(origin, 0.2f, dirZ.normalized, out _, dirZ.magnitude + 0.05f, _obstacleMask);

                if (!blockedX) newPos = new Vector3(newPos.x, 0, transform.position.z);
                else if (!blockedZ) newPos = new Vector3(transform.position.x, 0, newPos.z);
                else newPos = transform.position; // fully blocked
            }

            transform.position = newPos;

            if (modelRoot != null)
            {
                Vector3 moveDir = new Vector3(input.x, 0, input.y);
                modelRoot.transform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);
            }

            if (heroAnimator != null)
                heroAnimator.SetFloat("Speed", 1f);
            else if (ganzSeAnim != null)
                ganzSeAnim.CurrentState = GanzSeAnimator.AnimState.WalkRight;
        }
        else
        {
            if (heroAnimator != null)
                heroAnimator.SetFloat("Speed", 0f);
            else if (ganzSeAnim != null)
                ganzSeAnim.CurrentState = GanzSeAnimator.AnimState.Idle;

            // No idle facing — rotation only updates when attacking (FireAtTarget)

            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                var target = FindNearestEnemy(); // attack range limited
                if (target != null)
                {
                    float rate = baseAttackRate;
                    if (AbilitySystem.Instance != null)
                        rate /= AbilitySystem.Instance.AttackSpeedMult;
                    if (attackSpeedBuffTimer > 0)
                        rate *= attackSpeedBuffMult;
                    attackTimer = rate;

                    FireAtTarget(target);
                }
            }
        }
    }

    private Vector2 GetMovementInput()
    {
        // FloatingJoystick (Joystick Pack asset)
        if (_joystick != null)
        {
            Vector2 dir = _joystick.Direction;
            if (dir.sqrMagnitude > 0.01f)
                return dir;
        }

        // Keyboard fallback (editor)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
            return new Vector2(h, v).normalized;

        return Vector2.zero;
    }

    /// <summary>Attack range based on weapon type</summary>
    private float GetAttackRange()
    {
        return currentWeaponAnimType switch
        {
            0 => 2.2f,   // unarmed -- fist/kick range (wider than enemy melee 0.9)
            1 => 3.2f,   // sword -- melee swing range
            2 => 12f,    // gun/ranged -- projectile range
            _ => 12f
        };
    }

    /// <summary>
    /// Find priority target: lowest HP first, then closest distance.
    /// No range limit — used for idle facing direction.
    /// </summary>
    private ArenaEnemy FindClosestEnemyNoRange()
    {
        return FindPriorityTarget(float.MaxValue);
    }

    /// <summary>
    /// Find priority target within attack range.
    /// Priority: lowest HP → closest distance. LoS checked if obstacles exist.
    /// </summary>
    private ArenaEnemy FindNearestEnemy()
    {
        return FindPriorityTarget(GetAttackRange());
    }

    private ArenaEnemy FindPriorityTarget(float maxRange)
    {
        var manager = ArenaManager.Instance;
        if (manager == null) return null;

        ArenaEnemy best = null;
        int bestHP = int.MaxValue;
        float bestDist = float.MaxValue;

        foreach (var enemy in manager.ActiveEnemies)
        {
            if (enemy == null || enemy.IsDead) continue;

            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist > maxRange) continue;

            // LoS check — skip enemies behind obstacles
            if (!LineOfSight.Check(transform.position, enemy.transform.position))
                continue;

            int hp = enemy.CurrentHP;

            // Priority: lower HP wins, tie-break by closer distance
            if (hp < bestHP || (hp == bestHP && dist < bestDist))
            {
                best = enemy;
                bestHP = hp;
                bestDist = dist;
            }
        }

        return best;
    }

    private void FireAtTarget(ArenaEnemy target)
    {
        Vector3 dir = (target.transform.position - transform.position).normalized;
        dir.y = 0;

        if (modelRoot != null)
        {
            modelRoot.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        int damage = baseDamage;

        if (AbilitySystem.Instance != null)
            damage = Mathf.CeilToInt(baseDamage * AbilitySystem.Instance.DamageMult);

        // ── MELEE (unarmed or sword) ──
        if (currentWeaponAnimType <= 1)
        {
            // Alternate punch hand (unarmed only)
            if (currentWeaponAnimType == 0 && heroAnimator != null)
            {
                string punchAnim = punchLeft ? "Punch_Left" : "Punch_Right";
                heroAnimator.CrossFade(punchAnim, 0.05f);
                punchLeft = !punchLeft;
            }
            else if (heroAnimator != null)
            {
                heroAnimator.SetTrigger("Attack");
            }

            float meleeRange = currentWeaponAnimType == 0 ? 2.2f : 3.2f;
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist <= meleeRange)
            {
                target.TakeDamage(damage);
                target.ApplyKnockback(dir, 4f);
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlayHit();
            }
            return;
        }

        // Trigger attack animation (ranged)
        if (heroAnimator != null)
            heroAnimator.SetTrigger("Attack");

        // ── RANGED (gun/bow) ──
        int pierce = 0;
        int bounce = 0;
        float slow = 0;
        float slowDur = 0;

        if (AbilitySystem.Instance != null)
        {
            var abs = AbilitySystem.Instance;
            pierce = abs.PierceCount;
            bounce = abs.BounceCount;
            slow = abs.SlowOnHit;
            slowDur = slow > 0 ? 2f : 0f;
        }

        if (weaponSpecial == "pierce") pierce += 1;
        if (weaponSpecial == "tornado") pierce += 99;

        int totalShots = 1;
        if (AbilitySystem.Instance != null)
            totalShots += AbilitySystem.Instance.ExtraProjectiles;

        float spreadAngle = totalShots > 1 ? 10f : 0f;
        float startAngle = -(totalShots - 1) * spreadAngle * 0.5f;

        for (int i = 0; i < totalShots; i++)
        {
            float angle = startAngle + i * spreadAngle;
            Vector3 shotDir = Quaternion.Euler(0, angle, 0) * dir;
            SpawnProjectile(shotDir, damage, pierce, bounce, slow, slowDur);
        }

        if (AbilitySystem.Instance != null && AbilitySystem.Instance.HasRearArrow)
            SpawnProjectile(-dir, damage, pierce, bounce, slow, slowDur);

        if (AbilitySystem.Instance != null && AbilitySystem.Instance.HasDiagonalArrow)
        {
            SpawnProjectile(Quaternion.Euler(0, 45, 0) * dir, damage, pierce, bounce, slow, slowDur);
            SpawnProjectile(Quaternion.Euler(0, -45, 0) * dir, damage, pierce, bounce, slow, slowDur);
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayShoot();
    }

    private void SpawnProjectile(Vector3 dir, int dmg, int pierce, int bounce, float slow, float slowDur)
    {
        var projObj = new GameObject("PlayerProjectile");
        var proj = projObj.AddComponent<Projectile>();
        Vector3 muzzle = transform.position + Vector3.up * 0.5f + dir * 0.3f;
        proj.Init(Projectile.Owner.Player, muzzle, dir, projectileSpeed, dmg, pierce, bounce, 4f, slow, slowDur);
        proj.SetBounds(boundsX, boundsZ);

        // Weapon special behaviors
        if (weaponSpecial == "boomerang" || weaponSpecial == "tornado")
            proj.SetBoomerang(transform);
        if (weaponSpecial == "homing")
            proj.SetHoming();
    }

    // ── Overhead Bar (Archero-style HP + Shield) ──

    private void CreateOverheadBar()
    {
        float hpH = 0.10f;
        float shieldH = 0.05f;
        float outlineW = 0.015f;
        float yPos = 1.15f;

        _ghostHP = currentHP;

        barRoot = new GameObject("BarRoot").transform;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = new Vector3(0, yPos, 0);

        // ── HP Bar ──
        // Outline (black, slightly larger)
        CreateBarQuad(barRoot, "HPOutline", barWidth + outlineW, hpH + outlineW, new Color(0, 0, 0, 0.9f), 0);

        // Background (dark gray)
        CreateBarQuad(barRoot, "HPBarBG", barWidth, hpH, new Color(0.18f, 0.18f, 0.18f, 0.95f), 1);

        // Ghost fill (white, shows damage trail)
        hpGhostFill = CreateBarQuad(barRoot, "HPGhost", barWidth - 0.01f, hpH - 0.01f, new Color(1f, 1f, 1f, 0.6f), 2);
        hpGhostFill.localPosition = new Vector3(0, 0, -0.003f);

        // HP fill (green)
        hpBarFill = CreateBarQuad(barRoot, "HPFill", barWidth - 0.01f, hpH - 0.01f, new Color(0.2f, 0.85f, 0.25f), 3);
        hpBarFill.localPosition = new Vector3(0, 0, -0.006f);

        // ── Shield Bar (below HP) ──
        float shieldY = -(hpH * 0.5f + shieldH * 0.5f + 0.005f);
        bool hasShield = maxShield > 0;

        // Shield outline
        CreateBarQuad(barRoot, "ShieldOutline", barWidth + outlineW, shieldH + outlineW * 0.7f, new Color(0, 0, 0, 0.8f), 0)
            .localPosition = new Vector3(0, shieldY, 0);

        // Shield bg
        shieldBarBg = CreateBarQuad(barRoot, "ShieldBG", barWidth, shieldH, new Color(0.12f, 0.12f, 0.2f, 0.9f), 1);
        shieldBarBg.localPosition = new Vector3(0, shieldY, 0);

        // Shield fill
        shieldBarFill = CreateBarQuad(barRoot, "ShieldFill", barWidth - 0.01f, shieldH - 0.008f, new Color(0.35f, 0.6f, 1f), 3);
        shieldBarFill.localPosition = new Vector3(0, shieldY, -0.005f);

        shieldBarBg.gameObject.SetActive(true);
        shieldBarFill.gameObject.SetActive(hasShield && currentShield > 0);

        // ── HP value text (above HP bar) with outline ──
        float textY = hpH * 0.5f + 0.035f;
        float charSize = 0.028f;
        int fontSize = 36;
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        // Shadow/outline (8 offset copies for thick outline)
        float outOff = 0.012f;
        Vector3[] offsets = {
            new Vector3(outOff, 0, 0), new Vector3(-outOff, 0, 0),
            new Vector3(0, outOff, 0), new Vector3(0, -outOff, 0),
            new Vector3(outOff, outOff, 0), new Vector3(-outOff, outOff, 0),
            new Vector3(outOff, -outOff, 0), new Vector3(-outOff, -outOff, 0)
        };
        hpShadowTexts = new TextMesh[offsets.Length];
        for (int i = 0; i < offsets.Length; i++)
        {
            var shadow = new GameObject("HPShadow");
            shadow.transform.SetParent(barRoot, false);
            shadow.transform.localPosition = new Vector3(offsets[i].x, textY + offsets[i].y, -0.009f);
            var stm = shadow.AddComponent<TextMesh>();
            stm.text = $"{currentHP}/{maxHP}";
            stm.fontSize = fontSize;
            stm.characterSize = charSize;
            stm.anchor = TextAnchor.MiddleCenter;
            stm.alignment = TextAlignment.Center;
            stm.fontStyle = FontStyle.Bold;
            stm.color = Color.black;
            if (font != null) stm.font = font;
            shadow.GetComponent<MeshRenderer>().sortingOrder = 9;
            hpShadowTexts[i] = stm;
        }

        // Main text
        var textObj = new GameObject("HPText");
        textObj.transform.SetParent(barRoot, false);
        textObj.transform.localPosition = new Vector3(0, textY, -0.01f);
        hpValueText = textObj.AddComponent<TextMesh>();
        hpValueText.text = $"{currentHP}/{maxHP}";
        hpValueText.fontSize = fontSize;
        hpValueText.characterSize = charSize;
        hpValueText.anchor = TextAnchor.MiddleCenter;
        hpValueText.alignment = TextAlignment.Center;
        hpValueText.fontStyle = FontStyle.Bold;
        hpValueText.color = Color.white;
        if (font != null) hpValueText.font = font;
        textObj.GetComponent<MeshRenderer>().sortingOrder = 10;
    }

    private static Material _barMaterial;

    private static Material GetBarMaterial()
    {
        if (_barMaterial != null) return _barMaterial;
        _barMaterial = new Material(Shader.Find("Sprites/Default"));
        return _barMaterial;
    }

    private Transform CreateBarQuad(Transform parent, string name, float w, float h, Color color, int sortOrder = 1)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localScale = new Vector3(w, h, 1f);

        var col = obj.GetComponent<Collider>();
        if (col != null) { col.enabled = false; Object.Destroy(col); }

        var rend = obj.GetComponent<MeshRenderer>();
        var mat = new Material(GetBarMaterial());
        mat.color = color;
        mat.renderQueue = 3000 + sortOrder;
        rend.material = mat;
        rend.sortingOrder = sortOrder;

        return obj.transform;
    }

    private void LateUpdate()
    {
        if (barRoot == null) return;

        Camera cam = Camera.main;
        if (cam != null)
            barRoot.rotation = cam.transform.rotation;

        float hpH = 0.10f;
        float shieldH = 0.05f;
        float fillW = barWidth - 0.015f;
        float shieldY = -(hpH * 0.5f + shieldH * 0.5f + 0.005f);

        // ── Ghost bar: lerp toward actual HP (delayed white trail) ──
        if (_ghostHP > currentHP)
            _ghostHP = Mathf.Lerp(_ghostHP, currentHP, Time.deltaTime * 3f);
        else
            _ghostHP = currentHP;

        // HP fill
        if (hpBarFill != null && maxHP > 0)
        {
            float ratio = Mathf.Clamp01((float)currentHP / maxHP);
            hpBarFill.localScale = new Vector3(fillW * ratio, hpH - 0.01f, 1f);
            hpBarFill.localPosition = new Vector3(-fillW * (1f - ratio) * 0.5f, 0, -0.006f);

            Color barColor;
            if (ratio > 0.6f) barColor = new Color(0.2f, 0.85f, 0.25f);
            else if (ratio > 0.3f) barColor = Color.Lerp(new Color(1f, 0.8f, 0.1f), new Color(0.2f, 0.85f, 0.25f), (ratio - 0.3f) / 0.3f);
            else barColor = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.8f, 0.1f), ratio / 0.3f);
            var rend = hpBarFill.GetComponent<MeshRenderer>();
            if (rend != null) rend.material.color = barColor;
        }

        // Ghost fill (white trail behind HP)
        if (hpGhostFill != null && maxHP > 0)
        {
            float ghostRatio = Mathf.Clamp01(_ghostHP / maxHP);
            hpGhostFill.localScale = new Vector3(fillW * ghostRatio, hpH - 0.01f, 1f);
            hpGhostFill.localPosition = new Vector3(-fillW * (1f - ghostRatio) * 0.5f, 0, -0.003f);
        }

        // HP value text
        if (hpValueText != null)
        {
            string hpStr = $"{currentHP}/{maxHP}";
            hpValueText.text = hpStr;
            if (hpShadowTexts != null)
                foreach (var s in hpShadowTexts)
                    if (s != null) s.text = hpStr;
        }

        // Shield fill
        if (shieldBarFill != null && maxShield > 0)
        {
            float ratio = Mathf.Clamp01((float)currentShield / maxShield);
            shieldBarFill.localScale = new Vector3(fillW * ratio, shieldH - 0.008f, 1f);
            shieldBarFill.localPosition = new Vector3(-fillW * (1f - ratio) * 0.5f, shieldY, -0.005f);
            shieldBarFill.gameObject.SetActive(currentShield > 0);
        }
        else if (shieldBarFill != null)
        {
            shieldBarFill.gameObject.SetActive(false);
        }
    }

    private void CreateCharacterModel()
    {
        if (modelRoot != null) Destroy(modelRoot);

        modelRoot = new GameObject("HeroModel");
        modelRoot.transform.SetParent(transform, false);
        modelRoot.transform.localPosition = new Vector3(0, 0.05f, 0); // slight lift so feet are visible

        // Try ModelManager first (new system)
        if (ModelManager.Instance != null && ModelManager.Instance.IsLoaded)
        {
            var equipped = ModelManager.Instance.GetEquippedPlayerModel();
            if (equipped != null)
            {
                var hero = ModelManager.SpawnModel(equipped.prefab, modelRoot.transform);
                if (hero != null)
                {
                    heroAnimator = hero.GetComponentInChildren<Animator>();
                    if (heroAnimator != null)
                        SetupHeroAnimator(heroAnimator);

                    // Attach default weapon to character's hand
                    ModelManager.AttachDefaultWeapon(hero, equipped.prefab.name);

                    // Tiny steam puffs under feet
                    CreateFootSteam();
                    return;
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ArenaCharacter] ModelManager: Instance={ModelManager.Instance != null}, IsLoaded={ModelManager.Instance?.IsLoaded}");
        }

        // Fallback: Synty assets
        var synty = SyntyAssets.Instance;
        if (synty != null && synty.BeachHeroPrefab != null)
        {
            var hero = ModelManager.SpawnModel(synty.BeachHeroPrefab, modelRoot.transform);
            if (hero != null)
            {
                heroAnimator = hero.GetComponentInChildren<Animator>();
                if (heroAnimator != null)
                    SetupHeroAnimator(heroAnimator);
                return;
            }
        }

        // Last fallback: procedural
        MakePart(modelRoot, "Body", PrimitiveType.Cube,
            new Vector3(0, 0.3f, 0), new Vector3(0.3f, 0.4f, 0.2f), new Color(0.2f, 0.6f, 0.9f));
        MakePart(modelRoot, "Head", PrimitiveType.Sphere,
            new Vector3(0, 0.6f, 0), Vector3.one * 0.25f, new Color(1f, 0.82f, 0.62f));
    }

    // Weapon types for animation: 0=unarmed, 1=sword, 2=gun
    private int currentWeaponAnimType = 0;

    private void SetupHeroAnimator(Animator anim)
    {
        string modelName = "Beach";
        if (ModelManager.Instance != null)
        {
            var equipped = ModelManager.Instance.GetEquippedPlayerModel();
            if (equipped != null)
                modelName = equipped.prefab.name;
        }

        var controller = Resources.Load<RuntimeAnimatorController>($"Models/MainCharacter/{modelName}Animator");
        if (controller == null)
            controller = Resources.Load<RuntimeAnimatorController>($"Models/{modelName}Animator");

        if (controller != null)
        {
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false; // We control position/rotation manually
            Debug.Log($"[ArenaCharacter] Loaded animator for {modelName}");

            // Set weapon type based on equipped weapon
            UpdateWeaponAnimType();
            anim.SetInteger("WeaponType", currentWeaponAnimType);
        }
        else
        {
            Debug.LogWarning($"[ArenaCharacter] No AnimatorController for '{modelName}'.");
        }
    }

    private void UpdateWeaponAnimType()
    {
        // Determine weapon animation type from weaponSpecial / weapon id
        if (string.IsNullOrEmpty(weaponSpecial) && projectileSpeed <= 0)
            currentWeaponAnimType = 0; // unarmed
        else if (weaponSpecial == "pierce") // scythe/sword-like
            currentWeaponAnimType = 1; // sword
        else
            currentWeaponAnimType = 2; // gun/ranged

        if (heroAnimator != null)
            heroAnimator.SetInteger("WeaponType", currentWeaponAnimType);

        // Notify AbilitySystem about weapon category for ability filtering
        if (AbilitySystem.Instance != null)
        {
            var cat = currentWeaponAnimType <= 1
                ? AbilitySystem.WeaponCategory.Melee
                : AbilitySystem.WeaponCategory.Ranged;
            AbilitySystem.Instance.SetWeaponCategory(cat);
        }
    }

    private void SetHeroAnimation(string stateName, float speed = 0f)
    {
        if (heroAnimator == null || heroAnimator.runtimeAnimatorController == null) return;
        heroAnimator.SetFloat("Speed", speed);
    }

    private GameObject MakePart(GameObject parent, string name, PrimitiveType type,
        Vector3 pos, Vector3 scale, Color color)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = pos;
        obj.transform.localScale = scale;
        var c = obj.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }
        var rend = obj.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;
        return obj;
    }

    private ParticleSystem footSteamPS;

    private void CreateFootSteam()
    {
        var steamObj = new GameObject("FootSteam");
        steamObj.transform.SetParent(transform, false);
        steamObj.transform.localPosition = new Vector3(0, 0.02f, 0);

        footSteamPS = steamObj.AddComponent<ParticleSystem>();

        var main = footSteamPS.main;
        main.startLifetime = 0.4f;
        main.startSpeed = 0.15f;
        main.startSize = 0.06f;
        main.startColor = new Color(0.85f, 0.85f, 0.95f, 0.35f);
        main.maxParticles = 12;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = -0.05f; // float upward slightly

        var emission = footSteamPS.emission;
        emission.rateOverTime = 0f; // controlled manually
        emission.rateOverDistance = 8f; // emit while moving

        var shape = footSteamPS.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.08f;
        shape.rotation = new Vector3(90, 0, 0); // flat on ground

        var sizeOverLifetime = footSteamPS.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(
                new Keyframe(0, 0.5f),
                new Keyframe(0.3f, 1f),
                new Keyframe(1f, 0f)));

        var colorOverLifetime = footSteamPS.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0.3f, 0), new GradientAlphaKey(0f, 1) });
        colorOverLifetime.color = grad;

        // Use default particle material
        var renderer = steamObj.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.color = new Color(0.9f, 0.9f, 1f, 0.3f);
    }
}

/// <summary>
/// Proxy on trigger child so GetComponentInParent finds ArenaCharacter.
/// </summary>
public class ArenaCharacterTriggerProxy : MonoBehaviour
{
    [HideInInspector] public ArenaCharacter owner;
}
