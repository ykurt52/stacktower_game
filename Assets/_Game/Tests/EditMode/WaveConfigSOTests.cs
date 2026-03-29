using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode unit tests for WaveConfigSO pure-logic methods.
/// No scene objects or MonoBehaviour lifecycle required.
/// </summary>
public class WaveConfigSOTests
{
    private WaveConfigSO _config;

    [SetUp]
    public void SetUp()
    {
        _config = ScriptableObject.CreateInstance<WaveConfigSO>();
        _config.baseEnemyCount    = 2;
        _config.maxEnemiesPerWave = 25;
        _config.bossWaveInterval  = 10;
        _config.statScalePerWave  = 0.08f;
        _config.pickupStartWave   = 2;

        _config.waveTiers = new WaveTier[]
        {
            new WaveTier
            {
                fromWave     = 1,
                distribution = new EnemyWeight[]
                {
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Melee, weight = 1f }
                }
            },
            new WaveTier
            {
                fromWave     = 3,
                distribution = new EnemyWeight[]
                {
                    new EnemyWeight { type = ArenaEnemy.EnemyType.Ranged, weight = 1f }
                }
            },
        };

        _config.pickupCountSteps = new PickupCountStep[]
        {
            new PickupCountStep { fromWave = 2, count = 1 },
            new PickupCountStep { fromWave = 5, count = 2 },
        };

        _config.pickupWeights = new PickupWeight[]
        {
            new PickupWeight { type = ArenaPickup.PickupType.Heal, weight = 1f },
        };
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_config);
    }

    // ── GetEnemyCount ────────────────────────────────────────────────────────

    [Test]
    public void GetEnemyCount_Wave1_ReturnsBaseCountPlusWave()
    {
        // base(2) + wave(1) = 3, not a boss wave
        Assert.AreEqual(3, _config.GetEnemyCount(1));
    }

    [Test]
    public void GetEnemyCount_NormalWave_ReturnsBaseCountPlusWave()
    {
        // base(2) + wave(5) = 7
        Assert.AreEqual(7, _config.GetEnemyCount(5));
    }

    [Test]
    public void GetEnemyCount_BossWave_FewerRegularsPlusOneBossSlot()
    {
        // wave=10: raw=12 → boss formula: max(3, 12/2)+1 = max(3,6)+1 = 7
        Assert.AreEqual(7, _config.GetEnemyCount(10));
    }

    [Test]
    public void GetEnemyCount_BossWaveSmallCount_BossMinFloor()
    {
        // wave=10, base=1 → raw=11 → boss: max(3, 11/2)+1 = max(3,5)+1 = 6
        _config.baseEnemyCount = 1;
        Assert.AreEqual(6, _config.GetEnemyCount(10));
    }

    [Test]
    public void GetEnemyCount_HighWave_ClampedToMax()
    {
        // base(2)+wave(100)=102, clamped to maxEnemiesPerWave(25)
        Assert.AreEqual(25, _config.GetEnemyCount(100));
    }

    [Test]
    public void GetEnemyCount_Wave20BossWave_ClampedWhenAboveMax()
    {
        // wave=20: raw=22, boss: max(3,11)+1=12 → under cap → 12
        Assert.AreEqual(12, _config.GetEnemyCount(20));
    }

    // ── GetTierForWave ───────────────────────────────────────────────────────

    [Test]
    public void GetTierForWave_Wave1_ReturnsFirstTier()
    {
        Assert.AreEqual(1, _config.GetTierForWave(1).fromWave);
    }

    [Test]
    public void GetTierForWave_Wave2_StillFirstTier()
    {
        // Tier 2 starts at wave 3
        Assert.AreEqual(1, _config.GetTierForWave(2).fromWave);
    }

    [Test]
    public void GetTierForWave_Wave3_ReturnsSecondTier()
    {
        Assert.AreEqual(3, _config.GetTierForWave(3).fromWave);
    }

    [Test]
    public void GetTierForWave_HighWave_ReturnsLastTier()
    {
        Assert.AreEqual(3, _config.GetTierForWave(999).fromWave);
    }

    [Test]
    public void GetTierForWave_EmptyTiers_ReturnsDefault()
    {
        _config.waveTiers = new WaveTier[0];
        // Should not throw; returns default struct
        Assert.DoesNotThrow(() => _config.GetTierForWave(1));
    }

    // ── GetPickupCount ───────────────────────────────────────────────────────

    [Test]
    public void GetPickupCount_BeforeStartWave_ReturnsZero()
    {
        Assert.AreEqual(0, _config.GetPickupCount(1));
    }

    [Test]
    public void GetPickupCount_AtStartWave_ReturnsFirstStepCount()
    {
        Assert.AreEqual(1, _config.GetPickupCount(2));
    }

    [Test]
    public void GetPickupCount_BetweenSteps_ReturnsFirstStepCount()
    {
        Assert.AreEqual(1, _config.GetPickupCount(4));
    }

    [Test]
    public void GetPickupCount_AtSecondStep_ReturnsSecondCount()
    {
        Assert.AreEqual(2, _config.GetPickupCount(5));
    }

    [Test]
    public void GetPickupCount_BeyondLastStep_ReturnsLastCount()
    {
        Assert.AreEqual(2, _config.GetPickupCount(100));
    }

    [Test]
    public void GetPickupCount_NullSteps_ReturnsZero()
    {
        _config.pickupCountSteps = null;
        Assert.AreEqual(0, _config.GetPickupCount(5));
    }

    [Test]
    public void GetPickupCount_EmptySteps_ReturnsZero()
    {
        _config.pickupCountSteps = new PickupCountStep[0];
        Assert.AreEqual(0, _config.GetPickupCount(5));
    }

    // ── Stat scale formula ───────────────────────────────────────────────────

    [Test]
    public void StatScale_Wave1_MultiplierIsCorrect()
    {
        float mult = 1f + 1 * _config.statScalePerWave;
        Assert.AreEqual(1.08f, mult, 0.0001f);
    }

    [Test]
    public void StatScale_Wave10_MultiplierIsCorrect()
    {
        float mult = 1f + 10 * _config.statScalePerWave;
        Assert.AreEqual(1.80f, mult, 0.0001f);
    }
}
