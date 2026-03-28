using UnityEngine;

/// <summary>
/// Knight enemy — patrols on a platform and attacks with a sword when
/// the character is on the same platform (similar Y level).
/// Contact damage: 1. Sword damage: 2.
/// </summary>
public class TowerKnight : MonoBehaviour
{
    private float moveSpeed = 0.8f;
    private float leftBound;
    private float rightBound;
    private int direction = 1;
    private GameObject modelRoot;
    private GameObject swordArm;
    private TowerCharacter target;
    private int contactDamage = 1;
    private int swordDamage = 2;
    private float attackRange = 1.2f;
    private float attackCooldown = 1.5f;
    private float attackTimer;
    private float attackAnimTimer;
    private bool isAttacking;
    private float platformY;
    private float sameFloorThreshold = 1.0f;
    private BoxCollider swordCollider;
    private float fallVelocity;
    private bool isFalling;
    private float spawnGrace = 0.5f;
    private int hp = 3;

    public int Damage => contactDamage;

    public void TakeHit(int dmg)
    {
        hp -= dmg;
        if (hp <= 0)
        {
            if (VFXManager.Instance != null) VFXManager.Instance.PlayDeath(transform.position);
            if (ScoreManager.Instance != null) ScoreManager.Instance.AddScore(4);
            Destroy(gameObject, 0.1f);
        }
        else if (modelRoot != null)
        {
            modelRoot.SetActive(false);
            Invoke(nameof(ShowModel), 0.1f);
        }
    }

    public void SetHP(int newHP) { hp = newHP; }

    private void ShowModel() { if (modelRoot != null) modelRoot.SetActive(true); }

    public void Init(float platformX, float platformWidth, float platY)
    {
        platformY = platY;
        float halfW = platformWidth / 2f - 0.3f;
        float localCenterX = transform.parent != null ? transform.localPosition.x : platformX;
        leftBound = localCenterX - halfW;
        rightBound = localCenterX + halfW;
        direction = Random.value > 0.5f ? 1 : -1;
        attackTimer = attackCooldown * 0.5f;

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        BoxCollider col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.4f, 0.55f, 0.4f);
        col.center = new Vector3(0, 0.27f, 0);

