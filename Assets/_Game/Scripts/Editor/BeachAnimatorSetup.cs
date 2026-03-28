using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Linq;

/// <summary>
/// Editor utility to create an AnimatorController for the Beach character.
/// Run once via menu: Tools > Create Beach Animator.
/// </summary>
public class BeachAnimatorSetup
{
    // Clips that should loop
    private static readonly string[] loopingClips = {
        "Idle", "Idle_Gun", "Idle_Neutral", "Idle_Sword", "Idle_Gun_Pointing",
        "Run", "Run_Back", "Run_Left", "Run_Right", "Run_Shoot", "Walk"
    };

    [MenuItem("Tools/Create Beach Animator")]
    public static void CreateBeachAnimator()
    {
        string fbxPath = "Assets/_Game/Resources/Models/Beach.fbx";
        string controllerPath = "Assets/_Game/Resources/Models/BeachAnimator.controller";

        // Step 1: Fix loop settings on the FBX import
        FixLoopSettings(fbxPath);

        // Step 2: Load all clips from the FBX
        var allAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var clips = allAssets.OfType<AnimationClip>()
            .Where(c => !c.name.StartsWith("__preview__"))
            .ToArray();

        if (clips.Length == 0)
        {
            Debug.LogError("No animation clips found in " + fbxPath);
            return;
        }

        // Step 3: Create or overwrite controller
        if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath) != null)
            AssetDatabase.DeleteAsset(controllerPath);

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var rootStateMachine = controller.layers[0].stateMachine;

        // Add parameters
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Dead", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("WeaponType", AnimatorControllerParameterType.Int);

        AnimatorState idleGunState = null;
        AnimatorState runState = null;
        AnimatorState deathState = null;
        AnimatorState hitState = null;
        AnimatorState gunShootState = null;
        AnimatorState punchRightState = null;
        AnimatorState punchLeftState = null;

        foreach (var clip in clips)
        {
            string cleanName = clip.name;
            if (cleanName.Contains("|"))
                cleanName = cleanName.Split('|')[1];

            var state = rootStateMachine.AddState(cleanName);
            state.motion = clip;

            switch (cleanName)
            {
                case "Idle_Gun": idleGunState = state; break;
                case "Run": runState = state; break;
                case "Death": deathState = state; break;
                case "HitRecieve": hitState = state; break;
                case "Gun_Shoot": gunShootState = state; break;
                case "Punch_Right": punchRightState = state; break;
                case "Punch_Left": punchLeftState = state; break;
            }
        }

        // Set punch states to 2x speed
        if (punchRightState != null) punchRightState.speed = 2f;
        if (punchLeftState != null) punchLeftState.speed = 2f;

        // Default state
        if (idleGunState != null)
            rootStateMachine.defaultState = idleGunState;

        // Idle_Gun <-> Run (based on Speed)
        if (idleGunState != null && runState != null)
        {
            var toRun = idleGunState.AddTransition(runState);
            toRun.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            toRun.hasExitTime = false;
            toRun.duration = 0.1f;

            var toIdle = runState.AddTransition(idleGunState);
            toIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            toIdle.hasExitTime = false;
            toIdle.duration = 0.1f;
        }

        // Any -> Death
        if (deathState != null)
        {
            var t = rootStateMachine.AddAnyStateTransition(deathState);
            t.AddCondition(AnimatorConditionMode.If, 0, "Dead");
            t.hasExitTime = false;
            t.canTransitionToSelf = false;
            t.duration = 0.1f;
        }

        // Any -> HitRecieve -> back to Idle_Gun
        if (hitState != null)
        {
            var t = rootStateMachine.AddAnyStateTransition(hitState);
            t.AddCondition(AnimatorConditionMode.If, 0, "Hit");
            t.hasExitTime = false;
            t.duration = 0.05f;

            if (idleGunState != null)
            {
                var back = hitState.AddTransition(idleGunState);
                back.hasExitTime = true;
                back.exitTime = 0.9f;
                back.duration = 0.15f;
            }
        }

        // Any -> Punch_Right -> back to Idle_Gun (2x speed, unarmed attack)
        if (punchRightState != null)
        {
            punchRightState.speed = 2f;
            var tp = rootStateMachine.AddAnyStateTransition(punchRightState);
            tp.AddCondition(AnimatorConditionMode.If, 0, "Attack");
            tp.AddCondition(AnimatorConditionMode.Equals, 0, "WeaponType");
            tp.hasExitTime = false;
            tp.duration = 0.05f;

            if (idleGunState != null)
            {
                var backP = punchRightState.AddTransition(idleGunState);
                backP.hasExitTime = true;
                backP.exitTime = 0.85f;
                backP.duration = 0.1f;
            }
        }

        // Any -> Gun_Shoot -> back to Idle_Gun (2x speed)
        if (gunShootState != null)
        {
            gunShootState.speed = 2f;
            var t = rootStateMachine.AddAnyStateTransition(gunShootState);
            t.AddCondition(AnimatorConditionMode.If, 0, "Shoot");
            t.hasExitTime = false;
            t.duration = 0.05f;

            if (idleGunState != null)
            {
                var back = gunShootState.AddTransition(idleGunState);
                back.hasExitTime = true;
                back.exitTime = 0.85f;
                back.duration = 0.1f;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Beach AnimatorController created at {controllerPath} with {clips.Length} clips. Looping clips fixed.");
    }

    private static void FixLoopSettings(string fbxPath)
    {
        var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("Could not find ModelImporter for " + fbxPath);
            return;
        }

        var clipAnimations = importer.clipAnimations;

        // If no custom clip animations set yet, copy from defaults
        if (clipAnimations == null || clipAnimations.Length == 0)
            clipAnimations = importer.defaultClipAnimations;

        bool changed = false;
        for (int i = 0; i < clipAnimations.Length; i++)
        {
            string clipName = clipAnimations[i].name;
            if (clipName.Contains("|"))
                clipName = clipName.Split('|')[1];

            bool shouldLoop = loopingClips.Contains(clipName);
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
            Debug.Log("Fixed loop settings on Beach.fbx clips");
        }
    }
}
