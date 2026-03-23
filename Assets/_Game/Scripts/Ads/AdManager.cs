using UnityEngine;

/// <summary>
/// Manages AdMob interstitial ads. Wraps all SDK calls safely.
/// </summary>
public class AdManager : MonoBehaviour
{
    public static AdManager Instance { get; private set; }

    [Header("Ad Unit IDs (Test)")]
    [SerializeField] private string androidAdUnitId = "ca-app-pub-3940256099942544/1033173712";
    [SerializeField] private string iosAdUnitId = "ca-app-pub-3940256099942544/4411468910";

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

    /// <summary>
    /// Initializes the AdMob SDK.
    /// </summary>
    private void InitializeAds()
    {
        try
        {
            // TODO: Uncomment when Google Mobile Ads SDK is imported
            // MobileAds.Initialize(initStatus => { LoadInterstitial(); });
            Debug.Log("[AdManager] Ad SDK initialization skipped — SDK not imported yet.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[AdManager] Failed to initialize ads: " + e.Message);
        }
    }

    /// <summary>
    /// Loads an interstitial ad.
    /// </summary>
    private void LoadInterstitial()
    {
        try
        {
            // TODO: Implement when Google Mobile Ads SDK is imported
            // string adUnitId = Application.platform == RuntimePlatform.Android ? androidAdUnitId : iosAdUnitId;
            // var adRequest = new AdRequest();
            // InterstitialAd.Load(adUnitId, adRequest, (ad, error) => { ... });
            Debug.Log("[AdManager] Interstitial load skipped — SDK not imported yet.");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[AdManager] Failed to load interstitial: " + e.Message);
        }
    }

    /// <summary>
    /// Shows an interstitial ad if one is loaded. Skips silently if not ready.
    /// </summary>
    public void ShowInterstitial()
    {
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
}
