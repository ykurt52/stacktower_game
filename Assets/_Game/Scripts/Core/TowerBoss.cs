using UnityEngine;

/// <summary>
/// Boss enemy -- large enemy that appears every ~25 floors.
/// Patrols on a wide platform. Character must jump on top to deal hits.
/// Takes multiple hits to defeat. Awards coins on death.
/// Contact damage: 3. Has HP.
/// </summary>
public class TowerBoss : MonoBehaviour
{
    private float moveSpeed = 1.2f;
    private float leftBound;
    private float rightBound;
    private int direction = 1;
    private int contactDamage = 3;
    private int maxHits = 5;
    private int currentHits;
    private int coinReward = 10;
    private float hitCooldown;
    private bool isDead;
    private GameObject modelRoot;
    private GameObject hpBarRoot;
    private GameObject hpBarFill;
    private float platformY;
    private float flashTimer;
    private float fallVelocity;
    private bool isFalling;
    private float spawnGrace = 0.5f;

    public int Damage => contactDamage;
    public bool IsBossDead => isDead;

    public void Init(float platformX, float platformWidth, float platY)
    {
        platformY = platY;
        float halfW = platformWidth / 2f - 0.5f;
        float localCenterX = transform.parent != null ? transform.localPosition.x : platformX;
        leftBound = localCenterX - halfW;
        rightBound = localCenterX + halfW;
        direction = Random.value > 0.5f ? 1 : -1;
        currentHits = 0;

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        BoxCollider col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.7f, 0.8f, 0.5f);
        col.center = new Vector3(0, 0.4f, 0);

