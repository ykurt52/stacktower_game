using UnityEngine;

/// <summary>
/// Collectible emerald stone. Rarer than blue upgrade stones.
/// Green diamond visual, spins and glows.
/// </summary>
public class TowerEmeraldStone : MonoBehaviour
{
    private float spinSpeed = 200f;
    private float bobSpeed = 3.5f;
    private float bobAmount = 0.14f;
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

        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime);

        Vector3 pos = transform.position;
        pos.y = baseY + Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        transform.position = pos;

        glowTimer += Time.deltaTime;
        float pulse = 0.7f + 0.3f * Mathf.Sin(glowTimer * 4f);
        foreach (Transform child in transform)
        {
            var rend = child.GetComponent<Renderer>();
            if (rend != null)
            {
                Color c = rend.material.color;
                rend.material.color = new Color(c.r, c.g * pulse, c.b, c.a);
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
                ScoreManager.Instance.AddEmerald();
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayStone();
            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayCoinCollect(transform.position);

            FloatingText.Spawn(transform.position + Vector3.up * 0.5f,
                "+1 ZUMRUT", new Color(0.1f, 0.9f, 0.3f), 1f);

            Destroy(gameObject);
        }
    }

    private void CreateVisual()
    {
        Color emeraldColor = new Color(0.05f, 0.7f, 0.25f);
        Color coreColor = new Color(0.3f, 1f, 0.5f);

        // Main gem body — diamond shape via rotated cube
        var gem = GameObject.CreatePrimitive(PrimitiveType.Cube);
        gem.name = "EmeraldBody";
        gem.transform.SetParent(transform, false);
        gem.transform.localPosition = Vector3.zero;
        gem.transform.localRotation = Quaternion.Euler(0, 45f, 45f);
        gem.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
        var gc = gem.GetComponent<Collider>(); if (gc != null) { gc.enabled = false; Destroy(gc); }
        ApplyColor(gem, emeraldColor);

        // Inner glow core
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(transform, false);
        core.transform.localPosition = Vector3.zero;
        core.transform.localScale = new Vector3(0.13f, 0.13f, 0.13f);
        var cc = core.GetComponent<Collider>(); if (cc != null) { cc.enabled = false; Destroy(cc); }
        ApplyColor(core, coreColor);

        // Small sparkle accents
        for (int i = 0; i < 2; i++)
        {
            var sparkle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            sparkle.name = "Sparkle" + i;
            sparkle.transform.SetParent(transform, false);
            sparkle.transform.localPosition = new Vector3(
                (i == 0 ? -0.08f : 0.08f), (i == 0 ? 0.1f : -0.1f), 0);
            sparkle.transform.localRotation = Quaternion.Euler(45f, 0, 45f);
            sparkle.transform.localScale = Vector3.one * 0.05f;
            var sc = sparkle.GetComponent<Collider>(); if (sc != null) { sc.enabled = false; Destroy(sc); }
            ApplyColor(sparkle, new Color(0.5f, 1f, 0.6f));
        }
    }

    private void ApplyColor(GameObject obj, Color color)
    {
        var rend = obj.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;
    }
}
