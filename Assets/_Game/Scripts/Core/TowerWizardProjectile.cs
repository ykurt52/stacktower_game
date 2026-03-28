using UnityEngine;

/// <summary>
/// Magic projectile fired by TowerWizard. Flies in an arc from wizard to target position.
/// Shows a warning circle at the landing spot. Spawns a trap on arrival.
/// </summary>
public class TowerWizardProjectile : MonoBehaviour
{
    private Vector3 startPos;
    private Vector3 targetPos;
    private int trapDamage;
    private float flightDuration = 0.8f;
    private float warningDuration = 0.6f;
    private float timer;
    private GameObject orb;
    private GameObject warningCircle;
    private GameObject warningInner;
    private bool arrived;

    public void Init(Vector3 from, Vector3 to, int damage)
    {
        startPos = from;
        targetPos = to;
        trapDamage = damage;
        transform.position = from;

        CreateOrb();
        CreateWarningCircle();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (!arrived)
        {
            // Fly in an arc
            float t = Mathf.Clamp01(timer / flightDuration);
            Vector3 pos = Vector3.Lerp(startPos, targetPos, t);
            // Arc height
            float arc = Mathf.Sin(t * Mathf.PI) * 2f;
            pos.y += arc;
            transform.position = pos;

            // Spin and pulse the orb
            if (orb != null)
            {
                orb.transform.Rotate(0, 600f * Time.deltaTime, 0);
                float scale = 0.12f + Mathf.Sin(timer * 15f) * 0.03f;
                orb.transform.localScale = Vector3.one * scale;
            }

            // Pulse warning circle
            if (warningCircle != null)
            {
                float pulse = 0.5f + t * 0.3f + Mathf.Sin(timer * 10f) * 0.05f;
                warningCircle.transform.localScale = new Vector3(pulse, 0.01f, pulse);
                if (warningInner != null)
                    warningInner.transform.localScale = new Vector3(pulse * 0.5f, 0.01f, pulse * 0.5f);
            }

            if (t >= 1f)
            {
                arrived = true;
                // Destroy orb
                if (orb != null) Destroy(orb);

                // Spawn the actual trap
                GameObject trapObj = new GameObject("WizardTrap");
                var trap = trapObj.AddComponent<TowerWizardTrap>();
                trap.Init(targetPos, trapDamage);

                // Destroy warning
                if (warningCircle != null) Destroy(warningCircle);
                if (warningInner != null) Destroy(warningInner);

                // Impact VFX
                if (VFXManager.Instance != null)
                    VFXManager.Instance.PlayDeath(targetPos);

                Destroy(gameObject);
            }
        }
    }

    private void CreateOrb()
    {
        orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "MagicOrb";
        orb.transform.SetParent(transform, false);
        orb.transform.localPosition = Vector3.zero;
        orb.transform.localScale = Vector3.one * 0.12f;
        var c = orb.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }

        var rend = orb.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.7f, 0.2f, 1f); // Purple magic
        rend.material = mat;

        // Glow core
        var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        core.name = "OrbCore";
        core.transform.SetParent(orb.transform, false);
        core.transform.localPosition = Vector3.zero;
        core.transform.localScale = Vector3.one * 0.6f;
        var cc = core.GetComponent<Collider>();
        if (cc != null) { cc.enabled = false; Destroy(cc); }

        var coreRend = core.GetComponent<Renderer>();
        var coreMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        coreMat.color = new Color(1f, 0.6f, 1f); // Bright pink core
        coreRend.material = coreMat;

        // Trail particles (small cubes trailing behind)
        for (int i = 0; i < 3; i++)
        {
            var trail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trail.name = "Trail" + i;
            trail.transform.SetParent(orb.transform, false);
            trail.transform.localPosition = new Vector3(0, 0, 0.3f + i * 0.15f);
            trail.transform.localScale = Vector3.one * (0.3f - i * 0.08f);
            var tc = trail.GetComponent<Collider>();
            if (tc != null) { tc.enabled = false; Destroy(tc); }

            var tRend = trail.GetComponent<Renderer>();
            var tMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            tMat.color = new Color(0.5f, 0.1f, 0.8f, 0.6f);
            tRend.material = tMat;
        }
    }

    private void CreateWarningCircle()
    {
        // Warning circle at landing position — so the player can see where the trap will land
        warningCircle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        warningCircle.name = "WarningCircle";
        warningCircle.transform.position = targetPos;
        warningCircle.transform.localScale = new Vector3(0.5f, 0.01f, 0.5f);
        var c = warningCircle.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }

        var rend = warningCircle.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 0.2f, 0.2f, 0.5f); // Red warning
        rend.material = mat;

        // Inner ring
        warningInner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        warningInner.name = "WarningInner";
        warningInner.transform.position = targetPos + new Vector3(0, 0.005f, 0);
        warningInner.transform.localScale = new Vector3(0.25f, 0.01f, 0.25f);
        var ic = warningInner.GetComponent<Collider>();
        if (ic != null) { ic.enabled = false; Destroy(ic); }

        var iRend = warningInner.GetComponent<Renderer>();
        var iMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        iMat.color = new Color(1f, 0.5f, 0.1f, 0.7f); // Orange inner
        iRend.material = iMat;
    }
}
