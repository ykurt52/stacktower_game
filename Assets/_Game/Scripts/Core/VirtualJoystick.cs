using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Floating virtual joystick for mobile. Appears where the player touches.
/// Returns normalized direction on XZ plane.
/// Uses direct touch input as primary method for reliable Android support.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    public static VirtualJoystick Instance { get; private set; }

    [SerializeField] private float handleRange = 60f;
    [SerializeField] private float deadZone = 0.1f;

    private RectTransform background;
    private RectTransform handle;
    private Canvas canvas;
    private Camera uiCamera;
    private RectTransform canvasRect;

    private Vector2 input = Vector2.zero;
    public Vector2 Direction => input;
    public float Magnitude => input.magnitude;
    public bool IsPressed { get; private set; }

    // Touch tracking
    private int activeTouchId = -1;
    private Vector2 touchOrigin;

    private void Awake()
    {
        Instance = this;
    }

    private void OnEnable()
    {
        Instance = this;
    }

    private void Start()
    {
        if (background == null)
        {
            var bgT = transform.Find("JoystickBG");
            if (bgT != null) background = bgT.GetComponent<RectTransform>();
        }
        if (background != null && handle == null)
        {
            var knobT = background.Find("JoystickKnob");
            if (knobT != null) handle = knobT.GetComponent<RectTransform>();
        }
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            canvasRect = canvas.GetComponent<RectTransform>();
        }
        if (background != null)
            background.gameObject.SetActive(false);
    }

    public void Setup(RectTransform bg, RectTransform knob, Canvas parentCanvas)
    {
        background = bg;
        handle = knob;
        canvas = parentCanvas;
        uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        canvasRect = canvas.GetComponent<RectTransform>();
        background.gameObject.SetActive(false);
    }

    public void OnPointerDown(PointerEventData eventData) { }
    public void OnDrag(PointerEventData eventData) { }
    public void OnPointerUp(PointerEventData eventData) { }

    private void Update()
    {
        // Direct touch input (reliable on Android)
        if (Input.touchCount > 0)
        {
            HandleTouchInput();
            return;
        }

        // Mouse fallback (editor + PC)
        if (Input.GetMouseButtonDown(0))
        {
            StartJoystick(Input.mousePosition);
        }
        else if (Input.GetMouseButton(0) && IsPressed)
        {
            UpdateJoystick(Input.mousePosition);
        }
        else if (Input.GetMouseButtonUp(0) && IsPressed)
        {
            StopJoystick();
        }

        // Keyboard fallback for editor testing
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        if (Mathf.Abs(h) > 0.1f || Mathf.Abs(v) > 0.1f)
        {
            input = new Vector2(h, v).normalized;
            IsPressed = true;
        }
    }

    private void HandleTouchInput()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            if (touch.phase == TouchPhase.Began && activeTouchId == -1)
            {
                activeTouchId = touch.fingerId;
                StartJoystick(touch.position);
            }
            else if (touch.fingerId == activeTouchId)
            {
                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    UpdateJoystick(touch.position);
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    StopJoystick();
                    activeTouchId = -1;
                }
            }
        }
    }

    private void StartJoystick(Vector2 screenPos)
    {
        IsPressed = true;
        touchOrigin = screenPos;

        if (background != null && canvasRect != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, uiCamera, out Vector2 localPoint);
            background.anchoredPosition = localPoint;
            background.gameObject.SetActive(true);
            if (handle != null)
                handle.anchoredPosition = Vector2.zero;
        }

        input = Vector2.zero;
    }

    private void UpdateJoystick(Vector2 screenPos)
    {
        if (!IsPressed) return;

        if (background != null)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, screenPos, uiCamera, out Vector2 localPoint);

            Vector2 normalized = localPoint / (background.sizeDelta * 0.5f);
            normalized = Vector2.ClampMagnitude(normalized, 1f);

            if (normalized.magnitude < deadZone)
                normalized = Vector2.zero;

            input = normalized;

            if (handle != null)
                handle.anchoredPosition = normalized * handleRange;
        }
    }

    private void StopJoystick()
    {
        input = Vector2.zero;
        IsPressed = false;

        if (background != null)
            background.gameObject.SetActive(false);
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
    }
}
