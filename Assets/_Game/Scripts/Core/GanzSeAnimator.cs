using UnityEngine;

/// <summary>
/// Procedural bone animation for GanzSe modular character.
/// Drives idle, jump, walk, and dash poses by rotating bones at runtime.
/// </summary>
public class GanzSeAnimator : MonoBehaviour
{
    public enum AnimState { Idle, Jump, WalkRight, WalkLeft, DashRight, DashLeft, Fall }

    public AnimState CurrentState { get; set; } = AnimState.Idle;

    // Bones
    private Transform spine01, spine02, spine03;
    private Transform upperarmL, upperarmR;
    private Transform forearmL, forearmR;
    private Transform shoulderL, shoulderR;
    private Transform upperlegL, upperlegR;
    private Transform lowerlegL, lowerlegR;
    private Transform footL, footR;
    private Transform head, neck;
    private Transform root;

    // Animation time
    private float animTime;
    private float walkCycle;

    // Idle pose rotations (arms down from T-pose, not too tight to body)
    private static readonly Quaternion idleUpperArmL = Quaternion.Euler(0, 0, 45f);
    private static readonly Quaternion idleUpperArmR = Quaternion.Euler(0, 0, -45f);
    private static readonly Quaternion idleForearmL = Quaternion.Euler(0, 0, 12f);
    private static readonly Quaternion idleForearmR = Quaternion.Euler(0, 0, -12f);

    // Bind pose rotations for legs (saved from model's initial state)
    private Quaternion bindUpperLegL, bindUpperLegR;
    private Quaternion bindLowerLegL, bindLowerLegR;
    private Quaternion bindFootL, bindFootR;

    private void Start()
    {
        FindBones();
        SaveBindPose();
        // Apply idle pose immediately
        ApplyIdlePose();
    }

    private void SaveBindPose()
    {
        bindUpperLegL = upperlegL != null ? upperlegL.localRotation : Quaternion.identity;
        bindUpperLegR = upperlegR != null ? upperlegR.localRotation : Quaternion.identity;
        bindLowerLegL = lowerlegL != null ? lowerlegL.localRotation : Quaternion.identity;
        bindLowerLegR = lowerlegR != null ? lowerlegR.localRotation : Quaternion.identity;
        bindFootL = footL != null ? footL.localRotation : Quaternion.identity;
        bindFootR = footR != null ? footR.localRotation : Quaternion.identity;
    }

    private void FindBones()
    {
        foreach (var t in GetComponentsInChildren<Transform>())
        {
            switch (t.name.ToLower())
            {
                case "spine_01": spine01 = t; break;
                case "spine_02": spine02 = t; break;
                case "spine_03": spine03 = t; break;
                case "upperarm_l": upperarmL = t; break;
                case "upperarm_r": upperarmR = t; break;
                case "forearm_l": forearmL = t; break;
                case "forearm_r": forearmR = t; break;
                case "shoulder_l": shoulderL = t; break;
                case "shoulder_r": shoulderR = t; break;
                case "upperleg_l": upperlegL = t; break;
                case "upperleg_r": upperlegR = t; break;
                case "lowerleg_l": lowerlegL = t; break;
                case "lowerleg_r": lowerlegR = t; break;
                case "foot_l": footL = t; break;
                case "foot_r": footR = t; break;
                case "head": head = t; break;
                case "neck": neck = t; break;
            }
        }

        // Find root bone (usually "root" or first child with "root" in name)
        foreach (var t in GetComponentsInChildren<Transform>())
        {
            if (t.name.ToLower().Contains("root") && t != transform)
            {
                root = t;
                break;
            }
        }
    }

    private void LateUpdate()
    {
        animTime += Time.deltaTime;

        switch (CurrentState)
        {
            case AnimState.Idle:
                AnimateIdle();
                break;
            case AnimState.Jump:
                AnimateJump();
                break;
            case AnimState.Fall:
                AnimateFall();
                break;
            case AnimState.WalkRight:
            case AnimState.WalkLeft:
                AnimateWalk(CurrentState == AnimState.WalkLeft ? -1 : 1);
                break;
            case AnimState.DashRight:
            case AnimState.DashLeft:
                AnimateDash(CurrentState == AnimState.DashLeft ? -1 : 1);
                break;
        }
    }

