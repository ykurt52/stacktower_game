/// <summary>
/// Arena-wide numeric constants. All gameplay tuning values that are not
/// designer-configurable (i.e. not in a ScriptableObject) live here.
/// </summary>
public static class ArenaConstants
{
    // ── Enemy Lifecycle ──────────────────────────────────────────────────────

    /// <summary>Invincibility window right after an enemy spawns (seconds).</summary>
    public const float SPAWN_GRACE              = 0.5f;

    /// <summary>Duration of knockback velocity application (seconds).</summary>
    public const float KNOCKBACK_DURATION       = 0.25f;

    /// <summary>Per-frame velocity damping multiplier during knockback.</summary>
    public const float KNOCKBACK_DAMPING        = 0.85f;

    /// <summary>Rigidbody linear drag applied to all arena enemies.</summary>
    public const float ENEMY_LINEAR_DAMPING     = 5f;

    /// <summary>Y offset for the damage-trigger sphere centre above ground.</summary>
    public const float TRIGGER_CENTER_Y         = 0.4f;

    // ── Enemy Death ──────────────────────────────────────────────────────────

    /// <summary>Delay before returning a dead enemy to the pool (has death animation).</summary>
    public const float DEATH_DELAY_ANIMATED     = 1.5f;

    /// <summary>Delay before returning a dead enemy to the pool (no animation).</summary>
    public const float DEATH_DELAY_INSTANT      = 0.05f;

    // ── Combat ───────────────────────────────────────────────────────────────

    /// <summary>Damage used for instant-kill (e.g. revive mercy clear).</summary>
    public const int   INSTANT_KILL_DAMAGE      = 9999;

    /// <summary>Enemy projectile travel speed (world units/second).</summary>
    public const float PROJECTILE_SPEED         = 5f;

    /// <summary>Forward offset from enemy centre to projectile muzzle.</summary>
    public const float MUZZLE_FORWARD_OFFSET    = 0.3f;

    /// <summary>Height offset for the projectile muzzle spawn point.</summary>
    public const float MUZZLE_HEIGHT            = 0.5f;

    /// <summary>Spread half-angle (degrees) for the boss fan-shot attack.</summary>
    public const float BOSS_SPREAD_ANGLE        = 20f;

    /// <summary>Impulse force applied to the enemy body during a melee lunge.</summary>
    public const float MELEE_LUNGE_FORCE        = 3f;

    // ── Spawning ─────────────────────────────────────────────────────────────

    /// <summary>Inward margin from arena edges used when choosing enemy spawn positions.</summary>
    public const float SPAWN_EDGE_MARGIN        = 0.5f;

    /// <summary>Minimum distance between a chosen spawn point and the player.</summary>
    public const float SPAWN_MIN_PLAYER_DIST    = 3f;

    /// <summary>Inward margin from arena edges used when choosing pickup positions.</summary>
    public const float PICKUP_INNER_MARGIN      = 1.5f;

    /// <summary>Minimum distance between a chosen pickup position and the player.</summary>
    public const float PICKUP_MIN_PLAYER_DIST   = 2f;

    // ── Revive ───────────────────────────────────────────────────────────────

    /// <summary>Wave pause duration granted after a player revive (seconds).</summary>
    public const float REVIVE_WAVE_PAUSE        = 2f;

    // ── Input ────────────────────────────────────────────────────────────────

    /// <summary>Joystick magnitude threshold that triggers first game activation.</summary>
    public const float JOYSTICK_FIRST_INPUT_THRESHOLD = 0.1f;

    // ── Object Pool ──────────────────────────────────────────────────────────

    /// <summary>Initial capacity pre-allocated in the enemy ObjectPool.</summary>
    public const int ENEMY_POOL_DEFAULT_CAPACITY = 16;

    /// <summary>Maximum number of inactive enemies retained in the pool.</summary>
    public const int ENEMY_POOL_MAX_SIZE         = 32;
}
