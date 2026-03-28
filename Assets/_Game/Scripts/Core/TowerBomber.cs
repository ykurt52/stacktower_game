using UnityEngine;

/// <summary>
/// Bomber enemy — walks on a platform, drops bombs when character is nearby.
/// Bomb explodes after a delay with area damage.
/// Contact damage: 1. Bomb damage: 2.
/// </summary>
public class TowerBomber : MonoBehaviour
{
    private float moveSpeed = 0.7f;
    private float leftBound;
    private float rightBound;
    private int direction = 1;
    private float bombInterval = 3.5f;
    private float bombTimer;
    private float detectionRange = 5f;
    private int contactDamage = 1;
    private int bombDamage = 2;
    private GameObject modelRoot;
    private TowerCharacter target;
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

    public void Init(float platformX, float platformWidth)
    {
        float halfW = platformWidth / 2f - 0.3f;
        float localCenterX = transform.parent != null ? transform.localPosition.x : platformX;
        leftBound = localCenterX - halfW;
        rightBound = localCenterX + halfW;
        direction = Random.value > 0.5f ? 1 : -1;
        bombTimer = bombInterval * 0.5f;

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        BoxCollider col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.4f, 0.5f, 0.4f);
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

        // Patrol
        Patrol();

        // Drop bomb when character is close
        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist > detectionRange) return;

        bombTimer -= Time.deltaTime;
        if (bombTimer <= 0)
        {
            bombTimer = bombInterval;
            DropBomb();
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

    private void DropBomb()
    {
        Vector3 dropPos = transform.position + new Vector3(0, 0.2f, 0);

        GameObject bombObj = new GameObject("Bomb");
        var bomb = bombObj.AddComponent<TowerBomb>();
        bomb.Init(dropPos, bombDamage);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGun();
    }

    private void CreateVisual()
    {
        modelRoot = new GameObject("BomberModel");
        modelRoot.transform.SetParent(transform, false);
        modelRoot.transform.localPosition = Vector3.zero;

        // Try Synty town female model for bomber
        if (SyntyAssets.Instance != null && SyntyAssets.Instance.TownFemalePrefab != null)
        {
            SyntyAssets.Spawn(SyntyAssets.Instance.TownFemalePrefab, modelRoot.transform,
                new Vector3(0, 0, 0), 0.3f, 180f);
            return;
        }

        // Fallback: procedural bomber
        Color bodyColor = new Color(0.15f, 0.15f, 0.15f);
        Color vestColor = new Color(0.7f, 0.2f, 0.1f);
        Color skinColor = new Color(1f, 0.82f, 0.62f);
        Color fuseColor = new Color(1f, 0.5f, 0.1f);

        // Body
        MakePart(modelRoot, "Body", PrimitiveType.Cube,
            new Vector3(0, 0.22f, 0), new Vector3(0.26f, 0.25f, 0.16f), bodyColor);

        // Red vest
        MakePart(modelRoot, "Vest", PrimitiveType.Cube,
            new Vector3(0, 0.26f, -0.01f), new Vector3(0.22f, 0.18f, 0.13f), vestColor);

        // Head
        var head = MakePart(modelRoot, "Head", PrimitiveType.Sphere,
            new Vector3(0, 0.45f, 0), new Vector3(0.2f, 0.2f, 0.2f), skinColor);

        // Bandana
        MakePart(head, "Bandana", PrimitiveType.Cube,
            new Vector3(0, 0.15f, 0), new Vector3(1.1f, 0.3f, 1.05f), vestColor);

        // Crazy eyes
        for (int i = -1; i <= 1; i += 2)
        {
            var eye = MakePart(head, "Eye", PrimitiveType.Sphere,
                new Vector3(i * 0.25f, 0f, -0.4f), Vector3.one * 0.2f, Color.white);
            MakePart(eye, "Pupil", PrimitiveType.Sphere,
                new Vector3(i * 0.15f, -0.1f, -0.35f), Vector3.one * 0.45f, new Color(0.1f, 0.1f, 0.1f));
        }

        // Evil grin
        MakePart(head, "Mouth", PrimitiveType.Cube,
            new Vector3(0, -0.25f, -0.4f), new Vector3(0.45f, 0.08f, 0.1f), new Color(0.8f, 0.2f, 0.1f));

        // Legs
        for (int i = -1; i <= 1; i += 2)
        {
            MakePart(modelRoot, "Leg", PrimitiveType.Cube,
                new Vector3(i * 0.07f, 0.06f, 0), new Vector3(0.08f, 0.12f, 0.1f), bodyColor);
        }

        // Bomb in hand
        MakePart(modelRoot, "HeldBomb", PrimitiveType.Sphere,
            new Vector3(0.18f, 0.3f, 0), new Vector3(0.12f, 0.12f, 0.12f), new Color(0.1f, 0.1f, 0.1f));
        MakePart(modelRoot, "HeldFuse", PrimitiveType.Cube,
            new Vector3(0.18f, 0.38f, 0), new Vector3(0.02f, 0.06f, 0.02f), fuseColor);
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
