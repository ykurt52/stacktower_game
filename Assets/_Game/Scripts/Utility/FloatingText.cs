using UnityEngine;

/// <summary>
/// World-space floating damage/heal text. Pops up, rises, and fades out.
/// Archero-style: big bold number, scales up then shrinks.
/// </summary>
public class FloatingText : MonoBehaviour
{
    private float _lifetime = 0.9f;
    private float _riseSpeed = 1.8f;
    private float _timer;
    private TextMesh _textMesh;
    private Color _startColor;
    private float _startScale;

    public static void Spawn(Vector3 worldPos, string text, Color color, float scale = 1f)
    {
        var obj = new GameObject("DmgText");
        obj.transform.position = worldPos + Vector3.up * 0.3f;

        // Random horizontal offset so multiple hits don't overlap
        obj.transform.position += new Vector3(Random.Range(-0.15f, 0.15f), 0, 0);

        var tm = obj.AddComponent<TextMesh>();
        tm.text = text;
        tm.color = color;
        tm.fontSize = 48;
        tm.characterSize = 0.08f * scale;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;

        // Ensure it renders on top
        var rend = obj.GetComponent<MeshRenderer>();
        rend.sortingOrder = 100;

        var ft = obj.AddComponent<FloatingText>();
        ft._textMesh = tm;
        ft._startColor = color;
        ft._startScale = 0.08f * scale;
    }

    private void Update()
    {
        _timer += Time.deltaTime;
        if (_timer >= _lifetime) { Destroy(gameObject); return; }

        float t = _timer / _lifetime;

        // Rise upward
        transform.position += Vector3.up * _riseSpeed * Time.deltaTime;

        // Billboard: face camera
        Camera cam = Camera.main;
        if (cam != null)
            transform.rotation = cam.transform.rotation;

        // Scale: pop big then shrink
        float s;
        if (t < 0.1f)
            s = Mathf.Lerp(0.6f, 1.4f, t / 0.1f); // pop in
        else if (t < 0.3f)
            s = Mathf.Lerp(1.4f, 1.0f, (t - 0.1f) / 0.2f); // settle
        else
            s = Mathf.Lerp(1.0f, 0.3f, (t - 0.3f) / 0.7f); // shrink out

        _textMesh.characterSize = _startScale * s;

        // Fade out in last 40%
        Color c = _startColor;
        if (t > 0.6f)
            c.a = 1f - (t - 0.6f) / 0.4f;
        _textMesh.color = c;
    }
}
