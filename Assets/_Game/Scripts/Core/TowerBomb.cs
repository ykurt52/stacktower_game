using UnityEngine;

/// <summary>
/// Bomb dropped by TowerBomber. Falls, sits on ground, then explodes with area damage.
/// </summary>
public class TowerBomb : MonoBehaviour
{
    private float fuseTime = 2f;
    private float timer;
    private int damage = 2;
    private float explosionRadius = 1.5f;
    private float fallSpeed = 3f;
    private bool grounded;
    private bool exploded;
    private GameObject visual;
    private GameObject fuseVisual;
    private float flashTimer;

    public void Init(Vector3 position, int dmg)
    {
        transform.position = position;
        damage = dmg;
        timer = 0;

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        CreateVisual();
    }

    private void Update()
    {
        if (exploded) return;

        // Fall until landing
        if (!grounded)
        {
            Vector3 pos = transform.position;
            pos.y -= fallSpeed * Time.deltaTime;

            // Simple ground check via raycast
            RaycastHit hit;
            if (Physics.Raycast(transform.position, Vector3.down, out hit, fallSpeed * Time.deltaTime + 0.1f))
            {
                if (hit.collider.GetComponent<TowerBomb>() == null &&
                    hit.collider.GetComponent<TowerCharacter>() == null)
                {
                    pos.y = hit.point.y + 0.08f;
                    grounded = true;
                }
            }

            transform.position = pos;
        }

        timer += Time.deltaTime;

        // Flash faster as fuse runs out
        if (grounded)
        {
            flashTimer += Time.deltaTime;
            float flashRate = Mathf.Lerp(3f, 15f, timer / fuseTime);
            bool flash = Mathf.Sin(flashTimer * flashRate) > 0;
            if (fuseVisual != null)
            {
                var rend = fuseVisual.GetComponent<Renderer>();
                if (rend != null)
                    rend.material.color = flash ? new Color(1f, 0.2f, 0.1f) : new Color(1f, 0.7f, 0.1f);
            }
        }

        if (timer >= fuseTime)
        {
            Explode();
        }
    }

    private void Explode()
    {
        exploded = true;

        // Check for character in explosion radius
        var character = FindAnyObjectByType<TowerCharacter>();
        if (character != null && !character.IsDead)
        {
            float dist = Vector3.Distance(transform.position, character.transform.position);
            if (dist < explosionRadius)
            {
                character.TakeDamage(damage, GetComponent<Collider>());
            }
        }

        // Explosion VFX
        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayDeath(transform.position);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayHit();

        // Create explosion visual briefly
        var explosion = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        explosion.name = "Explosion";
        explosion.transform.position = transform.position;
        explosion.transform.localScale = Vector3.one * explosionRadius * 2f;
        var col = explosion.GetComponent<Collider>();
        if (col != null) { col.enabled = false; Destroy(col); }
        var eRend = explosion.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 0.5f, 0.1f, 0.6f);
        eRend.material = mat;
        Destroy(explosion, 0.3f);

        Destroy(gameObject);
    }

    private void CreateVisual()
    {
        // Bomb body
        visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visual.name = "BombBody";
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = new Vector3(0, 0.08f, 0);
        visual.transform.localScale = Vector3.one * 0.18f;
        var c = visual.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }

        var rend = visual.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.1f, 0.1f, 0.1f);
        rend.material = mat;

        // Fuse
        fuseVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        fuseVisual.name = "Fuse";
        fuseVisual.transform.SetParent(transform, false);
        fuseVisual.transform.localPosition = new Vector3(0, 0.2f, 0);
        fuseVisual.transform.localScale = new Vector3(0.025f, 0.08f, 0.025f);
        var fc = fuseVisual.GetComponent<Collider>();
        if (fc != null) { fc.enabled = false; Destroy(fc); }

        var fRend = fuseVisual.GetComponent<Renderer>();
        var fMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        fMat.color = new Color(1f, 0.5f, 0.1f);
        fRend.material = fMat;

        // Spark on fuse tip
        var spark = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        spark.name = "Spark";
        spark.transform.SetParent(fuseVisual.transform, false);
        spark.transform.localPosition = new Vector3(0, 0.6f, 0);
        spark.transform.localScale = Vector3.one * 3f;
        var sc = spark.GetComponent<Collider>();
        if (sc != null) { sc.enabled = false; Destroy(sc); }

        var sRend = spark.GetComponent<Renderer>();
        var sMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        sMat.color = new Color(1f, 0.9f, 0.2f);
        sRend.material = sMat;
    }
}
