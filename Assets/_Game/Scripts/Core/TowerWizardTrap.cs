using UnityEngine;

/// <summary>
/// Magic trap placed by TowerWizard. Glowing circle on ground that damages on contact.
/// Disappears after a few seconds.
/// </summary>
public class TowerWizardTrap : MonoBehaviour
{
    private float lifetime = 4f;
    private float timer;
    private int damage = 2;
    private float pulseTimer;
    private GameObject visual;
    private bool hasHit;

    public void Init(Vector3 position, int dmg)
    {
        transform.position = position;
        damage = dmg;
        timer = 0;

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        BoxCollider col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(0.7f, 0.3f, 0.7f);
        col.center = new Vector3(0, 0.15f, 0);

        CreateVisual();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        pulseTimer += Time.deltaTime;

        // Fade out in last second
        if (timer > lifetime - 1f)
        {
            float alpha = (lifetime - timer);
            if (visual != null)
                visual.transform.localScale = Vector3.one * (0.6f + alpha * 0.2f);
        }

        if (timer > lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Pulse animation
        if (visual != null)
        {
            float pulse = 1f + Mathf.Sin(pulseTimer * 6f) * 0.1f;
            visual.transform.localScale = new Vector3(0.7f * pulse, 0.05f, 0.7f * pulse);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        var character = other.GetComponent<TowerCharacter>();
        if (character != null && !character.IsDead)
        {
            character.TakeDamage(damage, GetComponent<Collider>());
            hasHit = true;

            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayDeath(transform.position);

            Destroy(gameObject, 0.1f);
        }
    }

    private void CreateVisual()
    {
        // Glowing magic circle
        visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "TrapCircle";
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(0.7f, 0.02f, 0.7f);
        var c = visual.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }

        var rend = visual.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.6f, 0.1f, 1f, 0.8f); // Purple glow
        rend.material = mat;

        // Inner ring
        var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        inner.name = "InnerRing";
        inner.transform.SetParent(transform, false);
        inner.transform.localPosition = new Vector3(0, 0.01f, 0);
        inner.transform.localScale = new Vector3(0.4f, 0.02f, 0.4f);
        var ic = inner.GetComponent<Collider>();
        if (ic != null) { ic.enabled = false; Destroy(ic); }

        var iRend = inner.GetComponent<Renderer>();
        var iMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        iMat.color = new Color(1f, 0.4f, 1f, 0.9f); // Bright pink
        iRend.material = iMat;
    }
}
