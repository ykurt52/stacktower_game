using UnityEngine;

/// <summary>
/// Base arena enemy. Chases player on XZ plane, attacks when in range.
/// Uses non-kinematic Rigidbody for proper collision separation.
/// Each enemy type has HP and optional armor (shield).
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
    private Animator enemyAnimator;
    private bool dead;
    private float spawnGrace = 0.5f;
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

    public void Init(EnemyType enemyType, Vector3 pos, int wave)
    {
        type = enemyType;
        transform.position = pos;

        float waveMult = 1f + wave * 0.08f;
        switch (type)
        {
            case EnemyType.Melee:
                maxHP = Mathf.CeilToInt(20 * waveMult);
                maxArmor = 0;
                moveSpeed = 2.5f;
                contactDamage = 8;
                attackRange = 0.9f;
                attackCooldown = 0.8f;
                xpDrop = 1; coinDrop = 1;
                bodyHeight = 0.5f;
                break;
            case EnemyType.Ranged:
                maxHP = Mathf.CeilToInt(15 * waveMult);
                maxArmor = 0;
                moveSpeed = 1.5f;
                contactDamage = 5;
                projectileDamage = 7;
                attackRange = 6f;
                attackCooldown = 2f;
                xpDrop = 2; coinDrop = 1;
                bodyHeight = 0.5f;
                break;
            case EnemyType.Heavy:
                maxHP = Mathf.CeilToInt(50 * waveMult);
                maxArmor = Mathf.CeilToInt(20 * waveMult);
                moveSpeed = 1.2f;
                contactDamage = 15;
                attackRange = 1f;
                attackCooldown = 1.2f;
                xpDrop = 3; coinDrop = 2;
                bodyHeight = 0.7f;
                break;
            case EnemyType.Bomber:
                maxHP = Mathf.CeilToInt(25 * waveMult);
                maxArmor = 0;
                moveSpeed = 2f;
                contactDamage = 5;
                projectileDamage = 12;
                attackRange = 4f;
                attackCooldown = 3f;
                xpDrop = 3; coinDrop = 2;
                bodyHeight = 0.5f;
                break;
            case EnemyType.Wizard:
                maxHP = Mathf.CeilToInt(25 * waveMult);
                maxArmor = Mathf.CeilToInt(10 * waveMult);
                moveSpeed = 1.8f;
                contactDamage = 5;
                projectileDamage = 10;
                attackRange = 7f;
                attackCooldown = 2.5f;
                xpDrop = 3; coinDrop = 2;
                bodyHeight = 0.5f;
                break;
            case EnemyType.IceMage:
                maxHP = Mathf.CeilToInt(25 * waveMult);
                maxArmor = Mathf.CeilToInt(10 * waveMult);
                moveSpeed = 1.5f;
                contactDamage = 5;
                projectileDamage = 8;
                attackRange = 6f;
                attackCooldown = 2.5f;
                xpDrop = 3; coinDrop = 2;
                bodyHeight = 0.5f;
                break;
            case EnemyType.Boss:
                maxHP = Mathf.CeilToInt(200 * waveMult);
                maxArmor = Mathf.CeilToInt(80 * waveMult);
                moveSpeed = 1.5f;
                contactDamage = 20;
                projectileDamage = 15;
                attackRange = 5f;
                attackCooldown = 1.5f;
                xpDrop = 15; coinDrop = 10;
                bodyHeight = 1f;
                break;
        }
        currentHP = maxHP;
        currentArmor = maxArmor;

        // Physics - non-kinematic for collision separation
        rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        rb.mass = type == EnemyType.Boss ? 5f : (type == EnemyType.Heavy ? 3f : 1.5f);
        rb.linearDamping = 5f;

        var col = gameObject.AddComponent<CapsuleCollider>();
        col.isTrigger = false; // Solid collision!
        col.radius = type == EnemyType.Boss ? 0.5f : 0.25f;
        col.height = type == EnemyType.Boss ? 1.2f : 0.8f;
        col.center = new Vector3(0, col.height / 2f, 0);

        // Trigger collider for damage detection (slightly larger)
        var triggerObj = new GameObject("DamageTrigger");
        triggerObj.transform.SetParent(transform, false);
        triggerObj.layer = gameObject.layer;
        var triggerCol = triggerObj.AddComponent<SphereCollider>();
        triggerCol.isTrigger = true;
        triggerCol.radius = type == EnemyType.Boss ? 0.55f : 0.2f;
        triggerCol.center = new Vector3(0, 0.4f, 0);
        var trigger = triggerObj.AddComponent<EnemyDamageTrigger>();
        trigger.Init(this);

        CreateVisual();
        CreateHealthBar();
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        knockbackVel = direction.normalized * force;
        knockbackTimer = 0.25f;
    }

    public void TakeDamage(int dmg, float slow = 0f, float slowDur = 0f)
    {
        if (dead) return;

        if (slow > 0 && slowDur > 0)
        {
            slowAmount = slow;
            slowTimer = slowDur;
        }

        // Armor absorbs first
        if (currentArmor > 0)
        {
            int absorbed = Mathf.Min(currentArmor, dmg);
            currentArmor -= absorbed;
            dmg -= absorbed;
        }

        currentHP -= dmg;

        if (enemyAnimator != null)
            enemyAnimator.SetTrigger("Hit");

        // Flash red
        if (modelRoot != null)
            StartCoroutine(DamageFlash());

        FloatingText.Spawn(transform.position + Vector3.up * 1f, "-" + (dmg > 0 ? dmg : 0), Color.white, 0.8f);

        if (currentHP <= 0)
            Die();
    }

    /// <summary>
    /// Called by EnemyDamageTrigger when player enters melee range.
    /// </summary>
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
        // Scale pulse — doesn't damage materials
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
            Debug.Log($"[ArenaEnemy] Die: animator={enemyAnimator.runtimeAnimatorController?.name}, Dead=true, hasState={enemyAnimator.HasState(0, Animator.StringToHash("Death"))}");
        }
        else
        {
            Debug.LogWarning("[ArenaEnemy] Die: enemyAnimator is NULL — no death animation");
        }

        if (VFXManager.Instance != null) VFXManager.Instance.PlayDeath(transform.position);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayEnemyDeath();

        var gemObj = new GameObject("XPGem");
        var gem = gemObj.AddComponent<XPGem>();
        gem.Init(transform.position, xpDrop);

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddCoins(coinDrop);

        if (ArenaManager.Instance != null)
            ArenaManager.Instance.OnEnemyKilled();

        // Wait for death animation before destroying
        float deathDelay = enemyAnimator != null ? 1.5f : 0.05f;
        Destroy(gameObject, deathDelay);
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
            knockbackVel *= 0.85f; // decelerate
            return; // skip movement/attack during knockback
        }

        if (player == null || player.IsDead)
        {
            player = FindAnyObjectByType<ArenaCharacter>();
            if (player == null) return;
        }

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;
        Vector3 dirToPlayer = dist > 0.01f ? toPlayer / dist : Vector3.forward;

        // Move toward player (stop at attack range)
        bool isMoving = dist > attackRange * 0.8f;
        if (isMoving)
        {
            float spd = moveSpeed;
            if (slowTimer > 0) spd *= (1f - slowAmount);

            Vector3 moveForce = dirToPlayer * spd;
            rb.linearVelocity = new Vector3(moveForce.x, 0, moveForce.z);
        }
        else
        {
            rb.linearVelocity = new Vector3(0, 0, 0);
        }

        // Animator: Speed
        if (enemyAnimator != null)
            enemyAnimator.SetFloat("Speed", isMoving ? 1f : 0f);

        // Face player
        if (modelRoot != null && dist > 0.1f)
        {
            float angle = Mathf.Atan2(dirToPlayer.x, dirToPlayer.z) * Mathf.Rad2Deg;
            modelRoot.transform.rotation = Quaternion.Euler(0, angle, 0);
        }

        // Attack
        attackTimer -= Time.deltaTime;
        if (dist <= attackRange && attackTimer <= 0)
        {
            attackTimer = attackCooldown;

            if (enemyAnimator != null)
                enemyAnimator.SetTrigger("Attack");

            Attack(dirToPlayer);
        }
    }

    private void Attack(Vector3 dir)
    {
        switch (type)
        {
            case EnemyType.Melee:
            case EnemyType.Heavy:
                // Melee attack: lunge forward briefly
                if (player != null && !player.IsDead)
                {
                    float dist = Vector3.Distance(transform.position, player.transform.position);
                    if (dist < attackRange * 1.2f)
                    {
                        player.TakeDamage(contactDamage);
                        // Small knockback push on attack
                        rb.AddForce(dir * 3f, ForceMode.Impulse);
                        if (AudioManager.Instance != null)
                            AudioManager.Instance.PlayHit();
                    }
                }
                break;

            case EnemyType.Ranged:
            case EnemyType.Wizard:
                ShootProjectile(dir, projectileDamage);
                break;

            case EnemyType.IceMage:
                ShootProjectile(dir, projectileDamage);
                break;

            case EnemyType.Bomber:
                SpawnBomb();
                break;

            case EnemyType.Boss:
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 d = Quaternion.Euler(0, i * 20f, 0) * dir;
                    ShootProjectile(d, projectileDamage);
                }
                break;
        }
    }

    private void ShootProjectile(Vector3 dir, int dmg)
    {
        var projObj = new GameObject("EnemyProjectile");
        var proj = projObj.AddComponent<Projectile>();
        Vector3 muzzle = transform.position + Vector3.up * 0.5f + dir * 0.3f;
        proj.Init(Projectile.Owner.Enemy, muzzle, dir, 5f, dmg);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayShoot();
    }

    private void SpawnBomb()
    {
        if (player == null) return;
        var bombObj = new GameObject("Bomb");
        bombObj.transform.position = player.transform.position;
        var bomb = bombObj.AddComponent<ArenaBomb>();
        bomb.Init(projectileDamage);
    }

    // ── Health Bar (Archero-style: HP red + Armor blue above head) ──

    private void CreateHealthBar()
    {
        barWidth = type == EnemyType.Boss ? 0.8f : 0.5f;
        float barHeight = 0.06f;
        float yPos = bodyHeight + 0.35f;

        barRoot = new GameObject("BarRoot").transform;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = new Vector3(0, yPos, 0);

        // Background (dark)
        hpBarBg = CreateBarQuad(barRoot, "HPBarBG", barWidth, barHeight, new Color(0.15f, 0.15f, 0.15f));

        // HP fill (red/green)
        hpBarFill = CreateBarQuad(barRoot, "HPFill", barWidth - 0.02f, barHeight - 0.01f, new Color(0.9f, 0.2f, 0.15f));
        hpBarFill.localPosition = new Vector3(0, 0, -0.005f);

        // Armor bar (blue, slightly above HP bar)
        if (maxArmor > 0)
        {
            armorBarFill = CreateBarQuad(barRoot, "ArmorFill", barWidth - 0.02f, barHeight * 0.6f, new Color(0.3f, 0.6f, 1f));
            armorBarFill.localPosition = new Vector3(0, barHeight * 0.6f, -0.005f);
        }
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

        // Billboard: face camera
        Camera cam = Camera.main;
        if (cam != null)
        {
            barRoot.rotation = cam.transform.rotation;
        }

        // Update HP bar fill
        if (hpBarFill != null && maxHP > 0)
        {
            float ratio = Mathf.Clamp01((float)currentHP / maxHP);
            float fullW = barWidth - 0.02f;
            hpBarFill.localScale = new Vector3(fullW * ratio, hpBarFill.localScale.y, 1f);
            hpBarFill.localPosition = new Vector3(-fullW * (1f - ratio) * 0.5f, 0, -0.005f);

            // Color: green > yellow > red
            Color barColor;
            if (ratio > 0.6f) barColor = Color.Lerp(new Color(1f, 0.9f, 0.1f), new Color(0.2f, 0.9f, 0.2f), (ratio - 0.6f) / 0.4f);
            else if (ratio > 0.3f) barColor = Color.Lerp(new Color(1f, 0.3f, 0.1f), new Color(1f, 0.9f, 0.1f), (ratio - 0.3f) / 0.3f);
            else barColor = new Color(0.9f, 0.15f, 0.1f);
            hpBarFill.GetComponent<Renderer>().material.color = barColor;
        }

        // Update armor bar fill
        if (armorBarFill != null && maxArmor > 0)
        {
            float ratio = Mathf.Clamp01((float)currentArmor / maxArmor);
            float fullW = barWidth - 0.02f;
            armorBarFill.localScale = new Vector3(fullW * ratio, armorBarFill.localScale.y, 1f);
            float barH = 0.06f;
            armorBarFill.localPosition = new Vector3(-fullW * (1f - ratio) * 0.5f, barH * 0.6f, -0.005f);

            if (ratio <= 0)
                armorBarFill.gameObject.SetActive(false);
        }
    }

    // ── Visuals ──

    private void CreateVisual()
    {
        modelRoot = new GameObject("Model");
        modelRoot.transform.SetParent(transform, false);

        Color bodyColor, trimColor;
        float bodyW;
        switch (type)
        {
            case EnemyType.Melee:
                bodyColor = new Color(0.8f, 0.3f, 0.2f); trimColor = new Color(0.5f, 0.2f, 0.1f);
                bodyW = 0.3f; break;
            case EnemyType.Ranged:
                bodyColor = new Color(0.3f, 0.5f, 0.8f); trimColor = new Color(0.2f, 0.3f, 0.5f);
                bodyW = 0.28f; break;
            case EnemyType.Heavy:
                bodyColor = new Color(0.5f, 0.5f, 0.55f); trimColor = new Color(0.3f, 0.3f, 0.35f);
                bodyW = 0.45f; break;
            case EnemyType.Bomber:
                bodyColor = new Color(0.8f, 0.6f, 0.2f); trimColor = new Color(0.6f, 0.4f, 0.1f);
                bodyW = 0.35f; break;
            case EnemyType.Wizard:
                bodyColor = new Color(0.6f, 0.2f, 0.8f); trimColor = new Color(0.4f, 0.1f, 0.5f);
                bodyW = 0.28f; break;
            case EnemyType.IceMage:
                bodyColor = new Color(0.4f, 0.7f, 0.9f); trimColor = new Color(0.2f, 0.4f, 0.6f);
                bodyW = 0.28f; break;
            case EnemyType.Boss:
                bodyColor = new Color(0.7f, 0.15f, 0.15f); trimColor = new Color(0.4f, 0.1f, 0.1f);
                bodyW = 0.6f; break;
            default:
                bodyColor = Color.red; trimColor = Color.black;
                bodyW = 0.3f; break;
        }

        // Try ModelManager — assign specific models to enemy types by name
        if (ModelManager.Instance != null && ModelManager.Instance.EnemyModels.Count > 0)
        {
            string modelId = type switch
            {
                EnemyType.Melee => "punk",
                EnemyType.Ranged => "zombie_ribcage",
                // TODO: assign more enemy models as they're configured
                EnemyType.Heavy => "punk",
                EnemyType.Bomber => "punk",
                EnemyType.Wizard => "punk",
                EnemyType.IceMage => "punk",
                EnemyType.Boss => "punk",
                _ => "punk"
            };
            var enemyModel = ModelManager.Instance.EnemyModels.Find(m => m.id == modelId)
                ?? ModelManager.Instance.EnemyModels[0];
            if (enemyModel != null)
            {
                float scale = type == EnemyType.Boss ? 1f : type == EnemyType.Heavy ? 0.85f : 0.75f;
                var model = ModelManager.SpawnModel(enemyModel.prefab, modelRoot.transform, scale);
                if (model != null)
                {
                    var anim = model.GetComponentInChildren<Animator>();
                    if (anim != null)
                    {
                        if (anim.runtimeAnimatorController == null)
                        {
                            string eName = enemyModel.prefab.name;
                            var ctrl = Resources.Load<RuntimeAnimatorController>($"Models/Enemies/{eName}Animator");
                            if (ctrl != null)
                                anim.runtimeAnimatorController = ctrl;
                            else
                                Destroy(anim);
                        }
                        if (anim != null && anim.runtimeAnimatorController != null)
                            enemyAnimator = anim;
                    }
                    return;
                }
            }
        }

        // Fallback: Synty models
        var synty = SyntyAssets.Instance;
        if (synty != null)
        {
            GameObject prefab = null;
            float scale = 0.5f;
            switch (type)
            {
                case EnemyType.Melee: prefab = synty.CopPrefab; break;
                case EnemyType.Ranged: prefab = synty.CowboyPrefab; break;
                case EnemyType.Heavy: prefab = synty.TownFemalePrefab; scale = 0.55f; break;
                case EnemyType.Boss: prefab = synty.FemalePrefab; scale = 0.7f; break;
            }
            if (prefab != null)
            {
                var model = SyntyAssets.Spawn(prefab, modelRoot.transform, Vector3.zero, scale, 0f);
                if (model != null)
                {
                    SyntyAssets.FixMaterials(model, bodyColor);
                    return;
                }
            }
        }

        // Fallback: procedural
        MakePart(modelRoot, "Body", PrimitiveType.Cube,
            new Vector3(0, bodyHeight / 2f, 0), new Vector3(bodyW, bodyHeight, bodyW * 0.7f), bodyColor);
        MakePart(modelRoot, "Head", PrimitiveType.Sphere,
            new Vector3(0, bodyHeight + 0.12f, 0), Vector3.one * bodyW * 0.7f, new Color(0.85f, 0.7f, 0.6f));
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(modelRoot, "Eye", PrimitiveType.Sphere,
                new Vector3(i * bodyW * 0.25f, bodyHeight + 0.15f, -bodyW * 0.3f),
                Vector3.one * 0.06f, Color.white);
        }

        // Armor visual indicator for Heavy/Wizard/Boss
        if (maxArmor > 0)
        {
            MakePart(modelRoot, "ArmorTrim", PrimitiveType.Cube,
                new Vector3(0, bodyHeight * 0.5f, 0),
                new Vector3(bodyW + 0.06f, bodyHeight * 0.3f, bodyW * 0.75f),
                new Color(0.4f, 0.55f, 0.75f));
        }

        MakePart(modelRoot, "Trim", PrimitiveType.Cube,
            new Vector3(0, bodyHeight * 0.7f, 0), new Vector3(bodyW + 0.04f, 0.04f, bodyW * 0.72f), trimColor);
    }

    private GameObject MakePart(GameObject parent, string name, PrimitiveType primitiveType,
        Vector3 pos, Vector3 scale, Color color)
    {
        var obj = GameObject.CreatePrimitive(primitiveType);
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
/// Separate trigger child for enemy contact damage.
/// Needed because the main collider is solid (non-trigger) for physics separation.
/// </summary>
public class EnemyDamageTrigger : MonoBehaviour
{
    private ArenaEnemy owner;

    public void Init(ArenaEnemy enemy)
    {
        owner = enemy;
    }

    private void OnTriggerStay(Collider other)
    {
        if (owner == null || owner.IsDead) return;
        var character = other.GetComponentInParent<ArenaCharacter>();
        if (character != null && !character.IsDead)
        {
            owner.DealContactDamage(character);
        }
    }
}
