using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

/// <summary>
/// Loads all character and enemy models from Resources/Models/ at runtime.
/// Drop new FBX files into the Resources folders -- no code changes needed.
/// </summary>
public class ModelManager : MonoBehaviour
{
    public static ModelManager Instance { get; private set; }

    public UnityEvent OnCharacterChanged;

    public List<CharacterModel> PlayerModels { get; private set; } = new List<CharacterModel>();
    public List<CharacterModel> EnemyModels { get; private set; } = new List<CharacterModel>();

    public bool IsLoaded { get; private set; }

    [System.Serializable]
    public class CharacterModel
    {
        public string id;           // filename without extension
        public string displayName;  // shown in shop
        public GameObject prefab;
        public int cost;            // coin cost (0 = free/default)
        public CharacterStats stats;
    }

    [System.Serializable]
    public class CharacterStats
    {
        public float speedMult = 1f;      // movement speed multiplier
        public float hpMult = 1f;         // max HP multiplier
        public float attackSpeedMult = 1f; // attack speed multiplier
        public float damageMult = 1f;     // damage multiplier
    }

    // Character definitions: Rogue(weakest) → Knight(strongest)
    private static readonly Dictionary<string, (int cost, CharacterStats stats)> characterDefs = new()
    {
        ["Rogue"]     = (0,    new CharacterStats { speedMult = 1.15f, hpMult = 0.9f,  attackSpeedMult = 1.1f,  damageMult = 0.95f }),
        ["Ranger"]    = (500,  new CharacterStats { speedMult = 1.05f, hpMult = 1.0f,  attackSpeedMult = 1.15f, damageMult = 1.0f }),
        ["Barbarian"] = (1500, new CharacterStats { speedMult = 0.95f, hpMult = 1.2f,  attackSpeedMult = 0.9f,  damageMult = 1.15f }),
        ["Mage"]      = (3000, new CharacterStats { speedMult = 1.0f,  hpMult = 0.85f, attackSpeedMult = 1.0f,  damageMult = 1.25f }),
        ["Knight"]    = (5000, new CharacterStats { speedMult = 0.85f, hpMult = 1.4f,  attackSpeedMult = 0.85f, damageMult = 1.1f }),
    };
    // Default weapon per character (Resources path under Models/Weapons/)
    private static readonly Dictionary<string, string> defaultWeapons = new()
    {
        ["Rogue"]     = "dagger",
        ["Ranger"]    = "bow",
        ["Barbarian"] = "axe_1handed",
        ["Mage"]      = "wand",
        ["Knight"]    = "sword_2handed",
    };