    private void ApplyIdlePose()
    {
        if (upperarmL != null) upperarmL.localRotation = idleUpperArmL;
        if (upperarmR != null) upperarmR.localRotation = idleUpperArmR;
        if (forearmL != null) forearmL.localRotation = idleForearmL;
        if (forearmR != null) forearmR.localRotation = idleForearmR;
    }

    private void AnimateIdle()
    {
        float bob = Mathf.Sin(animTime * 2f) * 0.5f;
        float breathe = Mathf.Sin(animTime * 1.5f) * 1f;

        // Subtle body bob
        if (spine02 != null)
            spine02.localRotation = Quaternion.Euler(breathe, 0, 0);

        // Arms relaxed with slight sway
        float armSway = Mathf.Sin(animTime * 1.8f) * 3f;
        if (upperarmL != null)
            upperarmL.localRotation = idleUpperArmL * Quaternion.Euler(armSway, 0, 0);
        if (upperarmR != null)
            upperarmR.localRotation = idleUpperArmR * Quaternion.Euler(-armSway, 0, 0);
        if (forearmL != null)
            forearmL.localRotation = idleForearmL * Quaternion.Euler(0, 0, bob);
        if (forearmR != null)
            forearmR.localRotation = idleForearmR * Quaternion.Euler(0, 0, -bob);

        // Legs straight (use bind pose as base)
        if (upperlegL != null) upperlegL.localRotation = bindUpperLegL;
        if (upperlegR != null) upperlegR.localRotation = bindUpperLegR;
        if (lowerlegL != null) lowerlegL.localRotation = bindLowerLegL;
        if (lowerlegR != null) lowerlegR.localRotation = bindLowerLegR;

        // Head slight look
        if (head != null)
            head.localRotation = Quaternion.Euler(bob * 0.5f, 0, 0);
    }

    private void AnimateJump()
    {
        // Arms up and out
        if (upperarmL != null)
            upperarmL.localRotation = Quaternion.Euler(-40f, 0, 45f);
        if (upperarmR != null)
            upperarmR.localRotation = Quaternion.Euler(-40f, 0, -45f);
        if (forearmL != null)
            forearmL.localRotation = Quaternion.Euler(0, 0, 30f);
        if (forearmR != null)
            forearmR.localRotation = Quaternion.Euler(0, 0, -30f);

        // Legs tucked
        if (upperlegL != null)
            upperlegL.localRotation = bindUpperLegL * Quaternion.Euler(-25f, 0, 0);
        if (upperlegR != null)
            upperlegR.localRotation = bindUpperLegR * Quaternion.Euler(-25f, 0, 0);
        if (lowerlegL != null)
            lowerlegL.localRotation = bindLowerLegL * Quaternion.Euler(30f, 0, 0);
        if (lowerlegR != null)
            lowerlegR.localRotation = bindLowerLegR * Quaternion.Euler(30f, 0, 0);

        // Spine straight, slight lean back
        if (spine02 != null)
            spine02.localRotation = Quaternion.Euler(-5f, 0, 0);
    }

    private void AnimateFall()
    {
        float flail = Mathf.Sin(animTime * 8f) * 15f;

        // Arms flailing
        if (upperarmL != null)
            upperarmL.localRotation = Quaternion.Euler(-20f + flail, 0, 60f);
        if (upperarmR != null)
            upperarmR.localRotation = Quaternion.Euler(-20f - flail, 0, -60f);
        if (forearmL != null)
            forearmL.localRotation = Quaternion.Euler(0, 0, 25f + flail * 0.5f);
        if (forearmR != null)
            forearmR.localRotation = Quaternion.Euler(0, 0, -25f - flail * 0.5f);

        // Legs dangling
        if (upperlegL != null)
            upperlegL.localRotation = bindUpperLegL * Quaternion.Euler(10f + flail * 0.3f, 0, 0);
        if (upperlegR != null)
            upperlegR.localRotation = bindUpperLegR * Quaternion.Euler(10f - flail * 0.3f, 0, 0);
        if (lowerlegL != null)
            lowerlegL.localRotation = bindLowerLegL * Quaternion.Euler(15f, 0, 0);
        if (lowerlegR != null)
            lowerlegR.localRotation = bindLowerLegR * Quaternion.Euler(15f, 0, 0);
    }