        CreateVisual();
    }

    private void Update()
    {
        if (spawnGrace > 0) { spawnGrace -= Time.deltaTime; }
        else if (!isFalling && !Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.4f, ~0, QueryTriggerInteraction.Ignore))
        {
            isFalling = true;
            transform.SetParent(null);
        }
        if (isFalling)
        {
            fallVelocity += -20f * Time.deltaTime;
            transform.position += new Vector3(0, fallVelocity * Time.deltaTime, 0);
            if (transform.position.y < -20f) Destroy(gameObject);
            return;
        }

        if (target == null || target.IsDead)
        {
            target = FindAnyObjectByType<TowerCharacter>();
            if (target == null) return;
        }

        // Check if character is on the same platform level
        float dy = Mathf.Abs(target.transform.position.y - platformY);
        bool samePlatform = dy < sameFloorThreshold;
        float dx = target.transform.position.x - transform.position.x;
        float dist = Mathf.Abs(dx);

        if (samePlatform && dist < attackRange && !isAttacking)
        {
            // Face the character
            int newDir = dx < 0 ? -1 : 1;
            if (newDir != direction)
            {
                direction = newDir;
                FlipModel();
            }

            // Attack
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                StartAttack();
                attackTimer = attackCooldown;
            }
        }
        else if (!isAttacking)
        {
            // Patrol
            Patrol();
        }

        // Attack animation
        if (isAttacking)
        {
            UpdateAttackAnim();
        }
    }

    private void Patrol()
    {
        Vector3 localPos = transform.localPosition;
        localPos.x += moveSpeed * direction * Time.deltaTime;

        if (localPos.x >= rightBound)
        {
            localPos.x = rightBound;
            direction = -1;
            FlipModel();
        }
        else if (localPos.x <= leftBound)
        {
            localPos.x = leftBound;
            direction = 1;
            FlipModel();
        }

        transform.localPosition = localPos;
    }

    private void FlipModel()
    {
        if (modelRoot != null)
            modelRoot.transform.localScale = new Vector3(direction, 1, 1);
    }

    private void StartAttack()
    {
        isAttacking = true;
        attackAnimTimer = 0f;

        // Enable sword collider during attack
        if (swordCollider != null)
            swordCollider.enabled = true;
    }

    private void UpdateAttackAnim()
    {
        attackAnimTimer += Time.deltaTime;
        float attackDuration = 0.4f;

        if (swordArm != null)
        {
            // Swing: raise up then slash down
            float t = attackAnimTimer / attackDuration;
            float angle;
            if (t < 0.3f)
            {
                // Wind up (raise sword)
                angle = Mathf.Lerp(0, 90f, t / 0.3f);
            }
            else if (t < 0.6f)
            {
                // Slash down
                angle = Mathf.Lerp(90f, -60f, (t - 0.3f) / 0.3f);
            }
            else
            {
                // Return to rest
                angle = Mathf.Lerp(-60f, 0f, (t - 0.6f) / 0.4f);
            }
            swordArm.transform.localRotation = Quaternion.Euler(0, 0, angle);
        }

        // Check hit at the slash moment
        if (attackAnimTimer > 0.2f && attackAnimTimer < 0.35f)
        {
            if (target != null && !target.IsDead)
            {
                float dx = Mathf.Abs(target.transform.position.x - transform.position.x);
                float dy = Mathf.Abs(target.transform.position.y - platformY);
                if (dx < attackRange && dy < sameFloorThreshold)
                {
                    target.TakeDamage(swordDamage, GetComponent<Collider>());
                    // Only hit once per swing
                    attackAnimTimer = 0.35f;
                }
            }
        }

        if (attackAnimTimer >= 0.4f)
        {
            isAttacking = false;
            if (swordArm != null)
                swordArm.transform.localRotation = Quaternion.identity;
            if (swordCollider != null)
                swordCollider.enabled = false;
        }
    }

    private void CreateVisual()
    {
        modelRoot = new GameObject("KnightModel");
        modelRoot.transform.SetParent(transform, false);
        modelRoot.transform.localPosition = Vector3.zero;

        // Try Synty town female model for knight (armored look)
        if (SyntyAssets.Instance != null && SyntyAssets.Instance.TownFemalePrefab != null)
        {
            SyntyAssets.Spawn(SyntyAssets.Instance.TownFemalePrefab, modelRoot.transform,
                new Vector3(0, 0, 0), 0.35f, 180f);
            // Still need swordArm for attack animation
            swordArm = new GameObject("SwordArm");
            swordArm.transform.SetParent(modelRoot.transform, false);
            swordArm.transform.localPosition = new Vector3(0.16f, 0.32f, 0);
            return;
        }

        // Fallback: procedural knight
        Color armorColor = new Color(0.55f, 0.55f, 0.6f);
        Color darkArmor = new Color(0.35f, 0.35f, 0.4f);
        Color visorColor = new Color(0.15f, 0.15f, 0.2f);
        Color swordColor = new Color(0.75f, 0.75f, 0.8f);
        Color handleColor = new Color(0.45f, 0.25f, 0.1f);
        Color plume = new Color(0.8f, 0.15f, 0.1f);
        Color eyeGlow = new Color(1f, 0.3f, 0.1f);

        // Body (armor torso)
        MakePart(modelRoot, "Body", PrimitiveType.Cube,
            new Vector3(0, 0.25f, 0), new Vector3(0.26f, 0.28f, 0.18f), armorColor);

        // Belt/waist
        MakePart(modelRoot, "Belt", PrimitiveType.Cube,
            new Vector3(0, 0.12f, 0), new Vector3(0.28f, 0.04f, 0.19f), darkArmor);

        // Shoulder pads
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(modelRoot, "Shoulder", PrimitiveType.Sphere,
                new Vector3(i * 0.16f, 0.38f, 0), new Vector3(0.12f, 0.1f, 0.14f), armorColor);
        }

        // Head (helmet)
        var head = MakePart(modelRoot, "Helmet", PrimitiveType.Cube,
            new Vector3(0, 0.5f, 0), new Vector3(0.22f, 0.22f, 0.22f), armorColor);

        // Visor slit
        MakePart(head, "Visor", PrimitiveType.Cube,
            new Vector3(0, -0.05f, -0.48f), new Vector3(0.7f, 0.15f, 0.08f), visorColor);

        // Glowing eyes behind visor
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(head, "Eye", PrimitiveType.Sphere,
                new Vector3(i * 0.2f, -0.05f, -0.45f), Vector3.one * 0.12f, eyeGlow);
        }

        // Helmet top ridge
        MakePart(head, "Ridge", PrimitiveType.Cube,
            new Vector3(0, 0.45f, 0), new Vector3(0.08f, 0.12f, 0.8f), darkArmor);

        // Red plume on top
        MakePart(head, "Plume", PrimitiveType.Sphere,
            new Vector3(0, 0.5f, 0.15f), new Vector3(0.08f, 0.15f, 0.35f), plume);

        // Legs
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(modelRoot, "Leg", PrimitiveType.Cube,
                new Vector3(i * 0.07f, 0.05f, 0), new Vector3(0.09f, 0.14f, 0.11f), darkArmor);
            MakePart(modelRoot, "Boot", PrimitiveType.Cube,
                new Vector3(i * 0.07f, -0.02f, -0.01f), new Vector3(0.1f, 0.05f, 0.14f), darkArmor);
        }

        // Shield arm (left side)
        var shieldArm = MakePart(modelRoot, "ShieldArm", PrimitiveType.Cube,
            new Vector3(-0.18f, 0.28f, 0), new Vector3(0.06f, 0.18f, 0.1f), armorColor);
        // Shield
        MakePart(shieldArm, "Shield", PrimitiveType.Cube,
            new Vector3(-0.4f, 0f, 0), new Vector3(0.6f, 1.2f, 0.8f), darkArmor);
        // Shield emblem
        MakePart(shieldArm, "Emblem", PrimitiveType.Sphere,
            new Vector3(-0.45f, 0f, -0.35f), new Vector3(0.3f, 0.3f, 0.1f), plume);

        // Sword arm (right side, rotates for attack)
        swordArm = new GameObject("SwordArm");
        swordArm.transform.SetParent(modelRoot.transform, false);
        swordArm.transform.localPosition = new Vector3(0.16f, 0.32f, 0);

        // Arm
        MakePart(swordArm, "Arm", PrimitiveType.Cube,
            new Vector3(0.06f, 0, 0), new Vector3(0.14f, 0.07f, 0.08f), armorColor);

        // Sword handle
        MakePart(swordArm, "Handle", PrimitiveType.Cube,
            new Vector3(0.16f, 0, 0), new Vector3(0.08f, 0.04f, 0.04f), handleColor);

        // Sword guard (crossguard)
        MakePart(swordArm, "Guard", PrimitiveType.Cube,
            new Vector3(0.21f, 0, 0), new Vector3(0.02f, 0.1f, 0.06f), darkArmor);

        // Sword blade
        MakePart(swordArm, "Blade", PrimitiveType.Cube,
            new Vector3(0.34f, 0.01f, 0), new Vector3(0.22f, 0.035f, 0.02f), swordColor);

        // Blade tip
        MakePart(swordArm, "BladeTip", PrimitiveType.Cube,
            new Vector3(0.46f, 0.01f, 0), new Vector3(0.04f, 0.025f, 0.015f), swordColor);
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
