using UnityEngine;
using TMPro;

/// <summary>
/// Controls visibility of UI panels and updates score labels reactively.
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject gamePanel;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Menu Panel")]
    [SerializeField] private TextMeshProUGUI bestScoreMenuText;

    [Header("Game Panel")]
    [SerializeField] private TextMeshProUGUI currentScoreText;

    [Header("Game Over Panel")]
    [SerializeField] private TextMeshProUGUI gameOverScoreText;
    [SerializeField] private TextMeshProUGUI gameOverBestText;

    [Header("Settings")]
    [SerializeField] private float gameOverDelay = 0.5f;

    private void Start()
    {
        GameManager.Instance.OnGameStart.AddListener(OnGameStart);
        GameManager.Instance.OnGameOver.AddListener(OnGameOver);
        GameManager.Instance.OnReturnToMenu.AddListener(OnReturnToMenu);

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged.AddListener(OnScoreChanged);
        }

        ShowMenuPanel();
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameStart.RemoveListener(OnGameStart);
            GameManager.Instance.OnGameOver.RemoveListener(OnGameOver);
            GameManager.Instance.OnReturnToMenu.RemoveListener(OnReturnToMenu);
        }

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.OnScoreChanged.RemoveListener(OnScoreChanged);
        }
    }

    /// <summary>
    /// Called by Play button.
    /// </summary>
    public void OnPlayButton()
    {
        GameManager.Instance.StartGame();
    }

    /// <summary>
    /// Called by Retry button.
    /// </summary>
    public void OnRetryButton()
    {
        GameManager.Instance.StartGame();
    }

    /// <summary>
    /// Called by Menu button.
    /// </summary>
    public void OnMenuButton()
    {
        GameManager.Instance.ReturnToMenu();
    }

    private void OnGameStart()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.ResetScore();
        }

        ShowGamePanel();
        UpdateScoreText(0);
    }

    private void OnGameOver()
    {
        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.SaveHighScore();
        }

        Invoke(nameof(ShowGameOverPanel), gameOverDelay);
    }

    private void OnReturnToMenu()
    {
        ShowMenuPanel();
    }

    private void OnScoreChanged(int score)
    {
        UpdateScoreText(score);
    }

    private void ShowMenuPanel()
    {
        menuPanel.SetActive(true);
        gamePanel.SetActive(false);
        gameOverPanel.SetActive(false);

        if (ScoreManager.Instance != null && bestScoreMenuText != null)
        {
            bestScoreMenuText.text = "BEST: " + ScoreManager.Instance.HighScore;
        }
    }

    private void ShowGamePanel()
    {
        menuPanel.SetActive(false);
        gamePanel.SetActive(true);
        gameOverPanel.SetActive(false);
    }

    private void ShowGameOverPanel()
    {
        menuPanel.SetActive(false);
        gamePanel.SetActive(false);
        gameOverPanel.SetActive(true);

        if (ScoreManager.Instance != null)
        {
            if (gameOverScoreText != null)
                gameOverScoreText.text = "SCORE: " + ScoreManager.Instance.CurrentScore;
            if (gameOverBestText != null)
                gameOverBestText.text = "BEST: " + ScoreManager.Instance.HighScore;
        }
    }

    private void UpdateScoreText(int score)
    {
        if (currentScoreText != null)
            currentScoreText.text = score.ToString();
    }
}
