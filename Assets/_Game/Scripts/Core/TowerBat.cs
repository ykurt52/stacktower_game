using UnityEngine;

/// <summary>
/// Bat enemy — flies in a sine wave pattern between platforms.
/// Dives toward the character when close enough.
/// Contact damage: 1.
/// </summary>
public class TowerBat : MonoBehaviour
{
    private float flySpeed = 1.5f;
    private float sineAmplitude = 1.5f;
    private float sineFrequency = 1.2f;
    private float diveSpeed = 6f;
    private float detectionRange = 4f;
    private float diveRange = 2.5f;
    private int contactDamage = 1;
    private int hitCount;
    private int maxHits = 2;
    private float leftBound;
    private float rightBound;
    private int direction = 1;
    private float baseY;
    private float originalBaseY;
    private float timer;
    private bool isDiving;
    private float diveCooldown;
    private Vector3 diveTarget;
    private GameObject modelRoot;
    private TowerCharacter target;

    public int Damage => contactDamage;
    public bool IsDead => hitCount >= maxHits;

    public void TakeHit()
    {
        hitCount++;
        if (hitCount >= maxHits)
        {
            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayDeath(transform.position);
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.AddScore(3);
            FloatingText.Spawn(transform.position + Vector3.up * 0.3f,
                "+3", new Color(1f, 0.9f, 0.3f), 1.2f);
            Destroy(gameObject, 0.1f);
        }
        else
        {
            // Flash on hit
            if (modelRoot != null)
            {
                modelRoot.SetActive(false);
                Invoke(nameof(ShowModel), 0.1f);
            }
            FloatingText.Spawn(transform.position + Vector3.up * 0.3f,
                $"HIT {hitCount}/{maxHits}", new Color(1f, 0.8f, 0.2f), 1f);
        }
    }

    private void ShowModel()
    {
        if (modelRoot != null) modelRoot.SetActive(true);
    }

    public void Init(Vector3 position, float areaWidth)
    {
        transform.position = position;
        // Store bounds and base in local space for moving platform support
        float localCenterX = transform.parent != null ? transform.localPosition.x : position.x;
        float localCenterY = transform.parent != null ? transform.localPosition.y : position.y;
        baseY = localCenterY;
        originalBaseY = localCenterY;
        leftBound = localCenterX - areaWidth / 2f;
        rightBound = localCenterX + areaWidth / 2f;
        direction = Random.value > 0.5f ? 1 : -1;
        timer = Random.Range(0f, 3f);

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        SphereCollider col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.2f;
        col.center = new Vector3(0, 0.15f, 0);

        CreateVisual();
    }

    private void Update()
    {
        if (target == null || target.IsDead)
        {
            target = FindAnyObjectByType<TowerCharacter>();
            if (target == null) return;
        }

        if (isDiving)
        {
            UpdateDive();
            return;
        }

        diveCooldown -= Time.deltaTime;
        timer += Time.deltaTime;

        // Fly in sine wave (local space for moving platform support)
        Vector3 localPos = transform.localPosition;
        localPos.x += flySpeed * direction * Time.deltaTime;
        localPos.y = baseY + Mathf.Sin(timer * sineFrequency) * sineAmplitude;

        if (localPos.x >= rightBound) { direction = -1; FlipModel(); }
        else if (localPos.x <= leftBound) { direction = 1; FlipModel(); }

        transform.localPosition = localPos;

        // Wing flap animation
        if (modelRoot != null)
        {
            float wingAngle = Mathf.Sin(timer * 12f) * 25f;
            Transform leftWing = modelRoot.transform.Find("LeftWing");
            Transform rightWing = modelRoot.transform.Find("RightWing");
            if (leftWing != null) leftWing.localRotation = Quaternion.Euler(0, 0, wingAngle);
            if (rightWing != null) rightWing.localRotation = Quaternion.Euler(0, 0, -wingAngle);
        }

        // Check for dive (use world position for distance to character)
        if (diveCooldown <= 0)
        {
            float dist = Vector3.Distance(transform.position, target.transform.position);
            if (dist < diveRange)
            {
                isDiving = true;
                diveTarget = target.transform.position + Vector3.up * 0.3f;
            }
        }
    }

