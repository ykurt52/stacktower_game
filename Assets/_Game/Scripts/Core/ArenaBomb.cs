using UnityEngine;

/// <summary>
/// Timed bomb that explodes after a delay, damaging the player if nearby.
/// </summary>
public class ArenaBomb : MonoBehaviour
{
    private int damage;
    private float fuseTime = 1.5f;
    private float explosionRadius = 1.5f;
    private GameObject visual;

    public void Init(int dmg)
    {
        damage = dmg;
        transform.position = new Vector3(transform.position.x, 0.1f, transform.position.z);

        // Warning visual (red circle on ground)
        visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = new Vector3(explosionRadius * 2f, 0.02f, explosionRadius * 2f);
        var c = visual.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }
        var rend = visual.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = new Color(1f, 0.2f, 0.1f, 0.35f);
        rend.material = mat;
    }

    private void Update()
    {
        fuseTime -= Time.deltaTime;

        // Pulse warning
        if (visual != null)
        {
            float pulse = Mathf.Abs(Mathf.Sin(fuseTime * 6f));
            var rend = visual.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = new Color(1f, 0.2f, 0.1f, 0.2f + pulse * 0.3f);
        }

        if (fuseTime <= 0)
            Explode();
    }

    private void Explode()
    {
        var player = FindAnyObjectByType<ArenaCharacter>();
        if (player != null && !player.IsDead)
        {
            float dist = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(player.transform.position.x, 0, player.transform.position.z));
            if (dist < explosionRadius)
                player.TakeDamage(damage);
        }

        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayDeath(transform.position);
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayExplosion();

        Destroy(gameObject);
    }
}
