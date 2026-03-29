#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools menu helpers for bonus code debugging.
/// </summary>
public static class BonusCodeDebug
{
    private const string USED_PREFIX = "bc_used_";

    private static readonly string[] ALL_CODES =
    {
        "WLCM99",
    };

    [MenuItem("Tools/StackTower/Reset Bonus Code Usages")]
    private static void ResetAllBonusCodeUsages()
    {
        int cleared = 0;
        foreach (string code in ALL_CODES)
        {
            string key = USED_PREFIX + code;
            if (PlayerPrefs.HasKey(key))
            {
                PlayerPrefs.DeleteKey(key);
                cleared++;
            }
        }

        PlayerPrefs.Save();
        Debug.Log($"[BonusCodeDebug] {cleared} bonus kod kullanimi sifirlandi.");
        EditorUtility.DisplayDialog("Bonus Code Reset",
            $"{cleared} bonus kod sifirlandi.\nTum kodlar tekrar kullanilabilir.", "Tamam");
    }
}
#endif
