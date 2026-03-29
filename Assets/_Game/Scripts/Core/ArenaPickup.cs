using UnityEngine;

/// <summary>
/// Map pickup that spawns between waves: heal, attack speed boost, magnet, shield.
/// Despawns after a timeout or when collected.
/// </summary>
public class ArenaPickup : MonoBehaviour
{
    public enum PickupType { Heal, AttackSpeed, Magnet, Shield, Bomb }

    private PickupType type;
    private float lifetime = 12f;
    private float bobPhase;
    private GameObject visual;
    private bool collected;

    public void Init(PickupType pickupType, Vector3 pos)
    {
        type = pickupType;
        transform.position = new Vector3(pos.x, 0.3f, pos.z);
        bobPhase = Random.value * Mathf.PI * 2f;

        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.5f;

        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        CreateVisual();
    }

    private void CreateVisual()
    {
        visual = new GameObject("PickupVisual");
        visual.transform.SetParent(transform, false);

        Color color;
        float size = 0.3f;

        switch (type)
        {
            case PickupType.Heal:
                color = new Color(0.2f, 1f, 0.3f); // Green
                CreateCross(visual, color);
                return;
            case PickupType.AttackSpeed:
                color = new Color(1f, 0.6f, 0.1f); // Orange
                break;
            case PickupType.Magnet:
                color = new Color(0.9f, 0.9f, 0.2f); // Yellow
                break;
            case PickupType.Shield:
                color = new Color(0.3f, 0.6f, 1f); // Blue
                break;
            case PickupType.Bomb:
                color = new Color(1f, 0.3f, 0.3f); // Red
                size = 0.35f;
                break;
            default:
                color = Color.white;
                break;
        }

        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphere.transform.SetParent(visual.transform, false);
        sphere.transform.localScale = Vector3.one * size;
        var c = sphere.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }
        var rend = sphere.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.SetFloat("_Smoothness", 0.8f);
        rend.material = mat;

        // Glow ring
        var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.transform.SetParent(visual.transform, false);
        ring.transform.localPosition = new Vector3(0, -0.15f, 0);
        ring.transform.localScale = new Vector3(0.6f, 0.01f, 0.6f);
        var rc = ring.GetComponent<Collider>();
        if (rc != null) { rc.enabled = false; Destroy(rc); }
        var ringRend = ring.GetComponent<Renderer>();
        var ringMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        ringMat.color = new Color(color.r, color.g, color.b, 0.4f);
        ringRend.material = ringMat;
    }

    private void CreateCross(GameObject parent, Color color)
    {
        // Horizontal bar
        var h = GameObject.CreatePrimitive(PrimitiveType.Cube);
        h.transform.SetParent(parent.transform, false);
        h.transform.localScale = new Vector3(0.35f, 0.1f, 0.1f);
        var hc = h.GetComponent<Collider>(); if (hc != null) { hc.enabled = false; Destroy(hc); }
        var hRend = h.GetComponent<Renderer>();
        var hMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        hMat.color = color;
        hRend.material = hMat;

        // Vertical bar
        var v = GameObject.CreatePrimitive(PrimitiveType.Cube);
        v.transform.SetParent(parent.transform, false);
        v.transform.localScale = new Vector3(0.1f, 0.35f, 0.1f);
        var vc = v.GetComponent<Collider>(); if (vc != null) { vc.enabled = false; Destroy(vc); }
        var vRend = v.GetComponent<Renderer>();
        var vMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        vMat.color = color;
        vRend.material = vMat;
    }

    private void Update()
    {
        if (collected) return;

        lifetime -= Time.deltaTime;
        if (lifetime <= 0)
        {
            Destroy(gameObject);
            return;
        }

        // Bob and rotate
        float bob = Mathf.Sin(Time.time * 2.5f + bobPhase) * 0.1f;
        transform.position = new Vector3(transform.position.x, 0.3f + bob, transform.position.z);
        if (visual != null)
            visual.transform.Rotate(0, 90f * Time.deltaTime, 0);

        // Blink when about to expire
        if (lifetime < 3f && visual != null)
            visual.SetActive(Mathf.Sin(lifetime * 8f) > 0);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        var player = other.GetComponentInParent<ArenaCharacter>();
        if (player == null || player.IsDead) return;

        collected = true;
        ApplyEffect(player);

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayPowerup();
        if (VFXManager.Instance != null)
            VFXManager.Instance.PlayCoinCollect(transform.position);

        Destroy(gameObject);
    }

    private void ApplyEffect(ArenaCharacter player)
    {
        switch (type)
        {
            case PickupType.Heal:
                player.HealPercent(0.15f);
                FloatingText.Spawn(player.transform.position + Vector3.up * 1f,
                    "+%15 CAN", new Color(0.2f, 1f, 0.3f), 1f);
                break;

            case PickupType.AttackSpeed:
                player.ApplyAttackSpeedBuff(5f, 0.5f);
                FloatingText.Spawn(player.transform.position + Vector3.up * 1f,
                    "HIZLI ATIS!", new Color(1f, 0.6f, 0.1f), 1f);
                break;

            case PickupType.Magnet:
                player.ApplyMagnetBuff(8f, 5f);
                FloatingText.Spawn(player.transform.position + Vector3.up * 1f,
                    "MIKNATIS!", new Color(0.9f, 0.9f, 0.2f), 1f);
                break;

            case PickupType.Shield:
                player.ApplyShield(Mathf.Max(10, player.MaxShield / 10));
                FloatingText.Spawn(player.transform.position + Vector3.up * 1f,
                    "+KALKAN", new Color(0.3f, 0.6f, 1f), 1f);
                break;

            case PickupType.Bomb:
                // Kill/damage all enemies on screen
                foreach (var enemy in FindObjectsByType<ArenaEnemy>(FindObjectsSortMode.None))
                {
                    if (!enemy.IsDead)
                        enemy.TakeDamage(enemy.MaxHP / 2);
                }
                FloatingText.Spawn(player.transform.position + Vector3.up * 1f,
                    "BOMBA!", new Color(1f, 0.3f, 0.3f), 1.5f);
                var cam = FindAnyObjectByType<ArenaCamera>();
                if (cam != null) cam.Shake(0.3f, 0.4f);
                break;
        }
    }
}
