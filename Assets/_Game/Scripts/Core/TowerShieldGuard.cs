using UnityEngine;

/// <summary>
/// Shield Guard enemy -- patrols with a shield in front.
/// Front contact pushes the character back (no damage).
/// Back contact or jumping on top deals damage TO the guard and destroys it.
/// If the character touches without jumping on top or hitting from behind, they bounce off.
/// Contact damage: 1 (only from behind or sides without shield).
/// </summary>
public class TowerShieldGuard : MonoBehaviour
{
    private float moveSpeed = 0.9f;
    private float leftBound;
    private float rightBound;
    private int direction = 1;
    private int contactDamage = 1;
    private GameObject modelRoot;
    private bool isDead;
    private float fallVelocity;
    private bool isFalling;
    private float spawnGrace = 0.5f;

    public int Damage => contactDamage;
    public int FacingDirection => direction;

    public void Init(float platformX, float platformWidth)
    {
        float halfW = platformWidth / 2f - 0.3f;
        float localCenterX = transform.parent != null ? transform.localPosition.x : platformX;
        leftBound = localCenterX - halfW;
        rightBound = localCenterX + halfW;
        direction = Random.value > 0.5f ? 1 : -1;

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        BoxCollider col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.45f, 0.55f, 0.4f);
        col.center = new Vector3(0, 0.27f, 0);

        CreateVisual();
        FlipModel();
    }

    private void Update()
    {
        if (isDead) return;

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
    }

    private void FlipModel()
    {
        if (modelRoot != null)
            modelRoot.transform.localScale = new Vector3(direction, 1, 1);
    }

    /// <summary>
    /// Returns true if the character hit from the shielded (front) side.
    /// </summary>
    public bool IsShieldedHit(Vector3 characterPos)
    {
        float dx = characterPos.x - transform.position.x;
        // Shield is on the front (facing direction)
        // If character is on the same side as facing direction, it's shielded
        return (dx * direction) > 0;
    }

    public void Die()
    {
        isDead = true;
        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayDeath(transform.position + Vector3.up * 0.3f);
        Destroy(gameObject, 0.1f);
    }

    private void CreateVisual()
    {
        modelRoot = new GameObject("ShieldGuardModel");
        modelRoot.transform.SetParent(transform, false);
        modelRoot.transform.localPosition = Vector3.zero;

        // Try Synty cop model for shield guard
        if (SyntyAssets.Instance != null && SyntyAssets.Instance.CopPrefab != null)
        {
            SyntyAssets.Spawn(SyntyAssets.Instance.CopPrefab, modelRoot.transform,
                new Vector3(0, 0, 0), 0.35f, 180f);
            return;
        }

        // Fallback: procedural shield guard
        Color armorColor = new Color(0.6f, 0.5f, 0.2f);
        Color shieldColor = new Color(0.5f, 0.45f, 0.15f);
        Color skinColor = new Color(1f, 0.82f, 0.62f);
        Color eyeColor = new Color(0.15f, 0.15f, 0.15f);
        Color helmetColor = new Color(0.55f, 0.45f, 0.15f);

        // Body
        MakePart(modelRoot, "Body", PrimitiveType.Cube,
            new Vector3(0, 0.25f, 0), new Vector3(0.24f, 0.26f, 0.16f), armorColor);

        // Head
        var head = MakePart(modelRoot, "Head", PrimitiveType.Sphere,
            new Vector3(0, 0.48f, 0), new Vector3(0.2f, 0.2f, 0.2f), skinColor);

        // Helmet
        MakePart(head, "Helmet", PrimitiveType.Sphere,
            new Vector3(0, 0.2f, 0), new Vector3(1.15f, 0.7f, 1.1f), helmetColor);
        // Helmet nose guard
        MakePart(head, "NoseGuard", PrimitiveType.Cube,
            new Vector3(0, -0.05f, -0.48f), new Vector3(0.12f, 0.35f, 0.08f), helmetColor);

        // Eyes
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(head, "Eye", PrimitiveType.Sphere,
                new Vector3(i * 0.22f, -0.02f, -0.38f), Vector3.one * 0.12f, eyeColor);
        }

        // Legs
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(modelRoot, "Leg", PrimitiveType.Cube,
                new Vector3(i * 0.06f, 0.06f, 0), new Vector3(0.08f, 0.12f, 0.1f), armorColor);
            MakePart(modelRoot, "Boot", PrimitiveType.Cube,
                new Vector3(i * 0.06f, -0.01f, -0.01f), new Vector3(0.09f, 0.04f, 0.13f),
                new Color(0.4f, 0.3f, 0.1f));
        }

        // BIG SHIELD (front side - positive X in local space)
        MakePart(modelRoot, "Shield", PrimitiveType.Cube,
            new Vector3(0.14f, 0.25f, 0), new Vector3(0.06f, 0.35f, 0.3f), shieldColor);
        // Shield emblem
        MakePart(modelRoot, "ShieldEmblem", PrimitiveType.Sphere,
            new Vector3(0.18f, 0.28f, 0), new Vector3(0.06f, 0.1f, 0.1f), new Color(0.8f, 0.15f, 0.1f));

        // Short sword (behind shield)
        MakePart(modelRoot, "Sword", PrimitiveType.Cube,
            new Vector3(-0.12f, 0.22f, 0), new Vector3(0.04f, 0.25f, 0.03f), new Color(0.7f, 0.7f, 0.75f));
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
