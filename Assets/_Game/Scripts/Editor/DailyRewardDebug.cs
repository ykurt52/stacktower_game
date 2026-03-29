using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools menu helpers for daily reward debugging.
/// </summary>
public static class DailyRewardDebug
{
    [MenuItem("Tools/Daily Reward/Reset Daily Reward")]
    private static void ResetDailyReward()
    {
        PlayerPrefs.DeleteKey("daily_last");
        PlayerPrefs.DeleteKey("daily_streak");
        PlayerPrefs.Save();
        Debug.Log("[DailyReward] PlayerPrefs cleared -- press Play to see the reward panel again.");
        EditorUtility.DisplayDialog("Daily Reward Reset",
            "daily_last ve daily_streak silindi.\nPlay modunda gunluk odul tekrar acilacak.", "Tamam");
    }

    [MenuItem("Tools/Daily Reward/Increment Day for Reward")]
    private static void IncrementDayForReward()
    {
        int streak = PlayerPrefs.GetInt("daily_streak", 0);
        streak++;
        PlayerPrefs.SetInt("daily_streak", streak);
        PlayerPrefs.DeleteKey("daily_last");
        PlayerPrefs.Save();
        int cycleDay = ((streak - 1) % 7) + 1;
        Debug.Log($"[DailyReward] Streak {streak} olarak ayarlandi (dongu gunu: {cycleDay}) -- Play modunda odul paneli acilacak.");
        EditorUtility.DisplayDialog("Increment Day for Reward",
            $"Streak: {streak} gun (dongu: Gun {cycleDay})\ndaily_last silindi -- Play modunda odul tekrar acilacak.", "Tamam");
    }

    [MenuItem("Tools/Daily Reward/Show Info")]
    private static void ShowDailyRewardInfo()
    {
        string lastDate = PlayerPrefs.GetString("daily_last", "(yok)");
        int streak = PlayerPrefs.GetInt("daily_streak", 0);
        string msg = $"Son talep tarihi : {lastDate}\nStreak (gun sayisi) : {streak}";
        Debug.Log("[DailyReward] " + msg);
        EditorUtility.DisplayDialog("Daily Reward Bilgisi", msg, "Tamam");
    }
}
