using UnityEngine;

/// <summary>
/// Central loader for Synty POLYGON prefabs.
/// Loads from Resources/Synty/ at runtime. Falls back gracefully if missing.
/// </summary>
public class SyntyAssets : MonoBehaviour
{
    public static SyntyAssets Instance { get; private set; }

    // Character prefabs
    public GameObject BeachHeroPrefab { get; private set; }
    public GameObject GanzSeHeroPrefab { get; private set; }
    public GameObject HeroPrefab { get; private set; }
    public GameObject CopPrefab { get; private set; }
    public GameObject CowboyPrefab { get; private set; }
    public GameObject FemalePrefab { get; private set; }
    public GameObject TownFemalePrefab { get; private set; }

    // Prop prefabs
    public GameObject CoinPrefab { get; private set; }
    public GameObject CratePrefab { get; private set; }
    public GameObject BarrelWoodPrefab { get; private set; }
    public GameObject BarrelMetalPrefab { get; private set; }
    public GameObject RockPrefab { get; private set; }

    // Weapon prefabs
    public GameObject ShieldPrefab { get; private set; }
    public GameObject AxePrefab { get; private set; }

    // Textures
    public Texture2D GanzSePalette { get; private set; }

    public bool IsLoaded { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        LoadAll();
    }

    private void LoadAll()
    {
        // Characters
        BeachHeroPrefab = Resources.Load<GameObject>("Models/Beach");
        GanzSeHeroPrefab = Resources.Load<GameObject>("Synty/GanzSe_Hero");
        HeroPrefab = Resources.Load<GameObject>("Synty/SM_Chr_Male_01");
        CopPrefab = Resources.Load<GameObject>("Synty/SM_Bean_Cop_01");
        CowboyPrefab = Resources.Load<GameObject>("Synty/SM_Bean_Cowboy_01");
        FemalePrefab = Resources.Load<GameObject>("Synty/SM_Bean_Female_01");
        TownFemalePrefab = Resources.Load<GameObject>("Synty/SM_Bean_Town_Female_01");

        // Props
        CoinPrefab = Resources.Load<GameObject>("Synty/SM_Gen_Prop_Coin_01");
        CratePrefab = Resources.Load<GameObject>("Synty/SM_Gen_Prop_Crate_01");
        BarrelWoodPrefab = Resources.Load<GameObject>("Synty/SM_Gen_Prop_Barrel_Wood_01");
        BarrelMetalPrefab = Resources.Load<GameObject>("Synty/SM_Gen_Prop_Barrel_Metal_01");
        RockPrefab = Resources.Load<GameObject>("Synty/SM_Gen_Env_Rock_01");

        // Weapons
        ShieldPrefab = Resources.Load<GameObject>("Synty/SM_Wep_Shield_04");
        AxePrefab = Resources.Load<GameObject>("Synty/SM_Gen_Wep_Axe_01");

        // Textures
        GanzSePalette = Resources.Load<Texture2D>("Synty/GanzSe_Palette");

        IsLoaded = BeachHeroPrefab != null || GanzSeHeroPrefab != null || HeroPrefab != null;

        if (IsLoaded)
            Debug.Log("[SyntyAssets] Loaded Synty prefabs successfully");
        else
            Debug.LogWarning("[SyntyAssets] Synty prefabs not found in Resources/Synty/ -- using procedural visuals");
    }

    /// <summary>
    /// Instantiate a Synty prefab as a child, scaled and positioned.
    /// Returns the instance or null if prefab is missing.
    /// Disables all colliders on the instance to avoid physics interference.
    /// </summary>
    public static GameObject Spawn(GameObject prefab, Transform parent, Vector3 localPos, float scale, float yRotation = 0f)
    {
        if (prefab == null) return null;

        var instance = Instantiate(prefab, parent);
        instance.transform.localPosition = localPos;
        instance.transform.localRotation = Quaternion.Euler(0, yRotation, 0);
        instance.transform.localScale = Vector3.one * scale;

        // Disable all colliders to prevent physics interference
        foreach (var col in instance.GetComponentsInChildren<Collider>())
        {
            col.enabled = false;
            Destroy(col);
        }

        // Disable any Rigidbodies
        foreach (var rb in instance.GetComponentsInChildren<Rigidbody>())
        {
            Destroy(rb);
        }

        // Handle Animators: pose the character naturally, then remove
        foreach (var anim in instance.GetComponentsInChildren<Animator>())
        {
            // Try to set a natural idle pose via bones before removing
            PoseCharacterIdle(anim.transform);
            Destroy(anim);
        }

        // Remove Animation (legacy) components
        foreach (var anim in instance.GetComponentsInChildren<Animation>())
        {
            Destroy(anim);
        }

        return instance;
    }

    /// <summary>
    /// Manually pose a rigged character into a natural idle stance by rotating bones.
    /// Works by finding common bone names and rotating arms down.
    /// </summary>
    private static void PoseCharacterIdle(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>())
        {
            string name = t.name.ToLower();
            bool isLeft = name.EndsWith("_l") || name.Contains("left") || name.Contains(".l");
            bool isRight = name.EndsWith("_r") || name.Contains("right") || name.Contains(".r");

            // Upper arms down (T-pose -> idle, not too tight)
            if (name.Contains("upperarm") || name.Contains("upper_arm"))
            {
                float sign = isLeft ? 1f : -1f;
                t.localRotation *= Quaternion.Euler(0, 0, sign * 45f);
            }
            // Forearms slightly bent inward
            else if (name.Contains("forearm") || name.Contains("lower_arm"))
            {
                float sign = isLeft ? 1f : -1f;
                t.localRotation *= Quaternion.Euler(0, 0, sign * 12f);
            }
            // Shoulders slightly down
            else if (name.Contains("shoulder") && (isLeft || isRight))
            {
                float sign = isLeft ? 1f : -1f;
                t.localRotation *= Quaternion.Euler(0, 0, sign * 10f);
            }
        }
    }

    /// <summary>
    /// Spawn and fix pink/missing materials by applying URP Lit with given color.
    /// </summary>
    public static GameObject SpawnWithColor(GameObject prefab, Transform parent, Vector3 localPos, float scale, Color color, float yRotation = 0f)
    {
        var instance = Spawn(prefab, parent, localPos, scale, yRotation);
        if (instance == null) return null;

        FixMaterials(instance, color);
        return instance;
    }

    /// <summary>
    /// Fix pink/missing materials on all renderers. Keeps existing texture if valid, applies color tint.
    /// </summary>
    public static void FixMaterials(GameObject obj, Color tint)
    {
        var litShader = Shader.Find("Universal Render Pipeline/Lit");
        if (litShader == null) return;

        // Get palette texture if available
        Texture2D palette = Instance != null ? Instance.GanzSePalette : null;

        foreach (var rend in obj.GetComponentsInChildren<Renderer>())
        {
            var mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                // If material is pink (shader broken) or null, replace it
                if (mats[i] == null || mats[i].shader == null || mats[i].shader.name == "Hidden/InternalErrorShader")
                {
                    var newMat = new Material(litShader);
                    // Apply palette texture
                    if (palette != null)
                    {
                        newMat.SetTexture("_BaseMap", palette);
                    }
                    newMat.color = tint;
                    mats[i] = newMat;
                }
            }
            rend.materials = mats;
        }
    }
}
