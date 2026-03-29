using UnityEngine;

/// <summary>
/// Base arena enemy. Chases player on XZ plane, attacks when in range.
/// Uses non-kinematic Rigidbody for proper collision separation.
/// Pool-compatible: physics components are created once and reused;
/// visual/HUD children are destroyed and recreated on each Init().
/// </summary>
public class ArenaEnemy : MonoBehaviour
{
    public enum EnemyType { Melee, Ranged, Heavy, Bomber, Wizard, IceMage, Boss }

    private EnemyType type;
    private int maxHP;
    private int currentHP;
    private int maxArmor;
    private int currentArmor;
    private float moveSpeed;
    private int contactDamage;
    private int projectileDamage;
    private float attackRange;
    private float attackCooldown;
    private float attackTimer;
    private float contactCooldown;
    private int xpDrop;
    private int coinDrop;
    private float slowTimer;
    private float slowAmount;
    private Vector3 knockbackVel;
    private float knockbackTimer;

    private ArenaCharacter player;
    private GameObject modelRoot;
    private Rigidbody rb;
    private CapsuleCollider _col;       // created once per pool object, updated on reuse
    private SphereCollider _triggerCol; // created once per pool object, updated on reuse
    private Animator enemyAnimator;
    private bool dead;
    private float spawnGrace;
    private float bodyHeight;

    // HP bar visuals (world-space quads above head)
    private Transform barRoot;
    private Transform hpBarFill;
    private Transform armorBarFill;
    private Transform hpBarBg;
    private float barWidth;

    public bool IsDead => dead;
    public EnemyType Type => type;
    public int CurrentHP => currentHP;
    public int MaxHP => maxHP;
    public int CurrentArmor => currentArmor;
    public int MaxArmor => maxArmor;

    /// <summary>
    /// Initialises (or re-initialises) the enemy from a ScriptableObject config.
    /// Safe to call multiple times on the same object (object-pool reuse).
    /// waveMult = 1 + wave * WaveConfigSO.statScalePerWave.
    /// </summary>
    public void Init(EnemyStatsSO stats, Vector3 pos, float waveMult)
    {
        // ── Reset transient state ─────────────────────────────────────────────
        dead            = false;
        spawnGrace      = ArenaConstants.SPAWN_GRACE;
        attackTimer     = 0f;
        contactCooldown = 0f;
        slowTimer       = 0f;
        slowAmount      = 0f;
        knockbackTimer  = 0f;
        knockbackVel    = Vector3.zero;
        player          = null;
        enemyAnimator   = null;

        type               = stats.type;
        transform.position = pos;

        maxHP            = Mathf.CeilToInt(stats.baseHP    * waveMult);
        maxArmor         = Mathf.CeilToInt(stats.baseArmor * waveMult);
        moveSpeed        = stats.moveSpeed;
        contactDamage    = stats.contactDamage;
        projectileDamage = stats.projectileDamage;
        attackRange      = stats.attackRange;
        attackCooldown   = stats.attackCooldown;
        xpDrop           = stats.xpDrop;
        coinDrop         = stats.coinDrop;
        bodyHeight       = stats.bodyHeight;
        barWidth         = stats.barWidth;

        currentHP    = maxHP;
        currentArmor = maxArmor;

        // ── Physics -- create once, update properties on reuse ─────────────────
        if (rb == null)
        {
            rb             = gameObject.AddComponent<Rigidbody>();
            rb.useGravity  = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        }
        rb.mass           = stats.mass;
        rb.linearDamping  = ArenaConstants.ENEMY_LINEAR_DAMPING;
        rb.linearVelocity = Vector3.zero;

        if (_col == null)
        {
            _col           = gameObject.AddComponent<CapsuleCollider>();
            _col.isTrigger = false;
        }
        _col.radius = stats.colliderRadius;
        _col.height = stats.colliderHeight;
        _col.center = new Vector3(0, _col.height / 2f, 0);

        // Damage trigger child -- create once, update radius on reuse
        if (_triggerCol == null)
        {
            var triggerObj = new GameObject("DamageTrigger");
            triggerObj.transform.SetParent(transform, false);
            triggerObj.layer  = gameObject.layer;
            _triggerCol           = triggerObj.AddComponent<SphereCollider>();
            _triggerCol.isTrigger = true;
            var trigger = triggerObj.AddComponent<EnemyDamageTrigger>();
            trigger.Init(this);
        }
        _triggerCol.radius = stats.triggerRadius;
        _triggerCol.center = new Vector3(0, ArenaConstants.TRIGGER_CENTER_Y, 0);

        // ── Visuals & HUD -- destroy old, create fresh ─────────────────────────
        if (modelRoot != null)
        {
            Destroy(modelRoot);
            modelRoot = null;
        }
        if (barRoot != null)
        {
            Destroy(barRoot.gameObject);
            barRoot      = null;
            hpBarFill    = null;
            armorBarFill = null;
            hpBarBg      = null;
        }

        CreateVisual(stats);
        CreateHealthBar();
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        knockbackVel   = direction.normalized * force;
        knockbackTimer = ArenaConstants.KNOCKBACK_DURATION;
    }

