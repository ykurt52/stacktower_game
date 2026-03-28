using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Manages three stores:
/// - SHOP: consumable items (one-time use per game, must rebuy each time)
/// - SKILLS: permanent upgrades (level-based, persist forever)
/// - WEAPONS: permanent, upgradeable (+0 to +10, Metin2-style enhancement with coins + stones)
/// </summary>
public class ShopManager : MonoBehaviour
{
    public static ShopManager Instance { get; private set; }

    public UnityEvent OnChanged;

    public enum ItemCategory { Shop, Skills, Weapons }

    [System.Serializable]
    public class ItemInfo
    {
        public string id;
        public string name;
        public string description;
        public ItemCategory category;
        public int maxLevel;   // Skills: max upgrade level. Shop: always 1. Weapons: 10
        public int[] costs;    // Skills: coin cost per level. Shop: single cost. Weapons: coin cost to buy (level 0)
        // Weapon-specific (base stats at +0)
        public int weaponDamage;
        public float weaponRate;
        public float weaponSpeed;  // Projectile speed
        public string weaponSpecial; // "", "pierce", "boomerang", "tornado", "homing"
        // Weapon upgrade costs
        public int[] upgradeCoinCosts;  // Coin cost per enhancement level (index 0 = +0 -> +1)
        public int[] upgradeStoneCosts; // Stone cost per enhancement level
    }

    // ── Item Definitions ──

