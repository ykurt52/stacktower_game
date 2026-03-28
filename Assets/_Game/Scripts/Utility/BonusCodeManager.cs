using UnityEngine;

/// <summary>
/// Manages bonus/promo codes. Each code can only be redeemed once.
/// </summary>
public class BonusCodeManager : MonoBehaviour
{
    public static BonusCodeManager Instance { get; private set; }

    private struct CodeDef
    {
        public string code;
        public int coins;
    }

    private static readonly CodeDef[] codes =
    {
        new CodeDef { code = "WLCM99", coins = 99999 },
    };

    private const string UsedPrefix = "bc_used_";

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
    /// Try to redeem a code.
    /// Returns positive coin amount if successful, -1 if already used, 0 if invalid.
    /// </summary>
    public int TryRedeem(string input)
    {
        if (string.IsNullOrEmpty(input)) return 0;

        string normalized = input.Trim().ToUpperInvariant();

        foreach (var def in codes)
        {
            if (def.code == normalized)
            {
                string key = UsedPrefix + def.code;
                if (PlayerPrefs.GetInt(key, 0) == 1)
                    return -1;

                PlayerPrefs.SetInt(key, 1);
                PlayerPrefs.Save();

                if (ScoreManager.Instance != null)
                    ScoreManager.Instance.AddCoins(def.coins);

                return def.coins;
            }
        }

        return 0;
    }
}