    public void TakeDamage(int dmg, float slow = 0f, float slowDur = 0f)
    {
        if (dead) return;

        if (slow > 0 && slowDur > 0)
        {
            slowAmount = slow;
            slowTimer  = slowDur;
        }

        // Armor absorbs first
        if (currentArmor > 0)
        {
            int absorbed = Mathf.Min(currentArmor, dmg);
            currentArmor -= absorbed;
            dmg          -= absorbed;
        }

        currentHP -= dmg;

        if (enemyAnimator != null)
            enemyAnimator.SetTrigger("Hit");

        if (modelRoot != null)
            StartCoroutine(DamageFlash());

        FloatingText.Spawn(transform.position + Vector3.up * 1f, "-" + (dmg > 0 ? dmg : 0), Color.white, 0.8f);

        if (currentHP <= 0)
            Die();
    }

    /// <summary>Called by EnemyDamageTrigger when player enters melee range.</summary>
    public void DealContactDamage(ArenaCharacter target)
    {
        if (dead || spawnGrace > 0 || contactCooldown > 0) return;
        if (contactDamage <= 0) return;

        target.TakeDamage(contactDamage);
        contactCooldown = attackCooldown;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayHit();
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        if (modelRoot == null) yield break;
        for (float t = 0; t < 0.15f; t += Time.deltaTime)
        {
            float pulse = 1f + Mathf.Sin(t * 40f) * 0.15f;
            modelRoot.transform.localScale = Vector3.one * pulse;
            yield return null;
        }
        modelRoot.transform.localScale = Vector3.one;
    }

    private void Die()
    {
        dead = true;
        rb.linearVelocity = Vector3.zero;

        if (enemyAnimator != null)
        {
            enemyAnimator.SetBool("Dead", true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[ArenaEnemy] Die: animator={enemyAnimator.runtimeAnimatorController?.name}, Dead=true");
#endif
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        else
        {
            Debug.LogWarning("[ArenaEnemy] Die: enemyAnimator is NULL -- no death animation");
        }
#endif

        if (VFXManager.Instance != null) VFXManager.Instance.PlayDeath(transform.position);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayEnemyDeath();

        var gemObj = new GameObject("XPGem");
        var gem    = gemObj.AddComponent<XPGem>();
        gem.Init(transform.position, xpDrop);

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddCoins(coinDrop);

        if (ArenaManager.Instance != null)
            ArenaManager.Instance.OnEnemyKilled();

        float deathDelay = enemyAnimator != null
            ? ArenaConstants.DEATH_DELAY_ANIMATED
            : ArenaConstants.DEATH_DELAY_INSTANT;
        StartCoroutine(ReleaseAfterDelay(deathDelay));
    }

    private System.Collections.IEnumerator ReleaseAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);

        // Clean up dynamic visual children before returning to pool
        if (modelRoot != null) { Destroy(modelRoot); modelRoot = null; }
        if (barRoot != null)
        {
            Destroy(barRoot.gameObject);
            barRoot = null; hpBarFill = null; armorBarFill = null; hpBarBg = null;
        }
        enemyAnimator = null;

        if (ArenaManager.Instance != null)
            ArenaManager.Instance.ReleaseEnemy(this);
        else
            Destroy(gameObject); // fallback if manager is gone (scene unload, etc.)
    }