    private static readonly ItemInfo[] allItems =
    {
        // SHOP (consumables — bought per game)
        new ItemInfo
        {
            id = "shield", name = "KALKAN",
            description = "Bu oyun kalkanla basla",
            category = ItemCategory.Shop,
            maxLevel = 1, costs = new[] { 25 }
        },
        new ItemInfo
        {
            id = "magnet", name = "MIKNATIS",
            description = "Coinler sana cekilsin",
            category = ItemCategory.Shop,
            maxLevel = 1, costs = new[] { 30 }
        },
        new ItemInfo
        {
            id = "headstart", name = "HIZLI BASLANGIC",
            description = "5. kattan basla",
            category = ItemCategory.Shop,
            maxLevel = 1, costs = new[] { 40 }
        },
        new ItemInfo
        {
            id = "slowlaser", name = "YAVAS LAZER",
            description = "Lazer %50 yavas baslasin",
            category = ItemCategory.Shop,
            maxLevel = 1, costs = new[] { 35 }
        },

        // SKILLS (permanent upgrades)
        new ItemInfo
        {
            id = "jump", name = "JUMP BOOST",
            description = "Ziplama gucunu arttirir",
            category = ItemCategory.Skills,
            maxLevel = 5, costs = new[] { 50, 120, 250, 500, 1000 }
        },
        new ItemInfo
        {
            id = "doublecoins", name = "DOUBLE COINS",
            description = "Coin kazanimini 2x yap",
            category = ItemCategory.Skills,
            maxLevel = 1, costs = new[] { 300 }
        },
        new ItemInfo
        {
            id = "extralife", name = "EKSTRA CAN",
            description = "Olunce 1 kez dirilis",
            category = ItemCategory.Skills,
            maxLevel = 3, costs = new[] { 200, 500, 1200 }
        },
        new ItemInfo
        {
            id = "coinrange", name = "COIN MENZILI",
            description = "Coinleri uzaktan topla",
            category = ItemCategory.Skills,
            maxLevel = 3, costs = new[] { 80, 200, 450 }
        },
        new ItemInfo
        {
            id = "laserresist", name = "LAZER DIRENCI",
            description = "Lazer hizi kalici azalsin",
            category = ItemCategory.Skills,
            maxLevel = 3, costs = new[] { 150, 400, 900 }
        },
        new ItemInfo
        {
            id = "armor", name = "ZIRH",
            description = "Gelen hasari azaltir",
            category = ItemCategory.Skills,
            maxLevel = 3, costs = new[] { 100, 300, 700 }
        },
        new ItemInfo
        {
            id = "healthregen", name = "CAN YENILEME",
            description = "Zamanla can yenile",
            category = ItemCategory.Skills,
            maxLevel = 3, costs = new[] { 120, 350, 800 }
        },
        new ItemInfo
        {
            id = "armorregen", name = "ZIRH YENILEME",
            description = "Zamanla zirh yenile",
            category = ItemCategory.Skills,
            maxLevel = 3, costs = new[] { 150, 400, 900 }
        },
        new ItemInfo
        {
            id = "hp", name = "CAN ARTISI",
            description = "Maksimum canini arttirir",
            category = ItemCategory.Skills,
            maxLevel = 198, costs = null // Generated dynamically
        },
        new ItemInfo
        {
            id = "shieldupgrade", name = "KALKAN ARTISI",
            description = "Kalkan satin al ve guclendir",
            category = ItemCategory.Skills,
            maxLevel = 198, costs = null // Generated dynamically
        },

        // WEAPONS (permanent, upgradeable +0 to +10, Archero-style)
        new ItemInfo
        {
            id = "bow", name = "YAY",
            description = "Dengeli baslangic silahi",
            category = ItemCategory.Weapons,
            maxLevel = 10, costs = new[] { 0 },
            weaponDamage = 10, weaponRate = 0.6f, weaponSpeed = 10f, weaponSpecial = "",
            upgradeCoinCosts =  new[] { 30,  60,  100, 160, 240, 350, 500, 700,  950,  1300 },
            upgradeStoneCosts = new[] { 1,   1,   2,   2,   3,   3,   4,   5,    6,    8 }
        },
        new ItemInfo
        {
            id = "scythe", name = "TIRPAN",
            description = "Delici atislar, yuksek hasar",
            category = ItemCategory.Weapons,
            maxLevel = 10, costs = new[] { 200 },
            weaponDamage = 15, weaponRate = 0.9f, weaponSpeed = 8f, weaponSpecial = "pierce",
            upgradeCoinCosts =  new[] { 50,  90,  150, 230, 340, 480, 660, 900,  1200, 1600 },
            upgradeStoneCosts = new[] { 1,   2,   2,   3,   3,   4,   5,   6,    7,    10 }
        },
        new ItemInfo
        {
            id = "sawblade", name = "TESTERE",
            description = "Bumerang gibi geri doner",
            category = ItemCategory.Weapons,
            maxLevel = 10, costs = new[] { 300 },
            weaponDamage = 8, weaponRate = 0.7f, weaponSpeed = 7f, weaponSpecial = "boomerang",
            upgradeCoinCosts =  new[] { 60,  110, 180, 270, 390, 550, 750, 1020, 1400, 1850 },
            upgradeStoneCosts = new[] { 2,   2,   3,   3,   4,   4,   5,   6,    8,    10 }
        },
        new ItemInfo
        {
            id = "tornado", name = "KASIRGA",
            description = "Her seyi deler ve geri doner",
            category = ItemCategory.Weapons,
            maxLevel = 10, costs = new[] { 500 },
            weaponDamage = 6, weaponRate = 0.8f, weaponSpeed = 6f, weaponSpecial = "tornado",
            upgradeCoinCosts =  new[] { 80,  140, 220, 330, 470, 650, 880, 1180, 1600, 2100 },
            upgradeStoneCosts = new[] { 2,   3,   3,   4,   4,   5,   6,   7,    9,    12 }
        },
        new ItemInfo
        {
            id = "spear", name = "MIZRAK",
            description = "Uzun menzil, cok hizli mermi",
            category = ItemCategory.Weapons,
            maxLevel = 10, costs = new[] { 400 },
            weaponDamage = 18, weaponRate = 1.0f, weaponSpeed = 16f, weaponSpecial = "",
            upgradeCoinCosts =  new[] { 70,  120, 200, 300, 430, 600, 820, 1100, 1500, 2000 },
            upgradeStoneCosts = new[] { 2,   2,   3,   3,   4,   5,   5,   6,    8,    10 }
        },
        new ItemInfo
        {
            id = "staff", name = "ASA",
            description = "Gudumlu mermiler, otomatik hedef",
            category = ItemCategory.Weapons,
            maxLevel = 10, costs = new[] { 600 },
            weaponDamage = 7, weaponRate = 0.5f, weaponSpeed = 7f, weaponSpecial = "homing",
            upgradeCoinCosts =  new[] { 90,  160, 250, 370, 520, 720, 980, 1300, 1750, 2300 },
            upgradeStoneCosts = new[] { 3,   3,   4,   4,   5,   5,   6,   7,    9,    12 }
        },
    };

    // Consumable flags — reset each game
    private bool shieldActive;
    private bool magnetActive;
    private bool headstartActive;
    private bool slowlaserActive;

    // Weapon — equipped weapon id (persistent selection, not consumed)
    private string equippedWeaponId;

    // Persistent storage
    private const string SkillKeyPrefix = "skill_";
    private const string SkillHashPrefix = "skill_h_";
    private const string WeaponKeyPrefix = "wpn_";
    private const string WeaponHashPrefix = "wpn_h_";
    private const string EquippedWeaponKey = "wpn_equipped";
    private const string Salt = "Sh0p_s4lt_!q9";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Load equipped weapon from prefs — also clear if weapon no longer exists in item list
        equippedWeaponId = PlayerPrefs.GetString(EquippedWeaponKey, "");
        if (!string.IsNullOrEmpty(equippedWeaponId))
        {
            if (GetItem(equippedWeaponId) == null || GetWeaponLevel(equippedWeaponId) < 0)
                equippedWeaponId = "";
        }

