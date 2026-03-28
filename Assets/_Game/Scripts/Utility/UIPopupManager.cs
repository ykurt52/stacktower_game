using UnityEngine;
using TMPro;

/// <summary>
/// Shows floating text popups on the screen canvas.
/// Converts world position to screen position for text display.
/// </summary>
public class UIPopupManager : MonoBehaviour
{
    public static UIPopupManager Instance { get; private set; }

    private Canvas canvas;
    private Camera cam;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        cam = Camera.main;
        canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
            canvas = FindAnyObjectByType<Canvas>();
    }

    public void ShowPopup(Vector3 worldPos, string text, Color color, float scale = 1f)
    {
        if (canvas == null || cam == null) return;

        GameObject obj = new GameObject("Popup");
        obj.transform.SetParent(canvas.transform, false);

        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300, 60);

        // Convert world to screen to canvas position
        Vector3 screenPos = cam.WorldToScreenPoint(worldPos + Vector3.up * 0.5f);
        if (screenPos.z < 0) { Destroy(obj); return; }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvas.transform as RectTransform, screenPos, null, out Vector2 canvasPos);
        rt.anchoredPosition = canvasPos;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 28 * scale;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontStyle = FontStyles.Bold;
        tmp.raycastTarget = false;

        obj.AddComponent<UIPopup>();
    }
}

public class UIPopup : MonoBehaviour
{
    private float timer;
    private float lifetime = 1.2f;
    private RectTransform rt;
    private TextMeshProUGUI tmp;

    private void Start()
    {
        rt = GetComponent<RectTransform>();
        tmp = GetComponent<TextMeshProUGUI>();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifetime) { Destroy(gameObject); return; }

        float t = timer / lifetime;

        // Rise
        if (rt != null)
            rt.anchoredPosition += Vector2.up * 120f * Time.deltaTime;

        // Scale pop
        float s = t < 0.15f ? Mathf.Lerp(0.5f, 1.2f, t / 0.15f) : Mathf.Lerp(1.2f, 0.8f, (t - 0.15f) / 0.85f);
        transform.localScale = Vector3.one * s;

        // Fade
        if (tmp != null)
        {
            Color c = tmp.color;
            c.a = 1f - t;
            tmp.color = c;
        }
    }
}
