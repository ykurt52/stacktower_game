using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tracks current score and high score with PlayerPrefs persistence.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Events")]
    public UnityEvent<int> OnScoreChanged;

    private const string HighScoreKey = "HighScore";

    public int CurrentScore { get; private set; }
    public int HighScore { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);
    }

    /// <summary>
    /// Adds one point to the current score and fires OnScoreChanged.
    /// </summary>
    public void AddPoint()
    {
        CurrentScore++;
        OnScoreChanged?.Invoke(CurrentScore);
    }

    /// <summary>
    /// Resets the current score to zero.
    /// </summary>
    public void ResetScore()
    {
        CurrentScore = 0;
        OnScoreChanged?.Invoke(CurrentScore);
    }

    /// <summary>
    /// Saves the high score to PlayerPrefs if current score exceeds it.
    /// </summary>
    public void SaveHighScore()
    {
        if (CurrentScore > HighScore)
        {
            HighScore = CurrentScore;
            PlayerPrefs.SetInt(HighScoreKey, HighScore);
            PlayerPrefs.Save();
        }
    }
}