        // Generate dynamic cost arrays for hp and shield upgrades (198 levels each)
        GenerateScalingCosts("hp", 198, 10, 500);
        GenerateScalingCosts("shieldupgrade", 198, 15, 600);

        // No auto-grant — player starts unarmed, buys weapons from shop
    }

    private void GenerateScalingCosts(string id, int levels, int baseCost, int maxCost)
    {
        var info = GetItem(id);
        if (info == null || info.costs != null) return;
        info.costs = new int[levels];
        for (int i = 0; i < levels; i++)
        {
            float t = (float)i / (levels - 1);
            info.costs[i] = Mathf.RoundToInt(Mathf.Lerp(baseCost, maxCost, t * t));
        }
    }

    // ── Public API ──

    public static ItemInfo[] GetAllItems() => allItems;

    public static ItemInfo[] GetItemsByCategory(ItemCategory cat)
    {
        int count = 0;
        foreach (var item in allItems)
            if (item.category == cat) count++;
        var result = new ItemInfo[count];
        int idx = 0;
        foreach (var item in allItems)
            if (item.category == cat) result[idx++] = item;
        return result;
    }

    // ── Skills (permanent) ──

    public int GetSkillLevel(string id)
    {
        string key = SkillKeyPrefix + id;
        string hashKey = SkillHashPrefix + id;
        return LoadSecure(key, hashKey);
    }

    public bool IsSkillMaxed(string id)
    {
        var info = GetItem(id);
        if (info == null) return true;
        return GetSkillLevel(id) >= info.maxLevel;
    }

    public int GetNextCost(string id)
    {
        var info = GetItem(id);
        if (info == null) return 0;

        if (info.category == ItemCategory.Shop)
            return info.costs[0];

        if (info.category == ItemCategory.Weapons)
        {
            int level = GetWeaponLevel(id);
            if (level < 0) return info.costs[0]; // not owned, return buy cost
            if (level >= info.maxLevel) return 0;
            return info.upgradeCoinCosts[level];
        }

        int skillLevel = GetSkillLevel(id);
        if (skillLevel >= info.maxLevel) return 0;
        return info.costs[skillLevel];
    }

    // ── Weapons (permanent, upgradeable) ──

    /// <summary>
    /// Returns weapon enhancement level. -1 = not owned, 0 = +0, 1 = +1, etc.
    /// </summary>
    public int GetWeaponLevel(string id)
    {
        string key = WeaponKeyPrefix + id;
        string hashKey = WeaponHashPrefix + id;
        if (!PlayerPrefs.HasKey(key)) return -1; // not owned
        return LoadSecure(key, hashKey);
    }

    public bool OwnsWeapon(string id) => GetWeaponLevel(id) >= 0;

    public bool IsWeaponMaxed(string id)
    {
        var info = GetItem(id);
        if (info == null) return true;
        int level = GetWeaponLevel(id);
        return level >= info.maxLevel;
    }

    public int GetWeaponUpgradeStoneCost(string id)
    {
        var info = GetItem(id);
        if (info == null) return 0;
        int level = GetWeaponLevel(id);
        if (level < 0 || level >= info.maxLevel) return 0;
        return info.upgradeStoneCosts[level];
    }

    public int GetWeaponUpgradeCoinCost(string id)
    {
        var info = GetItem(id);
        if (info == null) return 0;
        int level = GetWeaponLevel(id);
        if (level < 0 || level >= info.maxLevel) return 0;
        return info.upgradeCoinCosts[level];
    }

    /// <summary>
    /// Get weapon stats scaled by enhancement level.
    /// Each +1 gives: +12% damage, -3% rate (faster).
    /// </summary>
    public void GetWeaponStats(string id, out int damage, out float rate, out float speed, out string special)
    {
        var info = GetItem(id);
        if (info == null)
        {
            damage = 10; rate = 0.6f; speed = 10f; special = "";
            return;
        }
        int level = Mathf.Max(0, GetWeaponLevel(id));
        float lvlF = level;

        damage = Mathf.Max(1, Mathf.RoundToInt(info.weaponDamage * (1f + lvlF * 0.12f)));
        rate = Mathf.Max(0.1f, info.weaponRate * (1f - lvlF * 0.03f));
        speed = info.weaponSpeed;
        special = info.weaponSpecial ?? "";
    }

    public bool CanBuy(string id)
    {
        var info = GetItem(id);
        if (info == null) return false;

        if (info.category == ItemCategory.Shop)
        {
            if (IsConsumableActive(id)) return false;
            return ScoreManager.Instance != null && ScoreManager.Instance.Coins >= info.costs[0];
        }

        if (info.category == ItemCategory.Weapons)
        {
            if (ScoreManager.Instance == null) return false;
            int level = GetWeaponLevel(id);

            if (level < 0)
            {
                // Not owned — check buy cost
                return ScoreManager.Instance.Coins >= info.costs[0];
            }

            if (level >= info.maxLevel) return false;

            // Upgrade: need coins + stones
            return ScoreManager.Instance.Coins >= info.upgradeCoinCosts[level]
                && ScoreManager.Instance.UpgradeStones >= info.upgradeStoneCosts[level];
        }

        // Skill: check level cap
        int skillLevel = GetSkillLevel(id);
        if (skillLevel >= info.maxLevel) return false;
        return ScoreManager.Instance != null && ScoreManager.Instance.Coins >= info.costs[skillLevel];
    }

    public bool TryBuy(string id)
    {
        if (!CanBuy(id)) return false;

        var info = GetItem(id);

        if (info.category == ItemCategory.Shop)
        {
            int cost = info.costs[0];
            if (ScoreManager.Instance == null || !ScoreManager.Instance.SpendCoins(cost))
                return false;
            SetConsumableActive(id, true);
        }
        else if (info.category == ItemCategory.Weapons)
        {
            int level = GetWeaponLevel(id);

            if (level < 0)
            {
                // Buy weapon (acquire at +0)
                int cost = info.costs[0];
                if (ScoreManager.Instance == null || !ScoreManager.Instance.SpendCoins(cost))
                    return false;

                string key = WeaponKeyPrefix + id;
                string hashKey = WeaponHashPrefix + id;
                SaveSecure(key, hashKey, 0);

                // Auto-equip if nothing equipped
                if (string.IsNullOrEmpty(equippedWeaponId))
                {
                    equippedWeaponId = id;
                    PlayerPrefs.SetString(EquippedWeaponKey, equippedWeaponId);
                    PlayerPrefs.Save();
                }
            }
            else
            {
                // Upgrade weapon (+level)
                int coinCost = info.upgradeCoinCosts[level];
                int stoneCost = info.upgradeStoneCosts[level];

                if (ScoreManager.Instance == null) return false;
                if (!ScoreManager.Instance.SpendCoins(coinCost)) return false;
                if (!ScoreManager.Instance.SpendStones(stoneCost))
                {
                    // Refund coins if stones fail
                    ScoreManager.Instance.AddCoins(coinCost);
                    return false;
                }

                string key = WeaponKeyPrefix + id;
                string hashKey = WeaponHashPrefix + id;
                SaveSecure(key, hashKey, level + 1);
            }
        }
        else
        {
            // Skill: level up
            int level = GetSkillLevel(id);
            int cost = info.costs[level];
            if (ScoreManager.Instance == null || !ScoreManager.Instance.SpendCoins(cost))
                return false;

            string key = SkillKeyPrefix + id;
            string hashKey = SkillHashPrefix + id;
            SaveSecure(key, hashKey, level + 1);
        }

        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Equip an owned weapon (select for use in game).
    /// </summary>
    public bool EquipWeapon(string id)
    {
        if (!OwnsWeapon(id)) return false;
        equippedWeaponId = id;
        PlayerPrefs.SetString(EquippedWeaponKey, equippedWeaponId);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
        return true;
    }

    public void UnequipWeapon()
    {
        equippedWeaponId = "";
        PlayerPrefs.SetString(EquippedWeaponKey, "");
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    // ── Consumable state ──

    public bool IsConsumableActive(string id)
    {
        switch (id)
        {
            case "shield": return shieldActive;
            case "magnet": return magnetActive;
            case "headstart": return headstartActive;
            case "slowlaser": return slowlaserActive;
            default: return false;
        }
    }

    private void SetConsumableActive(string id, bool active)
    {
        switch (id)
        {
            case "shield": shieldActive = active; break;
            case "magnet": magnetActive = active; break;
            case "headstart": headstartActive = active; break;
            case "slowlaser": slowlaserActive = active; break;
        }
    }

    // Consume methods — called at game start, returns true if was active
    public bool ConsumeShield()
    {
        if (!shieldActive) return false;
        shieldActive = false;
        OnChanged?.Invoke();
        return true;
    }

    public bool ConsumeHeadstart()
    {
        if (!headstartActive) return false;
        headstartActive = false;
        OnChanged?.Invoke();
        return true;
    }

    public bool ConsumeSlowLaser()
    {
        if (!slowlaserActive) return false;
        slowlaserActive = false;
        OnChanged?.Invoke();
        return true;
    }

    public bool ConsumeMagnet()
    {
        if (!magnetActive) return false;
        magnetActive = false;
        OnChanged?.Invoke();
        return true;
    }

    // ── Weapon state ──

    public string GetEquippedWeaponId() => equippedWeaponId;

    public ItemInfo GetEquippedWeaponInfo()
    {
        if (string.IsNullOrEmpty(equippedWeaponId)) return null;
        if (!OwnsWeapon(equippedWeaponId)) return null;
        return GetItem(equippedWeaponId);
    }

    /// <summary>
    /// Get equipped weapon info for game start. Weapon is NOT consumed — it's permanent.
    /// Returns null if no weapon equipped.
    /// </summary>
    public ItemInfo GetWeaponForGame()
    {
        if (string.IsNullOrEmpty(equippedWeaponId)) return null;
        if (!OwnsWeapon(equippedWeaponId)) return null;
        return GetItem(equippedWeaponId);
    }

    // ── Gameplay queries (Skills) ──

    public float GetJumpMultiplier()
    {
        int level = GetSkillLevel("jump");
        return 1f + level * 0.08f; // +8% per level, max +40%
    }

    public bool HasDoubleCoins()
    {
        return GetSkillLevel("doublecoins") > 0;
    }

    public int GetExtraLives()
    {
        return GetSkillLevel("extralife");
    }

    public float GetCoinRangeMultiplier()
    {
        int level = GetSkillLevel("coinrange");
        return 1f + level * 0.5f; // 1x, 1.5x, 2x, 2.5x range
    }

    public float GetLaserSpeedMultiplier()
    {
        int level = GetSkillLevel("laserresist");
        return 1f - level * 0.1f; // 1.0, 0.9, 0.8, 0.7 speed
    }

    public int GetArmorPoints()
    {
        return GetSkillLevel("armor"); // 0, 1, 2, 3 armor
    }

    public float GetHealthRegenInterval()
    {
        int level = GetSkillLevel("healthregen");
        if (level <= 0) return 0f;
        return level switch { 1 => 15f, 2 => 10f, 3 => 6f, _ => 0f };
    }

    public float GetArmorRegenInterval()
    {
        int level = GetSkillLevel("armorregen");
        if (level <= 0) return 0f;
        return level switch { 1 => 20f, 2 => 14f, 3 => 8f, _ => 0f };
    }

    // ── Reset ──

    public void ResetAllSkills()
    {
        foreach (var item in allItems)
        {
            if (item.category != ItemCategory.Skills) continue;
            string key = SkillKeyPrefix + item.id;
            string hashKey = SkillHashPrefix + item.id;
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(hashKey);
        }
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public void ResetAllWeapons()
    {
        foreach (var item in allItems)
        {
            if (item.category != ItemCategory.Weapons) continue;
            string key = WeaponKeyPrefix + item.id;
            string hashKey = WeaponHashPrefix + item.id;
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.DeleteKey(hashKey);
        }
        equippedWeaponId = "";
        PlayerPrefs.SetString(EquippedWeaponKey, "");
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    // ── Helpers ──

    public ItemInfo GetItem(string id)
    {
        foreach (var item in allItems)
            if (item.id == id) return item;
        return null;
    }

    private void SaveSecure(string valueKey, string hashKey, int value)
    {
        PlayerPrefs.SetInt(valueKey, value);
        PlayerPrefs.SetString(hashKey, ComputeHash(valueKey, value));
        PlayerPrefs.Save();
    }

    private int LoadSecure(string valueKey, string hashKey)
    {
        if (!PlayerPrefs.HasKey(valueKey)) return 0;
        int value = PlayerPrefs.GetInt(valueKey, 0);
        string storedHash = PlayerPrefs.GetString(hashKey, "");
        if (storedHash != ComputeHash(valueKey, value))
        {
            PlayerPrefs.DeleteKey(valueKey);
            PlayerPrefs.DeleteKey(hashKey);
            PlayerPrefs.Save();
            return 0;
        }
        return value;
    }

    private string ComputeHash(string key, int value)
    {
        string raw = Salt + key + value.ToString() + Salt;
        int hash = 0;
        for (int i = 0; i < raw.Length; i++)
        {
            hash = hash * 31 + raw[i];
            hash ^= (hash >> 16);
        }
        return hash.ToString("X8");
    }
}
