using UnityEngine;

/// <summary>
/// World-space floating score indicator that rises and fades out.
/// Uses a scaled cube as a simple visual marker -- no TextMesh dependency.
/// For actual text, creates a UI element via the canvas.
/// </summary>
public class FloatingText : MonoBehaviour
{
    private float lifetime = 1.2f;
    private float riseSpeed = 2.5f;
    private float timer;
    private Renderer rend;
    private Color startColor;

    public static void Spawn(Vector3 worldPos, string text, Color color, float scale = 1f)
    {
        // Create a simple rising colored cube as visual indicator
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obj.name = "ScorePopup";
        obj.transform.position = worldPos + Vector3.up * 0.5f;
        obj.transform.localScale = Vector3.one * 0.2f * scale;
        var fc = obj.GetComponent<Collider>(); if (fc != null) { fc.enabled = false; Destroy(fc); }

        var rend = obj.GetComponent<Renderer>();
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = color;
        rend.material = mat;

        var ft = obj.AddComponent<FloatingText>();
        ft.rend = rend;
        ft.startColor = color;

        // Also create screen-space text via UIPopup
        if (UIPopupManager.Instance != null)
            UIPopupManager.Instance.ShowPopup(worldPos, text, color, scale);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifetime) { Destroy(gameObject); return; }

        float t = timer / lifetime;
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        // Scale: pop in then shrink
        float s = t < 0.15f ? Mathf.Lerp(0.5f, 1.3f, t / 0.15f) : Mathf.Lerp(1.3f, 0f, (t - 0.15f) / 0.85f);
        transform.localScale = Vector3.one * 0.2f * s;

        // Fade
        if (rend != null)
        {
            Color c = startColor;
            c.a = 1f - t;
            rend.material.color = c;
        }
    }
}
