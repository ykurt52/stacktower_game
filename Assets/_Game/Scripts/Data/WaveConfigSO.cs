using System;
using UnityEngine;

/// <summary>
/// Arena wave configuration: timing, enemy type distribution per tier, pickup rules.
/// Assign one instance to ArenaManager via the Inspector.
/// Create via Assets > Create > StackTower > Arena Wave Config.
/// </summary>
[CreateAssetMenu(fileName = "ArenaWaveConfig", menuName = "StackTower/Arena Wave Config")]
public class WaveConfigSO : ScriptableObject
{
    // ── Timing ──────────────────────────────────────────────
    [Header("Timing")]
    [Min(0f)] public float wavePauseDuration = 2.5f;
    [Min(0f)] public float bossPauseDuration = 3.5f;
    [Min(0.05f)] public float spawnInterval = 0.35f;
    [Min(0f)] public float initialSpawnDelay = 0.2f;
    [Min(0f)] public float initialWavePause = 1f;

    // ── Enemy Limits ─────────────────────────────────────────
    [Header("Enemy Limits")]
    [Min(1)] public int maxEnemiesAlive = 12;
    [Tooltip("Maximum enemies that can spawn in a single wave.")]
    [Min(1)] public int maxEnemiesPerWave = 25;
    [Tooltip("A boss spawns as the final enemy of every Nth wave.")]
    [Min(1)] public int bossWaveInterval = 10;
    [Tooltip("Base count added to wave number: count = base + wave.")]
    [Min(0)] public int baseEnemyCount = 2;

    // ── Difficulty Scaling ───────────────────────────────────
    [Header("Difficulty Scaling")]
    [Tooltip("Enemy stat multiplier per wave: mult = 1 + wave * statScalePerWave.")]
    [Range(0f, 0.5f)] public float statScalePerWave = 0.08f;

    // ── Wave Tiers ───────────────────────────────────────────
    [Header("Wave Tiers (sort by fromWave ascending)")]
    [Tooltip("Each tier defines the enemy type distribution from that wave number onwards.")]
    public WaveTier[] waveTiers;

    // ── Pickup Spawning ──────────────────────────────────────
    [Header("Pickup Spawning")]
    [Tooltip("Pickups start appearing from this wave.")]
    [Min(1)] public int pickupStartWave = 2;
    [Tooltip("Maps wave thresholds to pickup counts. Sort by fromWave ascending.")]
    public PickupCountStep[] pickupCountSteps;
    public PickupWeight[] pickupWeights;

    // ── Public API ───────────────────────────────────────────

    /// <summary>Returns the total enemy count for the given wave number.</summary>
    public int GetEnemyCount(int wave)
    {
        int count = baseEnemyCount + wave;
        if (wave % bossWaveInterval == 0)
            count = Mathf.Max(3, count / 2) + 1; // fewer regulars + 1 boss slot
        return Mathf.Min(count, maxEnemiesPerWave);
    }

    /// <summary>Returns the active WaveTier for the given wave number.</summary>
    public WaveTier GetTierForWave(int wave)
    {
        WaveTier active = waveTiers.Length > 0 ? waveTiers[0] : default;
        foreach (var tier in waveTiers)
        {
            if (wave >= tier.fromWave) active = tier;
            else break;
        }
        return active;
    }

    /// <summary>Returns the number of pickups to spawn for the given wave.</summary>
    public int GetPickupCount(int wave)
    {
        if (wave < pickupStartWave || pickupCountSteps == null || pickupCountSteps.Length == 0)
            return 0;
        int count = 1;
        foreach (var step in pickupCountSteps)
        {
            if (wave >= step.fromWave) count = step.count;
        }
        return count;
    }

    /// <summary>Picks a random pickup type using configured weights.</summary>
    public ArenaPickup.PickupType GetRandomPickupType()
    {
        if (pickupWeights == null || pickupWeights.Length == 0)
            return ArenaPickup.PickupType.Heal;

        float total = 0f;
        foreach (var w in pickupWeights) total += w.weight;
        if (total <= 0f) return ArenaPickup.PickupType.Heal;

        float r = UnityEngine.Random.value * total;
        float cumulative = 0f;
        foreach (var w in pickupWeights)
        {
            cumulative += w.weight;
            if (r <= cumulative) return w.type;
        }
        return pickupWeights[0].type;
    }
}

// ── Supporting types ─────────────────────────────────────────────────────────

[Serializable]
public struct EnemyWeight
{
    public ArenaEnemy.EnemyType type;
    [Min(0f)] public float weight;
}

/// <summary>
/// Defines the enemy type distribution for a range of wave numbers.
/// Active from <see cref="fromWave"/> until a higher-threshold tier takes over.
/// </summary>
[Serializable]
public struct WaveTier
{
    [Tooltip("This tier activates from this wave number onwards.")]
    public int fromWave;
    [Tooltip("Enemy types and their relative spawn weights for this tier.")]
    public EnemyWeight[] distribution;

    /// <summary>Returns a random enemy type using weighted distribution.</summary>
    public ArenaEnemy.EnemyType GetRandomType()
    {
        if (distribution == null || distribution.Length == 0)
            return ArenaEnemy.EnemyType.Melee;

        float total = 0f;
        foreach (var w in distribution) total += w.weight;
        if (total <= 0f) return ArenaEnemy.EnemyType.Melee;

        float r = UnityEngine.Random.value * total;
        float cumulative = 0f;
        foreach (var w in distribution)
        {
            cumulative += w.weight;
            if (r <= cumulative) return w.type;
        }
        return distribution[0].type;
    }
}

[Serializable]
public struct PickupWeight
{
    public ArenaPickup.PickupType type;
    [Min(0f)] public float weight;
}

[Serializable]
public struct PickupCountStep
{
    [Tooltip("From this wave number, spawn this many pickups per wave.")]
    public int fromWave;
    [Min(0)] public int count;
}
