using UnityEngine;

/// <summary>
/// The base stats for one enemy type, and the entry point that stamps them
/// onto a freshly spawned (or recycled) enemy.
///
/// ── THIS IS THE SPAWN PATH ──────────────────────────────────────────────
/// SetupEnemy is where a pooled enemy becomes a specific enemy again. It runs
/// on EVERY spawn, not just the first, so it is also the place where the spawn
/// contract is enforced: reset first, configure second, wake the graph last.
/// See Enemy_Spawn_Contract.md.
///
/// A value that isn't assigned here is a value a recycled enemy inherits from
/// its predecessor, or silently takes from whatever the prefab happened to
/// carry. That has caused enough bugs in this project that the default answer
/// is "put it in the config".
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Enemy Config", menuName = "Enemy/Enemy Config")]
public class EnemyScriptableObject : ScriptableObject
{
    #region Identity

    [Tooltip("The prefab the spawner pools for this enemy type.")]
    public Enemy Prefab;

    [Tooltip("Maximum health. Reset to full on every spawn.")]
    public int Health = 100;

    #endregion

    #region Movement

    [Header("Movement")]
    [Tooltip("Speed while patrolling or idle.")]
    public float walkSpeed = 1.5f;
    [Tooltip("Speed while chasing the player.")]
    public float chaseSpeed = 4.5f;
    [Tooltip("Degrees per second the enemy can turn. Too low and it swings at " +
             "where you were; too high and turns read as snapping.")]
    public float rotationSpeed = 180f;

    [Header("Turn Settings")]
    [Tooltip("Speed multiplier while turning sharply, so the enemy slows into " +
             "corners instead of skating around them.")]
    public float turnSlowFactor = 0.5f;
    [Tooltip("Turn angle beyond which turnSlowFactor applies.")]
    public float maxTurnAngle = 45f;

    #endregion

    #region Stagger

    [Header("Stagger")]
    [Tooltip("Stagger force at or above this plays the reaction animation, " +
             "interrupts the current attack or skill, and locks movement. Below " +
             "it, the hit only produces the procedural tilt. This is the knob " +
             "that makes a Giant feel heavy and a mutant feel flimsy.")]
    public float StaggerThreshold = 25f;

    [Tooltip("Extra movement lock after the stagger clip's EndStagger animation " +
             "event, covering the transition blend back into locomotion. The clip " +
             "ends before the visible POSE does — this covers the difference. " +
             "Too little and the enemy slides out of the reaction.")]
    public float staggerRecovery = 0.15f;

    [Tooltip("Safety net only. If the EndStagger animation event never fires, " +
             "the lock releases after this and logs a warning. Set above the " +
             "longest stagger clip.")]
    public float maxStaggerLockDuration = 2f;

    #endregion

    #region Hit Reaction
    // The procedural spine tilt. At the sub-threshold tier this is the ONLY
    // feedback a hit produces, so it has to be visible — an invisible tilt
    // reads as shots landing on nothing.

    [Header("Hit Reaction — Impulse")]
    [Tooltip("Degrees of tilt per unit of stagger force. Against a rifle's " +
             "StaggerForce of 8, a value of 8 gives 64° — clamped by maxAngle.")]
    public float degreesPerForce = 8f;
    [Tooltip("Hard ceiling on the tilt. Past ~45° it starts to read as rubbery " +
             "rather than impactful.")]
    public float maxAngle = 45f;
    [Tooltip("Random spread on the impact direction, degrees, so repeated hits " +
             "from the same angle don't produce an identical tilt.")]
    public float directionJitter = 25f;

    [Header("Hit Reaction — Settle")]
    [Tooltip("How long the reaction takes to settle back, seconds.")]
    public float duration = 0.7f;

    #endregion

    #region Utility Skills

    [Header("Utility Skills")]
    [Tooltip("Skills the graph invokes directly rather than through AI_Combat — " +
             "the alert call and the kamikaze detonation. Leave empty for enemies " +
             "that have none; hasUtilitySkill is derived from this.")]
    public SkillsScriptableObject UtilitySkill;

    #endregion

    #region Animator Parameters

    [Header("Animator Parameters")]
    public string speedParam = "Speed";
    public string isMovingParam = "IsMoving";
    public string ResetParam = "Reset";
    [Tooltip("1 = hit from the front, 2 = from behind.")]
    public string HitDirectionParam = "HitDirection";
    public string LookingAroundParam = "LookingAroundParam";
    [Tooltip("Plays the authored stagger reaction. Cleared on spawn, because " +
             "SetTrigger QUEUES — one set on the frame an enemy died would " +
             "otherwise fire on the next enemy's first frame.")]
    public string StaggerTrigger = "Stagger";
    [Tooltip("Alert call animation, used by the AlertEnemies skill.")]
    public string AlertTrigger = "Alert";

    #endregion

    #region Sub-Configs

    [Header("Sub-Configs")]
    public NavMeshAgentConfigurationScriptableObject NavMeshAgentConfig;
    public FOVConfigScriptableObject FOVConfig;
    public CombatConfigurationScriptableObject CombatConfig;
    public DeathScriptableObject DeathSO;

