using UnityEngine;

/// <summary>
/// Soldier enemy -- stands on a platform and shoots bullets at the character periodically.
/// Contact damage: 1. Bullet damage: 1.
/// </summary>
public class TowerSoldier : MonoBehaviour
{
    private float shootInterval = 2f;
    private float bulletSpeed = 5f;
    private float shootTimer;
    private float detectionRange = 8f;
    private int contactDamage = 1;
    private int bulletDamage = 1;
    private GameObject modelRoot;
    private GameObject gunArm;
    private TowerCharacter target;
    private int facingDir = -1;
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
        shootTimer = shootInterval * 0.5f; // first shot after half interval

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        BoxCollider col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.35f, 0.5f, 0.35f);
        col.center = new Vector3(0, 0.25f, 0);

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

        // Face the character
        float dx = target.transform.position.x - transform.position.x;
        int newDir = dx < 0 ? -1 : 1;
        if (newDir != facingDir)
        {
            facingDir = newDir;
            if (modelRoot != null)
                modelRoot.transform.localScale = new Vector3(facingDir, 1, 1);
        }

        // Aim gun arm toward character
        if (gunArm != null)
        {
            Vector3 toTarget = target.transform.position - gunArm.transform.position;
            float angle = Mathf.Atan2(toTarget.y, toTarget.x * facingDir) * Mathf.Rad2Deg;
            angle = Mathf.Clamp(angle, -45f, 60f);
            gunArm.transform.localRotation = Quaternion.Euler(0, 0, angle);
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

        Vector3 muzzle = transform.position + new Vector3(facingDir * 0.3f, 0.35f, 0);
        Vector3 dir = (target.transform.position + Vector3.up * 0.3f - muzzle).normalized;

        GameObject bulletObj = new GameObject("Bullet");
        var bullet = bulletObj.AddComponent<TowerBullet>();
        bullet.Init(muzzle, dir, bulletSpeed, bulletDamage);

        // Muzzle flash
        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayJump(muzzle);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGun();
    }

    private void CreateVisual()
    {
        modelRoot = new GameObject("SoldierModel");
        modelRoot.transform.SetParent(transform, false);
        modelRoot.transform.localPosition = Vector3.zero;

        // Try Synty cop model for soldier
        if (SyntyAssets.Instance != null && SyntyAssets.Instance.CopPrefab != null)
        {
            SyntyAssets.Spawn(SyntyAssets.Instance.CopPrefab, modelRoot.transform,
                new Vector3(0, 0, 0), 0.3f, 180f);
            // Still need gunArm for aiming
            gunArm = new GameObject("GunArm");
            gunArm.transform.SetParent(modelRoot.transform, false);
            gunArm.transform.localPosition = new Vector3(0.14f, 0.32f, 0);
            return;
        }

        // Fallback: procedural soldier
        Color bodyColor = new Color(0.3f, 0.45f, 0.3f);
        Color helmetColor = new Color(0.25f, 0.35f, 0.2f);
        Color skinColor = new Color(1f, 0.82f, 0.62f);
        Color gunColor = new Color(0.3f, 0.3f, 0.3f);
        Color beltColor = new Color(0.5f, 0.35f, 0.15f);

        // Body
        MakePart(modelRoot, "Body", PrimitiveType.Cube,
            new Vector3(0, 0.25f, 0), new Vector3(0.22f, 0.25f, 0.14f), bodyColor);

        // Belt
        MakePart(modelRoot, "Belt", PrimitiveType.Cube,
            new Vector3(0, 0.14f, 0), new Vector3(0.24f, 0.03f, 0.15f), beltColor);

        // Head
        var head = MakePart(modelRoot, "Head", PrimitiveType.Sphere,
            new Vector3(0, 0.48f, 0), new Vector3(0.2f, 0.2f, 0.2f), skinColor);

        // Helmet
        MakePart(head, "Helmet", PrimitiveType.Sphere,
            new Vector3(0, 0.2f, 0), new Vector3(1.15f, 0.7f, 1.1f), helmetColor);

        // Helmet brim
        MakePart(head, "Brim", PrimitiveType.Cube,
            new Vector3(0, 0.05f, -0.45f), new Vector3(1f, 0.15f, 0.3f), helmetColor);

        // Eyes
        for (int i = -1; i <= 1; i += 2)
        {
            var eye = MakePart(head, "Eye", PrimitiveType.Sphere,
                new Vector3(i * 0.25f, 0f, -0.4f), Vector3.one * 0.18f, Color.white);
            MakePart(eye, "Pupil", PrimitiveType.Sphere,
                new Vector3(0, 0, -0.35f), Vector3.one * 0.5f, new Color(0.15f, 0.15f, 0.15f));
        }

        // Angry mouth
        MakePart(head, "Mouth", PrimitiveType.Cube,
            new Vector3(0, -0.25f, -0.4f), new Vector3(0.4f, 0.08f, 0.1f), new Color(0.2f, 0.1f, 0.1f));

        // Legs
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(modelRoot, "Leg", PrimitiveType.Cube,
                new Vector3(i * 0.06f, 0.06f, 0), new Vector3(0.08f, 0.12f, 0.1f), bodyColor);
            MakePart(modelRoot, "Boot", PrimitiveType.Cube,
                new Vector3(i * 0.06f, -0.01f, -0.01f), new Vector3(0.09f, 0.04f, 0.13f),
                new Color(0.2f, 0.15f, 0.1f));
        }

        // Gun arm (rotates to aim)
        gunArm = new GameObject("GunArm");
        gunArm.transform.SetParent(modelRoot.transform, false);
        gunArm.transform.localPosition = new Vector3(0.14f, 0.32f, 0);

        // Arm
        MakePart(gunArm, "Arm", PrimitiveType.Cube,
            new Vector3(0.06f, 0, 0), new Vector3(0.16f, 0.07f, 0.08f), bodyColor);

        // Gun
        MakePart(gunArm, "Gun", PrimitiveType.Cube,
            new Vector3(0.18f, 0.02f, 0), new Vector3(0.14f, 0.05f, 0.05f), gunColor);

        // Gun barrel
        MakePart(gunArm, "Barrel", PrimitiveType.Cube,
            new Vector3(0.28f, 0.03f, 0), new Vector3(0.08f, 0.025f, 0.025f), gunColor);
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
