using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages acquired abilities and provides stat modifiers.
/// Archero-style ability pick on level up.
/// </summary>
public class AbilitySystem : MonoBehaviour
{
    public static AbilitySystem Instance { get; private set; }

    public enum AbilityId
    {
        MultiShot,
        Pierce,
        BounceWall,
        AttackSpeedUp,
        AttackDamageUp,
        MoveSpeedUp,
        HPBoost,
        Heal,
        RearArrow,
        DiagonalArrow,
        SlowProjectile,
        MagnetRange
    }

    [System.Serializable]
    public class Ability
    {
        public AbilityId id;
        public string name;
        public string description;
        public Color color;
        public int maxStacks;
        public int currentStacks;
    }

    private Dictionary<AbilityId, Ability> abilities = new Dictionary<AbilityId, Ability>();
    private List<Ability> allAbilities = new List<Ability>();

    // Computed stats from abilities
    public int ExtraProjectiles { get; private set; }
    public int PierceCount { get; private set; }
    public int BounceCount { get; private set; }
    public float AttackSpeedMult { get; private set; } = 1f;
    public float DamageMult { get; private set; } = 1f;
    public float MoveSpeedMult { get; private set; } = 1f;
    public int BonusHP { get; private set; }
    public bool HasRearArrow { get; private set; }
    public bool HasDiagonalArrow { get; private set; }
    public float SlowOnHit { get; private set; }
    public float BonusMagnet { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        InitAbilities();
    }

    private void InitAbilities()
    {
        allAbilities = new List<Ability>
        {
            new Ability { id = AbilityId.MultiShot, name = "Coklu Atis", description = "+1 mermi", color = new Color(1f, 0.8f, 0.2f), maxStacks = 2 },
            new Ability { id = AbilityId.Pierce, name = "Delici Atis", description = "Mermi dusmandan gecer", color = new Color(0.4f, 1f, 0.8f), maxStacks = 3 },
            new Ability { id = AbilityId.BounceWall, name = "Sekme", description = "Mermi duvardan seker", color = new Color(0.6f, 0.6f, 1f), maxStacks = 2 },
            new Ability { id = AbilityId.AttackSpeedUp, name = "Hizli Atis", description = "Atis hizi +15%", color = new Color(1f, 0.5f, 0.2f), maxStacks = 5 },
            new Ability { id = AbilityId.AttackDamageUp, name = "Guclu Atis", description = "Hasar +20%", color = new Color(1f, 0.2f, 0.2f), maxStacks = 5 },
            new Ability { id = AbilityId.MoveSpeedUp, name = "Hiz", description = "Hareket hizi +10%", color = new Color(0.3f, 0.9f, 0.3f), maxStacks = 3 },
            new Ability { id = AbilityId.HPBoost, name = "Can Artisi", description = "Maks can +1", color = new Color(1f, 0.4f, 0.5f), maxStacks = 5 },
            new Ability { id = AbilityId.Heal, name = "Iyiles", description = "1 can yenile", color = new Color(0.3f, 1f, 0.5f), maxStacks = 99 },
            new Ability { id = AbilityId.RearArrow, name = "Arka Ok", description = "Arkaya da ates et", color = new Color(0.8f, 0.4f, 1f), maxStacks = 1 },
            new Ability { id = AbilityId.DiagonalArrow, name = "Capraz Ok", description = "45° capraz mermiler", color = new Color(1f, 0.6f, 0.8f), maxStacks = 1 },
            new Ability { id = AbilityId.SlowProjectile, name = "Dondurucu", description = "Mermiler yavaslatir", color = new Color(0.4f, 0.8f, 1f), maxStacks = 2 },
            new Ability { id = AbilityId.MagnetRange, name = "Miknatis", description = "XP toplama menzili +", color = new Color(0.9f, 0.9f, 0.3f), maxStacks = 3 },
        };

        foreach (var a in allAbilities)
            abilities[a.id] = a;
    }

    public void Reset()
    {
        foreach (var a in allAbilities)
            a.currentStacks = 0;
        RecalculateStats();
    }

    public List<Ability> GetRandomChoices(int count = 3)
    {
        var available = new List<Ability>();
        foreach (var a in allAbilities)
        {
            if (a.currentStacks < a.maxStacks)
                available.Add(a);
        }

        // Shuffle and pick
        var result = new List<Ability>();
        for (int i = 0; i < count && available.Count > 0; i++)
        {
            int idx = Random.Range(0, available.Count);
            result.Add(available[idx]);
            available.RemoveAt(idx);
        }
        return result;
    }

    public void AcquireAbility(AbilityId id)
    {
        if (abilities.TryGetValue(id, out var ability))
        {
            if (ability.currentStacks < ability.maxStacks)
            {
                ability.currentStacks++;

                // Immediate effect for Heal
                if (id == AbilityId.Heal)
                {
                    var player = FindAnyObjectByType<ArenaCharacter>();
                    if (player != null) player.Heal(1);
                    ability.currentStacks--; // Heal doesn't stack
                }

                RecalculateStats();
            }
        }
    }

    private int GetStacks(AbilityId id)
    {
        return abilities.TryGetValue(id, out var a) ? a.currentStacks : 0;
    }

    private void RecalculateStats()
    {
        if (abilities == null || abilities.Count == 0) return;

        ExtraProjectiles = GetStacks(AbilityId.MultiShot);
        PierceCount = GetStacks(AbilityId.Pierce);
        BounceCount = GetStacks(AbilityId.BounceWall);
        AttackSpeedMult = 1f + GetStacks(AbilityId.AttackSpeedUp) * 0.15f;
        DamageMult = 1f + GetStacks(AbilityId.AttackDamageUp) * 0.20f;
        MoveSpeedMult = 1f + GetStacks(AbilityId.MoveSpeedUp) * 0.10f;
        BonusHP = GetStacks(AbilityId.HPBoost);
        HasRearArrow = GetStacks(AbilityId.RearArrow) > 0;
        HasDiagonalArrow = GetStacks(AbilityId.DiagonalArrow) > 0;
        SlowOnHit = GetStacks(AbilityId.SlowProjectile) * 0.3f;
        BonusMagnet = GetStacks(AbilityId.MagnetRange) * 1.5f;

        // Apply HP boost to player
        var player = FindAnyObjectByType<ArenaCharacter>();
        if (player != null) player.RecalculateMaxHP();
    }
}