    private void Update()
    {
        if (dead) return;

        if (spawnGrace > 0) { spawnGrace -= Time.deltaTime; return; }
        if (contactCooldown > 0) contactCooldown -= Time.deltaTime;
        if (slowTimer > 0) slowTimer -= Time.deltaTime;

        // Knockback
        if (knockbackTimer > 0)
        {
            knockbackTimer -= Time.deltaTime;
            transform.position += knockbackVel * Time.deltaTime;
            knockbackVel *= ArenaConstants.KNOCKBACK_DAMPING;
            return;
        }

        if (player == null || player.IsDead)
        {
            player = FindAnyObjectByType<ArenaCharacter>();
            if (player == null) return;
        }

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0;
        float dist       = toPlayer.magnitude;
        Vector3 dirToPlayer = dist > 0.01f ? toPlayer / dist : Vector3.forward;

        bool isMoving = dist > attackRange * 0.8f;
        if (isMoving)
        {
            float spd = moveSpeed;
            if (slowTimer > 0) spd *= (1f - slowAmount);
            rb.linearVelocity = new Vector3(dirToPlayer.x * spd, 0, dirToPlayer.z * spd);
        }
        else
        {
            rb.linearVelocity = new Vector3(0, 0, 0);
        }

        if (enemyAnimator != null)
            enemyAnimator.SetFloat("Speed", isMoving ? 1f : 0f);

        if (modelRoot != null && dist > 0.1f)
        {
            float angle = Mathf.Atan2(dirToPlayer.x, dirToPlayer.z) * Mathf.Rad2Deg;
            modelRoot.transform.rotation = Quaternion.Euler(0, angle, 0);
        }

        attackTimer -= Time.deltaTime;
        if (dist <= attackRange && attackTimer <= 0)
        {
            attackTimer = attackCooldown;
            if (enemyAnimator != null) enemyAnimator.SetTrigger("Attack");
            Attack(dirToPlayer);
        }
    }

