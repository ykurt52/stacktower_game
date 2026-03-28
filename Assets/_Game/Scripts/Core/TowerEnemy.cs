using UnityEngine;

/// <summary>
/// Moving enemy on a platform. Patrols left/right. Touching kills the character.
/// </summary>
public class TowerEnemy : MonoBehaviour
{
    private float moveSpeed = 1.0f;
    private float leftBound;
    private float rightBound;
    private int direction = 1;
    private GameObject modelRoot;
    private int damage = 1;
    private int hp = 2;
    private float fallVelocity;
    private bool isFalling;
    private float spawnGrace = 0.5f;

    public int Damage => damage;

    public void TakeHit(int dmg)
    {
        hp -= dmg;
        if (hp <= 0)
        {
            if (VFXManager.Instance != null) VFXManager.Instance.PlayDeath(transform.position);
            if (ScoreManager.Instance != null) ScoreManager.Instance.AddScore(2);
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
        // Store bounds in local space so patrol works on moving platforms
        float localCenterX = transform.parent != null ? transform.localPosition.x : platformX;
        leftBound = localCenterX - halfW;
        rightBound = localCenterX + halfW;
        direction = Random.value > 0.5f ? 1 : -1;

        // Kinematic rigidbody for trigger detection
        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Trigger collider
        BoxCollider col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.4f, 0.4f, 0.4f);
        col.center = new Vector3(0, 0.2f, 0);

        CreateVisual();
    }

    private void Update()
    {
        // Gravity: fall if no platform below (skip grace period after spawn)
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

    private void CreateVisual()
    {
        modelRoot = new GameObject("EnemyModel");
        modelRoot.transform.SetParent(transform, false);
        modelRoot.transform.localPosition = Vector3.zero;

        // Try Synty cop model
        if (SyntyAssets.Instance != null && SyntyAssets.Instance.CopPrefab != null)
        {
            SyntyAssets.Spawn(SyntyAssets.Instance.CopPrefab, modelRoot.transform,
                new Vector3(0, 0, 0), 0.3f, 180f);
            return;
        }

        // Fallback: procedural
        Color bodyColor = new Color(0.6f, 0.1f, 0.7f);
        Color eyeColor = Color.white;
        Color angerColor = new Color(0.9f, 0.15f, 0.1f);

        var body = CreatePart(modelRoot, "Body", PrimitiveType.Sphere,
            new Vector3(0, 0.2f, 0), new Vector3(0.35f, 0.3f, 0.35f), bodyColor);

        for (int i = -1; i <= 1; i += 2)
        {
            var eye = CreatePart(body, "Eye", PrimitiveType.Sphere,
                new Vector3(i * 0.25f, 0.15f, -0.35f), Vector3.one * 0.25f, eyeColor);
            CreatePart(eye, "Pupil", PrimitiveType.Sphere,
                new Vector3(0, -0.1f, -0.3f), Vector3.one * 0.5f, angerColor);
            var brow = CreatePart(body, "Brow", PrimitiveType.Cube,
                new Vector3(i * 0.25f, 0.35f, -0.4f), new Vector3(0.3f, 0.06f, 0.1f), angerColor);
            brow.transform.localRotation = Quaternion.Euler(0, 0, i * -20f);
        }

        for (int i = -1; i <= 1; i++)
        {
            CreatePart(body, "Spike", PrimitiveType.Sphere,
                new Vector3(i * 0.15f, 0.45f, 0), new Vector3(0.08f, 0.15f, 0.08f), angerColor);
        }
    }

    private GameObject CreatePart(GameObject parent, string name, PrimitiveType type,
        Vector3 pos, Vector3 scale, Color color)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = pos;
        obj.transform.localScale = scale;
        var partCol = obj.GetComponent<Collider>();
        if (partCol != null) { partCol.enabled = false; Destroy(partCol); }

        var rend = obj.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;

        return obj;
    }
}
