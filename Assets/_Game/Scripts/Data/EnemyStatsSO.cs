using UnityEngine;

/// <summary>
/// Configurable stats for a single arena enemy type.
/// Create one asset per enemy type via Assets > Create > StackTower > Enemy Stats.
/// Place assets in Assets/_Game/Data/Enemies/.
/// </summary>
[CreateAssetMenu(fileName = "EnemyStats_", menuName = "StackTower/Enemy Stats")]
public class EnemyStatsSO : ScriptableObject
{
    [Header("Identity")]
    public ArenaEnemy.EnemyType type;
    [Tooltip("Model ID used by ModelManager lookup (e.g. \"punk\", \"zombie_ribcage\").")]
    public string modelId = "punk";

    [Header("Base Stats -- multiplied by wave scale at runtime")]
    [Min(1)] public int baseHP = 20;
    [Min(0)] public int baseArmor = 0;

    [Header("Movement")]
    [Min(0f)] public float moveSpeed = 2.5f;

    [Header("Combat")]
    [Min(0)] public int contactDamage = 8;
    [Min(0)] public int projectileDamage = 0;
    [Min(0f)] public float attackRange = 0.9f;
    [Min(0.1f)] public float attackCooldown = 0.8f;

    [Header("Rewards")]
    [Min(0)] public int xpDrop = 1;
    [Min(0)] public int coinDrop = 1;

    [Header("Physics")]
    [Min(0.1f)] public float mass = 1.5f;
    [Min(0.05f)] public float colliderRadius = 0.25f;
    [Min(0.1f)] public float colliderHeight = 0.8f;
    [Min(0.05f)] public float triggerRadius = 0.2f;

    [Header("Visual -- Procedural Fallback")]
    [Min(0.1f)] public float bodyHeight = 0.5f;
    [Min(0.1f)] public float bodyWidth = 0.3f;
    public Color bodyColor = Color.red;
    public Color trimColor = Color.black;
    [Min(0.1f)] public float modelScale = 0.75f;
    [Min(0.1f)] public float barWidth = 0.5f;
}
