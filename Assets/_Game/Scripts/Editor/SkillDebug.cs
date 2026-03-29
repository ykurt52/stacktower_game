#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools menu helpers for skill/talent debugging.
/// </summary>
public static class SkillDebug
{
    [MenuItem("Tools/StackTower/Reset Upgradable Skills")]
    private static void ResetUpgradableSkills()
    {
        if (Application.isPlaying && ShopManager.Instance != null)
        {
            ShopManager.Instance.ResetAllSkills();
            if (MainMenuManager.Instance != null)
                MainMenuManager.Instance.RefreshCurrencies();
            Debug.Log("[SkillDebug] Tum skill'ler sifirlandi (runtime).");
        }
        else
        {
            // Offline: clear PlayerPrefs directly
            string[] skillIds = {
                "attack", "hp", "armor", "xpboost", "goldboost", "healthregen",
                "vampirism", "dodge",
                "speed", "critical", "attackspeed"
            };
            int cleared = 0;
            foreach (string id in skillIds)
            {
                string key = "skill_" + id;
                string hashKey = "skill_h_" + id;
                if (PlayerPrefs.HasKey(key))
                {
                    PlayerPrefs.DeleteKey(key);
                    PlayerPrefs.DeleteKey(hashKey);
                    cleared++;
                }
            }
            PlayerPrefs.Save();
            Debug.Log($"[SkillDebug] {cleared} skill sifirlandi (offline).");
        }

        EditorUtility.DisplayDialog("Reset Skills",
            "Tum upgradable skill'ler sifirlandi.", "Tamam");
    }
}
#endif
