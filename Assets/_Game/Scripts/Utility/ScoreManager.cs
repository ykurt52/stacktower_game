using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tracks current score, high score, and persistent coins.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Events")]
    public UnityEvent<int> OnScoreChanged;
    public UnityEvent<int> OnCoinChanged;

    private const string HighScoreKey = "hs_v";
    private const string HighScoreHashKey = "hs_h";
    private const string CoinKey = "cn_v";
    private const string CoinHashKey = "cn_h";
    private const string StoneKey = "st_v";
    private const string StoneHashKey = "st_h";
    private const string EmeraldKey = "em_v";
    private const string EmeraldHashKey = "em_h";
    private const string Salt = "TwR_s4lt_!x7";

    public UnityEvent<int, int> OnCombo; // combo count, bonus points
    public UnityEvent<int> OnStoneChanged;
    public UnityEvent<int> OnEmeraldChanged;
    public UnityEvent<int, int> OnXPChanged; // currentXP, xpToLevel
    public UnityEvent<int> OnLevelUp; // new level

    public int CurrentScore { get; private set; }
    public int HighScore { get; private set; }
    public int Coins { get; private set; }
    public int UpgradeStones { get; private set; }
    public int Emeralds { get; private set; }
    public int CurrentCombo { get; private set; }
    public int MaxCombo { get; private set; }
    public int CurrentXP { get; private set; }
    public int CurrentLevel { get; private set; } = 1;
    public int XPToNextLevel => CurrentLevel * 10 + 5;

    private int coinsThisRun;
    private int lastLandedFloor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        HighScore = LoadSecure(HighScoreKey, HighScoreHashKey);
        Coins = LoadSecure(CoinKey, CoinHashKey);
        UpgradeStones = LoadSecure(StoneKey, StoneHashKey);
        Emeralds = LoadSecure(EmeraldKey, EmeraldHashKey);
    }

    public void AddPoint()
    {
        CurrentScore++;
        OnScoreChanged?.Invoke(CurrentScore);
    }

    public void AddScore(int amount)
    {
        CurrentScore += amount;
        OnScoreChanged?.Invoke(CurrentScore);
    }

    /// <summary>
    /// Called when character lands on a platform. Floor = platform index.
    /// Skipping floors builds combo; landing on adjacent floor resets it.
    /// </summary>
    public void LandOnFloor(int floor)
    {
        if (floor <= lastLandedFloor) return;

        int skipped = floor - lastLandedFloor;
        lastLandedFloor = floor;

        if (skipped >= 2)
        {
            CurrentCombo += skipped;
            if (CurrentCombo > MaxCombo) MaxCombo = CurrentCombo;

            // Bonus: combo multiplier
            int bonus = skipped * Mathf.Max(1, CurrentCombo / 3);
            CurrentScore += bonus;
            OnScoreChanged?.Invoke(CurrentScore);
            OnCombo?.Invoke(CurrentCombo, bonus);
        }
        else
        {
            // Single step -- reset combo
            CurrentCombo = 0;
            CurrentScore++;
            OnScoreChanged?.Invoke(CurrentScore);
        }
    }

    public void AddCoin()
    {
        int amount = (ShopManager.Instance != null && ShopManager.Instance.HasDoubleCoins()) ? 2 : 1;
        coinsThisRun += amount;
        Coins += amount;
        SaveSecure(CoinKey, CoinHashKey, Coins);
        OnCoinChanged?.Invoke(Coins);
    }

    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        Coins += amount;
        SaveSecure(CoinKey, CoinHashKey, Coins);
        OnCoinChanged?.Invoke(Coins);
    }

    public bool SpendCoins(int amount)
    {
        if (amount <= 0 || Coins < amount) return false;
        Coins -= amount;
        SaveSecure(CoinKey, CoinHashKey, Coins);
        OnCoinChanged?.Invoke(Coins);
        return true;
    }

    // ── Upgrade Stones ──

    public void AddStone()
    {
        UpgradeStones++;
        SaveSecure(StoneKey, StoneHashKey, UpgradeStones);
        OnStoneChanged?.Invoke(UpgradeStones);
    }

    public void AddStones(int amount)
    {
        if (amount <= 0) return;
        UpgradeStones += amount;
        SaveSecure(StoneKey, StoneHashKey, UpgradeStones);
        OnStoneChanged?.Invoke(UpgradeStones);
    }

    public bool SpendStones(int amount)
    {
        if (amount <= 0 || UpgradeStones < amount) return false;
        UpgradeStones -= amount;
        SaveSecure(StoneKey, StoneHashKey, UpgradeStones);
        OnStoneChanged?.Invoke(UpgradeStones);
        return true;
    }

    // ── Emeralds ──

    public void AddEmerald()
    {
        Emeralds++;
        SaveSecure(EmeraldKey, EmeraldHashKey, Emeralds);
        OnEmeraldChanged?.Invoke(Emeralds);
    }

    public void AddEmeralds(int amount)
    {
        if (amount <= 0) return;
        Emeralds += amount;
        SaveSecure(EmeraldKey, EmeraldHashKey, Emeralds);
        OnEmeraldChanged?.Invoke(Emeralds);
    }

    public bool SpendEmeralds(int amount)
    {
        if (amount <= 0 || Emeralds < amount) return false;
        Emeralds -= amount;
        SaveSecure(EmeraldKey, EmeraldHashKey, Emeralds);
        OnEmeraldChanged?.Invoke(Emeralds);
        return true;
    }

    public void AddXP(int amount)
    {
        CurrentXP += amount;
        while (CurrentXP >= XPToNextLevel)
        {
            CurrentXP -= XPToNextLevel;
            CurrentLevel++;
            OnLevelUp?.Invoke(CurrentLevel);
        }
        OnXPChanged?.Invoke(CurrentXP, XPToNextLevel);
    }

    public void ResetXP()
    {
        CurrentXP = 0;
        CurrentLevel = 1;
        OnXPChanged?.Invoke(0, XPToNextLevel);
    }

    public void ResetCombo()
    {
        if (CurrentCombo > 0)
        {
            CurrentCombo = 0;
        }
    }

    public void ResetScore()
    {
        CurrentScore = 0;
        coinsThisRun = 0;
        CurrentCombo = 0;
        MaxCombo = 0;
        lastLandedFloor = 0;
        OnScoreChanged?.Invoke(CurrentScore);
        OnCoinChanged?.Invoke(Coins);
    }

    public void SaveHighScore()
    {
        if (CurrentScore > HighScore)
        {
            HighScore = CurrentScore;
            SaveSecure(HighScoreKey, HighScoreHashKey, HighScore);
        }
    }

    // ── Anti-tamper: value + HMAC hash ──

    private void SaveSecure(string valueKey, string hashKey, int value)
    {
        PlayerPrefs.SetInt(valueKey, value);
        PlayerPrefs.SetString(hashKey, ComputeHash(valueKey, value));
        PlayerPrefs.Save();
    }

    private int LoadSecure(string valueKey, string hashKey)
    {
        if (!PlayerPrefs.HasKey(valueKey)) return 0;

        int value = PlayerPrefs.GetInt(valueKey, 0);
        string storedHash = PlayerPrefs.GetString(hashKey, "");
        string expectedHash = ComputeHash(valueKey, value);

        if (storedHash != expectedHash)
        {
            // Tampered -- reset to 0
            PlayerPrefs.DeleteKey(valueKey);
            PlayerPrefs.DeleteKey(hashKey);
            PlayerPrefs.Save();
            return 0;
        }

        return value;
    }

    private string ComputeHash(string key, int value)
    {
        string raw = Salt + key + value.ToString() + Salt;
        // Simple hash -- not cryptographic but deters casual edits
        int hash = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            hash = hash * 31 + raw[i];
            hash ^= (hash >> 16);
        }
        return hash.ToString("X8");
    }
}
