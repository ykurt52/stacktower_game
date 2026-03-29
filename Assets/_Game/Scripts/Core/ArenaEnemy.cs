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

    // AI State Machine
    private enum AIState { Chase, Attack, Search, Patrol, Unstuck }
    private AIState _aiState;
    private Vector3 _lastKnownPlayerPos;
    private Vector3 _patrolTarget;
    private float _searchTimer;
    private const float SEARCH_DURATION = 2.5f;
    private const float PATROL_SPEED_MULT = 0.5f;
    private const float LOS_CHECK_INTERVAL = 0.2f;
    private float _losCheckTimer;
    private bool _hasLoS;

    // Stuck detection
    private Vector3 _lastPos;
    private float _stuckTimer;
    private float _unstuckTimer;
    private const float STUCK_CHECK_INTERVAL = 0.5f;
    private const float STUCK_THRESHOLD = 0.1f;  // if moved less than this in 0.5s, stuck
    private const float UNSTUCK_DURATION = 2f;

    private ArenaCharacter player;
    private GameObject modelRoot;
    private Rigidbody rb;
    private CapsuleCollider _col;       // created once per pool object, updated on reuse
    private SphereCollider _triggerCol; // created once per pool object, updated on reuse
    private Animator enemyAnimator;
    private bool dead;
    private float spawnGrace;
    private float bodyHeight;

    // HP bar visuals (world-space, Archero style)
    private Transform barRoot;
    private Transform hpBarFill;
    private Transform hpGhostFill;
    private Transform armorBarFill;
    private Transform hpBarBg;
    private float barWidth;
    private float _ghostHP;

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
        _aiState        = AIState.Chase;
        _hasLoS         = true;
        _losCheckTimer  = 0f;
        _searchTimer    = 0f;
        _stuckTimer     = 0f;
        _unstuckTimer   = 0f;
        _lastPos        = pos;

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
        _ghostHP     = maxHP;

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

        FloatingText.Spawn(transform.position + Vector3.up * 1.2f, "-" + (dmg > 0 ? dmg : 0), new Color(1f, 0.25f, 0.2f), 1f);

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
        attackTimer -= Time.deltaTime;

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
            if (player == null) { StopMoving(); return; }
        }

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0;
        float dist = toPlayer.magnitude;
        Vector3 dirToPlayer = dist > 0.01f ? toPlayer / dist : Vector3.forward;

        // Periodic LoS check (not every frame)
        _losCheckTimer -= Time.deltaTime;
        if (_losCheckTimer <= 0)
        {
            _losCheckTimer = LOS_CHECK_INTERVAL;
            _hasLoS = LineOfSight.Check(transform.position, player.transform.position);
            if (_hasLoS)
                _lastKnownPlayerPos = player.transform.position;
        }

        // ── Stuck detection (during Chase) ──
        if (_aiState == AIState.Chase)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= STUCK_CHECK_INTERVAL)
            {
                float moved = Vector3.Distance(transform.position, _lastPos);
                _lastPos = transform.position;
                _stuckTimer = 0f;

                if (moved < STUCK_THRESHOLD)
                {
                    // Stuck! Pick random point and go there
                    PickPatrolTarget();
                    _unstuckTimer = UNSTUCK_DURATION;
                    _aiState = AIState.Unstuck;
                }
            }
        }

        // ── State machine ──
        switch (_aiState)
        {
            case AIState.Chase:
                UpdateChase(dist, dirToPlayer);
                break;
            case AIState.Attack:
                UpdateAttack(dist, dirToPlayer);
                break;
            case AIState.Search:
                UpdateSearch();
                break;
            case AIState.Patrol:
                UpdatePatrol();
                break;
            case AIState.Unstuck:
                UpdateUnstuck(dist, dirToPlayer);
                break;
        }
    }

    private void UpdateChase(float dist, Vector3 dirToPlayer)
    {
        if (!_hasLoS)
        {
            // Lost sight — go to last known position
            _searchTimer = SEARCH_DURATION;
            _aiState = AIState.Search;
            return;
        }

        if (dist <= attackRange && attackTimer <= 0)
        {
            _aiState = AIState.Attack;
            UpdateAttack(dist, dirToPlayer);
            return;
        }

        // Move toward player
        MoveToward(dirToPlayer, moveSpeed);
        FaceDirection(dirToPlayer);
    }

    private void UpdateAttack(float dist, Vector3 dirToPlayer)
    {
        if (!_hasLoS)
        {
            _searchTimer = SEARCH_DURATION;
            _aiState = AIState.Search;
            StopMoving();
            return;
        }

        if (dist > attackRange)
        {
            _aiState = AIState.Chase;
            return;
        }

        // Stop and attack
        StopMoving();
        FaceDirection(dirToPlayer);

        if (attackTimer <= 0)
        {
            attackTimer = attackCooldown;
            if (enemyAnimator != null) enemyAnimator.SetTrigger("Attack");
            Attack(dirToPlayer);
        }
    }

    private void UpdateSearch()
    {
        // LoS regained → chase
        if (_hasLoS)
        {
            _aiState = AIState.Chase;
            return;
        }

        // Move toward last known position
        Vector3 toTarget = _lastKnownPlayerPos - transform.position;
        toTarget.y = 0;
        float dist = toTarget.magnitude;

        if (dist > 0.5f)
        {
            Vector3 dir = toTarget / dist;
            MoveToward(dir, moveSpeed * PATROL_SPEED_MULT);
            FaceDirection(dir);
        }
        else
        {
            // Arrived at last known pos, wait then patrol
            StopMoving();
            _searchTimer -= Time.deltaTime;
            if (_searchTimer <= 0)
            {
                PickPatrolTarget();
                _aiState = AIState.Patrol;
            }
        }
    }

    private void UpdatePatrol()
    {
        // LoS regained → chase
        if (_hasLoS)
        {
            _aiState = AIState.Chase;
            return;
        }

        Vector3 toTarget = _patrolTarget - transform.position;
        toTarget.y = 0;
        float dist = toTarget.magnitude;

        if (dist > 0.5f)
        {
            Vector3 dir = toTarget / dist;
            MoveToward(dir, moveSpeed * PATROL_SPEED_MULT);
            FaceDirection(dir);
        }
        else
        {
            // Reached patrol point, pick a new one
            PickPatrolTarget();
        }
    }

    private void UpdateUnstuck(float distToPlayer, Vector3 dirToPlayer)
    {
        _unstuckTimer -= Time.deltaTime;

        // If we can see the player and path is clear, go back to chase
        if (_hasLoS && _unstuckTimer < UNSTUCK_DURATION * 0.5f)
        {
            _aiState = AIState.Chase;
            _lastPos = transform.position;
            return;
        }

        // Walk toward random patrol target
        Vector3 toTarget = _patrolTarget - transform.position;
        toTarget.y = 0;
        float dist = toTarget.magnitude;

        if (dist > 0.5f)
        {
            Vector3 dir = toTarget / dist;
            MoveToward(dir, moveSpeed * 0.7f);
            FaceDirection(dir);
        }
        else
        {
            // Reached random point, pick new one or go chase
            if (_unstuckTimer <= 0)
            {
                _aiState = AIState.Chase;
                _lastPos = transform.position;
            }
            else
            {
                PickPatrolTarget();
            }
        }
    }

    private void MoveToward(Vector3 dir, float speed)
    {
        if (slowTimer > 0) speed *= (1f - slowAmount);
        rb.linearVelocity = new Vector3(dir.x * speed, 0, dir.z * speed);
        if (enemyAnimator != null)
            enemyAnimator.SetFloat("Speed", 1f);
    }

    private void StopMoving()
    {
        if (rb != null)
            rb.linearVelocity = Vector3.zero;
        if (enemyAnimator != null)
            enemyAnimator.SetFloat("Speed", 0f);
    }

    private void FaceDirection(Vector3 dir)
    {
        if (modelRoot != null && dir.sqrMagnitude > 0.01f)
        {
            // Use LookRotation instead of Euler to avoid gimbal drift
            modelRoot.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }
    }

    private void PickPatrolTarget()
    {
        // Random point within arena bounds
        float halfW = 4.5f;
        float halfD = 15f;
        _patrolTarget = new Vector3(
            Random.Range(-halfW, halfW),
            0,
            Random.Range(-halfD, halfD));
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
        float hpH      = 0.085f;
        float armorH   = 0.04f;
        float outlineW = 0.012f;
        float gap      = 0.005f;
        float yPos     = bodyHeight + 0.75f;
        float bw       = Mathf.Max(barWidth, 0.6f);

        barRoot = new GameObject("BarRoot").transform;
        barRoot.SetParent(transform, false);
        barRoot.localPosition = new Vector3(0, yPos, 0);

        // ── HP Bar ──
        // Outline (black)
        CreateBarQuad(barRoot, "HPOutline", bw + outlineW, hpH + outlineW, new Color(0, 0, 0, 0.9f), 0);

        // Background (dark)
        hpBarBg = CreateBarQuad(barRoot, "HPBarBG", bw, hpH, new Color(0.18f, 0.18f, 0.18f, 0.95f), 1);

        // Ghost fill (white damage trail)
        hpGhostFill = CreateBarQuad(barRoot, "HPGhost", bw - 0.01f, hpH - 0.01f, new Color(1f, 1f, 1f, 0.5f), 2);
        hpGhostFill.localPosition = new Vector3(0, 0, -0.003f);

        // HP fill (red/orange)
        hpBarFill = CreateBarQuad(barRoot, "HPFill", bw - 0.01f, hpH - 0.01f, new Color(0.95f, 0.3f, 0.15f), 3);
        hpBarFill.localPosition = new Vector3(0, 0, -0.006f);

        // ── Armor Bar (below HP) ──
        if (maxArmor > 0)
        {
            float armorY = -(hpH * 0.5f + armorH * 0.5f + gap);

            CreateBarQuad(barRoot, "ArmorOutline", bw + outlineW, armorH + outlineW * 0.7f, new Color(0, 0, 0, 0.8f), 0)
                .localPosition = new Vector3(0, armorY, 0);

            CreateBarQuad(barRoot, "ArmorBG", bw, armorH, new Color(0.1f, 0.1f, 0.2f, 0.9f), 1)
                .localPosition = new Vector3(0, armorY, 0);

            armorBarFill = CreateBarQuad(barRoot, "ArmorFill", bw - 0.01f, armorH - 0.008f, new Color(0.35f, 0.55f, 0.95f), 3);
            armorBarFill.localPosition = new Vector3(0, armorY, -0.005f);
        }
    }

    private static Material _barMaterial;

    private static Material GetBarMaterial()
    {
        if (_barMaterial != null) return _barMaterial;
        _barMaterial = new Material(Shader.Find("Sprites/Default"));
        return _barMaterial;
    }

    private Transform CreateBarQuad(Transform parent, string barName, float w, float h, Color color, int sortOrder = 1)
    {
        var obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        obj.name = barName;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localScale = new Vector3(w, h, 1f);

        var col = obj.GetComponent<Collider>();
        if (col != null) { col.enabled = false; Destroy(col); }

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
        if (cam != null) barRoot.rotation = cam.transform.rotation;

        float bw = Mathf.Max(barWidth, 0.6f);
        float fullW = bw - 0.012f;
        float hpH = 0.085f;
        float armorH = 0.04f;

        // Ghost bar: lerp toward actual HP
        if (_ghostHP > currentHP)
            _ghostHP = Mathf.Lerp(_ghostHP, currentHP, Time.deltaTime * 3f);
        else
            _ghostHP = currentHP;

        if (hpBarFill != null && maxHP > 0)
        {
            float ratio = Mathf.Clamp01((float)currentHP / maxHP);
            hpBarFill.localScale    = new Vector3(fullW * ratio, hpH - 0.01f, 1f);
            hpBarFill.localPosition = new Vector3(-fullW * (1f - ratio) * 0.5f, 0, -0.006f);

            Color barColor;
            if      (ratio > 0.6f) barColor = new Color(0.95f, 0.3f, 0.15f);
            else if (ratio > 0.3f) barColor = Color.Lerp(new Color(0.95f, 0.15f, 0.1f), new Color(0.95f, 0.3f, 0.15f), (ratio - 0.3f) / 0.3f);
            else                   barColor = new Color(0.85f, 0.1f, 0.08f);
            var rend = hpBarFill.GetComponent<MeshRenderer>();
            if (rend != null) rend.material.color = barColor;
        }

        // Ghost fill (white damage trail)
        if (hpGhostFill != null && maxHP > 0)
        {
            float ghostRatio = Mathf.Clamp01(_ghostHP / maxHP);
            hpGhostFill.localScale    = new Vector3(fullW * ghostRatio, hpH - 0.01f, 1f);
            hpGhostFill.localPosition = new Vector3(-fullW * (1f - ghostRatio) * 0.5f, 0, -0.003f);
        }

        if (armorBarFill != null && maxArmor > 0)
        {
            float ratio = Mathf.Clamp01((float)currentArmor / maxArmor);
            float armorY = -(hpH * 0.5f + armorH * 0.5f + 0.005f);
            armorBarFill.localScale    = new Vector3(fullW * ratio, armorH - 0.008f, 1f);
            armorBarFill.localPosition = new Vector3(-fullW * (1f - ratio) * 0.5f, armorY, -0.005f);
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
                        {
                            anim.applyRootMotion = false;
                            enemyAnimator = anim;
                        }

                        // Attach weapons to hands
                        Debug.Log($"[ArenaEnemy] {stats.modelId}: anim={anim != null}, isHuman={anim?.isHuman}, rightWeapon='{stats.rightHandWeapon}', leftWeapon='{stats.leftHandWeapon}'");
                        if (anim != null && anim.isHuman)
                        {
                            AttachEnemyWeapon(anim, HumanBodyBones.RightHand, stats.rightHandWeapon);
                            AttachEnemyWeapon(anim, HumanBodyBones.LeftHand, stats.leftHandWeapon);
                        }
                        else if (anim != null)
                        {
                            // Generic rig — try to find hand bone by name
                            var rightHand = FindChildRecursive(anim.transform, "RightHand")
                                         ?? FindChildRecursive(anim.transform, "mixamorig:RightHand")
                                         ?? FindChildRecursive(anim.transform, "Hand_R")
                                         ?? FindChildRecursive(anim.transform, "hand_r");
                            var leftHand = FindChildRecursive(anim.transform, "LeftHand")
                                        ?? FindChildRecursive(anim.transform, "mixamorig:LeftHand")
                                        ?? FindChildRecursive(anim.transform, "Hand_L")
                                        ?? FindChildRecursive(anim.transform, "hand_l");
                            Debug.Log($"[ArenaEnemy] Generic rig fallback: rightHand={rightHand?.name ?? "null"}, leftHand={leftHand?.name ?? "null"}");
                            AttachWeaponToTransform(rightHand, stats.rightHandWeapon);
                            AttachWeaponToTransform(leftHand, stats.leftHandWeapon);
                        }
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

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            var found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }

    private void AttachWeaponToTransform(Transform bone, string weaponName)
    {
        if (bone == null || string.IsNullOrEmpty(weaponName)) return;

        var prefab = Resources.Load<GameObject>($"Models/Enemies/{weaponName}");
        if (prefab == null) { Debug.LogWarning($"[ArenaEnemy] Weapon not found: Models/Enemies/{weaponName}"); return; }

        var weapon = Instantiate(prefab, bone);
        weapon.name = weaponName;
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        weapon.transform.localScale = Vector3.one;

        foreach (var col in weapon.GetComponentsInChildren<Collider>()) { col.enabled = false; Destroy(col); }
        foreach (var rb2 in weapon.GetComponentsInChildren<Rigidbody>()) Destroy(rb2);
    }

    private void AttachEnemyWeapon(Animator anim, HumanBodyBones bone, string weaponName)
    {
        if (string.IsNullOrEmpty(weaponName)) return;

        var prefab = Resources.Load<GameObject>($"Models/Enemies/{weaponName}");
        if (prefab == null)
        {
            Debug.LogWarning($"[ArenaEnemy] Weapon prefab not found: Models/Enemies/{weaponName}");
            return;
        }

        Transform hand = anim.GetBoneTransform(bone);
        if (hand == null)
        {
            Debug.LogWarning($"[ArenaEnemy] Bone {bone} not found on {anim.gameObject.name}");
            return;
        }

        var weapon = Instantiate(prefab, hand);
        weapon.name = weaponName;
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        weapon.transform.localScale = Vector3.one;

        foreach (var col in weapon.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
            Destroy(col);
        }
        foreach (var rb2 in weapon.GetComponentsInChildren<Rigidbody>())
            Destroy(rb2);

        Debug.Log($"[ArenaEnemy] Attached '{weaponName}' to {bone} of {anim.gameObject.name}");
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
