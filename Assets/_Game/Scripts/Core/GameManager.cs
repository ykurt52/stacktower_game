using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Owns all game state transitions. No other script may change CurrentState directly.
/// </summary>
public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        MENU,
        PLAYING,
        GAME_OVER
    }

    public static GameManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float minBlockWidth = 0.15f;

    [Header("Events")]
    public UnityEvent OnGameStart;
    public UnityEvent OnGameOver;
    public UnityEvent OnReturnToMenu;

    public GameState CurrentState { get; private set; } = GameState.MENU;
    public float MinBlockWidth => minBlockWidth;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Transitions to PLAYING state and fires OnGameStart.
    /// </summary>
    public void StartGame()
    {
        if (CurrentState == GameState.PLAYING) return;
        CurrentState = GameState.PLAYING;
        OnGameStart?.Invoke();
    }

    /// <summary>
    /// Transitions to GAME_OVER state and fires OnGameOver.
    /// </summary>
    public void TriggerGameOver()
    {
        if (CurrentState != GameState.PLAYING) return;
        CurrentState = GameState.GAME_OVER;
        OnGameOver?.Invoke();

        if (AdManager.Instance != null)
            AdManager.Instance.ShowInterstitial();

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayGameOver();
    }

    /// <summary>
    /// Transitions to MENU state and fires OnReturnToMenu.
    /// </summary>
    public void ReturnToMenu()
    {
        CurrentState = GameState.MENU;
        OnReturnToMenu?.Invoke();
    }
}
