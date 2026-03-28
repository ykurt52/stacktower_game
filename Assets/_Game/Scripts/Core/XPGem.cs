using UnityEngine;

/// <summary>
/// XP gem dropped by enemies. Floats toward player when nearby.
/// </summary>
public class XPGem : MonoBehaviour
{
    private int xpValue;
    private float magnetRange = 2.5f;
    private float magnetSpeed = 12f;
    private float collectRange = 0.5f;
    private bool collecting;
    private float lifetime = 20f;
    private ArenaCharacter player;

    public void Init(Vector3 pos, int xp)
    {
        xpValue = xp;
        transform.position = pos + Vector3.up * 0.2f;

        var col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.3f;

        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        CreateVisual(xp);
    }

    private void CreateVisual(int xp)
    {
        var gem = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        gem.transform.SetParent(transform, false);
        gem.transform.localPosition = Vector3.zero;

        float size;
        Color color;
        if (xp >= 5)
        {
            size = 0.25f;
            color = new Color(0.7f, 0.3f, 1f); // Purple - large
        }
        else if (xp >= 3)
        {
            size = 0.2f;
            color = new Color(0.3f, 0.5f, 1f); // Blue - medium
        }
        else
        {
            size = 0.15f;
            color = new Color(0.3f, 1f, 0.4f); // Green - small
        }

        gem.transform.localScale = Vector3.one * size;

        var c = gem.GetComponent<Collider>();
        if (c != null) { c.enabled = false; Destroy(c); }

        var rend = gem.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        mat.SetFloat("_Smoothness", 0.8f);
        rend.material = mat;
    }

    private void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0) { Destroy(gameObject); return; }

        if (player == null)
        {
            player = FindAnyObjectByType<ArenaCharacter>();
            if (player == null) return;
        }

        // Bob animation
        float bob = Mathf.Sin(Time.time * 3f + transform.position.x) * 0.05f;
        transform.position = new Vector3(transform.position.x, 0.2f + bob, transform.position.z);

        // Magnet toward player
        float dist = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(player.transform.position.x, 0, player.transform.position.z));

        float actualMagnetRange = magnetRange;
        if (ArenaManager.Instance != null)
            actualMagnetRange += ArenaManager.Instance.BonusMagnetRange;

        if (dist < actualMagnetRange || collecting)
        {
            collecting = true;
            Vector3 dir = (player.transform.position - transform.position).normalized;
            transform.position += dir * magnetSpeed * Time.deltaTime;
        }

        // Collect
        if (dist < collectRange)
        {
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.AddXP(xpValue);
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayCoin();
            Destroy(gameObject);
        }
    }
}
