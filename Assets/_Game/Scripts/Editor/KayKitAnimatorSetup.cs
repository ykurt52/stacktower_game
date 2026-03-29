using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Configures KayKit FBX import settings and creates AnimatorControllers.
/// Step 1: KayKit > 1. FBX Import Ayarla
/// Step 2: KayKit > 2. Animator Olustur
/// Debug:  KayKit > Debug Rig Info
/// </summary>
public static class KayKitAnimatorSetup
{
    private const string HERO_PATH = "Assets/_Game/Resources/Models/MainCharacter";
    private const string ENEMY_PATH = "Assets/_Game/Resources/Models/Enemies";
    private const string ENV_PATH = "Assets/_Game/Resources/Models/Environment";

    private static readonly string[] CHARACTER_NAMES = { "Rogue", "Ranger", "Barbarian", "Mage", "Knight", "Rogue_Hooded" };
    private static readonly string[] ANIM_FBXS = {
        "Rig_Medium_General", "Rig_Medium_MovementBasic", "Rig_Medium_MovementAdvanced",
        "Rig_Medium_CombatMelee", "Rig_Medium_CombatRanged", "Rig_Medium_Special"
    };

    private static readonly string[] SKELETON_NAMES = { "Skeleton_Minion", "Skeleton_Rogue", "Skeleton_Warrior", "Skeleton_Mage" };
    private static readonly string[] SKELETON_ANIM_FBXS = {
        "Skeleton_Rig_Medium_General", "Skeleton_Rig_Medium_MovementBasic", "Skeleton_Rig_Medium_MovementAdvanced",
        "Skeleton_Rig_Medium_CombatMelee", "Skeleton_Rig_Medium_CombatRanged", "Skeleton_Rig_Medium_Special"
    };

    // ─────────────────────────────────────────────
    // STEP 1: FBX Import Settings
    // ─────────────────────────────────────────────

    [MenuItem("KayKit/1. FBX Import Ayarla")]
    public static void SetupFBXImport()
    {
        int configured = 0;

        // Configure character FBX files
        foreach (var charName in CHARACTER_NAMES)
        {
            string fbxPath = $"{HERO_PATH}/{charName}.fbx";
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[KayKit] FBX bulunamadi: {fbxPath}");
                continue;
            }

            // Rig: Humanoid — universal avatar, clips work across all characters
            importer.animationType = ModelImporterAnimationType.Human;

            // Materials: Use External Materials
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnTextureName,
                ModelImporterMaterialSearch.RecursiveUp);

            // Scale
            importer.globalScale = 1f;
            importer.useFileScale = true;

            // Import animation to generate Avatar
            importer.importAnimation = true;

