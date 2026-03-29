#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.IO;
using System.Linq;
using System.Collections.Generic;

/// <summary>
/// Creates AnimatorControllers for all MainCharacter models.
/// All Quaternius characters share the same 24 clips.
/// Supports weapon types: Unarmed(0), Sword(1), Gun(2)
/// Run via: Tools > Setup All Model Animators
/// </summary>
public class ModelAnimatorSetup
{
    // All MainCharacter clips (shared across Beach, King, Medieval, Spacesuit, Suit, Swat)
    private static readonly string[] allClipNames = {
        "Death", "Gun_Shoot", "HitRecieve", "HitRecieve_2",
        "Idle", "Idle_Gun", "Idle_Gun_Pointing", "Idle_Gun_Shoot", "Idle_Neutral", "Idle_Sword",
        "Interact", "Kick_Left", "Kick_Right", "Punch_Left", "Punch_Right",
        "Roll", "Run", "Run_Back", "Run_Left", "Run_Right", "Run_Shoot",
        "Sword_Slash", "Walk", "Wave"
    };

    // Clips that should loop
    private static readonly HashSet<string> loopClips = new() {
        "Idle", "Idle_Gun", "Idle_Gun_Pointing", "Idle_Neutral", "Idle_Sword",
        "Run", "Run_Back", "Run_Left", "Run_Right", "Run_Shoot", "Walk"
    };