    private void AnimateWalk(int dir)
    {
        walkCycle += Time.deltaTime * 10f;
        float sin = Mathf.Sin(walkCycle);
        float cos = Mathf.Cos(walkCycle);

        // Legs alternate forward/back
        float legSwing = sin * 35f;
        if (upperlegL != null)
            upperlegL.localRotation = bindUpperLegL * Quaternion.Euler(legSwing, 0, 0);
        if (upperlegR != null)
            upperlegR.localRotation = bindUpperLegR * Quaternion.Euler(-legSwing, 0, 0);

        // Knees bend on back leg
        float kneeL = legSwing < 0 ? -legSwing * 0.8f : 0;
        float kneeR = -legSwing < 0 ? legSwing * 0.8f : 0;
        if (lowerlegL != null)
            lowerlegL.localRotation = bindLowerLegL * Quaternion.Euler(kneeL, 0, 0);
        if (lowerlegR != null)
            lowerlegR.localRotation = bindLowerLegR * Quaternion.Euler(kneeR, 0, 0);

        // Arms swing opposite to legs
        float armSwing = sin * 25f;
        if (upperarmL != null)
            upperarmL.localRotation = idleUpperArmL * Quaternion.Euler(-armSwing, 0, 0);
        if (upperarmR != null)
            upperarmR.localRotation = idleUpperArmR * Quaternion.Euler(armSwing, 0, 0);
        if (forearmL != null)
            forearmL.localRotation = idleForearmL * Quaternion.Euler(-10f, 0, 0);
        if (forearmR != null)
            forearmR.localRotation = idleForearmR * Quaternion.Euler(-10f, 0, 0);

        // Slight body lean and bob
        float bob = Mathf.Abs(sin) * 2f;
        if (spine02 != null)
            spine02.localRotation = Quaternion.Euler(5f + bob, 0, cos * 2f);
    }

    private void AnimateDash(int dir)
    {
        walkCycle += Time.deltaTime * 16f;
        float sin = Mathf.Sin(walkCycle);

        // Lean forward strongly
        if (spine02 != null)
            spine02.localRotation = Quaternion.Euler(25f, 0, 0);
        if (spine03 != null)
            spine03.localRotation = Quaternion.Euler(10f, 0, 0);

        // Arms swept back
        if (upperarmL != null)
            upperarmL.localRotation = Quaternion.Euler(40f, 0, 30f);
        if (upperarmR != null)
            upperarmR.localRotation = Quaternion.Euler(40f, 0, -30f);
        if (forearmL != null)
            forearmL.localRotation = Quaternion.Euler(0, 0, 40f);
        if (forearmR != null)
            forearmR.localRotation = Quaternion.Euler(0, 0, -40f);

        // Fast leg cycle
        float legSwing = sin * 45f;
        if (upperlegL != null)
            upperlegL.localRotation = bindUpperLegL * Quaternion.Euler(legSwing, 0, 0);
        if (upperlegR != null)
            upperlegR.localRotation = bindUpperLegR * Quaternion.Euler(-legSwing, 0, 0);

        float kneeL = legSwing < 0 ? -legSwing : 5f;
        float kneeR = -legSwing < 0 ? legSwing : 5f;
        if (lowerlegL != null)
            lowerlegL.localRotation = bindLowerLegL * Quaternion.Euler(kneeL, 0, 0);
        if (lowerlegR != null)
            lowerlegR.localRotation = bindLowerLegR * Quaternion.Euler(kneeR, 0, 0);
    }
}
