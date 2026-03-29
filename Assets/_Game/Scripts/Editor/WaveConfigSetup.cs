using UnityEditor;
using UnityEngine;

/// <summary>
/// Creates and populates the ArenaWaveConfig ScriptableObject asset.
/// Run via Tools > Setup > Create Wave Config Asset.
/// </summary>
public static class WaveConfigSetup
{
    private const string OUTPUT_PATH = "Assets/_Game/Data/Waves";
    private const string ASSET_NAME  = "ArenaWaveConfig";

    [MenuItem("Tools/Setup/Create Wave Config Asset")]
    private static void Create()
    {
        System.IO.Directory.CreateDirectory(Application.dataPath + "/../" + OUTPUT_PATH);
        AssetDatabase.Refresh();

        string path = $"{OUTPUT_PATH}/{ASSET_NAME}.asset";
        var config = AssetDatabase.LoadAssetAtPath<WaveConfigSO>(path)
                     ?? ScriptableObject.CreateInstance<WaveConfigSO>();

        // ── Timing ──────────────────────────────────────────
        config.wavePauseDuration  = 2.5f;
        config.bossPauseDuration  = 3.5f;
        config.spawnInterval      = 0.35f;
        config.initialSpawnDelay  = 0.2f;
        config.initialWavePause   = 1f;

        // ── Enemy Limits ─────────────────────────────────────
        config.maxEnemiesAlive    = 12;
        config.maxEnemiesPerWave  = 25;
        config.bossWaveInterval   = 10;
        config.baseEnemyCount     = 2;

        // ── Difficulty ───────────────────────────────────────
        config.statScalePerWave   = 0.08f;

        // ── Wave Tiers ───────────────────────────────────────
        config.waveTiers = new WaveTier[]
        {
            new WaveTier
            {
                fromWave = 1,
                distribution = new EnemyWeight[]
                {
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Melee, weight = 1f },
                }
            },
            new WaveTier
            {
                fromWave = 3,
                distribution = new EnemyWeight[]
                {
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Melee,  weight = 0.65f },
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Ranged, weight = 0.35f },
                }
            },
            new WaveTier
            {
                fromWave = 6,
                distribution = new EnemyWeight[]
                {
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Melee,  weight = 0.35f },
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Ranged, weight = 0.25f },
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Heavy,  weight = 0.40f },
                }
            },
            new WaveTier
            {
                fromWave = 9,
                distribution = new EnemyWeight[]
                {
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Melee,   weight = 0.20f },
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Ranged,  weight = 0.20f },
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Heavy,   weight = 0.15f },
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Bomber,  weight = 0.15f },
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Wizard,  weight = 0.15f },
                    new EnemyWeight { type = ArenaEnemy.EnemyType.IceMage, weight = 0.15f },
                }
            },
            new WaveTier
            {
                fromWave = 13,
                distribution = new EnemyWeight[]
                {
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Melee,   weight = 0.12f },
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Ranged,  weight = 0.13f },
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Heavy,   weight = 0.15f },
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Bomber,  weight = 0.15f },
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Wizard,  weight = 0.20f },
                    new EnemyWeight { type = ArenaEnemy.EnemyType.IceMage, weight = 0.25f },
                }
            },
        };

        // ── Pickup Count Steps ───────────────────────────────
        config.pickupStartWave   = 2;
        config.pickupCountSteps  = new PickupCountStep[]
        {
            new PickupCountStep { fromWave = 2,  count = 1 },
            new PickupCountStep { fromWave = 5,  count = 2 },
            new PickupCountStep { fromWave = 15, count = 3 },
        };

        // ── Pickup Weights ───────────────────────────────────
        config.pickupWeights = new PickupWeight[]
        {
            new PickupWeight { type = ArenaPickup.PickupType.Heal,        weight = 0.35f },
            new PickupWeight { type = ArenaPickup.PickupType.Shield,      weight = 0.20f },
            new PickupWeight { type = ArenaPickup.PickupType.AttackSpeed, weight = 0.20f },
            new PickupWeight { type = ArenaPickup.PickupType.Magnet,      weight = 0.15f },
            new PickupWeight { type = ArenaPickup.PickupType.Bomb,        weight = 0.05f },
        };

        // ── Save ─────────────────────────────────────────────
        if (!AssetDatabase.Contains(config))
            AssetDatabase.CreateAsset(config, path);

        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = config;
        Debug.Log($"[WaveConfigSetup] {ASSET_NAME}.asset created at {OUTPUT_PATH}");
        EditorUtility.DisplayDialog("Done", $"{ASSET_NAME}.asset created at:\n{OUTPUT_PATH}", "OK");
    }
}
