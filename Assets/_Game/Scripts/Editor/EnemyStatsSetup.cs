using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates and populates all EnemyStatsSO assets with default values.
/// Run via Tools > Setup > Create Enemy Stats Assets.
/// </summary>
public static class EnemyStatsSetup
{
    private const string OUTPUT_PATH = "Assets/_Game/Data/Enemies";

    [MenuItem("Tools/Setup/Create Enemy Stats Assets")]
    private static void CreateAll()
    {
        System.IO.Directory.CreateDirectory(Application.dataPath + "/../" + OUTPUT_PATH);
        AssetDatabase.Refresh();

        CreateAsset(new EnemyStatsSO
        {
            type             = ArenaEnemy.EnemyType.Melee,
            modelId          = "punk",
            baseHP           = 20,  baseArmor      = 0,
            moveSpeed        = 2.5f,
            contactDamage    = 8,   projectileDamage = 0,
            attackRange      = 0.9f, attackCooldown  = 0.8f,
            xpDrop           = 1,   coinDrop         = 1,
            mass             = 1.5f,
            colliderRadius   = 0.25f, colliderHeight = 0.8f, triggerRadius = 0.2f,
            bodyHeight       = 0.5f,  bodyWidth      = 0.30f, barWidth     = 0.5f, modelScale = 0.75f,
            bodyColor        = new Color(0.8f, 0.3f, 0.2f),
            trimColor        = new Color(0.5f, 0.2f, 0.1f),
        }, "EnemyStats_Melee");

        CreateAsset(new EnemyStatsSO
        {
            type             = ArenaEnemy.EnemyType.Ranged,
            modelId          = "zombie_ribcage",
            baseHP           = 15,  baseArmor      = 0,
            moveSpeed        = 1.5f,
            contactDamage    = 5,   projectileDamage = 7,
            attackRange      = 6f,  attackCooldown   = 2f,
            xpDrop           = 2,   coinDrop         = 1,
            mass             = 1.5f,
            colliderRadius   = 0.25f, colliderHeight = 0.8f, triggerRadius = 0.2f,
            bodyHeight       = 0.5f,  bodyWidth      = 0.28f, barWidth     = 0.5f, modelScale = 0.75f,
            bodyColor        = new Color(0.3f, 0.5f, 0.8f),
            trimColor        = new Color(0.2f, 0.3f, 0.5f),
        }, "EnemyStats_Ranged");

        CreateAsset(new EnemyStatsSO
        {
            type             = ArenaEnemy.EnemyType.Heavy,
            modelId          = "punk",
            baseHP           = 50,  baseArmor      = 20,
            moveSpeed        = 1.2f,
            contactDamage    = 15,  projectileDamage = 0,
            attackRange      = 1f,  attackCooldown   = 1.2f,
            xpDrop           = 3,   coinDrop         = 2,
            mass             = 3f,
            colliderRadius   = 0.25f, colliderHeight = 0.8f, triggerRadius = 0.2f,
            bodyHeight       = 0.7f,  bodyWidth      = 0.45f, barWidth     = 0.5f, modelScale = 0.85f,
            bodyColor        = new Color(0.5f, 0.5f, 0.55f),
            trimColor        = new Color(0.3f, 0.3f, 0.35f),
        }, "EnemyStats_Heavy");

        CreateAsset(new EnemyStatsSO
        {
            type             = ArenaEnemy.EnemyType.Bomber,
            modelId          = "punk",
            baseHP           = 25,  baseArmor      = 0,
            moveSpeed        = 2f,
            contactDamage    = 5,   projectileDamage = 12,
            attackRange      = 4f,  attackCooldown   = 3f,
            xpDrop           = 3,   coinDrop         = 2,
            mass             = 1.5f,
            colliderRadius   = 0.25f, colliderHeight = 0.8f, triggerRadius = 0.2f,
            bodyHeight       = 0.5f,  bodyWidth      = 0.35f, barWidth     = 0.5f, modelScale = 0.75f,
            bodyColor        = new Color(0.8f, 0.6f, 0.2f),
            trimColor        = new Color(0.6f, 0.4f, 0.1f),
        }, "EnemyStats_Bomber");

        CreateAsset(new EnemyStatsSO
        {
            type             = ArenaEnemy.EnemyType.Wizard,
            modelId          = "punk",
            baseHP           = 25,  baseArmor      = 10,
            moveSpeed        = 1.8f,
            contactDamage    = 5,   projectileDamage = 10,
            attackRange      = 7f,  attackCooldown   = 2.5f,
            xpDrop           = 3,   coinDrop         = 2,
            mass             = 1.5f,
            colliderRadius   = 0.25f, colliderHeight = 0.8f, triggerRadius = 0.2f,
            bodyHeight       = 0.5f,  bodyWidth      = 0.28f, barWidth     = 0.5f, modelScale = 0.75f,
            bodyColor        = new Color(0.6f, 0.2f, 0.8f),
            trimColor        = new Color(0.4f, 0.1f, 0.5f),
        }, "EnemyStats_Wizard");

        CreateAsset(new EnemyStatsSO
        {
            type             = ArenaEnemy.EnemyType.IceMage,
            modelId          = "punk",
            baseHP           = 25,  baseArmor      = 10,
            moveSpeed        = 1.5f,
            contactDamage    = 5,   projectileDamage = 8,
            attackRange      = 6f,  attackCooldown   = 2.5f,
            xpDrop           = 3,   coinDrop         = 2,
            mass             = 1.5f,
            colliderRadius   = 0.25f, colliderHeight = 0.8f, triggerRadius = 0.2f,
            bodyHeight       = 0.5f,  bodyWidth      = 0.28f, barWidth     = 0.5f, modelScale = 0.75f,
            bodyColor        = new Color(0.4f, 0.7f, 0.9f),
            trimColor        = new Color(0.2f, 0.4f, 0.6f),
        }, "EnemyStats_IceMage");

        CreateAsset(new EnemyStatsSO
        {
            type             = ArenaEnemy.EnemyType.Boss,
            modelId          = "punk",
            baseHP           = 200, baseArmor      = 80,
            moveSpeed        = 1.5f,
            contactDamage    = 20,  projectileDamage = 15,
            attackRange      = 5f,  attackCooldown   = 1.5f,
            xpDrop           = 15,  coinDrop         = 10,
            mass             = 5f,
            colliderRadius   = 0.5f, colliderHeight  = 1.2f, triggerRadius = 0.55f,
            bodyHeight       = 1f,   bodyWidth       = 0.60f, barWidth     = 0.8f, modelScale = 1f,
            bodyColor        = new Color(0.7f, 0.15f, 0.15f),
            trimColor        = new Color(0.4f, 0.1f, 0.1f),
        }, "EnemyStats_Boss");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[EnemyStatsSetup] 7 EnemyStatsSO assets created in " + OUTPUT_PATH);
        EditorUtility.DisplayDialog("Done", "7 EnemyStatsSO assets created in:\n" + OUTPUT_PATH, "OK");
    }

    private static void CreateAsset(EnemyStatsSO data, string fileName)
    {
        string path = $"{OUTPUT_PATH}/{fileName}.asset";

        // Overwrite if exists
        var existing = AssetDatabase.LoadAssetAtPath<EnemyStatsSO>(path);
        if (existing != null)
        {
            EditorUtility.CopySerialized(data, existing);
            EditorUtility.SetDirty(existing);
            return;
        }

        AssetDatabase.CreateAsset(data, path);
    }
}
