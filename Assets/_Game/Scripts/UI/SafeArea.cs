using UnityEngine;

/// <summary>
/// Dynamically adjusts a RectTransform to fit within the device's safe area.
/// Attach to a full-screen RectTransform that parents all interactive UI.
/// Background elements should NOT be children of this -- they stretch to screen edges.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeArea : MonoBehaviour
{
    private RectTransform rt;
    private Rect lastSafeArea;
    private Vector2Int lastScreenSize;
    private ScreenOrientation lastOrientation;

    private void Awake()
    {
        rt = GetComponent<RectTransform>();
        ApplySafeArea();
    }

    private void Update()
    {
        if (lastSafeArea != Screen.safeArea
            || lastScreenSize.x != Screen.width
            || lastScreenSize.y != Screen.height
            || lastOrientation != Screen.orientation)
        {
            ApplySafeArea();
        }
    }

    private void ApplySafeArea()
    {
        Rect safeArea = Screen.safeArea;
        lastSafeArea = safeArea;
        lastScreenSize = new Vector2Int(Screen.width, Screen.height);
        lastOrientation = Screen.orientation;

        if (Screen.width <= 0 || Screen.height <= 0) return;

        // Convert safe area from pixel coords to anchor coords (0-1)
        Vector2 anchorMin = safeArea.position;
        Vector2 anchorMax = safeArea.position + safeArea.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