    #endregion

    #region Setup

    /// <summary>
    /// Configures an enemy for a new life. Called by the spawner AFTER the
    /// enemy has been warped onto the NavMesh — several resets below are
    /// skipped when the agent isn't on the mesh, and a pooled enemy sits
    /// underground where the sink left it until it's placed.
    ///
    /// ORDER MATTERS:
    ///   1. ResetForSpawn  — wipe the previous life
    ///   2. Stats          — stamp this type's values
    ///   3. Sub-configs    — locomotion, FOV, combat, death
    ///   4. Blackboard     — last, so the graph wakes with everything in place
    /// </summary>
    public void SetupEnemy(Enemy enemy, GameObject player)
    {
        // ── 1. Wipe the previous life ────────────────────────────────────
        // Also called from Enemy.OnEnable, but that runs before the spawner
        // has positioned the enemy, so the NavMesh-guarded resets inside it
        // are skipped there. Running it again here — at the final position —
        // is what actually clears the stale path. Idempotent by design.
        enemy.ResetForSpawn();

        // ── 2. Stats ─────────────────────────────────────────────────────
        enemy.CurrentState = AIState.Idle;

        enemy.Health = Health;
        enemy.ResetHealth();

        enemy.DeathSO = DeathSO;

        enemy.AI_Locomotion.walkSpeed      = walkSpeed;
        enemy.AI_Locomotion.chaseSpeed     = chaseSpeed;
        enemy.AI_Locomotion.rotationSpeed  = rotationSpeed;
        enemy.AI_Locomotion.turnSlowFactor = turnSlowFactor;
        enemy.AI_Locomotion.maxTurnAngle   = maxTurnAngle;

        enemy.AI_Locomotion.staggerRecovery        = staggerRecovery;
        enemy.AI_Locomotion.maxStaggerLockDuration = maxStaggerLockDuration;

        enemy.StaggerThreshold = StaggerThreshold;
        enemy.HitReaction?.Configure(degreesPerForce, maxAngle, directionJitter, duration);

        enemy.UtilitySkill    = UtilitySkill;
        enemy.hasUtilitySkill = UtilitySkill != null;

        // Animator parameter names.
        enemy.ResetParam        = ResetParam;
        enemy.HitDirectionParam = HitDirectionParam;
        enemy.StaggerTrigger    = StaggerTrigger;
        enemy.AlertTrigger      = AlertTrigger;

        enemy.AI_Locomotion.speedParam         = speedParam;
        enemy.AI_Locomotion.isMovingParam      = isMovingParam;
        enemy.AI_Locomotion.LookingAroundParam = LookingAroundParam;

        // ── 3. Sub-configs ───────────────────────────────────────────────
        NavMeshAgentConfig.Setup_NavMeshAgentConfig(enemy);
        FOVConfig.Setup_FOVConfig(enemy, player);
        CombatConfig.Setup_CombatConfig(enemy);

        // ── 4. Blackboard ────────────────────────────────────────────────
        Setup_BlackboardConfig(enemy, player);
    }

    /// <summary>
    /// Hands the behavior graph its references and wipes every per-life value.
    ///
    /// The object references (Player, Enemy) are configuration. Everything
    /// below them is STATE, and a recycled enemy that inherits it wakes up
    /// believing whatever its predecessor believed — mid-Attack, already
    /// Alerted, with a skill still selected from a fight it lost. Every one of
    /// those has been a bug in this project.
    ///
    /// Blackboard writes fail SILENTLY on a typo, which is why the names are
    /// constants on Enemy rather than literals here.
    /// </summary>
    private void Setup_BlackboardConfig(Enemy enemy, GameObject player)
    {
        // Configuration.
        enemy.behavior.SetVariableValue(Enemy.BB_Player, player);
        enemy.behavior.SetVariableValue(Enemy.BB_Enemy, enemy);

        // Per-life state.
        enemy.behavior.SetVariableValue(Enemy.BB_FollowPlayer, true);
        enemy.behavior.SetVariableValue(Enemy.BB_AIState, AIState.Idle);
        enemy.behavior.SetVariableValue(Enemy.BB_PlayerDetected, false);
        enemy.behavior.SetVariableValue(Enemy.BB_Alerted, false);
        enemy.behavior.SetVariableValue(Enemy.BB_SightLostTime, -1f);
        enemy.behavior.SetVariableValue(Enemy.BB_LastKnownPosition, enemy.transform.position);
        enemy.behavior.SetVariableValue(Enemy.BB_SkillToUse, (SkillsScriptableObject)null);
    }

    #endregion

    // ⚠ FOVConfig.Setup_FOVConfig must, in this order:
    //     SetPatrolRadius_Angle(...)  /  SetAlertedRadius_Angle(...)
    //     SetPatrolFOV()              — every life starts on patrol values
    //     PerformFOVCheck = ...       — ASSIGNMENT, not +=, or it accumulates
    //                                   a subscriber per respawn
    //     Restart()                   — LAST; the perception coroutine dies
    //                                   with the pool and never comes back
    //                                   otherwise
}