        CreateVisual();
        CreateHPBar();
    }

    private void Update()
    {
        if (isDead) return;

        if (spawnGrace > 0) { spawnGrace -= Time.deltaTime; }
        else if (!isFalling && !Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, 0.6f, ~0, QueryTriggerInteraction.Ignore))
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

        hitCooldown -= Time.deltaTime;

        // Patrol
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

        // Flash on recent hit
        if (flashTimer > 0)
        {
            flashTimer -= Time.deltaTime;
            if (modelRoot != null)
            {
                bool flash = Mathf.Sin(flashTimer * 25f) > 0;
                modelRoot.SetActive(!flash || flashTimer <= 0);
            }
        }

        // HP bar faces camera
        if (hpBarRoot != null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                hpBarRoot.transform.rotation = Quaternion.LookRotation(
                    hpBarRoot.transform.position - cam.transform.position);
        }
    }

    /// <summary>
    /// Returns true if the character hit from above (stomp).
    /// </summary>
    public bool IsStompHit(Vector3 characterPos, float characterVelocityY)
    {
        return characterPos.y > transform.position.y + 0.5f && characterVelocityY <= 0;
    }

    public void SetMaxHits(int hits)
    {
        maxHits = hits;
    }

    /// <summary>
    /// Take a hit from a stomp. Returns true if boss dies.
    /// </summary>
    public bool TakeHit()
    {
        if (isDead || hitCooldown > 0) return false;

        currentHits++;
        hitCooldown = 0.5f;
        flashTimer = 0.3f;
        UpdateHPBar();

        FloatingText.Spawn(transform.position + Vector3.up * 0.9f,
            $"HIT {currentHits}/{maxHits}", new Color(1f, 0.8f, 0.2f), 1.2f);

        if (currentHits >= maxHits)
        {
            Die();
            return true;
        }

        return false;
    }

    private void Die()
    {
        isDead = true;

        // Award coins
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.AddCoins(coinReward);

        FloatingText.Spawn(transform.position + Vector3.up * 1.2f,
            $"BOSS DEFEATED!\n+{coinReward} COIN", new Color(1f, 0.85f, 0.1f), 1.8f);

        if (VFXManager.Instance != null)
        {
            VFXManager.Instance.PlayDeath(transform.position + Vector3.up * 0.3f);
            VFXManager.Instance.PlayCombo(transform.position, 10);
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameOver();

        Destroy(gameObject, 0.2f);
    }

    private void FlipModel()
    {
        if (modelRoot != null)
            modelRoot.transform.localScale = new Vector3(direction, 1, 1);
    }

    private void CreateHPBar()
    {
        hpBarRoot = new GameObject("BossHPBar");
        hpBarRoot.transform.SetParent(transform, false);
        hpBarRoot.transform.localPosition = new Vector3(0, 1.0f, 0);

        // Background
        var bg = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bg.name = "HPBg";
        bg.transform.SetParent(hpBarRoot.transform, false);
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localScale = new Vector3(0.7f, 0.08f, 0.02f);
        var bc = bg.GetComponent<Collider>();
        if (bc != null) { bc.enabled = false; Destroy(bc); }
        var bRend = bg.GetComponent<Renderer>();
        var bMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        bMat.color = new Color(0.2f, 0.2f, 0.2f);
        bRend.material = bMat;

        // Fill
        hpBarFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hpBarFill.name = "HPFill";
        hpBarFill.transform.SetParent(hpBarRoot.transform, false);
        hpBarFill.transform.localPosition = new Vector3(0, 0, -0.01f);
        hpBarFill.transform.localScale = new Vector3(0.68f, 0.06f, 0.02f);
        var fc = hpBarFill.GetComponent<Collider>();
        if (fc != null) { fc.enabled = false; Destroy(fc); }
        var fRend = hpBarFill.GetComponent<Renderer>();
        var fMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        fMat.color = new Color(0.9f, 0.2f, 0.1f);
        fRend.material = fMat;
    }

    private void UpdateHPBar()
    {
        if (hpBarFill == null) return;
        float ratio = 1f - (float)currentHits / maxHits;
        hpBarFill.transform.localScale = new Vector3(0.68f * ratio, 0.06f, 0.02f);
        hpBarFill.transform.localPosition = new Vector3(-0.34f * (1f - ratio), 0, -0.01f);

        var rend = hpBarFill.GetComponent<Renderer>();
        if (rend != null)
        {
            if (ratio > 0.5f)
                rend.material.color = Color.Lerp(new Color(1f, 0.8f, 0.1f), new Color(0.9f, 0.2f, 0.1f), (1f - ratio) / 0.5f);
            else
                rend.material.color = Color.Lerp(new Color(0.9f, 0.1f, 0.1f), new Color(1f, 0.8f, 0.1f), ratio / 0.5f);
        }
    }

    private void CreateVisual()
    {
        modelRoot = new GameObject("BossModel");
        modelRoot.transform.SetParent(transform, false);
        modelRoot.transform.localPosition = Vector3.zero;

        // Try Synty female model (large scale = boss)
        if (SyntyAssets.Instance != null && SyntyAssets.Instance.FemalePrefab != null)
        {
            SyntyAssets.Spawn(SyntyAssets.Instance.FemalePrefab, modelRoot.transform,
                new Vector3(0, 0, 0), 0.55f, 180f);
            return;
        }

        // Fallback: procedural boss
        Color bodyColor = new Color(0.4f, 0.15f, 0.15f);
        Color armorColor = new Color(0.3f, 0.1f, 0.1f);
        Color skinColor = new Color(0.6f, 0.85f, 0.4f);
        Color eyeColor = new Color(1f, 0.3f, 0.1f);
        Color hornColor = new Color(0.35f, 0.25f, 0.15f);
        Color clubColor = new Color(0.4f, 0.3f, 0.15f);

        // Large body
        MakePart(modelRoot, "Body", PrimitiveType.Cube,
            new Vector3(0, 0.35f, 0), new Vector3(0.45f, 0.45f, 0.3f), bodyColor);

        // Belly
        MakePart(modelRoot, "Belly", PrimitiveType.Sphere,
            new Vector3(0, 0.3f, -0.02f), new Vector3(0.4f, 0.35f, 0.28f), armorColor);

        // Belt with skull
        MakePart(modelRoot, "Belt", PrimitiveType.Cube,
            new Vector3(0, 0.15f, 0), new Vector3(0.48f, 0.06f, 0.32f), new Color(0.3f, 0.2f, 0.1f));
        MakePart(modelRoot, "BeltSkull", PrimitiveType.Sphere,
            new Vector3(0, 0.15f, -0.16f), new Vector3(0.08f, 0.08f, 0.04f), Color.white);

        // Large head
        var head = MakePart(modelRoot, "Head", PrimitiveType.Sphere,
            new Vector3(0, 0.7f, 0), new Vector3(0.35f, 0.3f, 0.3f), skinColor);

        // Horns
        for (int i = -1; i <= 1; i += 2)
        {
            var horn = MakePart(head, "Horn", PrimitiveType.Cube,
                new Vector3(i * 0.35f, 0.25f, 0), new Vector3(0.08f, 0.2f, 0.08f), hornColor);
            horn.transform.localRotation = Quaternion.Euler(0, 0, i * -25f);
        }

        // Angry eyes
        for (int i = -1; i <= 1; i += 2)
        {
            var eye = MakePart(head, "Eye", PrimitiveType.Sphere,
                new Vector3(i * 0.2f, 0f, -0.4f), Vector3.one * 0.18f, Color.white);
            MakePart(eye, "Pupil", PrimitiveType.Sphere,
                new Vector3(0, -0.1f, -0.3f), Vector3.one * 0.5f, eyeColor);

            // Angry brows
            var brow = MakePart(head, "Brow", PrimitiveType.Cube,
                new Vector3(i * 0.2f, 0.2f, -0.42f), new Vector3(0.25f, 0.06f, 0.08f), new Color(0.3f, 0.2f, 0.1f));
            brow.transform.localRotation = Quaternion.Euler(0, 0, i * -20f);
        }

        // Mouth with fangs
        MakePart(head, "Mouth", PrimitiveType.Cube,
            new Vector3(0, -0.22f, -0.38f), new Vector3(0.35f, 0.1f, 0.1f), new Color(0.3f, 0.05f, 0.05f));
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(head, "Fang", PrimitiveType.Cube,
                new Vector3(i * 0.1f, -0.32f, -0.38f), new Vector3(0.06f, 0.1f, 0.06f), Color.white);
        }

        // Thick legs
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(modelRoot, "Leg", PrimitiveType.Cube,
                new Vector3(i * 0.12f, 0.06f, 0), new Vector3(0.14f, 0.14f, 0.14f), bodyColor);
            MakePart(modelRoot, "Boot", PrimitiveType.Cube,
                new Vector3(i * 0.12f, -0.02f, -0.02f), new Vector3(0.15f, 0.06f, 0.18f),
                new Color(0.25f, 0.15f, 0.08f));
        }

        // Large shoulder pads
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(modelRoot, "Shoulder", PrimitiveType.Sphere,
                new Vector3(i * 0.28f, 0.55f, 0), new Vector3(0.15f, 0.12f, 0.15f), armorColor);
            // Spike on shoulder
            MakePart(modelRoot, "ShoulderSpike", PrimitiveType.Cube,
                new Vector3(i * 0.32f, 0.65f, 0), new Vector3(0.04f, 0.1f, 0.04f), hornColor);
        }

        // Club in right hand
        MakePart(modelRoot, "ClubHandle", PrimitiveType.Cube,
            new Vector3(0.32f, 0.4f, 0), new Vector3(0.06f, 0.35f, 0.06f), clubColor);
        MakePart(modelRoot, "ClubHead", PrimitiveType.Sphere,
            new Vector3(0.32f, 0.62f, 0), new Vector3(0.15f, 0.18f, 0.15f), clubColor);
        // Spikes on club
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(modelRoot, "ClubSpike", PrimitiveType.Cube,
                new Vector3(0.32f + i * 0.08f, 0.65f, 0), new Vector3(0.03f, 0.06f, 0.03f),
                new Color(0.5f, 0.5f, 0.55f));
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
