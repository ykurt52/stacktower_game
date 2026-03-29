using UnityEngine;

/// <summary>
/// Collectible coin that spins and gives bonus points.
/// </summary>
public class TowerCoin : MonoBehaviour
{
    private float spinSpeed = 180f;
    private float bobSpeed = 3f;
    private float bobAmount = 0.15f;
    private float baseY;
    private bool collected;
    private float magnetRange = 5f;
    private float magnetSpeed = 8f;
    private TowerCharacter cachedCharacter;

    public void Init(Vector3 position)
    {
        transform.position = position;
        baseY = position.y;

        // Trigger collider -- radius increased by coin range skill
        float rangeMultiplier = ShopManager.Instance != null ? ShopManager.Instance.GetCoinRangeMultiplier() : 1f;
        SphereCollider col = gameObject.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.3f * rangeMultiplier;

        CreateVisual();
    }

    private void Update()
    {
        if (collected) return;

        // Magnet: attract coin toward character if magnet consumable is active
        if (ShopManager.Instance != null && ShopManager.Instance.IsConsumableActive("magnet"))
        {
            if (cachedCharacter == null) cachedCharacter = FindAnyObjectByType<TowerCharacter>();
            if (cachedCharacter != null && !cachedCharacter.IsDead)
            {
                float dist = Vector3.Distance(transform.position, cachedCharacter.transform.position);
                if (dist < magnetRange)
                {
                    Vector3 dir = (cachedCharacter.transform.position - transform.position).normalized;
                    transform.position += dir * magnetSpeed * Time.deltaTime;
                    baseY = transform.position.y; // update base so bob doesn't fight magnet
                    return; // skip normal bob when being attracted
                }
            }
        }

        // Spin
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime);

        // Bob
        Vector3 pos = transform.position;
        pos.y = baseY + Mathf.Sin(Time.time * bobSpeed) * bobAmount;
        transform.position = pos;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        if (other.GetComponent<TowerCharacter>() != null)
        {
            collected = true;
            if (ScoreManager.Instance != null)
                ScoreManager.Instance.AddCoin();
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlayCoin();
            if (VFXManager.Instance != null)
                VFXManager.Instance.PlayCoinCollect(transform.position);

            Destroy(gameObject);
        }
    }

    private void CreateVisual()
    {
        // Try Synty coin model
        if (SyntyAssets.Instance != null && SyntyAssets.Instance.CoinPrefab != null)
        {
            SyntyAssets.Spawn(SyntyAssets.Instance.CoinPrefab, transform,
                Vector3.zero, 0.4f, 0f);
            return;
        }

        // Fallback: procedural
        Color goldColor = new Color(1f, 0.85f, 0.2f);
        Color goldDark = new Color(0.85f, 0.65f, 0.1f);

        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "CoinDisc";
        disc.transform.SetParent(transform, false);
        disc.transform.localPosition = Vector3.zero;
        disc.transform.localScale = new Vector3(0.35f, 0.04f, 0.35f);
        var dc = disc.GetComponent<Collider>(); if (dc != null) { dc.enabled = false; Destroy(dc); }
        ApplyColor(disc, goldColor);

        var center = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        center.name = "Emblem";
        center.transform.SetParent(disc.transform, false);
        center.transform.localPosition = new Vector3(0, 0.5f, 0);
        center.transform.localScale = new Vector3(0.4f, 2f, 0.4f);
        var cec = center.GetComponent<Collider>(); if (cec != null) { cec.enabled = false; Destroy(cec); }
        ApplyColor(center, goldDark);
    }

    private void ApplyColor(GameObject obj, Color color)
    {
        var rend = obj.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;
    }
}