    [MenuItem("Tools/Setup All Model Animators")]
    public static void SetupAll()
    {
        int created = 0;

        string folder = "Assets/_Game/Resources/Models/MainCharacter";
        if (Directory.Exists(folder))
        {
            foreach (string fbxPath in Directory.GetFiles(folder, "*.fbx", SearchOption.TopDirectoryOnly))
            {
                if (CreateMainCharacterController(fbxPath.Replace("\\", "/")))
                    created++;
            }
        }

        // Enemy controllers
        string enemyFolder = "Assets/_Game/Resources/Models/Enemies";
        if (Directory.Exists(enemyFolder))
        {
            foreach (string fbxPath in Directory.GetFiles(enemyFolder, "*.fbx", SearchOption.TopDirectoryOnly))
            {
                string name = Path.GetFileNameWithoutExtension(fbxPath);
                string unityPath = fbxPath.Replace("\\", "/");

                bool ok = name switch
                {
                    "Zombie_Ribcage" => CreateZombieController(unityPath),
                    // Punk and others with standard 24 clips use the same system as MainCharacter
                    _ => CreateEnemyStandardController(unityPath)
                };
                if (ok) created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[ModelAnimatorSetup] Created {created} AnimatorControllers");
    }

    private static bool CreateMainCharacterController(string fbxPath)
    {
        string modelName = Path.GetFileNameWithoutExtension(fbxPath);
        string controllerPath = Path.Combine(Path.GetDirectoryName(fbxPath), modelName + "Animator.controller")
            .Replace("\\", "/");

        // Fix loop settings first
        FixLoopSettings(fbxPath);

        // Load clips from FBX
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var asset in allAssets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                string name = clip.name;
                int pipe = name.LastIndexOf('|');
                if (pipe >= 0) name = name.Substring(pipe + 1);
                clips[name.Trim()] = clip;
            }
        }

        if (clips.Count == 0)
        {
            Debug.LogWarning($"[ModelAnimatorSetup] No clips in {fbxPath}");
            return false;
        }

        // Delete old controller
        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var sm = controller.layers[0].stateMachine;

        // Parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);     // 0=idle, >0=moving
        controller.AddParameter("WeaponType", AnimatorControllerParameterType.Int);  // 0=unarmed, 1=sword, 2=gun
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

        // ── Create states ──

        // Idle states (by weapon type)
        var idleUnarmed = AddState(sm, clips, "Idle");
        var idleSword = AddState(sm, clips, "Idle_Sword");
        var idleGun = AddState(sm, clips, "Idle_Gun");

        // Run state (shared)
        var run = AddState(sm, clips, "Run");
        var runShoot = AddState(sm, clips, "Run_Shoot");

        // Attack states (by weapon type)
        var punchRight = AddState(sm, clips, "Punch_Right");
        var punchLeft = AddState(sm, clips, "Punch_Left");
        var kickRight = AddState(sm, clips, "Kick_Right");
        var swordSlash = AddState(sm, clips, "Sword_Slash");
        var gunShoot = AddState(sm, clips, "Gun_Shoot");

        // Utility states
        var death = AddState(sm, clips, "Death");
        var hit = AddState(sm, clips, "HitRecieve");
        var roll = AddState(sm, clips, "Roll");

        // Add remaining clips as states (for future use)
        foreach (var (name, clip) in clips)
        {
            bool alreadyAdded = sm.states.Any(s => s.state.name == name);
            if (!alreadyAdded)
            {
                var s = sm.AddState(name);
                s.motion = clip;
            }
        }

        // ── Default state = Idle (unarmed) ──
        if (idleUnarmed != null)
            sm.defaultState = idleUnarmed;

        // ── TRANSITIONS ──

        // --- Idle → Run (any weapon, Speed > 0.1) ---
        AddTransition(idleUnarmed, run, "Speed", AnimatorConditionMode.Greater, 0.1f);
        AddTransition(idleSword, run, "Speed", AnimatorConditionMode.Greater, 0.1f);
        AddTransition(idleGun, run, "Speed", AnimatorConditionMode.Greater, 0.1f);

        // --- Run → Idle (based on weapon type, Speed < 0.1) ---
        if (run != null)
        {
            // Run → Idle_Unarmed (WeaponType == 0)
            var t0 = run.AddTransition(idleUnarmed);
            t0.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            t0.AddCondition(AnimatorConditionMode.Equals, 0, "WeaponType");
            t0.hasExitTime = false; t0.duration = 0.15f;

            // Run → Idle_Sword (WeaponType == 1)
            var t1 = run.AddTransition(idleSword);
            t1.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            t1.AddCondition(AnimatorConditionMode.Equals, 1, "WeaponType");
            t1.hasExitTime = false; t1.duration = 0.15f;

            // Run → Idle_Gun (WeaponType == 2)
            var t2 = run.AddTransition(idleGun);
            t2.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            t2.AddCondition(AnimatorConditionMode.Equals, 2, "WeaponType");
            t2.hasExitTime = false; t2.duration = 0.15f;
        }

        // --- Attack (based on weapon type) ---
        // Unarmed → Punch (2x speed, both hands)
        if (punchLeft != null) punchLeft.speed = 2f;
        if (punchRight != null)
        {
            punchRight.speed = 2f;
            var t = sm.AddAnyStateTransition(punchRight);
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            t.AddCondition(AnimatorConditionMode.Equals, 0, "WeaponType");
            t.hasExitTime = false; t.duration = 0.05f;
            AddReturnToIdle(punchRight, idleUnarmed);
        }

        // Sword → Slash
        if (swordSlash != null)
        {
            var t = sm.AddAnyStateTransition(swordSlash);
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            t.AddCondition(AnimatorConditionMode.Equals, 1, "WeaponType");
            t.hasExitTime = false; t.duration = 0.05f;
            AddReturnToIdle(swordSlash, idleSword);
        }

        // Gun → Shoot
        if (gunShoot != null)
        {
            var t = sm.AddAnyStateTransition(gunShoot);
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            t.AddCondition(AnimatorConditionMode.Equals, 2, "WeaponType");
            t.hasExitTime = false; t.duration = 0.05f;
            AddReturnToIdle(gunShoot, idleGun);
        }

        // --- Death (any state) ---
        if (death != null)
        {
            var t = sm.AddAnyStateTransition(death);
            t.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            t.hasExitTime = false; t.duration = 0.1f;
            t.canTransitionToSelf = false;
        }

        // --- Hit (any state → hit → back to idle) ---
        if (hit != null)
        {
            var t = sm.AddAnyStateTransition(hit);
            t.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            t.hasExitTime = false; t.duration = 0.05f;

            // Return to appropriate idle
            var b0 = hit.AddTransition(idleUnarmed);
            b0.AddCondition(AnimatorConditionMode.Equals, 0, "WeaponType");
            b0.hasExitTime = true; b0.exitTime = 0.9f; b0.duration = 0.15f;

            var b1 = hit.AddTransition(idleSword);
            b1.AddCondition(AnimatorConditionMode.Equals, 1, "WeaponType");
            b1.hasExitTime = true; b1.exitTime = 0.9f; b1.duration = 0.15f;

            var b2 = hit.AddTransition(idleGun);
            b2.AddCondition(AnimatorConditionMode.Equals, 2, "WeaponType");
            b2.hasExitTime = true; b2.exitTime = 0.9f; b2.duration = 0.15f;
        }

        Debug.Log($"[ModelAnimatorSetup] {modelName}: {clips.Count} clips, controller created at {controllerPath}");
        return true;
    }

