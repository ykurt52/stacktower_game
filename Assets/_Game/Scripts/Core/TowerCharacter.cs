using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Tower climbing character. Tap to jump -- no auto-jump.
/// Tap left side = jump left, tap right side = jump right.
/// Only jumps when grounded on a platform.
/// </summary>
public class TowerCharacter : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float jumpForce = 11.25f;
    [SerializeField] private float hopForce = 3f;
    [SerializeField] private float horizontalHopForce = 2.5f;
    [SerializeField] private float airDashForce = 3f;
    [SerializeField] private float gravity = -22f;

    [Header("Bounds")]
    [SerializeField] private float edgeMargin = 0.3f;

    [Header("Visual")]
    [SerializeField] private float squashAmount = 0.15f;
    [SerializeField] private float squashSpeed = 12f;

    [Header("Health")]
    [SerializeField] private int maxHP = 5;

    private Vector3 velocity;
    private bool isActive;
    private bool isGrounded;
    private int airDashCount;
    private int maxAirDashes = 5;
    private bool hasShield;
    private bool isLaunching;
    private int launchBonusPoints;
    private GameObject modelRoot;
    private GanzSeAnimator ganzSeAnim;
    private GameObject shieldVisual;
    private Vector3 modelBaseScale = Vector3.one;
    private float squashTimer;
    private TowerPlatform currentPlatform;
    private float lastPlatformX;
    private bool isDashing;
    private float dashTrailTimer;
    private bool hasDoubleJump;
    private float xMin = -4f;
    private float xMax = 4f;
    private int currentHP;
    private float damageFlashTimer;
    private float slowTimer;
    private float slowMultiplier = 1f;
    private GameObject slowVisual;
    private int armorPoints;
    private int currentArmor;
    private float healthRegenTimer;
    private float healthRegenInterval;
    private float armorRegenTimer;
    private float armorRegenInterval;
    private float iceSlideVelocity;
    private float jumpTrailTimer;

    // Weapon system
    private string weaponId;
    private int weaponAmmo;
    private int weaponMaxAmmo;
    private int weaponDamage;
    private float weaponRate;
    private float weaponRange; // 0 = melee
    private float weaponCooldown;
    private GameObject weaponVisual;
    private int facingDir = 1;
    public int WeaponAmmo => weaponAmmo;
    public int WeaponMaxAmmo => weaponMaxAmmo;
    public bool HasWeapon => !string.IsNullOrEmpty(weaponId);

    public bool IsDead { get; private set; }
    public float CurrentY => transform.position.y;
    public float VelocityY => velocity.y;
    public float HighestLandedY { get; private set; }

    public void SetHighestLandedY(float y) { HighestLandedY = y; }
    public bool HasJumped { get; private set; }
    public int LandedFloorCount { get; private set; }
    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public int CurrentArmor => currentArmor;
    public int MaxArmor => armorPoints;

    public void Init(Vector3 pos)
    {
        transform.position = pos;
        velocity = Vector3.zero;
        IsDead = false;
        isActive = false;
        isGrounded = true;
        HasJumped = false;
        LandedFloorCount = 0;
        HighestLandedY = pos.y;
        currentHP = maxHP;
        damageFlashTimer = 0;
        damageImmunityTimer = 0;
        knockbackTimer = 0;
        slowTimer = 0;
        slowMultiplier = 1f;

        // Armor & regen from skills
        armorPoints = ShopManager.Instance != null ? ShopManager.Instance.GetArmorPoints() : 0;
        currentArmor = armorPoints;
        healthRegenInterval = ShopManager.Instance != null ? ShopManager.Instance.GetHealthRegenInterval() : 0f;
        armorRegenInterval = ShopManager.Instance != null ? ShopManager.Instance.GetArmorRegenInterval() : 0f;
        healthRegenTimer = 0;
        armorRegenTimer = 0;

        // Kinematic rigidbody for trigger detection
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Trigger collider for obstacle/enemy/coin detection
        SphereCollider col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.25f;
        col.center = new Vector3(0, 0.3f, 0);

        CreateCharacterModel();
    }

    public void SetStartingPlatform(TowerPlatform platform)
    {
        currentPlatform = platform;
        if (platform != null)
            lastPlatformX = platform.transform.position.x;
    }

    public void Activate()
    {
        isActive = true;
    }

    public enum DeathType { HitByEnemy, FellOff, Flung, WallSplat }

    private DeathType deathType;
    private bool deathAnimPlaying;
    private float deathTimer;

    private float flingSpinSpeed;
    private int splatSide;
    private DeathType pendingDeathType;

    public void ActivateShield()
    {
        hasShield = true;

        // Visual: glowing sphere around character
        if (shieldVisual != null) Destroy(shieldVisual);
        shieldVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shieldVisual.name = "ShieldBubble";
        shieldVisual.transform.SetParent(transform, false);
        shieldVisual.transform.localPosition = new Vector3(0, 0.3f, 0);
        shieldVisual.transform.localScale = Vector3.one * 0.8f;
        var sc = shieldVisual.GetComponent<Collider>(); if (sc != null) { sc.enabled = false; Destroy(sc); }
        var rend = shieldVisual.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.3f, 0.7f, 1f, 0.3f);
        rend.material = mat;
    }

    private void ConsumeShield()
    {
        hasShield = false;
        if (shieldVisual != null)
        {
            Destroy(shieldVisual);
            shieldVisual = null;
        }
    }

    /// <summary>
    /// Spring pad launch -- massive upward boost.
    /// </summary>
    public void LaunchUp(float force, int bonusPoints)
    {
        if (IsDead) return;
        isGrounded = false;
        isLaunching = true;
        launchBonusPoints = bonusPoints;
        airDashCount = 0;
        velocity.y = force;
        velocity.x = 0;
        squashTimer = 1f;
    }

    public void Revive(Vector3 position)
    {
        transform.position = position;
        velocity = Vector3.zero;
        isGrounded = false;
        IsDead = false;
        isActive = true;
        deathAnimPlaying = false;
        hasDoubleJump = false;
        airDashCount = 0;
        currentPlatform = null;
        squashTimer = 0;
        currentHP = maxHP;
        currentArmor = armorPoints;
        healthRegenTimer = 0;
        armorRegenTimer = 0;

        ClearSlow();

        // Flash shield briefly for visual feedback
        ActivateShield();
        Invoke(nameof(ConsumeShield), 2f);
    }

    public void ApplySlow(float duration, float multiplier)
    {
        slowTimer = duration;
        slowMultiplier = multiplier;

        // Visual: blue tint overlay
        if (slowVisual == null)
        {
            slowVisual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            slowVisual.name = "SlowEffect";
            slowVisual.transform.SetParent(transform, false);
            slowVisual.transform.localPosition = new Vector3(0, 0.3f, 0);
            slowVisual.transform.localScale = Vector3.one * 0.6f;
            var sc = slowVisual.GetComponent<Collider>();
            if (sc != null) { sc.enabled = false; Destroy(sc); }
            var rend = slowVisual.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.3f, 0.6f, 1f, 0.25f);
            rend.material = mat;
        }
    }

    private void ClearSlow()
    {
        slowTimer = 0;
        slowMultiplier = 1f;
        if (slowVisual != null)
        {
            Destroy(slowVisual);
            slowVisual = null;
        }
    }

    // ── Weapon System ──

    public void EquipWeapon(ShopManager.ItemInfo info)
    {
        if (info == null) return;
        weaponId = info.id;

        // Get level-scaled stats from ShopManager
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.GetWeaponStats(info.id, out int dmg, out float rate, out float spd, out string special);
            weaponDamage = dmg;
            weaponRate = rate;
            weaponRange = spd; // Use speed as range for tower mode
            weaponAmmo = 50;
            weaponMaxAmmo = 50;
        }
        else
        {
            weaponDamage = info.weaponDamage;
            weaponRate = info.weaponRate;
            weaponRange = info.weaponSpeed;
            weaponAmmo = 50;
            weaponMaxAmmo = 50;
        }

        weaponCooldown = 0;
        CreateWeaponVisual();
    }

    public void Attack()
    {
        if (IsDead || !isActive) return;
        if (string.IsNullOrEmpty(weaponId)) return;
        if (weaponCooldown > 0) return;
        if (weaponAmmo <= 0) { ClearWeapon(); return; }

        weaponAmmo--;
        weaponCooldown = weaponRate;

        if (weaponRange <= 0)
        {
            // Melee: sword swing -- damage enemies in front
            SwordSwing();
        }
        else
        {
            // Ranged: fire projectile
            FireProjectile();
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySword();

        if (weaponAmmo <= 0)
            ClearWeapon();
    }

    private void SwordSwing()
    {
        // Brief visual: swing arc
        if (weaponVisual != null)
            weaponVisual.transform.localRotation = Quaternion.Euler(0, 0, facingDir * -60f);
        Invoke(nameof(ResetWeaponRotation), 0.15f);

        // Damage enemies in melee range
        Vector3 center = transform.position + new Vector3(facingDir * 0.4f, 0.3f, 0);
        Collider[] hits = Physics.OverlapSphere(center, 0.6f);
        foreach (var col in hits)
        {
            var enemy = col.GetComponent<TowerEnemy>();
            var soldier = col.GetComponent<TowerSoldier>();
            var knight = col.GetComponent<TowerKnight>();
            var bat = col.GetComponent<TowerBat>();
            var wizard = col.GetComponent<TowerWizard>();
            var bomber = col.GetComponent<TowerBomber>();
            var iceMage = col.GetComponent<TowerIceMage>();
            var boss = col.GetComponent<TowerBoss>();
            var shieldGuard = col.GetComponent<TowerShieldGuard>();

            if (enemy != null) enemy.TakeHit(weaponDamage);
            else if (soldier != null) soldier.TakeHit(weaponDamage);
            else if (knight != null) knight.TakeHit(weaponDamage);
            else if (bat != null && !bat.IsDead) bat.TakeHit();
            else if (wizard != null) wizard.TakeHit(weaponDamage);
            else if (bomber != null) bomber.TakeHit(weaponDamage);
            else if (iceMage != null) iceMage.TakeHit(weaponDamage);
            else if (boss != null && !boss.IsBossDead) boss.TakeHit();
            else if (shieldGuard != null)
            {
                if (!shieldGuard.IsShieldedHit(transform.position))
                    shieldGuard.Die();
            }
            else continue;

            FloatingText.Spawn(col.transform.position + Vector3.up * 0.5f,
                "-" + weaponDamage, new Color(1f, 1f, 0.3f), 1f);
        }

        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayJump(center);
    }

    private void ResetWeaponRotation()
    {
        if (weaponVisual != null)
            weaponVisual.transform.localRotation = Quaternion.identity;
    }

    private void FireProjectile()
    {
        Vector3 muzzle = transform.position + new Vector3(facingDir * 0.3f, 0.35f, 0);
        Vector3 dir = new Vector3(facingDir, 0, 0);

        var projObj = new GameObject("PlayerProjectile");
        var proj = projObj.AddComponent<TowerPlayerProjectile>();
        proj.Init(muzzle, dir, weaponRange, weaponDamage, weaponId);

        // Muzzle flash for guns
        if (weaponId != "bow" && VFXManager.Instance != null)
            VFXManager.Instance.PlayJump(muzzle);
    }

    private bool IsEnemyInMeleeRange()
    {
        Vector3 center = transform.position + new Vector3(facingDir * 0.4f, 0.3f, 0);
        Collider[] hits = Physics.OverlapSphere(center, 0.8f);
        foreach (var col in hits)
        {
            if (col.gameObject == gameObject) continue;
            if (col.GetComponent<TowerEnemy>() != null ||
                col.GetComponent<TowerSoldier>() != null ||
                col.GetComponent<TowerKnight>() != null ||
                col.GetComponent<TowerWizard>() != null ||
                col.GetComponent<TowerBomber>() != null ||
                col.GetComponent<TowerIceMage>() != null ||
                col.GetComponent<TowerShieldGuard>() != null ||
                (col.GetComponent<TowerBat>() != null && !col.GetComponent<TowerBat>().IsDead) ||
                (col.GetComponent<TowerBoss>() != null && !col.GetComponent<TowerBoss>().IsBossDead))
                return true;
        }
        return false;
    }

    private bool IsEnemyInRange(float range)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, range);
        foreach (var col in hits)
        {
            if (col.gameObject == gameObject) continue;
            if (col.GetComponent<TowerEnemy>() != null ||
                col.GetComponent<TowerSoldier>() != null ||
                col.GetComponent<TowerKnight>() != null ||
                col.GetComponent<TowerWizard>() != null ||
                col.GetComponent<TowerBomber>() != null ||
                col.GetComponent<TowerIceMage>() != null ||
                col.GetComponent<TowerShieldGuard>() != null ||
                (col.GetComponent<TowerBat>() != null && !col.GetComponent<TowerBat>().IsDead) ||
                (col.GetComponent<TowerBoss>() != null && !col.GetComponent<TowerBoss>().IsBossDead))
            {
                // Update facing direction toward enemy
                float dir = col.transform.position.x - transform.position.x;
                if (Mathf.Abs(dir) > 0.1f)
                    facingDir = dir > 0 ? 1 : -1;
                return true;
            }
        }
        return false;
    }

    private void ClearWeapon()
    {
        weaponId = null;
        weaponAmmo = 0;
        if (weaponVisual != null) { Destroy(weaponVisual); weaponVisual = null; }
        FloatingText.Spawn(transform.position + Vector3.up * 0.6f,
            "WEAPON BROKEN!", new Color(1f, 0.4f, 0.2f), 1.2f);
    }

    private void CreateWeaponVisual()
    {
        if (weaponVisual != null) Destroy(weaponVisual);
        if (modelRoot == null) return;

        weaponVisual = new GameObject("WeaponVisual");
        weaponVisual.transform.SetParent(modelRoot.transform, false);
        weaponVisual.transform.localPosition = new Vector3(0.18f, 0.25f, -0.05f);

        if (weaponId == "bow")
        {
            // Bow arc
            MakePart(weaponVisual, "BowArc", PrimitiveType.Cube,
                new Vector3(0, 0.08f, 0), new Vector3(0.02f, 0.25f, 0.02f), new Color(0.5f, 0.3f, 0.15f));
            MakePart(weaponVisual, "String", PrimitiveType.Cube,
                new Vector3(-0.03f, 0.08f, 0), new Vector3(0.005f, 0.22f, 0.005f), new Color(0.8f, 0.8f, 0.7f));
        }
        else if (weaponId == "scythe")
        {
            // Scythe handle
            MakePart(weaponVisual, "Handle", PrimitiveType.Cube,
                new Vector3(0, 0.05f, 0), new Vector3(0.025f, 0.3f, 0.025f), new Color(0.4f, 0.25f, 0.1f));
            // Blade
            MakePart(weaponVisual, "Blade", PrimitiveType.Cube,
                new Vector3(0.06f, 0.2f, 0), new Vector3(0.12f, 0.03f, 0.02f), new Color(0.6f, 0.6f, 0.65f));
        }
        else if (weaponId == "sawblade")
        {
            // Circular blade
            MakePart(weaponVisual, "Blade", PrimitiveType.Cylinder,
                new Vector3(0, 0.05f, 0), new Vector3(0.15f, 0.02f, 0.15f), new Color(0.8f, 0.5f, 0.15f));
        }
        else if (weaponId == "tornado")
        {
            // Swirl shape
            MakePart(weaponVisual, "Core", PrimitiveType.Sphere,
                new Vector3(0, 0.05f, 0), new Vector3(0.1f, 0.15f, 0.1f), new Color(0.3f, 0.8f, 0.9f));
        }
        else if (weaponId == "spear")
        {
            // Spear shaft
            MakePart(weaponVisual, "Shaft", PrimitiveType.Cube,
                new Vector3(0, 0.1f, 0), new Vector3(0.02f, 0.35f, 0.02f), new Color(0.5f, 0.3f, 0.15f));
            // Spear tip
            MakePart(weaponVisual, "Tip", PrimitiveType.Cube,
                new Vector3(0, 0.3f, 0), new Vector3(0.04f, 0.08f, 0.02f), new Color(0.7f, 0.7f, 0.75f));
        }
        else if (weaponId == "staff")
        {
            // Staff rod
            MakePart(weaponVisual, "Rod", PrimitiveType.Cube,
                new Vector3(0, 0.08f, 0), new Vector3(0.025f, 0.3f, 0.025f), new Color(0.35f, 0.2f, 0.5f));
            // Crystal orb
            MakePart(weaponVisual, "Orb", PrimitiveType.Sphere,
                new Vector3(0, 0.25f, 0), new Vector3(0.06f, 0.06f, 0.06f), new Color(0.4f, 0.3f, 1f));
        }
    }

    private float damageImmunityTimer;
    private float knockbackTimer; // Prevents landing check right after knockback

    public void TakeDamage(int damage, Collider source)
    {
        if (IsDead || !isActive) return;
        if (damageImmunityTimer > 0) return; // Invincibility frames

        if (hasShield)
        {
            ConsumeShield();
            float shieldKnockY = 3f + damage * 1f;
            float shieldKnockX = 1.5f + damage * 0.8f;
            velocity.y = Mathf.Min(shieldKnockY, 6f);
            velocity.x = (transform.position.x > source.transform.position.x) ? shieldKnockX : -shieldKnockX;
            isGrounded = false;
            currentPlatform = null;
            knockbackTimer = 0.3f;
            return;
        }

        // Reset combo on damage
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ResetCombo();

        // Armor absorbs damage first
        int remaining = damage;
        if (currentArmor > 0)
        {
            int absorbed = Mathf.Min(currentArmor, remaining);
            currentArmor -= absorbed;
            remaining -= absorbed;
            FloatingText.Spawn(transform.position + Vector3.up * 0.7f,
                "ARMOR -" + absorbed, new Color(0.4f, 0.7f, 1f), 1f);
        }
        currentHP -= remaining;
        damageFlashTimer = 0.5f;
        damageImmunityTimer = 0.8f; // Brief invincibility after taking damage


        // Bounce away from source -- knockback scales with damage
        float knockY = 3f + damage * 1.2f;   // dmg1=4.2, dmg2=5.4, dmg3=6.6
        float knockX = 1.5f + damage * 0.8f;  // dmg1=2.3, dmg2=3.1, dmg3=3.9
        velocity.y = Mathf.Min(knockY, 8f);
        velocity.x = (transform.position.x > source.transform.position.x)
            ? Mathf.Min(knockX, 5f) : -Mathf.Min(knockX, 5f);
        isGrounded = false;
        currentPlatform = null;
        knockbackTimer = 0.25f + damage * 0.05f; // Slightly longer stun for harder hits

        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayDeath(transform.position + Vector3.up * 0.3f);

        var cam = Camera.main?.GetComponent<TowerCamera>();
        if (cam != null) cam.Shake(0.1f + damage * 0.05f, 0.1f + damage * 0.05f);

        // Show damage number
        FloatingText.Spawn(transform.position + Vector3.up * 0.5f,
            "-" + damage, new Color(1f, 0.3f, 0.2f), 1.2f);

        if (AudioManager.Instance != null) AudioManager.Instance.PlayHit();

        if (currentHP <= 0)
        {
            currentHP = 0;
            // Don't Kill() yet -- GameManager may offer revive
            // Store death info for later if revive is declined
            pendingDeathType = source.GetComponent<TowerObstacle>() != null
                ? DeathType.Flung : DeathType.HitByEnemy;
            isActive = false; // Stop movement/input until resolved
            velocity = Vector3.zero;
            GameManager.Instance.TriggerGameOver();
        }
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;
        currentHP = Mathf.Min(currentHP + amount, maxHP);
    }


    /// <summary>
    /// Set pending death state -- character is paused, waiting for revive decision.
    /// </summary>
    public void SetPendingDeath(DeathType type)
    {
        pendingDeathType = type;
        isActive = false;
        velocity = Vector3.zero;
    }

    /// <summary>
    /// Execute pending death -- called when revive is declined.
    /// </summary>
    public void ExecutePendingDeath()
    {
        Kill(pendingDeathType);
    }

    public void Kill(DeathType type = DeathType.FellOff)
    {
        if (IsDead) return;
        IsDead = true;
        isActive = false;
        deathType = type;
        deathAnimPlaying = true;
        deathTimer = 0f;

        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayDeath(transform.position + Vector3.up * 0.3f);

        var cam = Camera.main?.GetComponent<TowerCamera>();
        if (cam != null) cam.Shake(0.4f, 0.4f);

        if (type == DeathType.HitByEnemy)
        {
            // Stop movement, fall flat on current platform
            velocity = Vector3.zero;
        }
        else if (type == DeathType.Flung)
        {
            // Launch character into the air with spin
            float flingDir = velocity.x >= 0 ? -1f : 1f; // fling opposite to movement
            velocity = new Vector3(flingDir * 5f, 14f, 0);
            flingSpinSpeed = flingDir * -600f; // spin in fling direction
        }
        else if (type == DeathType.WallSplat)
        {
            // Splat against camera -- zoom in and flatten
            velocity = Vector3.zero;
        }
        else
        {
            // Fell off: let gravity pull down to next platform
            velocity.x = 0;
            if (velocity.y > 0) velocity.y = 0;
        }
    }

    /// <summary>
    /// Big jump upward -- to reach the next platform above.
    /// </summary>
    public bool TryJumpUp()
    {
        if (!isActive || IsDead) return false;

        if (isGrounded)
        {
            isGrounded = false;
            currentPlatform = null;
            airDashCount = 0;
            hasDoubleJump = true;
            HasJumped = true;
            float boost = ShopManager.Instance != null ? ShopManager.Instance.GetJumpMultiplier() : 1f;
            velocity.y = jumpForce * boost;
            velocity.x = 0;
            squashTimer = 1f;
        }
        else if (hasDoubleJump)
        {
            hasDoubleJump = false;
            float boost = ShopManager.Instance != null ? ShopManager.Instance.GetJumpMultiplier() : 1f;
            velocity.y = jumpForce * 0.75f * boost;
            velocity.x = 0;
            squashTimer = 1f;
        }
        else
        {
            return false;
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayJump();
        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayJump(transform.position);

        return true;
    }

    /// <summary>
    /// Small hop left or right -- stays on the same platform level.
    /// If airborne, does an air dash instead (once per jump).
    /// Direction: -1 = left, 1 = right.
    /// </summary>
    public bool TryHopSide(int direction)
    {
        if (!isActive || IsDead) return false;

        if (isGrounded)
        {
            // Ground hop -- boost on ice
            isGrounded = false;
            velocity.y = hopForce;
            velocity.x = direction * horizontalHopForce + iceSlideVelocity;
            iceSlideVelocity = 0;
            squashTimer = 1f;
        }
        else
        {
            // Air dash (up to 5 per jump)
            if (airDashCount >= maxAirDashes) return false;
            airDashCount++;
            isDashing = true;
            dashTrailTimer = 0.3f;
            velocity.x = direction * airDashForce;
            // Small upward boost to keep airtime
            if (velocity.y < 0)
                velocity.y *= 0.3f;
        }

        if (direction != 0)
        {
            facingDir = direction;
            if (modelRoot != null)
                modelRoot.transform.localRotation = Quaternion.Euler(0, direction > 0 ? 90f : -90f, 0);
        }

        return true;
    }

    private void UpdateScreenBounds()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        // Calculate visible half-width at character's Z distance from camera
        float dist = Mathf.Abs(cam.transform.position.z - transform.position.z);
        if (cam.orthographic)
        {
            float halfWidth = cam.orthographicSize * cam.aspect;
            xMin = cam.transform.position.x - halfWidth + edgeMargin;
            xMax = cam.transform.position.x + halfWidth - edgeMargin;
        }
        else
        {
            float halfHeight = dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * cam.aspect;
            xMin = cam.transform.position.x - halfWidth + edgeMargin;
            xMax = cam.transform.position.x + halfWidth - edgeMargin;
        }
    }

    private void Update()
    {
        UpdateScreenBounds();

        if (deathAnimPlaying)
        {
            UpdateDeathAnimation();
            return;
        }

        if (!isActive || IsDead) return;

        if (isGrounded)
        {
            // Check if platform is still under us
            if (!HasPlatformBelow())
            {
                // Platform moved away -- start falling
                isGrounded = false;
                currentPlatform = null;
                velocity = Vector3.zero;
            }
            else
            {
                // Move with platform
                if (currentPlatform != null)
                {
                    float platformDeltaX = currentPlatform.transform.position.x - lastPlatformX;
                    if (Mathf.Abs(platformDeltaX) > 0.001f)
                    {
                        Vector3 platPos = transform.position;
                        platPos.x += platformDeltaX;
                        platPos.x = Mathf.Clamp(platPos.x, xMin, xMax);
                        transform.position = platPos;
                    }
                    lastPlatformX = currentPlatform.transform.position.x;
                }

                // Ice sliding
                if (currentPlatform != null && currentPlatform.IsIcy)
                {
                    // Random drift direction on first frame
                    if (Mathf.Abs(iceSlideVelocity) < 0.1f)
                        iceSlideVelocity = (Random.value > 0.5f ? 1f : -1f) * 0.5f;

                    iceSlideVelocity = Mathf.Lerp(iceSlideVelocity, iceSlideVelocity * 1.02f, Time.deltaTime);
                    iceSlideVelocity = Mathf.Clamp(iceSlideVelocity, -3f, 3f);

                    Vector3 slidePos = transform.position;
                    slidePos.x += iceSlideVelocity * Time.deltaTime;
                    slidePos.x = Mathf.Clamp(slidePos.x, xMin, xMax);
                    if (slidePos.x <= xMin || slidePos.x >= xMax)
                        iceSlideVelocity = -iceSlideVelocity * 0.5f;
                    transform.position = slidePos;
                }
                else
                {
                    iceSlideVelocity = 0;
                }

                velocity = Vector3.zero;
            }
        }

        // Weapon cooldown (runs always -- grounded or airborne)
        if (weaponCooldown > 0)
            weaponCooldown -= Time.deltaTime;

        // Auto-attack: all weapons auto-fire when enemies are nearby
        if (!string.IsNullOrEmpty(weaponId) && weaponCooldown <= 0 && weaponAmmo > 0)
        {
            if (weaponRange <= 0)
            {
                // Melee: auto-swing when enemy is close
                if (IsEnemyInMeleeRange())
                    Attack();
            }
            else
            {
                // Ranged: auto-fire when enemy is in range
                if (IsEnemyInRange(weaponRange * 1.5f))
                    Attack();
            }
        }

        if (isGrounded)
        {
            UpdateSquashStretch();
            return;
        }

        // Slow effect countdown
        if (slowTimer > 0)
        {
            slowTimer -= Time.deltaTime;
            if (slowTimer <= 0)
                ClearSlow();
        }

        // Health regen
        if (healthRegenInterval > 0 && currentHP < maxHP)
        {
            healthRegenTimer += Time.deltaTime;
            if (healthRegenTimer >= healthRegenInterval)
            {
                healthRegenTimer = 0;
                currentHP = Mathf.Min(currentHP + 1, maxHP);
        
                FloatingText.Spawn(transform.position + Vector3.up * 0.7f,
                    "+1 HP", new Color(0.3f, 1f, 0.4f), 1f);
            }
        }

        // Armor regen
        if (armorRegenInterval > 0 && currentArmor < armorPoints)
        {
            armorRegenTimer += Time.deltaTime;
            if (armorRegenTimer >= armorRegenInterval)
            {
                armorRegenTimer = 0;
                currentArmor = Mathf.Min(currentArmor + 1, armorPoints);
        
                FloatingText.Spawn(transform.position + Vector3.up * 0.7f,
                    "+1 ARMOR", new Color(0.4f, 0.7f, 1f), 1f);
            }
        }

        // Airborne: apply gravity
        velocity.y += gravity * Time.deltaTime;

        // Air drag on horizontal
        velocity.x = Mathf.Lerp(velocity.x, 0, 2f * Time.deltaTime);

        // Move (apply slow multiplier)
        transform.position += velocity * slowMultiplier * Time.deltaTime;

        // Wall splat death: if character hits screen edge while airborne with speed
        Vector3 pos = transform.position;
        if ((pos.x <= xMin || pos.x >= xMax) && !isGrounded && Mathf.Abs(velocity.x) > 1f)
        {
            pos.x = Mathf.Clamp(pos.x, xMin, xMax);
            transform.position = pos;
            splatSide = pos.x <= xMin ? -1 : 1;
            SetPendingDeath(DeathType.WallSplat);
            GameManager.Instance.TriggerGameOver();
            return;
        }
        pos.x = Mathf.Clamp(pos.x, xMin, xMax);
        if (pos.x <= xMin || pos.x >= xMax) velocity.x = 0;
        transform.position = pos;

        // Knockback timer -- prevents instant re-landing after being hit
        if (knockbackTimer > 0)
            knockbackTimer -= Time.deltaTime;

        // Platform landing check (only when falling AND not in knockback)
        if (velocity.y <= 0 && knockbackTimer <= 0)
            CheckPlatformLanding();

        // Dash trail
        if (isDashing && dashTrailTimer > 0)
        {
            dashTrailTimer -= Time.deltaTime;
            SpawnTrailParticle();
            if (dashTrailTimer <= 0) isDashing = false;
        }

        // Jump trail -- subtle dots while ascending
        if (!isGrounded && velocity.y > 2f)
        {
            jumpTrailTimer -= Time.deltaTime;
            if (jumpTrailTimer <= 0)
            {
                jumpTrailTimer = 0.06f;
                SpawnJumpTrailDot();
            }
        }
        else
        {
            jumpTrailTimer = 0;
        }

        // Damage immunity countdown
        if (damageImmunityTimer > 0)
            damageImmunityTimer -= Time.deltaTime;

        // Damage flash
        if (damageFlashTimer > 0)
        {
            damageFlashTimer -= Time.deltaTime;
            if (modelRoot != null)
            {
                if (damageFlashTimer <= 0)
                {
                    modelRoot.SetActive(true);
                }
                else
                {
                    bool flash = Mathf.Sin(damageFlashTimer * 30f) > 0;
                    modelRoot.SetActive(!flash);
                }
            }
        }

        // Update GanzSe animation state
        UpdateGanzSeAnimation();

        // Squash & stretch animation
        UpdateSquashStretch();
    }

    private void CheckPlatformLanding()
    {
        // Use OverlapBox to find any platform below/around the character
        // This is far more reliable than raycasting for thin platforms
        Vector3 checkCenter = transform.position + Vector3.up * 0.1f;
        Vector3 halfExtents = new Vector3(0.2f, 0.25f, 0.5f);

        Collider[] hits = Physics.OverlapBox(checkCenter, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

        TowerPlatform bestPlatform = null;
        float bestTop = float.MinValue;
        Collider bestCollider = null;

        foreach (var col in hits)
        {
            TowerPlatform platform = col.GetComponent<TowerPlatform>();
            if (platform == null) continue;

            float platformTop = col.transform.position.y + col.transform.localScale.y * 0.5f;

            // Platform top must be below or near our feet (we're falling onto it)
            if (platformTop <= transform.position.y + 0.15f && platformTop > bestTop)
            {
                bestTop = platformTop;
                bestPlatform = platform;
                bestCollider = col;
            }
        }

        if (bestPlatform != null)
        {
            Vector3 landPos = transform.position;
            landPos.y = bestTop;
            transform.position = landPos;

            velocity = Vector3.zero;
            isGrounded = true;
            isDashing = false;
            currentPlatform = bestPlatform;
            lastPlatformX = bestPlatform.transform.position.x;

            // Face camera on landing
            if (modelRoot != null)
                modelRoot.transform.localRotation = Quaternion.identity;

            isLaunching = false;
            launchBonusPoints = 0;

            if (landPos.y > HighestLandedY)
            {
                HighestLandedY = landPos.y;
                LandedFloorCount++;
            }

            squashTimer = 1f;

            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayLand(transform.position);

            if (!bestPlatform.Visited)
            {
                bestPlatform.MarkVisited();
                if (ScoreManager.Instance != null)
                    ScoreManager.Instance.LandOnFloor(bestPlatform.Floor);
            }
        }
    }

    private bool HasPlatformBelow()
    {
        // Box check under feet -- more reliable than thin raycast
        Vector3 checkCenter = transform.position + Vector3.down * 0.05f;
        Vector3 halfExtents = new Vector3(0.15f, 0.15f, 0.4f);
        Collider[] hits = Physics.OverlapBox(checkCenter, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
        foreach (var col in hits)
        {
            if (col.GetComponent<TowerPlatform>() != null)
                return true;
        }
        return false;
    }

    private void UpdateGanzSeAnimation()
    {
        if (ganzSeAnim == null) return;

        if (isDashing)
        {
            ganzSeAnim.CurrentState = velocity.x < 0
                ? GanzSeAnimator.AnimState.DashLeft
                : GanzSeAnimator.AnimState.DashRight;
        }
        else if (!isGrounded)
        {
            ganzSeAnim.CurrentState = velocity.y > 0
                ? GanzSeAnimator.AnimState.Jump
                : GanzSeAnimator.AnimState.Fall;
        }
        else if (Mathf.Abs(velocity.x) > 0.5f)
        {
            ganzSeAnim.CurrentState = velocity.x < 0
                ? GanzSeAnimator.AnimState.WalkLeft
                : GanzSeAnimator.AnimState.WalkRight;
        }
        else
        {
            ganzSeAnim.CurrentState = GanzSeAnimator.AnimState.Idle;
        }
    }

    private void UpdateSquashStretch()
    {
        if (modelRoot == null) return;

        if (squashTimer > 0)
        {
            squashTimer -= squashSpeed * Time.deltaTime;
            float t = Mathf.Clamp01(squashTimer);

            if (isGrounded)
            {
                // Landing: squash (wide and short)
                float s = Mathf.Sin(t * Mathf.PI) * squashAmount;
                modelRoot.transform.localScale = new Vector3(1f + s, 1f - s, 1f + s);
            }
            else
            {
                // Jumping: stretch (tall and thin)
                float s = Mathf.Sin(t * Mathf.PI) * squashAmount;
                modelRoot.transform.localScale = new Vector3(1f - s * 0.5f, 1f + s, 1f - s * 0.5f);
            }
        }
        else
        {
            modelRoot.transform.localScale = Vector3.one;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isActive || IsDead) return;

        // Invincible during spring pad launch or in early floors (first 3 landings)
        if (isLaunching || LandedFloorCount < 3) return;

        var obstacle = other.GetComponent<TowerObstacle>();
        var enemy = other.GetComponent<TowerEnemy>();
        var soldier = other.GetComponent<TowerSoldier>();
        var knight = other.GetComponent<TowerKnight>();
        var bat = other.GetComponent<TowerBat>();
        var wizard = other.GetComponent<TowerWizard>();
        var bomber = other.GetComponent<TowerBomber>();
        var iceMage = other.GetComponent<TowerIceMage>();
        var boss = other.GetComponent<TowerBoss>();
        var shieldGuard = other.GetComponent<TowerShieldGuard>();

        if (obstacle != null)
        {
            TakeDamage(obstacle.Damage, other);
        }
        else if (enemy != null)
        {
            TakeDamage(enemy.Damage, other);
        }
        else if (soldier != null)
        {
            TakeDamage(soldier.Damage, other);
        }
        else if (knight != null)
        {
            TakeDamage(knight.Damage, other);
        }
        else if (bat != null)
        {
            if (!bat.IsDead)
            {
                TakeDamage(bat.Damage, other);
                bat.TakeHit();
            }
        }
        else if (wizard != null)
        {
            TakeDamage(wizard.Damage, other);
        }
        else if (bomber != null)
        {
            TakeDamage(bomber.Damage, other);
        }
        else if (iceMage != null)
        {
            TakeDamage(iceMage.Damage, other);
        }
        else if (shieldGuard != null)
        {
            if (shieldGuard.IsShieldedHit(transform.position))
            {
                // Bounced off shield -- no damage, just push back
                velocity.y = 4f;
                velocity.x = (transform.position.x > shieldGuard.transform.position.x) ? 4f : -4f;
                isGrounded = false;
            }
            else
            {
                // Hit from behind or side -- guard dies
                shieldGuard.Die();
                velocity.y = 5f; // Bounce up
                if (ScoreManager.Instance != null)
                    ScoreManager.Instance.AddScore(5);
            }
        }
        else if (boss != null)
        {
            if (boss.IsStompHit(transform.position, velocity.y))
            {
                // Stomp on boss
                bool killed = boss.TakeHit();
                velocity.y = 8f; // Bounce high
                isGrounded = false;
                if (killed && ScoreManager.Instance != null)
                    ScoreManager.Instance.AddScore(50);
            }
            else
            {
                // Side/front contact -- take damage
                TakeDamage(boss.Damage, other);
            }
        }
    }

    private void UpdateDeathAnimation()
    {
        deathTimer += Time.deltaTime;

        if (deathType == DeathType.Flung)
        {
            // Fly through the air spinning
            velocity.y += gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;

            if (modelRoot != null)
                modelRoot.transform.Rotate(0, 0, flingSpinSpeed * Time.deltaTime, Space.Self);

            // Fade out after 2 seconds
            if (deathTimer > 2f)
                deathAnimPlaying = false;

            return;
        }

        if (deathType == DeathType.WallSplat)
        {
            WallSplatAnimation();
            return;
        }

        if (deathType == DeathType.HitByEnemy)
        {
            // Immediately fall flat on current platform
            FallFlatAnimation();
        }
        else // FellOff
        {
            // Fall down with gravity until hitting a platform or timeout
            if (!isGrounded && deathTimer < 3f)
            {
                velocity.y += gravity * Time.deltaTime;
                transform.position += velocity * Time.deltaTime;

                // Check if we landed on a lower platform
                if (velocity.y <= 0)
                {
                    Vector3 checkCenter = transform.position + Vector3.up * 0.1f;
                    Vector3 halfExtents = new Vector3(0.2f, 0.25f, 0.5f);
                    Collider[] hits = Physics.OverlapBox(checkCenter, halfExtents, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);

                    foreach (var col in hits)
                    {
                        TowerPlatform platform = col.GetComponent<TowerPlatform>();
                        if (platform == null) continue;

                        float platformTop = col.transform.position.y + col.transform.localScale.y * 0.5f;
                        if (platformTop <= transform.position.y + 0.15f)
                        {
                            Vector3 deathPos = transform.position;
                            deathPos.y = platformTop;
                            transform.position = deathPos;
                            velocity = Vector3.zero;
                            isGrounded = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                // Landed on lower platform (or timed out) -- fall flat
                FallFlatAnimation();
            }
        }
    }

    private void SpawnTrailParticle()
    {
        GameObject trail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        trail.name = "DashTrail";
        trail.transform.position = transform.position + new Vector3(0, 0.3f, 0);
        trail.transform.localScale = Vector3.one * Random.Range(0.08f, 0.15f);
        trail.transform.rotation = Random.rotation;
        var tc = trail.GetComponent<Collider>(); if (tc != null) { tc.enabled = false; Destroy(tc); }

        var rend = trail.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        // Trail color: cyan to white
        mat.color = Color.Lerp(new Color(0.2f, 0.8f, 1f), Color.white, Random.value);
        rend.material = mat;

        var particle = trail.AddComponent<VFXParticle>();
        particle.Init(new Vector3(-velocity.x * 0.15f, Random.Range(-0.5f, 0.5f), 0), mat.color);
    }

    private void SpawnJumpTrailDot()
    {
        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        dot.name = "JumpTrail";
        dot.transform.position = transform.position + new Vector3(
            Random.Range(-0.08f, 0.08f), -0.05f, Random.Range(-0.05f, 0.05f));
        dot.transform.localScale = Vector3.one * 0.04f;
        var c = dot.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }
        var rend = dot.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 1f, 1f, 0.3f);
        rend.material = mat;
        var particle = dot.AddComponent<VFXParticle>();
        particle.Init(Vector3.down * 0.3f, mat.color);
    }

    private void WallSplatAnimation()
    {
        if (modelRoot == null) { deathAnimPlaying = false; return; }

        // Move character toward camera (negative Z) to simulate zoom/splat
        Vector3 pos = transform.position;
        float targetZ = -9f; // close to camera
        pos.z = Mathf.Lerp(pos.z, targetZ, 8f * Time.deltaTime);
        transform.position = pos;

        // Flatten against "screen" -- scale Z to near zero, stretch X/Y
        float t = Mathf.Clamp01(deathTimer * 3f);
        float flatZ = Mathf.Lerp(1f, 0.05f, t);
        float stretchXY = Mathf.Lerp(1f, 2.5f, t);
        modelRoot.transform.localScale = new Vector3(stretchXY, stretchXY, flatZ);

        // Rotate to face camera
        modelRoot.transform.localRotation = Quaternion.Euler(0, 180f, 0);

        if (deathTimer > 1f)
            deathAnimPlaying = false;
    }

    private void FallFlatAnimation()
    {
        if (modelRoot == null) { deathAnimPlaying = false; return; }

        // Rotate model to lie flat (X rotation 90 = face down)
        Quaternion target = Quaternion.Euler(90f, modelRoot.transform.localEulerAngles.y, 0);
        modelRoot.transform.localRotation = Quaternion.Lerp(
            modelRoot.transform.localRotation, target, 10f * Time.deltaTime);

        // Shrink slightly
        float scale = Mathf.Lerp(modelRoot.transform.localScale.x, 0.8f, 5f * Time.deltaTime);
        modelRoot.transform.localScale = Vector3.one * scale;

        // Stop after animation settles
        if (Quaternion.Angle(modelRoot.transform.localRotation, target) < 2f)
        {
            modelRoot.transform.localRotation = target;
            deathAnimPlaying = false;
        }
    }

    private void CreateCharacterModel()
    {
        if (modelRoot != null) Destroy(modelRoot);

        modelRoot = new GameObject("HeroModel");
        modelRoot.transform.SetParent(transform, false);
        modelRoot.transform.localPosition = Vector3.zero;
        modelRoot.transform.localRotation = Quaternion.Euler(0, 0, 0);

        // Try GanzSe modular character first (best looking hero)
        var synty = SyntyAssets.Instance;
        if (synty != null && synty.GanzSeHeroPrefab != null)
        {
            var hero = SyntyAssets.Spawn(synty.GanzSeHeroPrefab, modelRoot.transform,
                new Vector3(0, -0.15f, 0), 0.55f, 180f);
            if (hero != null)
            {
                SyntyAssets.FixMaterials(hero, Color.white);
                ganzSeAnim = hero.AddComponent<GanzSeAnimator>();
            }
            return;
        }
        // Fallback: Synty bean cowboy
        if (synty != null && synty.CowboyPrefab != null)
        {
            SyntyAssets.Spawn(synty.CowboyPrefab, modelRoot.transform,
                new Vector3(0, 0, 0), 0.35f, 180f);
            return;
        }

        // Fallback: procedural hero
        Color skinColor = new Color(1f, 0.82f, 0.62f);
        Color suitColor = new Color(0.85f, 0.12f, 0.12f);
        Color capeColor = new Color(0.12f, 0.12f, 0.85f);
        Color beltColor = new Color(0.95f, 0.85f, 0.1f);
        Color hairColor = new Color(0.2f, 0.15f, 0.1f);

        // Head
        var head = MakePart(modelRoot, "Head", PrimitiveType.Sphere,
            new Vector3(0, 0.52f, 0), new Vector3(0.22f, 0.24f, 0.22f), skinColor);

        MakePart(modelRoot, "Hair", PrimitiveType.Sphere,
            new Vector3(0, 0.6f, 0.01f), new Vector3(0.24f, 0.12f, 0.24f), hairColor);

        MakePart(head, "Mask", PrimitiveType.Cube,
            new Vector3(0, 0.1f, -0.05f), new Vector3(1.1f, 0.2f, 0.3f), new Color(0.1f, 0.1f, 0.1f));

        for (int i = -1; i <= 1; i += 2)
        {
            var eye = MakePart(head, "Eye", PrimitiveType.Sphere,
                new Vector3(i * 0.25f, 0.1f, -0.4f), Vector3.one * 0.18f, Color.white);
            MakePart(eye, "Pupil", PrimitiveType.Sphere,
                new Vector3(0, 0, -0.35f), Vector3.one * 0.5f, new Color(0.1f, 0.1f, 0.15f));
        }

        MakePart(modelRoot, "Body", PrimitiveType.Cube,
            new Vector3(0, 0.28f, 0), new Vector3(0.24f, 0.28f, 0.15f), suitColor);

        MakePart(modelRoot, "Cape", PrimitiveType.Cube,
            new Vector3(0, 0.28f, 0.08f), new Vector3(0.2f, 0.32f, 0.03f), capeColor);

        MakePart(modelRoot, "Belt", PrimitiveType.Cube,
            new Vector3(0, 0.155f, 0), new Vector3(0.26f, 0.03f, 0.16f), beltColor);

        MakePart(modelRoot, "Emblem", PrimitiveType.Sphere,
            new Vector3(0, 0.32f, -0.08f), new Vector3(0.1f, 0.1f, 0.02f), beltColor);

        for (int i = -1; i <= 1; i += 2)
        {
            var arm = MakePart(modelRoot, "Arm", PrimitiveType.Cube,
                new Vector3(i * 0.16f, 0.28f, 0), new Vector3(0.08f, 0.24f, 0.1f), suitColor);
            MakePart(arm, "Glove", PrimitiveType.Sphere,
                new Vector3(0, -0.55f, 0), new Vector3(0.7f, 0.3f, 0.7f), beltColor);
        }

        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(modelRoot, "Leg", PrimitiveType.Cube,
                new Vector3(i * 0.065f, 0.08f, 0), new Vector3(0.1f, 0.16f, 0.12f), suitColor);
            MakePart(modelRoot, "Boot", PrimitiveType.Cube,
                new Vector3(i * 0.065f, -0.01f, -0.02f), new Vector3(0.1f, 0.06f, 0.16f), beltColor);
        }
    }

    private GameObject MakePart(GameObject parent, string name, PrimitiveType type,
        Vector3 pos, Vector3 scale, Color color)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = pos;
        obj.transform.localScale = scale;
        // Disable immediately so it can't interfere with physics this frame
        var partCol = obj.GetComponent<Collider>();
        if (partCol != null) { partCol.enabled = false; Destroy(partCol); }

        var rend = obj.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;

        return obj;
    }
}
