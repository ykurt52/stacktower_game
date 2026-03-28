using UnityEngine;

/// <summary>
/// Unified projectile for both player and enemy attacks. Travels on XZ plane.
/// Supports special weapon behaviors: boomerang (return to owner) and homing (track nearest enemy).
/// </summary>
public class Projectile : MonoBehaviour
{
    public enum Owner { Player, Enemy }

    private Owner owner;
    private Vector3 direction;
    private float speed;
    private int damage;
    private int pierceLeft;
    private int bounceLeft;
    private float lifetime;
    private float slowAmount;
    private float slowDuration;
    private float arenaBoundsX = 7f;
    private float arenaBoundsZ = 7f;

    // Special weapon behaviors
    private bool isBoomerang;
    private Transform boomerangOwner;
    private float boomerangTimer;
    private bool boomerangReturning;

    private bool isHoming;
    private float homingStrength = 5f;

    public void Init(Owner src, Vector3 pos, Vector3 dir, float spd, int dmg,
        int pierce = 0, int bounce = 0, float life = 4f,
        float slow = 0f, float slowDur = 0f)
    {
        owner = src;
        transform.position = pos;
        direction = dir.normalized;
        speed = spd;
        damage = dmg;
        pierceLeft = pierce;
        bounceLeft = bounce;
        lifetime = life;
        slowAmount = slow;
        slowDuration = slowDur;

        // Collider
        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = owner == Owner.Player ? 0.15f : 0.12f;

        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        // Visual
        CreateVisual();
    }

    public void SetBoomerang(Transform ownerTransform)
    {
        isBoomerang = true;
        boomerangOwner = ownerTransform;
        boomerangTimer = 0f;
        boomerangReturning = false;
        lifetime = 6f; // Longer life for return trip
    }

    public void SetHoming()
    {
        isHoming = true;
    }

    private void CreateVisual()
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(transform, false);
        sphere.transform.localPosition = Vector3.zero;

        float size = owner == Owner.Player ? 0.2f : 0.15f;
        sphere.transform.localScale = Vector3.one * size;

        var c = sphere.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }

        var rend = sphere.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = owner == Owner.Player
            ? new Color(1f, 0.9f, 0.2f)
            : new Color(1f, 0.3f, 0.3f);
        mat.SetFloat("_Smoothness", 0.9f);
        rend.material = mat;
    }

    private void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0)
        {
            Destroy(gameObject);
            return;
        }

        // Boomerang: after traveling forward, reverse toward owner
        if (isBoomerang)
        {
            boomerangTimer += Time.deltaTime;
            if (!boomerangReturning && boomerangTimer > 0.4f)
            {
                boomerangReturning = true;
            }

            if (boomerangReturning && boomerangOwner != null)
            {
                Vector3 toOwner = boomerangOwner.position + Vector3.up * 0.5f - transform.position;
                toOwner.y = 0;
                if (toOwner.magnitude < 0.5f)
                {
                    Destroy(gameObject);
                    return;
                }
                direction = Vector3.Lerp(direction, toOwner.normalized, Time.deltaTime * 8f).normalized;
            }
        }

        // Homing: curve toward nearest enemy
        if (isHoming && owner == Owner.Player)
        {
            ArenaEnemy nearest = null;
            float minDist = 12f;
            foreach (var enemy in Object.FindObjectsByType<ArenaEnemy>(FindObjectsSortMode.None))
            {
                if (enemy.IsDead) continue;
                float d = Vector3.Distance(transform.position, enemy.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = enemy;
                }
            }
            if (nearest != null)
            {
                Vector3 toEnemy = nearest.transform.position - transform.position;
                toEnemy.y = 0;
                direction = Vector3.Lerp(direction, toEnemy.normalized, Time.deltaTime * homingStrength).normalized;
            }
        }

        transform.position += direction * speed * Time.deltaTime;

        // Wall bounce
        Vector3 pos = transform.position;
        if (bounceLeft > 0)
        {
            if (Mathf.Abs(pos.x) > arenaBoundsX)
            {
                direction.x = -direction.x;
                pos.x = Mathf.Clamp(pos.x, -arenaBoundsX, arenaBoundsX);
                transform.position = pos;
                bounceLeft--;
            }
            if (Mathf.Abs(pos.z) > arenaBoundsZ)
            {
                direction.z = -direction.z;
                pos.z = Mathf.Clamp(pos.z, -arenaBoundsZ, arenaBoundsZ);
                transform.position = pos;
                bounceLeft--;
            }
        }

        // Out of bounds destroy (skip for boomerang returning)
        if (!boomerangReturning)
        {
            if (Mathf.Abs(pos.x) > arenaBoundsX + 2f || Mathf.Abs(pos.z) > arenaBoundsZ + 2f)
                Destroy(gameObject);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (owner == Owner.Player)
        {
            var enemy = other.GetComponentInParent<ArenaEnemy>();
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(damage, slowAmount, slowDuration);
                if (VFXManager.Instance != null)
                    VFXManager.Instance.PlayHit(transform.position);

                if (pierceLeft > 0)
                {
                    pierceLeft--;
                    return; // Don't destroy, continue through
                }

                // Boomerang projectiles always pierce on the way back
                if (isBoomerang && boomerangReturning)
                    return;

                Destroy(gameObject);
            }
        }
        else if (owner == Owner.Enemy)
        {
            var player = other.GetComponentInParent<ArenaCharacter>();
            if (player != null && !player.IsDead)
            {
                player.TakeDamage(damage);
                Destroy(gameObject);
            }
        }
    }

    public void SetBounds(float x, float z)
    {
        arenaBoundsX = x;
        arenaBoundsZ = z;
    }
}