    private static readonly int[] enemyCosts = { 0 }; // enemies aren't purchasable

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        MigrateOldCharacters();
        LoadAll();
    }

    private void MigrateOldCharacters()
    {
        // Clean up old Synty character keys
        string[] oldIds = { "beach", "medieval", "king", "spacesuit", "suit", "swat" };
        foreach (var id in oldIds)
        {
            PlayerPrefs.DeleteKey("char_" + id);
        }
        string equipped = PlayerPrefs.GetString("equipped_character", "");
        if (System.Array.Exists(oldIds, x => x == equipped))
        {
            PlayerPrefs.DeleteKey("equipped_character");
        }
        PlayerPrefs.Save();
    }

    private void LoadAll()
    {
        // Load player characters (skip animation-only FBX files)
        var playerPrefabs = Resources.LoadAll<GameObject>("Models/MainCharacter");
        foreach (var prefab in playerPrefabs)
        {
            string name = prefab.name;
            if (name.StartsWith("Rig_")) continue; // skip animation FBX files
            var def = characterDefs.ContainsKey(name) ? characterDefs[name]
                : (cost: 9999, stats: new CharacterStats());

            PlayerModels.Add(new CharacterModel
            {
                id = name.ToLower().Replace(" ", "_"),
                displayName = FormatName(name),
                prefab = prefab,
                cost = def.cost,
                stats = def.stats
            });
        }

        // Sort by cost (Beach first, Swat last)
        PlayerModels.Sort((a, b) => a.cost.CompareTo(b.cost));

        // Load enemy models
        var enemyPrefabs = Resources.LoadAll<GameObject>("Models/Enemies");
        for (int i = 0; i < enemyPrefabs.Length; i++)
        {
            var prefab = enemyPrefabs[i];
            EnemyModels.Add(new CharacterModel
            {
                id = prefab.name.ToLower().Replace(" ", "_"),
                displayName = FormatName(prefab.name),
                prefab = prefab,
                cost = 0
            });
        }

        IsLoaded = PlayerModels.Count > 0 || EnemyModels.Count > 0;

        Debug.Log($"[ModelManager] Loaded {PlayerModels.Count} player models, {EnemyModels.Count} enemy models");
    }

    /// <summary>Get a player model by id. Returns null if not found.</summary>
    public CharacterModel GetPlayerModel(string id)
    {
        return PlayerModels.Find(m => m.id == id);
    }

    /// <summary>Get the currently equipped player model.</summary>
    public CharacterModel GetEquippedPlayerModel()
    {
        string equipped = PlayerPrefs.GetString("equipped_character", "");
        if (!string.IsNullOrEmpty(equipped))
        {
            var model = GetPlayerModel(equipped);
            if (model != null) return model;
        }
        // Fallback to first model
        return PlayerModels.Count > 0 ? PlayerModels[0] : null;
    }

    /// <summary>Check if player owns a character.</summary>
    public bool OwnsCharacter(string id)
    {
        // First character is always owned
        if (PlayerModels.Count > 0 && PlayerModels[0].id == id) return true;
        return PlayerPrefs.GetInt("char_" + id, 0) == 1;
    }

    /// <summary>Purchase a character. Returns true if successful.</summary>
    public bool PurchaseCharacter(string id)
    {
        var model = GetPlayerModel(id);
        if (model == null) return false;
        if (OwnsCharacter(id)) return false;
        if (ScoreManager.Instance == null || ScoreManager.Instance.Coins < model.cost) return false;

        ScoreManager.Instance.SpendCoins(model.cost);
        PlayerPrefs.SetInt("char_" + id, 1);
        PlayerPrefs.Save();
        return true;
    }

    /// <summary>Equip a character.</summary>
    public void EquipCharacter(string id)
    {
        if (!OwnsCharacter(id)) return;
        PlayerPrefs.SetString("equipped_character", id);
        PlayerPrefs.Save();
        OnCharacterChanged?.Invoke();
    }

    /// <summary>Get a random enemy model. Returns null if none loaded.</summary>
    public CharacterModel GetRandomEnemyModel()
    {
        if (EnemyModels.Count == 0) return null;
        return EnemyModels[Random.Range(0, EnemyModels.Count)];
    }

    /// <summary>Get enemy model by index (for specific enemy types).</summary>
    public CharacterModel GetEnemyModel(int index)
    {
        if (index < 0 || index >= EnemyModels.Count) return null;
        return EnemyModels[index];
    }

    /// <summary>
    /// Spawn a model as child of parent. Cleans up colliders, rigidbodies.
    /// Keeps Animator intact for animation support.
    /// </summary>
    public static GameObject SpawnModel(GameObject prefab, Transform parent, float scale = 0.45f)
    {
        if (prefab == null) return null;

        var instance = Instantiate(prefab, parent);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one * scale;

        // Remove colliders
        foreach (var col in instance.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
            Destroy(col);
        }

        // Remove rigidbodies
        foreach (var rb in instance.GetComponentsInChildren<Rigidbody>())
            Destroy(rb);

        return instance;
    }

    /// <summary>
    /// Attach the default weapon model to the character's right hand bone.
    /// Call after SpawnModel. Uses Humanoid Avatar's RightHand bone.
    /// </summary>
    public static GameObject AttachDefaultWeapon(GameObject characterInstance, string characterName)
    {
        if (characterInstance == null || string.IsNullOrEmpty(characterName)) return null;

        // Find default weapon for this character
        if (!defaultWeapons.ContainsKey(characterName)) return null;
        string weaponName = defaultWeapons[characterName];

        // Load weapon prefab
        var weaponPrefab = Resources.Load<GameObject>($"Models/Weapons/{weaponName}");
        if (weaponPrefab == null)
        {
            Debug.LogWarning($"[ModelManager] Weapon not found: Models/Weapons/{weaponName}");
            return null;
        }

        // Find right hand bone via Animator (Humanoid rig)
        var animator = characterInstance.GetComponentInChildren<Animator>();
        if (animator == null || !animator.isHuman)
        {
            Debug.LogWarning("[ModelManager] No Humanoid Animator found for weapon attach");
            return null;
        }

        Transform handBone = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (handBone == null)
        {
            Debug.LogWarning("[ModelManager] RightHand bone not found");
            return null;
        }

        // Instantiate weapon as child of hand bone
        var weapon = Instantiate(weaponPrefab, handBone);
        weapon.name = "Weapon_" + weaponName;
        weapon.transform.localPosition = Vector3.zero;
        weapon.transform.localRotation = Quaternion.identity;
        weapon.transform.localScale = Vector3.one;

        // Remove colliders/rigidbodies from weapon
        foreach (var col in weapon.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
            Destroy(col);
        }
        foreach (var rb in weapon.GetComponentsInChildren<Rigidbody>())
            Destroy(rb);

        Debug.Log($"[ModelManager] Attached weapon '{weaponName}' to {characterName}'s right hand");
        return weapon;
    }

    /// <summary>Get the default weapon name for a character.</summary>
    public static string GetDefaultWeaponName(string characterName)
    {
        return defaultWeapons.ContainsKey(characterName) ? defaultWeapons[characterName] : null;
    }

    private static string FormatName(string rawName)
    {
        // "Hazmat Man" -> "HAZMAT MAN", "Characters_Captain_Barbarossa" -> "CAPTAIN BARBAROSSA"
        string name = rawName.Replace("_", " ").Replace("Characters ", "").Replace("Character ", "");
        return name.ToUpper().Trim();
    }
}
