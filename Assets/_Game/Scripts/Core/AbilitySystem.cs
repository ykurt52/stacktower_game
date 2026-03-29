using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Rarity-tiered ability system. Each ability has 4 rarity levels with different values.
/// Abilities are filtered by weapon type and cumulative cap.
/// </summary>
public class AbilitySystem : MonoBehaviour
{
    public static AbilitySystem Instance { get; private set; }

    public enum AbilityId
    {
        // General
        MultiHit, AttackSpeedUp, AttackDamageUp, MoveSpeedUp, HPBoost,
        Heal, SlowOnHit, MagnetRange, Armor, Vampirism, Reflect, CriticalHit, Dodge,
        // Ranged only
        Pierce, BounceWall, RearArrow, DiagonalArrow,
        // Melee only
        Cleave, Stun,
    }

    public enum Rarity { Low, Medium, High, VeryHigh }
    public enum WeaponCategory { Melee, Ranged }

    public struct AbilityTier
    {
        public Rarity rarity;
        public float value;       // the amount this tier adds
        public string description;
    }

    public class AbilityDef
    {
        public AbilityId id;
        public string name;
        public float cap;              // max cumulative value (0 = no cap)
        public WeaponCategory? requiredWeapon; // null = all
        public AbilityTier[] tiers;    // always 4 entries (Low, Med, High, VH)
        public float accumulated;      // current cumulative value this run
    }

    // Rarity drop weights
    // Low=65%, Med=25%, High=8%, VeryHigh=2%
    private static readonly int[] RARITY_WEIGHTS = { 65, 25, 8, 2 };

    private List<AbilityDef> _allAbilities;
    private Dictionary<AbilityId, AbilityDef> _lookup;
    private WeaponCategory _weaponCategory = WeaponCategory.Melee;

    // ── Computed stats (read by ArenaCharacter, ArenaEnemy, Projectile, etc.) ──
    public int ExtraProjectiles { get; private set; }
    public float AttackSpeedMult { get; private set; } = 1f;
    public float DamageMult { get; private set; } = 1f;
    public float MoveSpeedMult { get; private set; } = 1f;
    public float BonusHPPercent { get; private set; }
    public float SlowOnHit { get; private set; }
    public float BonusMagnet { get; private set; }
    public float ArmorReduction { get; private set; }
    public float VampirismPercent { get; private set; }
    public float ReflectChance { get; private set; }
    public float CriticalChance { get; private set; }
    public float DodgeChance { get; private set; }
    public int PierceCount { get; private set; }
    public int BounceCount { get; private set; }
    public bool HasRearArrow { get; private set; }
    public bool HasDiagonalArrow { get; private set; }
    public bool HasCleave { get; private set; }
    public float StunChance { get; private set; }