            importer.SaveAndReimport();
            configured++;
            Debug.Log($"[KayKit] Configured character FBX: {charName} (Humanoid)");
        }

        // Configure animation FBX files
        foreach (var animName in ANIM_FBXS)
        {
            string fbxPath = $"{HERO_PATH}/{animName}.fbx";
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[KayKit] Animation FBX bulunamadi: {fbxPath}");
                continue;
            }

            // Rig: Humanoid (must match character rig type)
            importer.animationType = ModelImporterAnimationType.Human;

            // Animation: import clips
            importer.importAnimation = true;

            // Don't import materials from animation FBX
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            // Set looping on movement clips (Idle, Run, Walk)
            var clipAnims = importer.defaultClipAnimations;
            for (int i = 0; i < clipAnims.Length; i++)
            {
                string n = clipAnims[i].name.ToLower();
                bool shouldLoop = n.Contains("idle") || n.Contains("run") || n.Contains("walk");
                clipAnims[i].loopTime = shouldLoop;
            }
            importer.clipAnimations = clipAnims;

            importer.SaveAndReimport();
            configured++;
            Debug.Log($"[KayKit] Configured animation FBX: {animName} (Humanoid, loop set)");
        }

        // ── Skeleton enemy FBX files ──
        foreach (var skelName in SKELETON_NAMES)
        {
            string fbxPath = $"{ENEMY_PATH}/{skelName}.fbx";
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) { Debug.LogWarning($"[KayKit] Skeleton FBX bulunamadi: {fbxPath}"); continue; }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnTextureName,
                ModelImporterMaterialSearch.RecursiveUp);
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.importAnimation = true;

            importer.SaveAndReimport();
            configured++;
            Debug.Log($"[KayKit] Configured skeleton FBX: {skelName} (Humanoid)");
        }

        // Skeleton animation FBX files
        foreach (var animName in SKELETON_ANIM_FBXS)
        {
            string fbxPath = $"{ENEMY_PATH}/{animName}.fbx";
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) continue;

            importer.animationType = ModelImporterAnimationType.Human;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;

            var clipAnims = importer.defaultClipAnimations;
            for (int i = 0; i < clipAnims.Length; i++)
            {
                string n = clipAnims[i].name.ToLower();
                bool shouldLoop = n.Contains("idle") || n.Contains("run") || n.Contains("walk");
                clipAnims[i].loopTime = shouldLoop;
            }
            importer.clipAnimations = clipAnims;

            importer.SaveAndReimport();
            configured++;
            Debug.Log($"[KayKit] Configured skeleton animation FBX: {animName} (Humanoid, loop set)");
        }

        // Skeleton weapon FBX files (no rig, no animation)
        if (System.IO.Directory.Exists(ENEMY_PATH))
        {
            var skelWeaponFiles = System.IO.Directory.GetFiles(ENEMY_PATH, "Skeleton_*.fbx");
            foreach (var weaponPath in skelWeaponFiles)
            {
                string assetPath = weaponPath.Replace("\\", "/");
                // Skip character and animation FBX files
                string fileName = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                if (System.Array.Exists(SKELETON_NAMES, x => x == fileName)) continue;
                if (System.Array.Exists(SKELETON_ANIM_FBXS, x => x == fileName)) continue;

                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null) continue;

                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnTextureName,
                    ModelImporterMaterialSearch.RecursiveUp);
                importer.globalScale = 1f;
                importer.useFileScale = true;

                importer.SaveAndReimport();
                configured++;
            }
            Debug.Log($"[KayKit] Configured skeleton weapon FBX files");
        }

        // ── Hero weapon FBX files ──
        var weaponFiles = System.IO.Directory.GetFiles(
            "Assets/_Game/Resources/Models/Weapons", "*.fbx");
        foreach (var weaponPath in weaponFiles)
        {
            string assetPath = weaponPath.Replace("\\", "/");
            var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null) continue;

            importer.animationType = ModelImporterAnimationType.None;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnTextureName,
                ModelImporterMaterialSearch.RecursiveUp);
            importer.globalScale = 1f;
            importer.useFileScale = true;

            importer.SaveAndReimport();
            configured++;
        }
        Debug.Log($"[KayKit] Configured {weaponFiles.Length} weapon FBX files");

        // ── Environment FBX files (no rig, no animation) ──
        if (System.IO.Directory.Exists(ENV_PATH))
        {
            var envFiles = System.IO.Directory.GetFiles(ENV_PATH, "*.fbx");
            foreach (var envPath in envFiles)
            {
                string assetPath = envPath.Replace("\\", "/");
                var importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
                if (importer == null) continue;

                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                importer.SearchAndRemapMaterials(ModelImporterMaterialName.BasedOnTextureName,
                    ModelImporterMaterialSearch.RecursiveUp);
                importer.globalScale = 1f;
                importer.useFileScale = true;

                importer.SaveAndReimport();
                configured++;
            }
            Debug.Log($"[KayKit] Configured {envFiles.Length} environment FBX files");
        }

        AssetDatabase.Refresh();

        var clips = CollectAllClips();

        EditorUtility.DisplayDialog("KayKit FBX Import",
            $"{configured} FBX dosyasi konfigure edildi (Humanoid).\n" +
            $"{clips.Count} animasyon clip bulundu.\n\n" +
            "Simdi 'KayKit > 2. Animator Olustur' calistirin.",
            "Tamam");
    }

    // ─────────────────────────────────────────────
    // STEP 2: Animator Controllers
    // ─────────────────────────────────────────────

    [MenuItem("KayKit/2. Animator Olustur")]
    public static void SetupAnimators()
    {
        var allClips = CollectAllClips();

        if (allClips.Count == 0)
        {
            EditorUtility.DisplayDialog("KayKit Animator Setup",
                "Animasyon clip bulunamadi!\n\n" +
                "Once 'KayKit > 1. FBX Import Ayarla' calistirin.\n" +
                "Sonra tekrar deneyin.",
                "Tamam");
            return;
        }

        // ── Shared clips ──
        AnimationClip idleClip = FindClip(allClips, "Idle_A", "Idle_B");
        AnimationClip runClip = FindClip(allClips, "Running_A", "Running_B");
        AnimationClip deathClip = FindClip(allClips, "Death_A", "Death_B");
        AnimationClip hitClip = FindClip(allClips, "Hit_A", "Hit_B");
        AnimationClip dodgeClip = FindClip(allClips, "Dodge_Forward", "Dodge_Right", "Dodge_Left");

        // ── Character-specific attack clips ──
        AnimationClip meleeSlash = FindClip(allClips, "Melee_1H_Attack_Slice_Diagonal", "Melee_1H_Attack_Chop");
        AnimationClip melee2H = FindClip(allClips, "Melee_2H_Attack_Slice", "Melee_2H_Attack_Chop");
        AnimationClip bowShoot = FindClip(allClips, "Ranged_Bow_Release", "Ranged_Bow_Draw");
        AnimationClip magicCast = FindClip(allClips, "Ranged_Magic_Spellcasting", "Ranged_Magic_Shoot");
        AnimationClip ranged1H = FindClip(allClips, "Ranged_1H_Shoot", "Ranged_1H_Shooting");
        AnimationClip throwClip = FindClip(allClips, "Throw");

        // Skeleton-specific
        AnimationClip skelIdle = FindClip(allClips, "Skeletons_Idle");
        AnimationClip skelWalk = FindClip(allClips, "Skeletons_Walking");
        AnimationClip skelDeath = FindClip(allClips, "Skeletons_Death");

        if (skelIdle == null) skelIdle = idleClip;
        if (skelWalk == null) skelWalk = runClip;
        if (skelDeath == null) skelDeath = deathClip;

        // Fallbacks
        if (dodgeClip == null) dodgeClip = runClip;
        if (melee2H == null) melee2H = meleeSlash;
        if (ranged1H == null) ranged1H = throwClip ?? meleeSlash;

        // ── Hero AnimatorControllers (character-specific attacks) ──
        // Rogue: dagger → 1H melee slash
        CreateAnimatorController(HERO_PATH, "Rogue", idleClip, runClip, meleeSlash, deathClip, hitClip, dodgeClip, throwClip);
        // Ranger: bow → bow shoot
        CreateAnimatorController(HERO_PATH, "Ranger", idleClip, runClip, bowShoot ?? meleeSlash, deathClip, hitClip, dodgeClip, throwClip);
        // Barbarian: axe → 2H melee
        CreateAnimatorController(HERO_PATH, "Barbarian", idleClip, runClip, melee2H, deathClip, hitClip, dodgeClip, throwClip);
        // Mage: wand → magic spellcasting
        CreateAnimatorController(HERO_PATH, "Mage", idleClip, runClip, magicCast ?? meleeSlash, deathClip, hitClip, dodgeClip, throwClip);
        // Knight: sword 2H → 2H melee
        CreateAnimatorController(HERO_PATH, "Knight", idleClip, runClip, melee2H, deathClip, hitClip, dodgeClip, throwClip);

        // ── Skeleton AnimatorControllers ──
        // Minion: 1H melee (blade)
        CreateAnimatorController(ENEMY_PATH, "Skeleton_Minion", skelIdle, skelWalk, meleeSlash, skelDeath, hitClip, dodgeClip, null);
        // Warrior: 2H melee (axe)
        CreateAnimatorController(ENEMY_PATH, "Skeleton_Warrior", skelIdle, skelWalk, melee2H, skelDeath, hitClip, dodgeClip, null);
        // Rogue: ranged (crossbow)
        CreateAnimatorController(ENEMY_PATH, "Skeleton_Rogue", skelIdle, skelWalk, ranged1H, skelDeath, hitClip, dodgeClip, null);
        // Mage: magic (staff)
        CreateAnimatorController(ENEMY_PATH, "Skeleton_Mage", skelIdle, skelWalk, magicCast ?? meleeSlash, skelDeath, hitClip, dodgeClip, null);

        int total = 5 + SKELETON_NAMES.Length;
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var skelClipCount = CollectSkeletonClips().Count;
        EditorUtility.DisplayDialog("KayKit Animator Setup",
            $"{total} AnimatorController olusturuldu!\n" +
            $"Hero clip: {allClips.Count}, Skeleton clip: {skelClipCount}",
            "Tamam");
    }

    // ─────────────────────────────────────────────
    // DEBUG
    // ─────────────────────────────────────────────

    // ─────────────────────────────────────────────
    // STEP 3: Create Skeleton Enemy Stats
    // ─────────────────────────────────────────────

    [MenuItem("KayKit/3. Skeleton Enemy Stats Olustur")]
    public static void CreateSkeletonEnemyStats()
    {
        string dir = "Assets/_Game/Data/Enemies";
        if (!AssetDatabase.IsValidFolder(dir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Data"))
                AssetDatabase.CreateFolder("Assets/_Game", "Data");
            AssetDatabase.CreateFolder("Assets/_Game/Data", "Enemies");
        }

        // Delete old Synty enemy stats
        var oldGuids = AssetDatabase.FindAssets("t:EnemyStatsSO", new[] { dir });
        foreach (var g in oldGuids)
            AssetDatabase.DeleteAsset(AssetDatabase.GUIDToAssetPath(g));

        int created = 0;

        // Minion Low — blade only, weakest
        CreateEnemySO(dir, "Skeleton_MinionLow", ArenaEnemy.EnemyType.Melee,
            "skeleton_minion", hp: 15, armor: 0, speed: 2.2f,
            contactDmg: 6, attackRange: 0.9f, cooldown: 0.9f,
            xp: 1, coin: 1, rightWeapon: "Skeleton_Blade", leftWeapon: "");

        // Minion Med — blade + shield, more armor
        CreateEnemySO(dir, "Skeleton_MinionMed", ArenaEnemy.EnemyType.Melee,
            "skeleton_minion", hp: 25, armor: 10, speed: 2.0f,
            contactDmg: 8, attackRange: 0.9f, cooldown: 0.85f,
            xp: 2, coin: 2, rightWeapon: "Skeleton_Blade", leftWeapon: "Skeleton_Shield_Small_A");

        // Warrior Low — axe only
        CreateEnemySO(dir, "Skeleton_WarriorLow", ArenaEnemy.EnemyType.Heavy,
            "skeleton_warrior", hp: 40, armor: 5, speed: 1.6f,
            contactDmg: 12, attackRange: 1.1f, cooldown: 1.2f,
            xp: 3, coin: 3, rightWeapon: "Skeleton_Axe", leftWeapon: "");

        // Warrior Med — axe + shield, tanky
        CreateEnemySO(dir, "Skeleton_WarriorMed", ArenaEnemy.EnemyType.Heavy,
            "skeleton_warrior", hp: 60, armor: 20, speed: 1.4f,
            contactDmg: 15, attackRange: 1.1f, cooldown: 1.1f,
            xp: 4, coin: 4, rightWeapon: "Skeleton_Axe", leftWeapon: "Skeleton_Shield_Small_A");

        // Rogue Low — crossbow, ranged
        CreateEnemySO(dir, "Skeleton_RogueLow", ArenaEnemy.EnemyType.Ranged,
            "skeleton_rogue", hp: 18, armor: 0, speed: 2.5f,
            contactDmg: 5, attackRange: 8f, cooldown: 1.5f,
            xp: 2, coin: 2, rightWeapon: "Skeleton_Crossbow", leftWeapon: "",
            projDmg: 10);

        // Mage Low — staff, wizard
        CreateEnemySO(dir, "Skeleton_MageLow", ArenaEnemy.EnemyType.Wizard,
            "skeleton_mage", hp: 22, armor: 0, speed: 1.8f,
            contactDmg: 4, attackRange: 10f, cooldown: 2.0f,
            xp: 3, coin: 3, rightWeapon: "Skeleton_Staff", leftWeapon: "",
            projDmg: 14);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Skeleton Enemy Stats",
            $"6 EnemyStatsSO olusturuldu.\n" +
            "Simdi 'Tools > StackTower > Setup Scene' ile sahneyi yeniden kurun.",
            "Tamam");
    }

    private static void CreateEnemySO(string dir, string name, ArenaEnemy.EnemyType type,
        string modelId, int hp, int armor, float speed,
        int contactDmg, float attackRange, float cooldown,
        int xp, int coin, string rightWeapon, string leftWeapon,
        int projDmg = 0)
    {
        var so = ScriptableObject.CreateInstance<EnemyStatsSO>();
        so.type = type;
        so.modelId = modelId;
        so.baseHP = hp;
        so.baseArmor = armor;
        so.moveSpeed = speed;
        so.contactDamage = contactDmg;
        so.projectileDamage = projDmg;
        so.attackRange = attackRange;
        so.attackCooldown = cooldown;
        so.xpDrop = xp;
        so.coinDrop = coin;
        so.rightHandWeapon = rightWeapon;
        so.leftHandWeapon = leftWeapon;
        so.modelScale = 0.45f;
        so.mass = type == ArenaEnemy.EnemyType.Heavy ? 2.5f : 1.5f;
        so.colliderRadius = 0.2f;
        so.colliderHeight = 0.6f;
        so.triggerRadius = 0.2f;
        so.bodyHeight = 0.5f;
        so.bodyWidth = 0.3f;
        so.bodyColor = type == ArenaEnemy.EnemyType.Wizard ? new Color(0.6f, 0.3f, 0.5f) :
                       type == ArenaEnemy.EnemyType.Heavy ? new Color(0.5f, 0.4f, 0.3f) :
                       type == ArenaEnemy.EnemyType.Ranged ? new Color(0.5f, 0.3f, 0.3f) :
                       new Color(0.7f, 0.6f, 0.4f);
        so.barWidth = 0.5f;

        AssetDatabase.CreateAsset(so, $"{dir}/{name}.asset");
    }

    // ─────────────────────────────────────────────
    // DEBUG
    // ─────────────────────────────────────────────

    [MenuItem("KayKit/4. Obstacle Layer Olustur")]
    public static void CreateObstacleLayer()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadMainAssetAtPath("ProjectSettings/TagManager.asset"));
        var layers = tagManager.FindProperty("layers");

        // Check if Obstacle layer already exists
        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == "Obstacle")
            {
                EditorUtility.DisplayDialog("Obstacle Layer", $"'Obstacle' layer zaten mevcut (Layer {i}).", "Tamam");
                return;
            }
        }

        // Find first empty user layer (8-31)
        for (int i = 8; i < 32; i++)
        {
            if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            {
                layers.GetArrayElementAtIndex(i).stringValue = "Obstacle";
                tagManager.ApplyModifiedProperties();
                EditorUtility.DisplayDialog("Obstacle Layer", $"'Obstacle' layer olusturuldu (Layer {i}).", "Tamam");
                return;
            }
        }

        EditorUtility.DisplayDialog("Obstacle Layer", "Bos layer bulunamadi!", "Tamam");
    }

    [MenuItem("KayKit/Debug Rig Info")]
    public static void DebugRigInfo()
    {
        string[] allFBXs = CHARACTER_NAMES.Concat(ANIM_FBXS).ToArray();
        foreach (var name in allFBXs)
        {
            string path = $"{HERO_PATH}/{name}.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) { Debug.Log($"[KayKit] {name}: NOT FOUND"); continue; }

            string avatarName = "none";
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(path) as GameObject;
            if (mainAsset != null)
            {
                var anim = mainAsset.GetComponentInChildren<Animator>();
                if (anim != null && anim.avatar != null)
                    avatarName = anim.avatar.name + (anim.avatar.isValid ? " (valid)" : " (INVALID)");
            }

            Debug.Log($"[KayKit] {name}: animationType={importer.animationType}, " +
                      $"importAnim={importer.importAnimation}, avatar={avatarName}");
        }

        var clips = CollectAllClips();
        foreach (var kvp in clips)
        {
            var clip = kvp.Value;
            string assetPath = AssetDatabase.GetAssetPath(clip);
            Debug.Log($"[KayKit] Clip '{kvp.Key}' from {assetPath}, length={clip.length:F2}s, legacy={clip.legacy}");
        }
    }

    // ─────────────────────────────────────────────
    // HELPERS
    // ─────────────────────────────────────────────

    private static void CreateAnimatorController(string basePath, string charName,
        AnimationClip idle, AnimationClip run, AnimationClip attack,
        AnimationClip death, AnimationClip hit, AnimationClip roll,
        AnimationClip throwAnim)
    {
        string path = $"{basePath}/{charName}Animator.controller";

        // Delete existing
        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path) != null)
            AssetDatabase.DeleteAsset(path);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(path);

        // Parameters (matching ArenaCharacter usage)
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("WeaponType", AnimatorControllerParameterType.Int);

        var rootLayer = controller.layers[0];
        var sm = rootLayer.stateMachine;

        // Idle state (default)
        var idleState = sm.AddState("Idle");
        idleState.motion = idle;
        sm.defaultState = idleState;

        // Run state
        var runState = sm.AddState("Run");
        runState.motion = run;

        // Idle <-> Run transitions based on Speed
        var toRun = idleState.AddTransition(runState);
        toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
        toRun.hasExitTime = false;
        toRun.duration = 0.15f;

        var toIdle = runState.AddTransition(idleState);
        toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
        toIdle.hasExitTime = false;
        toIdle.duration = 0.15f;

        // Attack state (from any state via trigger)
        if (attack != null)
        {
            var attackState = sm.AddState("Attack");
            attackState.motion = attack;

            var toAttack = sm.AddAnyStateTransition(attackState);
            toAttack.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            toAttack.hasExitTime = false;
            toAttack.duration = 0.1f;

            var fromAttack = attackState.AddTransition(idleState);
            fromAttack.hasExitTime = true;
            fromAttack.exitTime = 0.9f;
            fromAttack.duration = 0.1f;
        }

        // Death state (any state -> Death when Dead=true, no exit)
        if (death != null)
        {
            var deathState = sm.AddState("Death");
            deathState.motion = death;

            var toDeath = sm.AddAnyStateTransition(deathState);
            toDeath.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            toDeath.hasExitTime = false;
            toDeath.duration = 0.15f;
            toDeath.canTransitionToSelf = false;
        }

        // Hit state (any state -> Hit via trigger, returns to Idle)
        if (hit != null)
        {
            var hitState = sm.AddState("Hit");
            hitState.motion = hit;

            var toHit = sm.AddAnyStateTransition(hitState);
            toHit.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            toHit.hasExitTime = false;
            toHit.duration = 0.1f;
            toHit.canTransitionToSelf = false;

            var fromHit = hitState.AddTransition(idleState);
            fromHit.hasExitTime = true;
            fromHit.exitTime = 0.8f;
            fromHit.duration = 0.15f;
        }

        // Roll/Dodge state
        if (roll != null)
        {
            var rollState = sm.AddState("Roll");
            rollState.motion = roll;

            var fromRoll = rollState.AddTransition(idleState);
            fromRoll.hasExitTime = true;
            fromRoll.exitTime = 0.85f;
            fromRoll.duration = 0.15f;
        }

        // Punch_Left / Punch_Right — use attack clip as fallback
        if (attack != null)
        {
            var punchLState = sm.AddState("Punch_Left");
            punchLState.motion = attack;
            var fromPunchL = punchLState.AddTransition(idleState);
            fromPunchL.hasExitTime = true;
            fromPunchL.exitTime = 0.85f;
            fromPunchL.duration = 0.1f;

            var punchRState = sm.AddState("Punch_Right");
            punchRState.motion = attack;
            var fromPunchR = punchRState.AddTransition(idleState);
            fromPunchR.hasExitTime = true;
            fromPunchR.exitTime = 0.85f;
            fromPunchR.duration = 0.1f;
        }

        // Throw state (ranged attack alternative)
        if (throwAnim != null)
        {
            var throwState = sm.AddState("Throw");
            throwState.motion = throwAnim;
            var fromThrow = throwState.AddTransition(idleState);
            fromThrow.hasExitTime = true;
            fromThrow.exitTime = 0.9f;
            fromThrow.duration = 0.1f;
        }

        Debug.Log($"[KayKit] Created {path}");
    }

    private static Dictionary<string, AnimationClip> CollectAllClips()
    {
        var allClips = new Dictionary<string, AnimationClip>();

        // Collect from animation FBX files
        foreach (var fbxName in ANIM_FBXS)
        {
            string fbxPath = $"{HERO_PATH}/{fbxName}.fbx";
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                    allClips[clip.name] = clip;
            }
        }

        // Fallback: check character FBX files for embedded animations
        if (allClips.Count == 0)
        {
            foreach (var charName in CHARACTER_NAMES)
            {
                string fbxPath = $"{HERO_PATH}/{charName}.fbx";
                var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
                foreach (var asset in assets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                        allClips[clip.name] = clip;
                }
            }
        }

        Debug.Log($"[KayKit] Found {allClips.Count} animation clips:");
        foreach (var kvp in allClips)
            Debug.Log($"  - {kvp.Key}");

        return allClips;
    }

    private static Dictionary<string, AnimationClip> CollectSkeletonClips()
    {
        var clips = new Dictionary<string, AnimationClip>();

        foreach (var fbxName in SKELETON_ANIM_FBXS)
        {
            string fbxPath = $"{ENEMY_PATH}/{fbxName}.fbx";
            var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            foreach (var asset in assets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                    clips[clip.name] = clip;
            }
        }

        // Fallback: check skeleton character FBX
        if (clips.Count == 0)
        {
            foreach (var skelName in SKELETON_NAMES)
            {
                string fbxPath = $"{ENEMY_PATH}/{skelName}.fbx";
                var assets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
                foreach (var asset in assets)
                {
                    if (asset is AnimationClip clip && !clip.name.StartsWith("__"))
                        clips[clip.name] = clip;
                }
            }
        }

        Debug.Log($"[KayKit] Found {clips.Count} skeleton animation clips");
        return clips;
    }

    private static AnimationClip FindClip(Dictionary<string, AnimationClip> clips, params string[] names)
    {
        // Exact match first
        foreach (var name in names)
        {
            if (clips.ContainsKey(name)) return clips[name];
        }

        // Case-insensitive partial match
        foreach (var name in names)
        {
            string lower = name.ToLower();
            foreach (var kvp in clips)
            {
                if (kvp.Key.ToLower().Contains(lower)) return kvp.Value;
            }
        }

        return null;
    }
}
