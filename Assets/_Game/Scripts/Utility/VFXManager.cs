using UnityEngine;

/// <summary>
/// Spawns simple particle effects for game events.
/// All effects are procedural -- no external assets needed.
/// </summary>
public class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void PlayJump(Vector3 position)
    {
        SpawnBurst(position, 6, new Color(1f, 1f, 1f, 0.6f), 0.15f, 2f);
    }

    public void PlayLand(Vector3 position)
    {
        SpawnBurst(position, 8, new Color(0.8f, 0.7f, 0.5f, 0.8f), 0.2f, 3f);
    }

    public void PlayCombo(Vector3 position, int combo)
    {
        Color c = combo > 10 ? new Color(1f, 0.3f, 0.1f) :
                  combo > 5 ? new Color(1f, 0.8f, 0.1f) :
                  new Color(0.2f, 1f, 0.4f);
        int count = Mathf.Min(6 + combo, 20);
        SpawnBurst(position + Vector3.up * 0.5f, count, c, 0.25f, 5f);
    }

    public void PlayCoinCollect(Vector3 position)
    {
        SpawnBurst(position, 5, new Color(1f, 0.85f, 0.2f), 0.12f, 3f);
    }

    public void PlayHit(Vector3 position)
    {
        SpawnBurst(position, 4, new Color(1f, 0.9f, 0.3f), 0.1f, 3f);
    }

    public void PlayDeath(Vector3 position)
    {
        SpawnBurst(position, 15, new Color(0.9f, 0.2f, 0.1f), 0.2f, 5f);
    }

    public void PlaySpringPad(Vector3 position)
    {
        SpawnBurst(position, 10, new Color(0.2f, 0.9f, 0.3f), 0.2f, 6f);
    }

    public void PlayShieldCollect(Vector3 position)
    {
        SpawnBurst(position, 8, new Color(0.3f, 0.7f, 1f), 0.18f, 4f);
    }

    private void SpawnBurst(Vector3 position, int count, Color color, float size, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
            p.name = "VFX";
            p.transform.position = position;
            p.transform.localScale = Vector3.one * size * Random.Range(0.5f, 1f);
            p.transform.rotation = Random.rotation;
            var vc = p.GetComponent<Collider>(); if (vc != null) { vc.enabled = false; Destroy(vc); }

            var rend = p.GetComponent<Renderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            rend.material = mat;

            var particle = p.AddComponent<VFXParticle>();
            Vector3 dir = new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 1.5f), Random.Range(-0.3f, 0.3f)).normalized;
            particle.Init(dir * speed * Random.Range(0.5f, 1f), color);
        }
    }
}

/// <summary>
/// Simple particle that moves, shrinks, and fades out.
/// </summary>
public class VFXParticle : MonoBehaviour
{
    private Vector3 velocity;
    private Color startColor;
    private float life = 0.6f;
    private float timer;
    private Renderer rend;

    public void Init(Vector3 vel, Color color)
    {
        velocity = vel;
        startColor = color;
        rend = GetComponent<Renderer>();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= life) { Destroy(gameObject); return; }

        float t = timer / life;
        velocity.y -= 12f * Time.deltaTime; // gravity
        transform.position += velocity * Time.deltaTime;
        transform.localScale *= (1f - 2f * Time.deltaTime); // shrink
        transform.Rotate(300f * Time.deltaTime, 200f * Time.deltaTime, 0);

        // Fade
        if (rend != null)
        {
            Color c = startColor;
            c.a = Mathf.Lerp(startColor.a, 0, t);
            rend.material.color = c;
        }
    }
}
