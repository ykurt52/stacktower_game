using UnityEngine;

/// <summary>
/// Projectile fired by TowerSoldier. Moves in a direction, damages character on hit.
/// </summary>
public class TowerBullet : MonoBehaviour
{
    private Vector3 velocity;
    private float lifetime = 4f;
    private float timer;
    private int damage = 1;

    public void Init(Vector3 position, Vector3 direction, float speed = 5f, int dmg = 1)
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
    }

    private void OnTriggerEnter(Collider other)
    {
        var character = other.GetComponent<TowerCharacter>();
        if (character != null && !character.IsDead)
        {
            character.TakeDamage(damage, GetComponent<Collider>());
            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayLand(transform.position);
            Destroy(gameObject);
        }
    }

    private void CreateVisual()
    {
        // Glowing orange bullet
        var bullet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bullet.name = "BulletVisual";
        bullet.transform.SetParent(transform, false);
        bullet.transform.localPosition = Vector3.zero;
        bullet.transform.localScale = Vector3.one * 0.15f;
        var col = bullet.GetComponent<Collider>();
        if (col != null) { col.enabled = false; Destroy(col); }

        var rend = bullet.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 0.6f, 0.1f);
        rend.material = mat;

        // Inner glow
        var glow = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        glow.name = "Glow";
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = Vector3.zero;
        glow.transform.localScale = Vector3.one * 0.25f;
        var gc = glow.GetComponent<Collider>();
        if (gc != null) { gc.enabled = false; Destroy(gc); }

        var gRend = glow.GetComponent<Renderer>();
        var gMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        gMat.color = new Color(1f, 0.4f, 0.05f, 0.4f);
        gRend.material = gMat;
    }
}
