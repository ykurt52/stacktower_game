using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Invisible touch zone that fires a method on TowerManager when pressed.
/// Uses IPointerDownHandler for instant response (no click delay).
/// </summary>
public class TouchZone : MonoBehaviour, IPointerDownHandler
{
    public enum ZoneAction { JumpUp, HopLeft, HopRight }

    [SerializeField] private ZoneAction action;
    [SerializeField] private TextMeshProUGUI hintLabel;

    private static readonly int hideAfterFloor = 5;
    private bool hintVisible = true;
    private float hintPulseTimer;

    private void Start()
    {
        GameManager.Instance.OnGameStart.AddListener(OnGameStart);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnGameStart.RemoveListener(OnGameStart);
    }

    private void OnGameStart()
    {
        // Show hint at game start with full visibility
        hintVisible = true;
        hintPulseTimer = 0;
        if (hintLabel != null)
        {
            hintLabel.gameObject.SetActive(true);
            hintLabel.alpha = 1f;
            hintLabel.fontSize = action == ZoneAction.JumpUp ? 28f : 24f;
            hintLabel.characterSpacing = 4f;
        }
    }

    private void Update()
    {
        if (hintLabel == null || !hintVisible) return;

        var character = FindAnyObjectByType<TowerCharacter>();
        if (character == null) return;

        int floor = Mathf.Max(0, Mathf.FloorToInt(character.HighestLandedY / 1.8f));

        if (floor >= hideAfterFloor)
        {
            // Fade out
            hintLabel.alpha -= Time.deltaTime * 2f;
            if (hintLabel.alpha <= 0f)
            {
                hintLabel.gameObject.SetActive(false);
                hintVisible = false;
            }
        }
        else
        {
            // Pulse animation for visibility
            hintPulseTimer += Time.deltaTime * 2.5f;
            float pulse = 0.7f + Mathf.Sin(hintPulseTimer) * 0.3f;
            hintLabel.alpha = pulse;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        TowerManager manager = FindAnyObjectByType<TowerManager>();
        if (manager == null) return;

        switch (action)
        {
            case ZoneAction.JumpUp:
                manager.OnTapJumpUp();
                break;
            case ZoneAction.HopLeft:
                manager.OnTapHopLeft();
                break;
            case ZoneAction.HopRight:
                manager.OnTapHopRight();
                break;
        }
    }
}
