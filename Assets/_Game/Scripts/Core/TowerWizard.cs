using UnityEngine;

/// <summary>
/// Wizard enemy -- stands on a platform and periodically places magic trap circles
/// at the character's position. Traps deal damage when stepped on.
/// Contact damage: 1. Trap damage: 2.
/// </summary>
public class TowerWizard : MonoBehaviour
{
    private float trapInterval = 3f;
    private float trapTimer;
    private float detectionRange = 6f;
    private int contactDamage = 1;
    private int trapDamage = 2;
    private GameObject modelRoot;
    private TowerCharacter target;
    private int facingDir = -1;
    private float castAnimTimer;
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
        trapTimer = trapInterval * 0.6f;

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

        // Cast animation
        if (castAnimTimer > 0)
        {
            castAnimTimer -= Time.deltaTime;
            // Bob up/down during cast
            if (modelRoot != null)
            {
                float bob = Mathf.Sin(castAnimTimer * 20f) * 0.03f;
                modelRoot.transform.localPosition = new Vector3(0, bob, 0);
            }
        }

        // Trap timer
        trapTimer -= Time.deltaTime;
        if (trapTimer <= 0)
        {
            trapTimer = trapInterval;
            PlaceTrap();
        }
    }

    private void PlaceTrap()
    {
        if (target == null) return;

        castAnimTimer = 0.5f;

        // Target position: where the trap will land
        Vector3 trapPos = target.transform.position;
        trapPos.y = Mathf.Round(trapPos.y / 1.8f) * 1.8f + 0.1f;

        // Spawn a projectile that flies from staff to target, then spawns trap on arrival
        Vector3 staffPos = transform.position + new Vector3(facingDir * 0.2f, 0.68f, 0);
        GameObject projObj = new GameObject("WizardProjectile");
        var proj = projObj.AddComponent<TowerWizardProjectile>();
        proj.Init(staffPos, trapPos, trapDamage);

        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayJump(staffPos);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayPowerup();
    }

    private void CreateVisual()
    {
        modelRoot = new GameObject("WizardModel");
        modelRoot.transform.SetParent(transform, false);
        modelRoot.transform.localPosition = Vector3.zero;

        // No Synty wizard model -- use procedural (looks like actual wizard)
        Color robeColor = new Color(0.25f, 0.1f, 0.5f);
        Color hatColor = new Color(0.2f, 0.08f, 0.4f);
        Color skinColor = new Color(0.85f, 0.75f, 0.6f);
        Color staffColor = new Color(0.5f, 0.3f, 0.1f);
        Color gemColor = new Color(0.5f, 1f, 0.5f);
        Color starColor = new Color(1f, 0.9f, 0.2f);

        // Robe body (wider at bottom)
        MakePart(modelRoot, "Robe", PrimitiveType.Cube,
            new Vector3(0, 0.18f, 0), new Vector3(0.28f, 0.3f, 0.18f), robeColor);
        MakePart(modelRoot, "RobeBottom", PrimitiveType.Cube,
            new Vector3(0, 0.04f, 0), new Vector3(0.34f, 0.08f, 0.22f), robeColor);

        // Head
        var head = MakePart(modelRoot, "Head", PrimitiveType.Sphere,
            new Vector3(0, 0.45f, 0), new Vector3(0.18f, 0.18f, 0.18f), skinColor);

        // Wizard hat (cone-like)
        MakePart(head, "HatBrim", PrimitiveType.Cube,
            new Vector3(0, 0.15f, 0), new Vector3(1.6f, 0.1f, 1.4f), hatColor);
        MakePart(head, "HatMid", PrimitiveType.Cube,
            new Vector3(0, 0.45f, 0), new Vector3(1f, 0.5f, 0.9f), hatColor);
        MakePart(head, "HatTop", PrimitiveType.Cube,
            new Vector3(0.1f, 0.85f, 0), new Vector3(0.5f, 0.4f, 0.5f), hatColor);

        // Star on hat
        MakePart(head, "Star", PrimitiveType.Sphere,
            new Vector3(0, 0.3f, -0.42f), Vector3.one * 0.15f, starColor);

        // Eyes
        for (int i = -1; i <= 1; i += 2)
        {
            var eye = MakePart(head, "Eye", PrimitiveType.Sphere,
                new Vector3(i * 0.22f, -0.05f, -0.4f), Vector3.one * 0.18f, Color.white);
            MakePart(eye, "Pupil", PrimitiveType.Sphere,
                new Vector3(0, 0, -0.35f), Vector3.one * 0.5f, new Color(0.3f, 0.1f, 0.5f));
        }

        // Beard
        MakePart(head, "Beard", PrimitiveType.Cube,
            new Vector3(0, -0.45f, -0.2f), new Vector3(0.4f, 0.4f, 0.2f), new Color(0.8f, 0.8f, 0.85f));

        // Staff (right side)
        MakePart(modelRoot, "Staff", PrimitiveType.Cube,
            new Vector3(0.2f, 0.35f, 0), new Vector3(0.04f, 0.6f, 0.04f), staffColor);
        MakePart(modelRoot, "StaffGem", PrimitiveType.Sphere,
            new Vector3(0.2f, 0.68f, 0), Vector3.one * 0.08f, gemColor);
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