    private void UpdateDive()
    {
        // Dive uses world position since target is in world space
        Vector3 dir = (diveTarget - transform.position).normalized;
        transform.position += dir * diveSpeed * Time.deltaTime;

        float dist = Vector3.Distance(transform.position, diveTarget);
        if (dist < 0.3f || transform.position.y < diveTarget.y - 1f)
        {
            // Return to original patrol height (local space)
            isDiving = false;
            diveCooldown = 2f;
            baseY = originalBaseY;
            // Restore local Y so sine wave resumes from correct height
            Vector3 localPos = transform.localPosition;
            localPos.y = originalBaseY;
            transform.localPosition = localPos;
            timer = 0;
        }
    }

    private void FlipModel()
    {
        if (modelRoot != null)
            modelRoot.transform.localScale = new Vector3(direction, 1, 1);
    }

    private void CreateVisual()
    {
        Color bodyColor = new Color(0.2f, 0.1f, 0.25f);   // Dark purple
        Color wingColor = new Color(0.3f, 0.15f, 0.35f);  // Purple wings
        Color eyeColor = new Color(1f, 0.2f, 0.1f);       // Red eyes
        Color earColor = new Color(0.25f, 0.12f, 0.3f);

        modelRoot = new GameObject("BatModel");
        modelRoot.transform.SetParent(transform, false);
        modelRoot.transform.localPosition = Vector3.zero;

        // Body (small sphere)
        var body = MakePart(modelRoot, "Body", PrimitiveType.Sphere,
            new Vector3(0, 0.15f, 0), new Vector3(0.2f, 0.18f, 0.15f), bodyColor);

        // Head
        var head = MakePart(modelRoot, "Head", PrimitiveType.Sphere,
            new Vector3(0, 0.28f, -0.02f), new Vector3(0.16f, 0.14f, 0.14f), bodyColor);

        // Ears
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(head, "Ear", PrimitiveType.Cube,
                new Vector3(i * 0.3f, 0.45f, 0), new Vector3(0.2f, 0.35f, 0.1f), earColor);
        }

        // Red eyes
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(head, "Eye", PrimitiveType.Sphere,
                new Vector3(i * 0.25f, 0f, -0.4f), Vector3.one * 0.15f, eyeColor);
        }

        // Fangs
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(head, "Fang", PrimitiveType.Cube,
                new Vector3(i * 0.12f, -0.35f, -0.35f), new Vector3(0.08f, 0.18f, 0.08f), Color.white);
        }

        // Left wing
        var leftWing = new GameObject("LeftWing");
        leftWing.transform.SetParent(modelRoot.transform, false);
        leftWing.transform.localPosition = new Vector3(-0.12f, 0.18f, 0);
        MakePart(leftWing, "Wing1", PrimitiveType.Cube,
            new Vector3(-0.12f, 0, 0), new Vector3(0.22f, 0.04f, 0.12f), wingColor);
        MakePart(leftWing, "Wing2", PrimitiveType.Cube,
            new Vector3(-0.25f, -0.03f, 0), new Vector3(0.12f, 0.03f, 0.1f), wingColor);

        // Right wing
        var rightWing = new GameObject("RightWing");
        rightWing.transform.SetParent(modelRoot.transform, false);
        rightWing.transform.localPosition = new Vector3(0.12f, 0.18f, 0);
        MakePart(rightWing, "Wing1", PrimitiveType.Cube,
            new Vector3(0.12f, 0, 0), new Vector3(0.22f, 0.04f, 0.12f), wingColor);
        MakePart(rightWing, "Wing2", PrimitiveType.Cube,
            new Vector3(0.25f, -0.03f, 0), new Vector3(0.12f, 0.03f, 0.1f), wingColor);

        // Tiny feet
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(modelRoot, "Foot", PrimitiveType.Sphere,
                new Vector3(i * 0.05f, 0.04f, 0), new Vector3(0.04f, 0.04f, 0.04f), bodyColor);
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
