using UnityEngine;

/// <summary>
/// Shield pickup -- protects character from one hit.
/// Glowing bubble visual that orbits the character when active.
/// </summary>
public class TowerShield : MonoBehaviour
{
    private bool collected;
    private float spinTimer;

    public void Init(Vector3 position)
    {
        transform.position = position;

        SphereCollider col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.35f;

        CreateVisual();
    }

    private void Update()
    {
        if (collected) return;

        // Float and pulse
        spinTimer += Time.deltaTime;
        transform.Rotate(0, 120f * Time.deltaTime, 0);
        float pulse = 1f + Mathf.Sin(spinTimer * 4f) * 0.1f;
        transform.localScale = Vector3.one * pulse;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;

        TowerCharacter character = other.GetComponent<TowerCharacter>();
        if (character == null || character.IsDead) return;

        collected = true;
        character.ActivateShield();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayPowerup();
        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayShieldCollect(transform.position);

        Destroy(gameObject);
    }

    private void CreateVisual()
    {
        // Try Synty shield model
        if (SyntyAssets.Instance != null && SyntyAssets.Instance.ShieldPrefab != null)
        {
            SyntyAssets.Spawn(SyntyAssets.Instance.ShieldPrefab, transform,
                new Vector3(0, 0.3f, 0), 0.4f, 0f);
            return;
        }

        // Fallback: procedural
        Color shieldColor = new Color(0.3f, 0.7f, 1f, 0.8f);
        Color coreColor = new Color(0.5f, 0.9f, 1f);

        var bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bubble.name = "Bubble";
        bubble.transform.SetParent(transform, false);
        bubble.transform.localPosition = new Vector3(0, 0.3f, 0);
        bubble.transform.localScale = Vector3.one * 0.5f;
        var buc = bubble.GetComponent<Collider>(); if (buc != null) { buc.enabled = false; Destroy(buc); }
        var rend = bubble.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = shieldColor;
        rend.material = mat;

        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "Core";
        core.transform.SetParent(bubble.transform, false);
        core.transform.localScale = Vector3.one * 0.5f;
        var coc = core.GetComponent<Collider>(); if (coc != null) { coc.enabled = false; Destroy(coc); }
        var coreRend = core.GetComponent<Renderer>();
        var coreMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        coreMat.color = coreColor;
        coreRend.material = coreMat;
    }
}
