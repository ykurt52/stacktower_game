using UnityEngine;

/// <summary>
/// Trampoline/spring pad that launches the character 10 platforms up.
/// Spawns a safe landing platform at the destination.
/// </summary>
public class TowerSpringPad : MonoBehaviour
{
    private float launchForce;
    private int jumpFloors;
    private bool used;
    private float bounceTimer;

    public void Init(Vector3 position, int floors)
    {
        jumpFloors = floors;
        // Scale force proportionally: ~2.2 force per floor
        launchForce = floors * 2.2f;
        transform.position = position;

        // Trigger collider
        BoxCollider col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.8f, 0.4f, 0.8f);
        col.center = new Vector3(0, 0.2f, 0);

        CreateVisual();
    }

    private void Update()
    {
        if (used) return;

        // Idle bounce animation
        bounceTimer += Time.deltaTime * 3f;
        float bounce = Mathf.Sin(bounceTimer) * 0.05f;
        transform.localScale = new Vector3(1, 1 + bounce, 1);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (used) return;

        TowerCharacter character = other.GetComponent<TowerCharacter>();
        if (character == null || character.IsDead) return;

        // Don't activate on early floors
        if (character.LandedFloorCount < 3) return;

        used = true;

        // Launch character high up, award skipped floors on landing
        character.LaunchUp(launchForce, jumpFloors);

        // Squash animation on pad
        StartCoroutine(SquashAndDisappear());

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayJump();
        if (VFXManager.Instance != null)
            VFXManager.Instance.PlaySpringPad(transform.position);

        var cam = Camera.main?.GetComponent<TowerCamera>();
        if (cam != null) cam.Shake(0.25f, 0.2f);
    }

    private System.Collections.IEnumerator SquashAndDisappear()
    {
        // Quick squash
        float t = 0;
        while (t < 0.3f)
        {
            t += Time.deltaTime;
            float s = Mathf.Sin(t / 0.3f * Mathf.PI);
            transform.localScale = new Vector3(1 + s * 0.3f, 1 - s * 0.5f, 1 + s * 0.3f);
            yield return null;
        }

        Destroy(gameObject, 0.5f);
    }

    private void CreateVisual()
    {
        Color padColor = new Color(0.2f, 0.9f, 0.3f);
        Color springColor = new Color(0.7f, 0.7f, 0.7f);
        Color arrowColor = new Color(1f, 1f, 1f);

        // Base
        var basePart = CreatePart("Base", PrimitiveType.Cylinder,
            new Vector3(0, 0.03f, 0), new Vector3(0.6f, 0.03f, 0.6f), springColor);

        // Spring coil (stacked rings)
        for (int i = 0; i < 3; i++)
        {
            CreatePart("Coil" + i, PrimitiveType.Cylinder,
                new Vector3(0, 0.08f + i * 0.05f, 0),
                new Vector3(0.45f - i * 0.05f, 0.015f, 0.45f - i * 0.05f), springColor);
        }

        // Pad top (the bouncy part)
        var pad = CreatePart("Pad", PrimitiveType.Cylinder,
            new Vector3(0, 0.22f, 0), new Vector3(0.55f, 0.04f, 0.55f), padColor);

        // Up arrow on top
        var arrow = CreatePart("Arrow", PrimitiveType.Cube,
            new Vector3(0, 0.28f, 0), new Vector3(0.05f, 0.01f, 0.15f), arrowColor);

        // Arrow head
        CreatePart("ArrowHead", PrimitiveType.Cube,
            new Vector3(0, 0.28f, -0.1f), new Vector3(0.15f, 0.01f, 0.05f), arrowColor);
    }

    private GameObject CreatePart(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Color color)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = name;
        obj.transform.SetParent(transform, false);
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
