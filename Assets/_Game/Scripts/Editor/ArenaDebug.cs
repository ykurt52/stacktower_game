using UnityEngine;
using UnityEditor;

public static class ArenaDebug
{
    [MenuItem("Tools/StackTower/Kill All Enemies")]
    public static void KillAllEnemies()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ArenaDebug] Play modunda calistirin.");
            return;
        }

        if (ArenaManager.Instance == null) return;

        int killed = 0;
        foreach (var enemy in ArenaManager.Instance.ActiveEnemies)
        {
            if (enemy != null && !enemy.IsDead)
            {
                enemy.TakeDamage(99999);
                killed++;
            }
        }
        Debug.Log($"[ArenaDebug] {killed} dusman olduruldu.");
    }

    [MenuItem("Tools/StackTower/Next Wave")]
    public static void NextWave()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ArenaDebug] Play modunda calistirin.");
            return;
        }

        // Kill all then wave advances automatically
        KillAllEnemies();
        Debug.Log("[ArenaDebug] Sonraki dalgaya geciliyor...");
    }

    [MenuItem("Tools/StackTower/Trigger Level Up")]
    public static void TriggerLevelUp()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ArenaDebug] Play modunda calistirin.");
            return;
        }

        if (AbilitySystem.Instance == null)
        {
            Debug.LogWarning("[ArenaDebug] AbilitySystem bulunamadi.");
            return;
        }

        // Find UIManager and trigger level up via XP
        var uiManager = Object.FindAnyObjectByType<UIManager>();
        if (uiManager != null)
        {
            uiManager.SendMessage("OnLevelUp", 1, SendMessageOptions.DontRequireReceiver);
            Debug.Log("[ArenaDebug] Level up tetiklendi.");
        }
        else
        {
            Debug.LogWarning("[ArenaDebug] UIManager bulunamadi.");
        }
    }

    [MenuItem("Tools/StackTower/Skip 5 Waves")]
    public static void Skip5Waves()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[ArenaDebug] Play modunda calistirin.");
            return;
        }

        for (int i = 0; i < 5; i++)
            KillAllEnemies();

        Debug.Log("[ArenaDebug] 5 dalga atlandı.");
    }
}
