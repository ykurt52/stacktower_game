using UnityEngine;

/// <summary>
/// Projectile fired by the player's ranged weapon.
/// Flies in a direction, damages the first enemy hit, then destroys itself.
/// </summary>
public class TowerPlayerProjectile : MonoBehaviour
{
    private Vector3 velocity;
    private int damage;
    private float lifetime = 3f;
    private string weaponType;

    public void Init(Vector3 position, Vector3 dir, float speed, int dmg, string type)
    {
        transform.position = position;
        velocity = dir.normalized * speed;
        damage = dmg;
        weaponType = type;

        Rigidbody rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        SphereCollider col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.1f;

        CreateVisual();
    }

    private void Update()
    {
        transform.position += velocity * Time.deltaTime;
        lifetime -= Time.deltaTime;
        if (lifetime <= 0) Destroy(gameObject);

        // Spin for arrows
        if (weaponType == "bow")
            transform.rotation = Quaternion.LookRotation(Vector3.forward, velocity);
    }

    private void OnTriggerEnter(Collider other)
    {
        // Try to damage any enemy type
        int dealt = 0;

        var enemy = other.GetComponent<TowerEnemy>();
        var soldier = other.GetComponent<TowerSoldier>();
        var knight = other.GetComponent<TowerKnight>();
        var bat = other.GetComponent<TowerBat>();
        var wizard = other.GetComponent<TowerWizard>();
        var bomber = other.GetComponent<TowerBomber>();
        var iceMage = other.GetComponent<TowerIceMage>();
        var boss = other.GetComponent<TowerBoss>();
        var shieldGuard = other.GetComponent<TowerShieldGuard>();

        if (enemy != null) { enemy.TakeHit(damage); dealt = damage; }
        else if (soldier != null) { soldier.TakeHit(damage); dealt = damage; }
        else if (knight != null) { knight.TakeHit(damage); dealt = damage; }
        else if (bat != null && !bat.IsDead) { bat.TakeHit(); dealt = damage; }
        else if (wizard != null) { wizard.TakeHit(damage); dealt = damage; }
        else if (bomber != null) { bomber.TakeHit(damage); dealt = damage; }
        else if (iceMage != null) { iceMage.TakeHit(damage); dealt = damage; }
        else if (boss != null && !boss.IsBossDead) { boss.TakeHit(); dealt = damage; }
        else if (shieldGuard != null)
        {
            if (shieldGuard.IsShieldedHit(transform.position))
            {
                // Bounced off shield
                FloatingText.Spawn(transform.position, "BLOCK!", new Color(0.8f, 0.8f, 0.2f), 1f);
            }
            else
            {
                shieldGuard.Die();
                dealt = damage;
            }
        }
        else
        {
            return; // Hit nothing relevant
        }

        if (dealt > 0)
        {
            FloatingText.Spawn(other.transform.position + Vector3.up * 0.5f,
                "-" + dealt, new Color(1f, 1f, 0.3f), 1f);
            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayJump(other.transform.position);
        }

        Destroy(gameObject);
    }

    private void CreateVisual()
    {
        GameObject visual;

        if (weaponType == "bow")
        {
            // Arrow: thin long stick with point
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = new Vector3(0.03f, 0.2f, 0.03f);
            ApplyColor(visual, new Color(0.6f, 0.4f, 0.2f));
            // Arrowhead
            var head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.transform.SetParent(transform, false);
            head.transform.localPosition = new Vector3(0, 0.12f, 0);
            head.transform.localScale = new Vector3(0.05f, 0.06f, 0.05f);
            ApplyColor(head, new Color(0.5f, 0.5f, 0.55f));
            RemoveCollider(head);
        }
        else if (weaponType == "spear")
        {
            // Spear: long thin projectile
            visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = new Vector3(0.03f, 0.18f, 0.03f);
            ApplyColor(visual, new Color(0.7f, 0.7f, 0.75f));
        }
        else
        {
            // Default: energy orb
            visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.transform.SetParent(transform, false);
            visual.transform.localScale = Vector3.one * 0.08f;
            ApplyColor(visual, new Color(1f, 0.9f, 0.3f));
        }

        RemoveCollider(visual);
    }

    private void ApplyColor(GameObject obj, Color color)
    {
        var rend = obj.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;
    }

    private void RemoveCollider(GameObject obj)
    {
        var c = obj.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }
    }
}
