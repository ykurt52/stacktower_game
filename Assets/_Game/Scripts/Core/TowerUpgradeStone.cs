using UnityEngine;

/// <summary>
/// Collectible upgrade stone. Collected stones are used to enhance weapons (Metin2-style).
/// Spawns on platforms, spins and glows purple.
/// </summary>
public class TowerUpgradeStone : MonoBehaviour
{
    private float spinSpeed = 220f;
    private float bobSpeed = 4f;
    private float bobAmount = 0.12f;
    private float baseY;
    private bool collected;
    private float glowTimer;

    public void Init(Vector3 position)
    {
        transform.position = position;
        baseY = position.y;

        SphereCollider col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.35f;

        CreateVisual();
    }

    private void Update()
    {
        if (collected) return;

        // Spin
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime);

        // Bob
        Vector3 pos = transform.position;
        pos.y = baseY + Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        transform.position = pos;

        // Glow pulse on children
        glowTimer += Time.deltaTime;
        float pulse = 0.7f + 0.3f * Mathf.Sin(glowTimer * 5f);
        foreach (Transform child in transform)
        {
            var rend = child.GetComponent<Renderer>();
            if (rend != null)
            {
                Color c = rend.material.color;
                rend.material.color = new Color(c.r * pulse, c.g * pulse, c.b, c.a);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (other.GetComponent<TowerCharacter>() != null)
        {
            collected = true;
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.AddStone();
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayStone();
            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayCoinCollect(transform.position);

            FloatingText.Spawn(transform.position + Vector3.up * 0.5f,
                "+1 TAS", new Color(0.7f, 0.3f, 1f), 1f);

            Destroy(gameObject);
        }
    }

    private void CreateVisual()
    {
        Color stoneColor = new Color(0.6f, 0.2f, 0.9f);
        Color coreColor = new Color(0.9f, 0.5f, 1f);

        // Main gem body — diamond shape via rotated cube
        var gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gem.name = "GemBody";
        gem.transform.SetParent(transform, false);
        gem.transform.localPosition = Vector3.zero;
        gem.transform.localRotation = Quaternion.Euler(0, 45f, 45f);
        gem.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
        var gc = gem.GetComponent<Collider>(); if (gc != null) { gc.enabled = false; Destroy(gc); }
        ApplyColor(gem, stoneColor);

        // Inner glow core
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(transform, false);
        core.transform.localPosition = Vector3.zero;
        core.transform.localScale = new Vector3(0.12f, 0.12f, 0.12f);
        var cc = core.GetComponent<Collider>(); if (cc != null) { cc.enabled = false; Destroy(cc); }
        ApplyColor(core, coreColor);
    }

    private void ApplyColor(GameObject obj, Color color)
    {
        var rend = obj.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;
    }
}