    // Backward compat
    public int BonusHP => Mathf.CeilToInt(BonusHPPercent * 0.01f);

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitAbilities();
    }

    public void SetWeaponCategory(WeaponCategory cat) => _weaponCategory = cat;

    // ── Ability Definitions ──

    private void InitAbilities()
    {
        _allAbilities = new List<AbilityDef>
        {
            // ── GENERAL ──
            Def(AbilityId.MultiHit,      "Coklu Vurus",    cap: 3,   weapon: null, tiers: new[] {
                T(Rarity.Low, 1, "+1 darbe kazan!"), T(Rarity.Medium, 1, "+1 darbe kazan!"),
                T(Rarity.High, 2, "+2 darbe kazan!"), T(Rarity.VeryHigh, 3, "+3 darbe kazan!") }),

            Def(AbilityId.AttackSpeedUp,  "Saldiri Hizi",   cap: 200, weapon: null, tiers: new[] {
                T(Rarity.Low, 10, "Saldirilarin %10 daha hizli!"), T(Rarity.Medium, 20, "Saldirilarin %20 daha hizli!"),
                T(Rarity.High, 40, "Saldirilarin %40 daha hizli!"), T(Rarity.VeryHigh, 60, "Saldirilarin %60 daha hizli!") }),

            Def(AbilityId.AttackDamageUp, "Hasar Artisi",   cap: 200, weapon: null, tiers: new[] {
                T(Rarity.Low, 10, "Saldirilarin %10 daha guclu!"), T(Rarity.Medium, 20, "Saldirilarin %20 daha guclu!"),
                T(Rarity.High, 40, "Saldirilarin %40 daha guclu!"), T(Rarity.VeryHigh, 60, "Saldirilarin %60 daha guclu!") }),

            Def(AbilityId.MoveSpeedUp,    "Hiz",            cap: 75,  weapon: null, tiers: new[] {
                T(Rarity.Low, 5, "Hareket hizin %5 artar!"), T(Rarity.Medium, 10, "Hareket hizin %10 artar!"),
                T(Rarity.High, 20, "Hareket hizin %20 artar!"), T(Rarity.VeryHigh, 30, "Hareket hizin %30 artar!") }),

            Def(AbilityId.HPBoost,        "Max Can",        cap: 300, weapon: null, tiers: new[] {
                T(Rarity.Low, 15, "Maksimum canin %15 artar!"), T(Rarity.Medium, 30, "Maksimum canin %30 artar!"),
                T(Rarity.High, 50, "Maksimum canin %50 artar!"), T(Rarity.VeryHigh, 75, "Maksimum canin %75 artar!") }),

            Def(AbilityId.Heal,           "Iyiles",         cap: 0,   weapon: null, tiers: new[] {
                T(Rarity.Low, 10, "%10 can tedavisi kazan!"), T(Rarity.Medium, 20, "%20 can tedavisi kazan!"),
                T(Rarity.High, 35, "%35 can tedavisi kazan!"), T(Rarity.VeryHigh, 50, "%50 can tedavisi kazan!") }),

            Def(AbilityId.SlowOnHit,      "Dondurucu",      cap: 125, weapon: null, tiers: new[] {
                T(Rarity.Low, 10, "Vurdugun dusman %10 yavaslar!"), T(Rarity.Medium, 20, "Vurdugun dusman %20 yavaslar!"),
                T(Rarity.High, 40, "Vurdugun dusman %40 yavaslar!"), T(Rarity.VeryHigh, 60, "Vurdugun dusman %60 yavaslar!") }),

            Def(AbilityId.MagnetRange,    "Miknatis",       cap: 10,  weapon: null, tiers: new[] {
                T(Rarity.Low, 1, "Toplama menzili biraz artar!"), T(Rarity.Medium, 2, "Toplama menzili artar!"),
                T(Rarity.High, 3, "Toplama menzili cok artar!"), T(Rarity.VeryHigh, 5, "Toplama menzili muazzam artar!") }),

            Def(AbilityId.Armor,          "Zirh",           cap: 100, weapon: null, tiers: new[] {
                T(Rarity.Low, 5, "Gelen hasar %5 azalir!"), T(Rarity.Medium, 10, "Gelen hasar %10 azalir!"),
                T(Rarity.High, 20, "Gelen hasar %20 azalir!"), T(Rarity.VeryHigh, 35, "Gelen hasar %35 azalir!") }),

            Def(AbilityId.Vampirism,      "Vampir",         cap: 40,  weapon: null, tiers: new[] {
                T(Rarity.Low, 3, "Oldurdugun dusmandan %3 can kazan!"), T(Rarity.Medium, 5, "Oldurdugun dusmandan %5 can kazan!"),
                T(Rarity.High, 10, "Oldurdugun dusmandan %10 can kazan!"), T(Rarity.VeryHigh, 15, "Oldurdugun dusmandan %15 can kazan!") }),

            Def(AbilityId.Reflect,        "Yansima",        cap: 50,  weapon: null, tiers: new[] {
                T(Rarity.Low, 5, "%5 sansla hasari geri yansit!"), T(Rarity.Medium, 10, "%10 sansla hasari geri yansit!"),
                T(Rarity.High, 15, "%15 sansla hasari geri yansit!"), T(Rarity.VeryHigh, 25, "%25 sansla hasari geri yansit!") }),

            Def(AbilityId.CriticalHit,    "Kritik Vurus",   cap: 100, weapon: null, tiers: new[] {
                T(Rarity.Low, 5, "%5 kritik vurus sansi kazan!"), T(Rarity.Medium, 10, "%10 kritik vurus sansi kazan!"),
                T(Rarity.High, 20, "%20 kritik vurus sansi kazan!"), T(Rarity.VeryHigh, 30, "%30 kritik vurus sansi kazan!") }),

            Def(AbilityId.Dodge,          "Kacinma",        cap: 50,  weapon: null, tiers: new[] {
                T(Rarity.Low, 5, "%5 sansla gelen hasari yoksay!"), T(Rarity.Medium, 10, "%10 sansla gelen hasari yoksay!"),
                T(Rarity.High, 15, "%15 sansla gelen hasari yoksay!"), T(Rarity.VeryHigh, 20, "%20 sansla gelen hasari yoksay!") }),

            // ── RANGED ONLY ──
            Def(AbilityId.Pierce,         "Delici Atis",    cap: 5,   weapon: WeaponCategory.Ranged, tiers: new[] {
                T(Rarity.Low, 1, "Mermi 1 dusmandan gecer!"), T(Rarity.Medium, 2, "Mermi 2 dusmandan gecer!"),
                T(Rarity.High, 3, "Mermi 3 dusmandan gecer!"), T(Rarity.VeryHigh, 5, "Mermi 5 dusmandan gecer!") }),

            Def(AbilityId.BounceWall,     "Sekme",          cap: 5,   weapon: WeaponCategory.Ranged, tiers: new[] {
                T(Rarity.Low, 1, "Mermi 1 kez seker!"), T(Rarity.Medium, 1, "Mermi 1 kez seker!"),
                T(Rarity.High, 2, "Mermi 2 kez seker!"), T(Rarity.VeryHigh, 3, "Mermi 3 kez seker!") }),

            Def(AbilityId.RearArrow,      "Arka Atis",      cap: 1,   weapon: WeaponCategory.Ranged, tiers: new[] {
                T(Rarity.Low, 0, ""), T(Rarity.Medium, 0, ""),
                T(Rarity.High, 1, "+1 arka atis kazan!"), T(Rarity.VeryHigh, 1, "+1 arka atis kazan!") }),

            Def(AbilityId.DiagonalArrow,  "Capraz Atis",    cap: 1,   weapon: WeaponCategory.Ranged, tiers: new[] {
                T(Rarity.Low, 0, ""), T(Rarity.Medium, 0, ""),
                T(Rarity.High, 1, "+1 capraz atis kazan!"), T(Rarity.VeryHigh, 1, "+1 capraz atis kazan!") }),

            // ── MELEE ONLY ──
            Def(AbilityId.Cleave,         "Savurma",        cap: 1,   weapon: WeaponCategory.Melee, tiers: new[] {
                T(Rarity.Low, 0, ""), T(Rarity.Medium, 0, ""),
                T(Rarity.High, 1, "Saldirin cevredeki herkese isabet eder!"), T(Rarity.VeryHigh, 1, "Saldirin cevredeki herkese isabet eder!") }),

            Def(AbilityId.Stun,           "Sersemletme",    cap: 30,  weapon: WeaponCategory.Melee, tiers: new[] {
                T(Rarity.Low, 3, "%3 sansla dusmani 1sn sersemlet!"), T(Rarity.Medium, 5, "%5 sansla dusmani 1sn sersemlet!"),
                T(Rarity.High, 10, "%10 sansla dusmani 1sn sersemlet!"), T(Rarity.VeryHigh, 15, "%15 sansla dusmani 1sn sersemlet!") }),
        };

        _lookup = new Dictionary<AbilityId, AbilityDef>();
        foreach (var a in _allAbilities)
            _lookup[a.id] = a;
    }

    // ── Helper constructors ──

    private static AbilityDef Def(AbilityId id, string name, float cap, WeaponCategory? weapon, AbilityTier[] tiers)
    {
        return new AbilityDef { id = id, name = name, cap = cap, requiredWeapon = weapon, tiers = tiers, accumulated = 0 };
    }

    private static AbilityTier T(Rarity r, float val, string desc)
    {
        return new AbilityTier { rarity = r, value = val, description = desc };
    }

    // ── Public API ──

    public void Reset()
    {
        foreach (var a in _allAbilities)
            a.accumulated = 0;
        RecalculateStats();
    }

    /// <summary>
    /// Picks 3 random ability+tier combos, filtered by weapon and cap.
    /// </summary>
    public List<(AbilityDef ability, AbilityTier tier)> GetRandomChoices(int count = 3)
    {
        // Build pool of valid (ability, tier) pairs
        var pool = new List<(AbilityDef ability, AbilityTier tier, int weight)>();

        foreach (var a in _allAbilities)
        {
            // Weapon filter
            if (a.requiredWeapon.HasValue && a.requiredWeapon.Value != _weaponCategory)
                continue;

            // Cap check — skip if fully capped
            if (a.cap > 0 && a.accumulated >= a.cap)
                continue;

            foreach (var t in a.tiers)
            {
                // Skip tiers with 0 value (e.g. Low/Med for RearArrow)
                if (t.value <= 0) continue;

                // Skip if this tier would exceed cap
                if (a.cap > 0 && a.accumulated + t.value > a.cap)
                    continue;

                int weight = RARITY_WEIGHTS[(int)t.rarity];
                pool.Add((a, t, weight));
            }
        }

        // Weighted random selection without repeating same ability
        var result = new List<(AbilityDef, AbilityTier)>();
        var usedAbilities = new HashSet<AbilityId>();

        for (int i = 0; i < count && pool.Count > 0; i++)
        {
            // Calculate total weight of remaining pool
            int totalWeight = 0;
            foreach (var entry in pool)
            {
                if (!usedAbilities.Contains(entry.ability.id))
                    totalWeight += entry.weight;
            }

            if (totalWeight <= 0) break;

            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;

            for (int j = 0; j < pool.Count; j++)
            {
                var entry = pool[j];
                if (usedAbilities.Contains(entry.ability.id)) continue;

                cumulative += entry.weight;
                if (roll < cumulative)
                {
                    result.Add((entry.ability, entry.tier));
                    usedAbilities.Add(entry.ability.id);
                    break;
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Acquire ability with specific tier value.
    /// </summary>
    public void AcquireAbility(AbilityId id, float tierValue)
    {
        if (!_lookup.TryGetValue(id, out var def)) return;

        // Heal is immediate effect, doesn't accumulate
        if (id == AbilityId.Heal)
        {
            var player = FindAnyObjectByType<ArenaCharacter>();
            if (player != null)
            {
                int healAmount = Mathf.CeilToInt(player.MaxHP * tierValue * 0.01f);
                player.Heal(healAmount);
            }
            RecalculateStats();
            return;
        }

        def.accumulated += tierValue;

        // Clamp to cap
        if (def.cap > 0 && def.accumulated > def.cap)
            def.accumulated = def.cap;

        RecalculateStats();
    }

    // ── Stat Recalculation ──

    private float Get(AbilityId id) => _lookup.TryGetValue(id, out var d) ? d.accumulated : 0;

    private void RecalculateStats()
    {
        if (_lookup == null || _lookup.Count == 0) return;

        ExtraProjectiles  = Mathf.RoundToInt(Get(AbilityId.MultiHit));
        AttackSpeedMult   = 1f + Get(AbilityId.AttackSpeedUp) * 0.01f;    // 200% cap → 3x
        DamageMult        = 1f + Get(AbilityId.AttackDamageUp) * 0.01f;   // 200% cap → 3x
        MoveSpeedMult     = 1f + Get(AbilityId.MoveSpeedUp) * 0.01f;      // 75% cap → 1.75x
        BonusHPPercent    = Get(AbilityId.HPBoost);                         // 300% cap
        SlowOnHit         = Get(AbilityId.SlowOnHit) * 0.01f;             // 125% cap → 1.25 slow
        BonusMagnet       = Get(AbilityId.MagnetRange);                     // cap 10
        ArmorReduction    = Get(AbilityId.Armor) * 0.01f;                  // 100% cap → 100% reduction
        VampirismPercent  = Get(AbilityId.Vampirism) * 0.01f;             // 40% cap
        ReflectChance     = Get(AbilityId.Reflect) * 0.01f;               // 50% cap
        CriticalChance    = Get(AbilityId.CriticalHit) * 0.01f;           // 100% cap
        DodgeChance       = Get(AbilityId.Dodge) * 0.01f;                  // 50% cap
        PierceCount       = Mathf.RoundToInt(Get(AbilityId.Pierce));        // cap 5
        BounceCount       = Mathf.RoundToInt(Get(AbilityId.BounceWall));    // cap 5
        HasRearArrow      = Get(AbilityId.RearArrow) > 0;
        HasDiagonalArrow  = Get(AbilityId.DiagonalArrow) > 0;
        HasCleave         = Get(AbilityId.Cleave) > 0;
        StunChance        = Get(AbilityId.Stun) * 0.01f;                  // 30% cap

        // Apply HP change
        var player = FindAnyObjectByType<ArenaCharacter>();
        if (player != null) player.RecalculateMaxHP();
    }

    // ── Rarity Color Helper ──

    public static Color GetRarityColor(Rarity r)
    {
        return r switch
        {
            Rarity.Low      => new Color(0.3f, 0.75f, 0.3f),   // Yeşil
            Rarity.Medium   => new Color(0.3f, 0.5f, 0.9f),    // Mavi
            Rarity.High     => new Color(0.65f, 0.3f, 0.85f),   // Mor
            Rarity.VeryHigh => new Color(0.7f, 0.15f, 0.2f),    // Bordo
            _ => Color.white
        };
    }

    public static string GetRarityName(Rarity r)
    {
        return r switch
        {
            Rarity.Low      => "Siradan",
            Rarity.Medium   => "Nadir",
            Rarity.High     => "Epik",
            Rarity.VeryHigh => "Efsanevi",
            _ => ""
        };
    }
}
