using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages AdMob ads: interstitial + rewarded video.
/// All ad callbacks route through UnityEvents so UI/game logic stays decoupled.
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    [Header("Ad Unit IDs (Test)")]
    [SerializeField] private string androidInterstitialId = "ca-app-pub-3940256099942544/1033173712";
    [SerializeField] private string iosInterstitialId = "ca-app-pub-3940256099942544/4411468910";
    [SerializeField] private string androidRewardedId = "ca-app-pub-3940256099942544/5224354917";
    [SerializeField] private string iosRewardedId = "ca-app-pub-3940256099942544/1712485313";

    [Header("Settings")]
    [SerializeField] private int interstitialEveryNGames = 3;

    /// <summary>Fired when a rewarded ad completes successfully. Passes the reward type string.</summary>
    public UnityEvent<string> OnRewardEarned;

    private int gamesPlayedSinceAd;
    private string pendingRewardType;

    // Reward type constants
    public const string RewardRevive = "revive";
    public const string RewardDoubleCoins = "double_coins";
    public const string RewardFreeStone = "free_stone";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeAds();
    }

    private void InitializeAds()
    {
        try
        {
            // TODO: Uncomment when Google Mobile Ads SDK is imported
            // MobileAds.Initialize(initStatus => { LoadInterstitial(); LoadRewarded(); });
            Debug.Log("[AdManager] Ad SDK initialization skipped — SDK not imported yet.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[AdManager] Failed to initialize ads: " + e.Message);
        }
    }

    // ── Interstitial ──

    private void LoadInterstitial()
    {
        try
        {
            Debug.Log("[AdManager] Interstitial load skipped — SDK not imported yet.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[AdManager] Failed to load interstitial: " + e.Message);
        }
    }

    /// <summary>
    /// Shows interstitial every N games (not every game — reduces churn).
    /// </summary>
    public void ShowInterstitial()
    {
        gamesPlayedSinceAd++;
        if (gamesPlayedSinceAd < interstitialEveryNGames)
        {
            Debug.Log("[AdManager] Interstitial skipped — " + gamesPlayedSinceAd + "/" + interstitialEveryNGames);
            return;
        }

        gamesPlayedSinceAd = 0;
        try
        {
            // TODO: Implement when Google Mobile Ads SDK is imported
            // if (interstitialAd != null && interstitialAd.CanShowAd()) { interstitialAd.Show(); }
            Debug.Log("[AdManager] ShowInterstitial called — SDK not imported yet.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[AdManager] Failed to show interstitial: " + e.Message);
        }
    }

    // ── Rewarded Video ──

    private void LoadRewarded()
    {
        try
        {
            Debug.Log("[AdManager] Rewarded load skipped — SDK not imported yet.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[AdManager] Failed to load rewarded: " + e.Message);
        }
    }

    /// <summary>
    /// Request a rewarded ad. rewardType is passed back via OnRewardEarned when completed.
    /// </summary>
    public void ShowRewarded(string rewardType)
    {
        pendingRewardType = rewardType;
        try
        {
            // TODO: Implement when Google Mobile Ads SDK is imported
            // if (rewardedAd != null && rewardedAd.CanShowAd()) { rewardedAd.Show(OnUserEarnedReward); return; }

            // SDK not imported — grant reward immediately for testing
            Debug.Log("[AdManager] ShowRewarded('" + rewardType + "') — SDK not imported, granting reward for testing.");
            GrantReward();
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[AdManager] Failed to show rewarded: " + e.Message);
        }
    }

    /// <summary>
    /// Returns true if a rewarded ad is ready to show.
    /// Always returns true in test mode (SDK not imported).
    /// </summary>
    public bool IsRewardedReady()
    {
        // TODO: return rewardedAd != null && rewardedAd.CanShowAd();
        return true;
    }

    // Called by AdMob SDK callback
    private void OnUserEarnedReward()
    {
        GrantReward();
    }

    private void GrantReward()
    {
        if (string.IsNullOrEmpty(pendingRewardType)) return;
        string type = pendingRewardType;
        pendingRewardType = null;
        OnRewardEarned?.Invoke(type);
        LoadRewarded(); // Preload next
    }
}
