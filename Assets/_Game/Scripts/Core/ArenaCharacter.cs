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
    [SerializeField] private float baseSpeed = 5f;

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

    // Direct touch input (Android fallback)
    private int directTouchId = -1;
    private Vector2 directTouchOrigin;
    private bool ignoreNextTouch;

    // Auto-backstep after melee attack
    private Vector3 backstepDir;
    private float backstepTimer;
    private const float BACKSTEP_DURATION = 0.2f;
    private const float BACKSTEP_SPEED = 8f;

    // Dodge roll
    private float dodgeCooldown;
    private float dodgeTimer;
    private Vector3 dodgeDir;
    private const float DODGE_DURATION = 0.8f;
    private const float DODGE_SPEED = 5f;
    private const float DODGE_COOLDOWN = 1.5f;

    private GameObject modelRoot;
    private GanzSeAnimator ganzSeAnim;
    private Animator heroAnimator;

    // Overhead HP/Shield bar
    private Transform barRoot;
    private Transform hpBarFill;
    private Transform shieldBarFill;
    private Transform shieldBarBg;
    private float barWidth = 0.6f;

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

    public void Init(Vector3 position)
    {
        transform.position = position;

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
        directTouchOrigin = Vector2.zero;
        directTouchId = -1;
        ignoreNextTouch = true;
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
            FloatingText.Spawn(transform.position + Vector3.up * 1.2f,
                "-" + absorbed, new Color(0.3f, 0.6f, 1f), 0.8f);
            if (damage <= 0)
            {
                damageImmunity = 0.3f;
                return;
            }
        }

        currentHP -= damage;
        damageImmunity = 0.8f;
        damageFlashTimer = 0.3f;

        FloatingText.Spawn(transform.position + Vector3.up * 1f,
            "-" + damage, new Color(1f, 0.3f, 0.3f), 1f);

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
        if (dodgeCooldown > 0) dodgeCooldown -= Time.deltaTime;

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

        // Dodge roll -- invincible + fast movement
        if (dodgeTimer > 0)
        {
            dodgeTimer -= Time.deltaTime;
            damageImmunity = 0.1f;
            Vector3 dodgeMove = dodgeDir * DODGE_SPEED * Time.deltaTime;
            Vector3 newPos = transform.position + dodgeMove;
            newPos.x = Mathf.Clamp(newPos.x, -boundsX, boundsX);
            newPos.z = Mathf.Clamp(newPos.z, -boundsZ, boundsZ);
            newPos.y = 0;
            transform.position = newPos;

            // Dodge just finished -- force back to idle so next frame transitions correctly
            if (dodgeTimer <= 0 && heroAnimator != null)
            {
                heroAnimator.SetFloat("Speed", 0f);
                heroAnimator.Play("Idle", 0, 0f);
            }
            return;
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

        // Dodge roll trigger: double-tap or move while taking damage
        if (isMoving && dodgeCooldown <= 0 && damageImmunity > -0.5f && damageImmunity < 0)
        {
            // Auto-dodge when recently hit and trying to move away
            TriggerDodge(new Vector3(input.x, 0, input.y));
            return;
        }

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
            transform.position = newPos;

            if (modelRoot != null)
            {
                float angle = Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg;
                modelRoot.transform.rotation = Quaternion.Euler(0, angle, 0);
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

            // Face nearest enemy while idle (no range limit)
            var closestAny = FindClosestEnemyNoRange();
            if (closestAny != null && modelRoot != null)
            {
                Vector3 toEnemy = closestAny.transform.position - transform.position;
                toEnemy.y = 0;
                if (toEnemy.magnitude > 0.1f)
                {
                    float angle = Mathf.Atan2(toEnemy.x, toEnemy.z) * Mathf.Rad2Deg;
                    modelRoot.transform.rotation = Quaternion.Slerp(
                        modelRoot.transform.rotation,
                        Quaternion.Euler(0, angle, 0),
                        Time.deltaTime * 10f);
                }
            }

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

    private bool IsTouchInJoystickZone(Vector2 screenPos)
    {
        // Joystick zone: between bottom 5% and top 15% of screen
        float y = screenPos.y / Screen.height;
        return y > 0.05f && y < 0.80f;
    }

    private Vector2 GetMovementInput()
    {
        // Wait for previous touch to end before accepting new input
        if (ignoreNextTouch)
        {
            if (Input.touchCount == 0 && !Input.GetMouseButton(0))
                ignoreNextTouch = false;
            return Vector2.zero;
        }

        // Touch (Android/mobile)
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);

            if (touch.phase == TouchPhase.Began)
            {
                if (IsTouchInJoystickZone(touch.position))
                    directTouchOrigin = touch.position;
                else
                    directTouchOrigin = Vector2.zero; // Outside zone, ignore
            }

            if (directTouchOrigin != Vector2.zero &&
                (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary))
            {
                Vector2 delta = touch.position - directTouchOrigin;
                if (delta.magnitude > 20f)
                    return delta.normalized;
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                directTouchOrigin = Vector2.zero;

            return Vector2.zero;
        }

        // Mouse (editor fallback)
        if (Input.GetMouseButtonDown(0) && IsTouchInJoystickZone(Input.mousePosition))
            directTouchOrigin = Input.mousePosition;
        if (Input.GetMouseButton(0) && directTouchOrigin != Vector2.zero)
        {
            Vector2 delta = (Vector2)Input.mousePosition - directTouchOrigin;
            if (delta.magnitude > 20f)
                return delta.normalized;
        }
        if (Input.GetMouseButtonUp(0))
            directTouchOrigin = Vector2.zero;

        // Keyboard (editor)
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
            return new Vector2(h, v).normalized;

        return Vector2.zero;
    }

    private void TriggerDodge(Vector3 direction)
    {
        dodgeDir = direction.normalized;
        dodgeTimer = DODGE_DURATION;
        dodgeCooldown = DODGE_COOLDOWN;
        damageImmunity = DODGE_DURATION + 0.1f;

        if (heroAnimator != null)
            heroAnimator.CrossFade("Roll", 0.1f);
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

    private ArenaEnemy FindClosestEnemyNoRange()
    {
        ArenaEnemy nearest = null;
        float minDist = float.MaxValue;
        foreach (var enemy in FindObjectsByType<ArenaEnemy>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead) continue;
            float d = Vector3.Distance(transform.position, enemy.transform.position);
            if (d < minDist)
            {
                minDist = d;
                nearest = enemy;
            }
        }
        return nearest;
    }

    private ArenaEnemy FindNearestEnemy()
    {
        ArenaEnemy nearest = null;
        float minDist = float.MaxValue;
        float range = GetAttackRange();

        foreach (var enemy in FindObjectsByType<ArenaEnemy>(FindObjectsSortMode.None))
        {
            if (enemy.IsDead) continue;
            float d = Vector3.Distance(transform.position, enemy.transform.position);
            if (d < range && d < minDist)
            {
                minDist = d;
                nearest = enemy;
            }
        }
        return nearest;
    }

    private void FireAtTarget(ArenaEnemy target)
    {
        Vector3 dir = (target.transform.position - transform.position).normalized;
        dir.y = 0;

        if (modelRoot != null)
        {
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            modelRoot.transform.rotation = Quaternion.Euler(0, angle, 0);
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
        float barH = 0.04f;
        float yPos = 0.75f;

        barRoot = new GameObject("BarRoot").transform;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = new Vector3(0, yPos, 0);

        // HP background (gray -- represents missing HP)
        CreateBarQuad(barRoot, "HPBarBG", barWidth, barH, new Color(0.25f, 0.25f, 0.25f));

        // HP fill (green -- represents current HP)
        hpBarFill = CreateBarQuad(barRoot, "HPFill", barWidth, barH - 0.005f, new Color(0.2f, 0.85f, 0.2f));
        hpBarFill.localPosition = new Vector3(0, 0, -0.005f);

        // Shield bar background (gray -- represents missing/no shield)
        bool hasShield = maxShield > 0;
        Color shieldBgColor = hasShield ? new Color(0.2f, 0.2f, 0.3f) : new Color(0.2f, 0.2f, 0.2f);
        shieldBarBg = CreateBarQuad(barRoot, "ShieldBG", barWidth, barH * 0.5f, shieldBgColor);
        shieldBarBg.localPosition = new Vector3(0, barH * 0.55f, 0);

        // Shield bar fill (blue -- represents current shield)
        shieldBarFill = CreateBarQuad(barRoot, "ShieldFill", barWidth, barH * 0.5f - 0.003f, new Color(0.3f, 0.6f, 1f));
        shieldBarFill.localPosition = new Vector3(0, barH * 0.55f, -0.005f);

        shieldBarBg.gameObject.SetActive(true);
        shieldBarFill.gameObject.SetActive(hasShield && currentShield > 0);
    }

    private Transform CreateBarQuad(Transform parent, string name, float w, float h, Color color)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        obj.name = name;
        obj.transform.SetParent(parent, false);
        obj.transform.localScale = new Vector3(w, h, 1f);
        var c = obj.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }
        var rend = obj.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = color;
        rend.material = mat;
        return obj.transform;
    }

    private void LateUpdate()
    {
        if (barRoot == null) return;

        Camera cam = Camera.main;
        if (cam != null)
            barRoot.rotation = cam.transform.rotation;

        float barH = 0.04f;

        // HP: green fill shrinks left-to-right, gray bg always full width
        if (hpBarFill != null && maxHP > 0)
        {
            float ratio = Mathf.Clamp01((float)currentHP / maxHP);
            hpBarFill.localScale = new Vector3(barWidth * ratio, barH - 0.005f, 1f);
            hpBarFill.localPosition = new Vector3(-barWidth * (1f - ratio) * 0.5f, 0, -0.005f);

            // Green when healthy, yellow mid, red low
            Color barColor;
            if (ratio > 0.6f) barColor = new Color(0.2f, 0.85f, 0.2f);
            else if (ratio > 0.3f) barColor = Color.Lerp(new Color(1f, 0.8f, 0.1f), new Color(0.2f, 0.85f, 0.2f), (ratio - 0.3f) / 0.3f);
            else barColor = Color.Lerp(new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.8f, 0.1f), ratio / 0.3f);
            hpBarFill.GetComponent<Renderer>().material.color = barColor;
        }

        // Shield: blue fill shrinks, gray/dark bg always full width
        if (shieldBarFill != null && maxShield > 0)
        {
            float ratio = Mathf.Clamp01((float)currentShield / maxShield);
            shieldBarFill.localScale = new Vector3(barWidth * ratio, barH * 0.5f - 0.003f, 1f);
            shieldBarFill.localPosition = new Vector3(-barWidth * (1f - ratio) * 0.5f, barH * 0.55f, -0.005f);

            shieldBarBg.gameObject.SetActive(true);
            shieldBarBg.GetComponent<Renderer>().material.color = new Color(0.2f, 0.2f, 0.3f);
            shieldBarFill.gameObject.SetActive(currentShield > 0);
            shieldBarFill.GetComponent<Renderer>().material.color = new Color(0.3f, 0.6f, 1f);
        }
        else if (shieldBarBg != null)
        {
            // No shield purchased -- fully gray
            shieldBarBg.gameObject.SetActive(true);
            shieldBarBg.GetComponent<Renderer>().material.color = new Color(0.2f, 0.2f, 0.2f);
            if (shieldBarFill != null) shieldBarFill.gameObject.SetActive(false);
        }
    }

    private void CreateCharacterModel()
    {
        if (modelRoot != null) Destroy(modelRoot);

        modelRoot = new GameObject("HeroModel");
        modelRoot.transform.SetParent(transform, false);
        modelRoot.transform.localPosition = Vector3.zero;

        // Try ModelManager first (new system)
        if (ModelManager.Instance != null && ModelManager.Instance.IsLoaded)
        {
            var equipped = ModelManager.Instance.GetEquippedPlayerModel();
            Debug.Log($"[ArenaCharacter] ModelManager equipped: {equipped?.displayName ?? "null"}, prefab: {equipped?.prefab?.name ?? "null"}");
            if (equipped != null)
            {
                var hero = ModelManager.SpawnModel(equipped.prefab, modelRoot.transform);
                Debug.Log($"[ArenaCharacter] SpawnModel result: {(hero != null ? hero.name : "null")}");
                if (hero != null)
                {
                    heroAnimator = hero.GetComponentInChildren<Animator>();
                    Debug.Log($"[ArenaCharacter] Animator: {(heroAnimator != null ? "FOUND" : "NULL")}, controller: {heroAnimator?.runtimeAnimatorController?.name ?? "none"}");
                    if (heroAnimator != null)
                        SetupHeroAnimator(heroAnimator);
                    else
                        Debug.LogWarning("[ArenaCharacter] No Animator on spawned model! Check FBX Rig settings (must be Generic).");
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
}

/// <summary>
/// Proxy on trigger child so GetComponentInParent finds ArenaCharacter.
/// </summary>
public class ArenaCharacterTriggerProxy : MonoBehaviour
{
    [HideInInspector] public ArenaCharacter owner;
}
