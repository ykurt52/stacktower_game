using UnityEngine;

/// <summary>
/// Ice projectile fired by TowerIceMage. Damages and slows character on hit.
/// </summary>
public class TowerIceProjectile : MonoBehaviour
{
    private Vector3 velocity;
    private float lifetime = 4f;
    private float timer;
    private int damage = 1;
    private float slowDuration = 2f;
    private float slowMultiplier = 0.5f;

    public void Init(Vector3 position, Vector3 direction, float speed = 4f, int dmg = 1)
    {
        transform.position = position;
        velocity = direction.normalized * speed;
        damage = dmg;
        timer = 0;

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        SphereCollider col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.12f;

        CreateVisual();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer > lifetime)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += velocity * Time.deltaTime;

        // Spin
        transform.Rotate(0, 0, 360f * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        var character = other.GetComponent<TowerCharacter>();
        if (character != null && !character.IsDead)
        {
            character.TakeDamage(damage, GetComponent<Collider>());
            character.ApplySlow(slowDuration, slowMultiplier);

            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayLand(transform.position);

            Destroy(gameObject);
        }
    }

    private void CreateVisual()
    {
        // Ice crystal projectile
        var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
        crystal.name = "IceCrystal";
        crystal.transform.SetParent(transform, false);
        crystal.transform.localPosition = Vector3.zero;
        crystal.transform.localScale = Vector3.one * 0.12f;
        crystal.transform.localRotation = Quaternion.Euler(45, 45, 0);
        var c = crystal.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }

        var rend = crystal.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(0.5f, 0.85f, 1f);
        rend.material = mat;

        // Glow
        var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        glow.name = "Glow";
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = Vector3.zero;
        glow.transform.localScale = Vector3.one * 0.2f;
        var gc = glow.GetComponent<Collider>();
        if (gc != null) { gc.enabled = false; Destroy(gc); }

        var gRend = glow.GetComponent<Renderer>();
        var gMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        gMat.color = new Color(0.3f, 0.6f, 1f, 0.4f);
        gRend.material = gMat;
    }
}
