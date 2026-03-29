using UnityEngine;

/// <summary>
/// Ice Mage enemy -- stands on a platform, shoots ice projectiles that slow the character.
/// Also freezes the platform it stands on (visual effect + slippery).
/// Contact damage: 1. Ice projectile damage: 1 + slow effect.
/// </summary>
public class TowerIceMage : MonoBehaviour
{
    private float shootInterval = 2.5f;
    private float shootTimer;
    private float detectionRange = 7f;
    private float projectileSpeed = 4f;
    private int contactDamage = 1;
    private int projectileDamage = 1;
    private GameObject modelRoot;
    private TowerCharacter target;
    private int facingDir = -1;
    private float floatTimer;
    private float fallVelocity;
    private bool isFalling;
    private float spawnGrace = 0.5f;
    private int hp = 2;

    public int Damage => contactDamage;

    public void TakeHit(int dmg)
    {
        hp -= dmg;
        if (hp <= 0)
        {
            if (VFXManager.Instance != null) VFXManager.Instance.PlayDeath(transform.position);
            if (ScoreManager.Instance != null) ScoreManager.Instance.AddScore(3);
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

    public void Init(Vector3 position)
    {
        transform.position = position;
        shootTimer = shootInterval * 0.5f;

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        BoxCollider col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.35f, 0.55f, 0.35f);
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

        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist > detectionRange) return;

        // Face character
        float dx = target.transform.position.x - transform.position.x;
        int newDir = dx < 0 ? -1 : 1;
        if (newDir != facingDir)
        {
            facingDir = newDir;
            if (modelRoot != null)
                modelRoot.transform.localScale = new Vector3(facingDir, 1, 1);
        }

        // Float animation
        floatTimer += Time.deltaTime;
        if (modelRoot != null)
        {
            float bob = Mathf.Sin(floatTimer * 2f) * 0.03f;
            modelRoot.transform.localPosition = new Vector3(0, bob, 0);
        }

        // Shoot timer
        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0)
        {
            shootTimer = shootInterval;
            Shoot();
        }
    }

    private void Shoot()
    {
        if (target == null) return;

        Vector3 muzzle = transform.position + new Vector3(facingDir * 0.25f, 0.35f, 0);
        Vector3 dir = (target.transform.position + Vector3.up * 0.3f - muzzle).normalized;

        GameObject projObj = new GameObject("IceProjectile");
        var proj = projObj.AddComponent<TowerIceProjectile>();
        proj.Init(muzzle, dir, projectileSpeed, projectileDamage);

        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayJump(muzzle);
    }

    private void CreateVisual()
    {
        modelRoot = new GameObject("IceMageModel");
        modelRoot.transform.SetParent(transform, false);
        modelRoot.transform.localPosition = Vector3.zero;

        // No Synty ice mage model -- use procedural (looks like actual mage)
        Color robeColor = new Color(0.4f, 0.7f, 0.9f);
        Color darkRobe = new Color(0.2f, 0.4f, 0.6f);
        Color skinColor = new Color(0.7f, 0.8f, 0.9f);
        Color staffColor = new Color(0.6f, 0.85f, 1f);
        Color eyeColor = new Color(0.3f, 0.6f, 1f);
        Color crystalColor = new Color(0.5f, 0.9f, 1f);

        // Robe body
        MakePart(modelRoot, "Robe", PrimitiveType.Cube,
            new Vector3(0, 0.18f, 0), new Vector3(0.26f, 0.28f, 0.16f), robeColor);
        MakePart(modelRoot, "RobeBottom", PrimitiveType.Cube,
            new Vector3(0, 0.04f, 0), new Vector3(0.32f, 0.08f, 0.2f), robeColor);

        // Ice trim
        MakePart(modelRoot, "Trim", PrimitiveType.Cube,
            new Vector3(0, 0.32f, 0), new Vector3(0.27f, 0.03f, 0.17f), crystalColor);

        // Head
        var head = MakePart(modelRoot, "Head", PrimitiveType.Sphere,
            new Vector3(0, 0.45f, 0), new Vector3(0.18f, 0.18f, 0.18f), skinColor);

        // Hood
        MakePart(head, "Hood", PrimitiveType.Sphere,
            new Vector3(0, 0.15f, 0.05f), new Vector3(1.3f, 0.9f, 1.2f), darkRobe);
        MakePart(head, "HoodFront", PrimitiveType.Cube,
            new Vector3(0, 0.2f, -0.4f), new Vector3(1.1f, 0.4f, 0.15f), darkRobe);

        // Glowing blue eyes
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(head, "Eye", PrimitiveType.Sphere,
                new Vector3(i * 0.2f, -0.05f, -0.42f), Vector3.one * 0.14f, eyeColor);
        }

        // Ice staff (right side)
        MakePart(modelRoot, "Staff", PrimitiveType.Cube,
            new Vector3(0.2f, 0.35f, 0), new Vector3(0.035f, 0.55f, 0.035f), new Color(0.6f, 0.6f, 0.7f));

        // Crystal on staff
        var crystal = MakePart(modelRoot, "Crystal", PrimitiveType.Cube,
            new Vector3(0.2f, 0.65f, 0), new Vector3(0.08f, 0.08f, 0.08f), crystalColor);
        crystal.transform.localRotation = Quaternion.Euler(0, 0, 45f);

        // Ice particles around feet
        for (int i = 0; i < 3; i++)
        {
            float angle = i * 120f * Mathf.Deg2Rad;
            float px = Mathf.Cos(angle) * 0.2f;
            float pz = Mathf.Sin(angle) * 0.2f;
            MakePart(modelRoot, "IceParticle", PrimitiveType.Cube,
                new Vector3(px, 0.02f, pz), Vector3.one * 0.04f, crystalColor);
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
        var c = obj.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }

        var rend = obj.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;

        return obj;
    }
}