    /// <summary>
    /// Standard enemy controller -- same 24 clips as MainCharacter but simpler:
    /// Only uses Idle, Run, Punch_Right (melee attack), Death, HitRecieve.
    /// Enemies don't have weapon types.
    /// </summary>
    private static bool CreateEnemyStandardController(string fbxPath)
    {
        string modelName = Path.GetFileNameWithoutExtension(fbxPath);
        string controllerPath = Path.Combine(Path.GetDirectoryName(fbxPath), modelName + "Animator.controller")
            .Replace("\\", "/");

        FixLoopSettings(fbxPath);

        var allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var asset in allAssets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                string name = clip.name;
                int pipe = name.LastIndexOf('|');
                if (pipe >= 0) name = name.Substring(pipe + 1);
                clips[name.Trim()] = clip;
            }
        }
        if (clips.Count == 0) return false;

        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var sm = controller.layers[0].stateMachine;

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        // Idle first (becomes default)
        var idle = AddState(sm, clips, "Idle");
        var run = AddState(sm, clips, "Run");
        var attack = AddState(sm, clips, "Punch_Right") ?? AddState(sm, clips, "Sword_Slash") ?? AddState(sm, clips, "Kick_Right");
        var death = AddState(sm, clips, "Death");
        var hit = AddState(sm, clips, "HitRecieve") ?? AddState(sm, clips, "HitReact");

        if (idle != null) sm.defaultState = idle;

        // Idle <-> Run
        AddTransition(idle, run, "Speed", AnimatorConditionMode.Greater, 0.1f);
        AddTransition(run, idle, "Speed", AnimatorConditionMode.Less, 0.1f);

        // Any -> Death
        if (death != null)
        {
            var t = sm.AddAnyStateTransition(death);
            t.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            t.hasExitTime = false; t.duration = 0.1f;
            t.canTransitionToSelf = false;
        }

        // Any -> Hit -> Idle
        if (hit != null)
        {
            var t = sm.AddAnyStateTransition(hit);
            t.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            t.hasExitTime = false; t.duration = 0.05f;
            AddReturnToIdle(hit, idle);
        }

        // Any -> Attack -> Idle
        if (attack != null)
        {
            var t = sm.AddAnyStateTransition(attack);
            t.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            t.hasExitTime = false; t.duration = 0.05f;
            AddReturnToIdle(attack, idle);
        }

        Debug.Log($"[ModelAnimatorSetup] Enemy {modelName}: Idle={idle?.name ?? "?"}, Run={run?.name ?? "?"}, Attack={attack?.name ?? "?"}, Death={death?.name ?? "?"}, Hit={hit?.name ?? "?"}");
        return true;
    }

    /// <summary>
    /// Zombie controller -- crawls on ground, contact damage only, no attack animation.
    /// Idle=Crawl, Move=Crawl, Death=Death, Hit=HitReact
    /// </summary>
    private static bool CreateZombieController(string fbxPath)
    {
        string modelName = Path.GetFileNameWithoutExtension(fbxPath);
        string controllerPath = Path.Combine(Path.GetDirectoryName(fbxPath), modelName + "Animator.controller")
            .Replace("\\", "/");

        // Fix loop: Crawl, Idle, Run, Walk should loop
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer != null)
        {
            var clipAnims = importer.clipAnimations;
            if (clipAnims == null || clipAnims.Length == 0)
                clipAnims = importer.defaultClipAnimations;

            bool changed = false;
            var shouldLoop = new HashSet<string> { "Crawl", "Idle", "Run", "Walk" };
            for (int i = 0; i < clipAnims.Length; i++)
            {
                string n = clipAnims[i].name;
                int p = n.LastIndexOf('|');
                if (p >= 0) n = n.Substring(p + 1).Trim();
                bool loop = shouldLoop.Contains(n);
                if (clipAnims[i].loopTime != loop) { clipAnims[i].loopTime = loop; changed = true; }
            }
            if (changed) { importer.clipAnimations = clipAnims; importer.SaveAndReimport(); }
        }

        var allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var clips = new Dictionary<string, AnimationClip>();
        foreach (var asset in allAssets)
        {
            if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
            {
                string name = clip.name;
                int pipe = name.LastIndexOf('|');
                if (pipe >= 0) name = name.Substring(pipe + 1);
                clips[name.Trim()] = clip;
            }
        }
        if (clips.Count == 0) return false;

        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var sm = controller.layers[0].stateMachine;

        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);

        // Crawl is both idle and movement for zombie
        var crawl = AddState(sm, clips, "Crawl");
        var idle = AddState(sm, clips, "Idle");
        var death = AddState(sm, clips, "Death");
        var hit = AddState(sm, clips, "HitReact");

        // Default = Crawl (zombie always crawling)
        if (crawl != null)
            sm.defaultState = crawl;
        else if (idle != null)
            sm.defaultState = idle;

        // Any -> Death
        if (death != null)
        {
            var t = sm.AddAnyStateTransition(death);
            t.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            t.hasExitTime = false; t.duration = 0.1f;
            t.canTransitionToSelf = false;
        }

        // Any -> Hit -> Crawl
        if (hit != null)
        {
            var t = sm.AddAnyStateTransition(hit);
            t.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            t.hasExitTime = false; t.duration = 0.05f;
            AddReturnToIdle(hit, crawl ?? idle);
        }

        Debug.Log($"[ModelAnimatorSetup] Zombie {modelName}: Crawl={crawl?.name ?? "?"}, Death={death?.name ?? "?"}, Hit={hit?.name ?? "?"}");
        return true;
    }

    private static AnimatorState AddState(AnimatorStateMachine sm, Dictionary<string, AnimationClip> clips, string clipName)
    {
        if (!clips.ContainsKey(clipName)) return null;
        var state = sm.AddState(clipName);
        state.motion = clips[clipName];
        return state;
    }

    private static void AddTransition(AnimatorState from, AnimatorState to, string param, AnimatorConditionMode mode, float threshold)
    {
        if (from == null || to == null) return;
        var t = from.AddTransition(to);
        t.AddCondition(mode, threshold, param);
        t.hasExitTime = false;
        t.duration = 0.15f;
    }

    private static void AddReturnToIdle(AnimatorState from, AnimatorState idle)
    {
        if (from == null || idle == null) return;
        var t = from.AddTransition(idle);
        t.hasExitTime = true;
        t.exitTime = 0.85f;
        t.duration = 0.1f;
    }

    private static void FixLoopSettings(string fbxPath)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null) return;

        var clipAnimations = importer.clipAnimations;
        if (clipAnimations == null || clipAnimations.Length == 0)
            clipAnimations = importer.defaultClipAnimations;

        bool changed = false;
        for (int i = 0; i < clipAnimations.Length; i++)
        {
            string name = clipAnimations[i].name;
            int pipe = name.LastIndexOf('|');
            if (pipe >= 0) name = name.Substring(pipe + 1).Trim();

            bool shouldLoop = loopClips.Contains(name);
            if (clipAnimations[i].loopTime != shouldLoop)
            {
                clipAnimations[i].loopTime = shouldLoop;
                changed = true;
            }
        }

        if (changed)
        {
            importer.clipAnimations = clipAnimations;
            importer.SaveAndReimport();
        }
    }
}
#endif
