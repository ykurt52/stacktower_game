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
        GAME_OVER,
        WAITING_REVIVE  // Waiting for player to watch ad or skip
    }

    public static GameManager Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float minBlockWidth = 0.15f;

    [Header("Events")]
    public UnityEvent OnGameStart;
    public UnityEvent OnGameOver;
    public UnityEvent OnReturnToMenu;
    public UnityEvent OnRevivePrompt;  // Show "watch ad to revive" UI
    public UnityEvent OnRevive;        // Player revived via ad

    public GameState CurrentState { get; private set; } = GameState.MENU;
    public float MinBlockWidth => minBlockWidth;

    private bool hasUsedReviveThisGame;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void StartGame()
    {
        if (CurrentState == GameState.PLAYING) return;
        CurrentState = GameState.PLAYING;
        hasUsedReviveThisGame = false;
        OnGameStart?.Invoke();

        if (AudioManager.Instance != null)
            AudioManager.Instance.StartMusic();
    }

    /// <summary>
    /// Called when player dies. If revive hasn't been used, show revive prompt first.
    /// </summary>
    public void TriggerGameOver()
    {
        if (CurrentState != GameState.PLAYING) return;

        if (!hasUsedReviveThisGame && AdManager.Instance != null && AdManager.Instance.IsRewardedReady())
        {
            // Show revive prompt instead of immediate game over
            CurrentState = GameState.WAITING_REVIVE;
            OnRevivePrompt?.Invoke();
            return;
        }

        FinalGameOver();
    }

    /// <summary>
    /// Player chose to watch ad and revive. Called by UIManager after ad completes.
    /// </summary>
    public void RevivePlayer()
    {
        if (CurrentState != GameState.WAITING_REVIVE) return;
        hasUsedReviveThisGame = true;
        CurrentState = GameState.PLAYING;
        OnRevive?.Invoke();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayRevive();
            AudioManager.Instance.StartMusic();
        }
    }

    /// <summary>
    /// Player declined revive or revive prompt timed out.
    /// </summary>
    public void DeclineRevive()
    {
        if (CurrentState != GameState.WAITING_REVIVE) return;
        FinalGameOver();
    }

    private void FinalGameOver()
    {
        CurrentState = GameState.GAME_OVER;
        OnGameOver?.Invoke();

        if (AdManager.Instance != null)
            AdManager.Instance.ShowInterstitial();

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
            AudioManager.Instance.PlayGameOver();
        }
    }

    public void ReturnToMenu()
    {
        CurrentState = GameState.MENU;
        OnReturnToMenu?.Invoke();
    }
}