    private void Attack(Vector3 dir)
    {
        switch (type)
        {
            case EnemyType.Melee:
            case EnemyType.Heavy:
                if (player != null && !player.IsDead)
                {
                    float dist = Vector3.Distance(transform.position, player.transform.position);
                    if (dist < attackRange * 1.2f)
                    {
                        player.TakeDamage(contactDamage);
                        rb.AddForce(dir * ArenaConstants.MELEE_LUNGE_FORCE, ForceMode.Impulse);
                        if (AudioManager.Instance != null) AudioManager.Instance.PlayHit();
                    }
                }
                break;

            case EnemyType.Ranged:
            case EnemyType.Wizard:
            case EnemyType.IceMage:
                ShootProjectile(dir, projectileDamage);
                break;

            case EnemyType.Bomber:
                SpawnBomb();
                break;

            case EnemyType.Boss:
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 d = Quaternion.Euler(0, i * ArenaConstants.BOSS_SPREAD_ANGLE, 0) * dir;
                    ShootProjectile(d, projectileDamage);
                }
                break;
        }
    }

    private void ShootProjectile(Vector3 dir, int dmg)
    {
        var projObj = new GameObject("EnemyProjectile");
        var proj    = projObj.AddComponent<Projectile>();
        Vector3 muzzle = transform.position
            + Vector3.up * ArenaConstants.MUZZLE_HEIGHT
            + dir        * ArenaConstants.MUZZLE_FORWARD_OFFSET;
        proj.Init(Projectile.Owner.Enemy, muzzle, dir, ArenaConstants.PROJECTILE_SPEED, dmg);

        if (AudioManager.Instance != null) AudioManager.Instance.PlayShoot();
    }

    private void SpawnBomb()
    {
        if (player == null) return;
        var bombObj = new GameObject("Bomb");
        bombObj.transform.position = player.transform.position;
        var bomb = bombObj.AddComponent<ArenaBomb>();
        bomb.Init(projectileDamage);
    }

    // ── Health Bar ───────────────────────────────────────────────────────────

    private void CreateHealthBar()
    {
        float barHeight = 0.06f;
        float yPos      = bodyHeight + 0.35f;

        barRoot = new GameObject("BarRoot").transform;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = new Vector3(0, yPos, 0);

        hpBarBg   = CreateBarQuad(barRoot, "HPBarBG",  barWidth,          barHeight,           new Color(0.15f, 0.15f, 0.15f));
        hpBarFill = CreateBarQuad(barRoot, "HPFill",   barWidth - 0.02f,  barHeight - 0.01f,   new Color(0.9f,  0.2f,  0.15f));
        hpBarFill.localPosition = new Vector3(0, 0, -0.005f);

        if (maxArmor > 0)
        {
            armorBarFill = CreateBarQuad(barRoot, "ArmorFill", barWidth - 0.02f, barHeight * 0.6f, new Color(0.3f, 0.6f, 1f));
            armorBarFill.localPosition = new Vector3(0, barHeight * 0.6f, -0.005f);
        }
    }

    private Transform CreateBarQuad(Transform parent, string barName, float w, float h, Color color)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        obj.name = barName;
        obj.transform.SetParent(parent, false);
        obj.transform.localScale = new Vector3(w, h, 1f);
        var c = obj.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }
        var rend = obj.GetComponent<Renderer>();
        var mat  = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = color;
        rend.material = mat;
        return obj.transform;
    }

    private void LateUpdate()
    {
        if (barRoot == null) return;

        Camera cam = Camera.main;
        if (cam != null) barRoot.rotation = cam.transform.rotation;

        if (hpBarFill != null && maxHP > 0)
        {
            float ratio = Mathf.Clamp01((float)currentHP / maxHP);
            float fullW = barWidth - 0.02f;
            hpBarFill.localScale    = new Vector3(fullW * ratio, hpBarFill.localScale.y, 1f);
            hpBarFill.localPosition = new Vector3(-fullW * (1f - ratio) * 0.5f, 0, -0.005f);

            Color barColor;
            if      (ratio > 0.6f) barColor = Color.Lerp(new Color(1f, 0.9f, 0.1f), new Color(0.2f, 0.9f, 0.2f), (ratio - 0.6f) / 0.4f);
            else if (ratio > 0.3f) barColor = Color.Lerp(new Color(1f, 0.3f, 0.1f), new Color(1f,   0.9f, 0.1f), (ratio - 0.3f) / 0.3f);
            else                   barColor = new Color(0.9f, 0.15f, 0.1f);
            hpBarFill.GetComponent<Renderer>().material.color = barColor;
        }

        if (armorBarFill != null && maxArmor > 0)
        {
            float ratio = Mathf.Clamp01((float)currentArmor / maxArmor);
            float fullW = barWidth - 0.02f;
            float barH  = 0.06f;
            armorBarFill.localScale    = new Vector3(fullW * ratio, armorBarFill.localScale.y, 1f);
            armorBarFill.localPosition = new Vector3(-fullW * (1f - ratio) * 0.5f, barH * 0.6f, -0.005f);
            if (ratio <= 0) armorBarFill.gameObject.SetActive(false);
        }
    }

    // ── Visuals ──────────────────────────────────────────────────────────────

    private void CreateVisual(EnemyStatsSO stats)
    {
        modelRoot = new GameObject("Model");
        modelRoot.transform.SetParent(transform, false);

        // Priority 1: ModelManager lookup via SO model ID
        if (ModelManager.Instance != null && !string.IsNullOrEmpty(stats.modelId)
            && ModelManager.Instance.EnemyModels.Count > 0)
        {
            var enemyModel = ModelManager.Instance.EnemyModels.Find(m => m.id == stats.modelId)
                             ?? ModelManager.Instance.EnemyModels[0];
            if (enemyModel != null)
            {
                var model = ModelManager.SpawnModel(enemyModel.prefab, modelRoot.transform, stats.modelScale);
                if (model != null)
                {
                    var anim = model.GetComponentInChildren<Animator>();
                    if (anim != null)
                    {
                        if (anim.runtimeAnimatorController == null)
                        {
                            var ctrl = Resources.Load<RuntimeAnimatorController>(
                                $"Models/Enemies/{enemyModel.prefab.name}Animator");
                            if (ctrl != null) anim.runtimeAnimatorController = ctrl;
                            else Destroy(anim);
                        }
                        if (anim != null && anim.runtimeAnimatorController != null)
                            enemyAnimator = anim;
                    }
                    return;
                }
            }
        }

        // Priority 2: Synty models
        var synty = SyntyAssets.Instance;
        if (synty != null)
        {
            GameObject prefab     = null;
            float      syntyScale = stats.modelScale;
            switch (type)
            {
                case EnemyType.Melee:  prefab = synty.CopPrefab;          break;
                case EnemyType.Ranged: prefab = synty.CowboyPrefab;       break;
                case EnemyType.Heavy:  prefab = synty.TownFemalePrefab;   syntyScale = 0.55f; break;
                case EnemyType.Boss:   prefab = synty.FemalePrefab;       syntyScale = 0.7f;  break;
            }
            if (prefab != null)
            {
                var model = SyntyAssets.Spawn(prefab, modelRoot.transform, Vector3.zero, syntyScale, 0f);
                if (model != null)
                {
                    SyntyAssets.FixMaterials(model, stats.bodyColor);
                    return;
                }
            }
        }

        // Priority 3: Procedural geometry using SO visual data
        MakePart(modelRoot, "Body", PrimitiveType.Cube,
            new Vector3(0, stats.bodyHeight / 2f, 0),
            new Vector3(stats.bodyWidth, stats.bodyHeight, stats.bodyWidth * 0.7f), stats.bodyColor);
        MakePart(modelRoot, "Head", PrimitiveType.Sphere,
            new Vector3(0, stats.bodyHeight + 0.12f, 0),
            Vector3.one * stats.bodyWidth * 0.7f, new Color(0.85f, 0.7f, 0.6f));
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(modelRoot, "Eye", PrimitiveType.Sphere,
                new Vector3(i * stats.bodyWidth * 0.25f, stats.bodyHeight + 0.15f, -stats.bodyWidth * 0.3f),
                Vector3.one * 0.06f, Color.white);
        }
        if (stats.baseArmor > 0)
        {
            MakePart(modelRoot, "ArmorTrim", PrimitiveType.Cube,
                new Vector3(0, stats.bodyHeight * 0.5f, 0),
                new Vector3(stats.bodyWidth + 0.06f, stats.bodyHeight * 0.3f, stats.bodyWidth * 0.75f),
                new Color(0.4f, 0.55f, 0.75f));
        }
        MakePart(modelRoot, "Trim", PrimitiveType.Cube,
            new Vector3(0, stats.bodyHeight * 0.7f, 0),
            new Vector3(stats.bodyWidth + 0.04f, 0.04f, stats.bodyWidth * 0.72f), stats.trimColor);
    }

    private GameObject MakePart(GameObject parent, string partName, PrimitiveType primitiveType,
        Vector3 pos, Vector3 scale, Color color)
    {
        var obj = GameObject.CreatePrimitive(primitiveType);
        obj.name = partName;
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = pos;
        obj.transform.localScale    = scale;
        var c = obj.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }
        var rend = obj.GetComponent<Renderer>();
        var mat  = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;
        return obj;
    }
}

/// <summary>
/// Separate trigger child for enemy contact damage.
/// The main collider is solid (non-trigger) for physics separation.
/// </summary>
public class EnemyDamageTrigger : MonoBehaviour
{
    private ArenaEnemy owner;

    public void Init(ArenaEnemy enemy) { owner = enemy; }

    private void OnTriggerStay(Collider other)
    {
        if (owner == null || owner.IsDead) return;
        var character = other.GetComponentInParent<ArenaCharacter>();
        if (character != null && !character.IsDead)
            owner.DealContactDamage(character);
    }
}
