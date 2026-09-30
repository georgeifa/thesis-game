using MyBox;
using UnityEngine;

/// <summary>
/// Per-enemy-type combat tuning: how it attacks, how its melee connects, and
/// which skills it can use.
///
/// ── WHY EVERYTHING LIVES HERE RATHER THAN ON THE PREFAB ─────────────────
/// A value that exists only on the prefab is invisible to the config, which
/// means a new enemy silently inherits whatever the template happened to
/// carry. That is exactly how `skillsUnlocked` cost an afternoon: it was the
/// one field on AI_Combat this class didn't assign, so it was false forever
/// and nothing said why.
///
/// The rule: if it tunes BEHAVIOUR, it belongs in a config. Only genuine
/// scene/prefab references — WeaponObjects, SkillsVFX — stay on the prefab,
/// because a ScriptableObject cannot reference a transform inside one.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Combat Config", menuName = "Enemy/Combat Config")]
public class CombatConfigurationScriptableObject : ScriptableObject
{
    #region Animator

    [Header("Animator Settings")]
    [Tooltip("Do attacks and skills move the enemy via root motion? If the clip " +
             "was authored to travel, this must be on or the enemy slides.")]
    public bool useRootMotion = false;
    [Tooltip("Int parameter selecting which attack plays. 0 = none.")]
    public string attackTrigger = "AttackNo";
    [Tooltip("How many attack animations this enemy has, for the random pick.")]
    public int attacksCount = 3;
    [Tooltip("Int parameter selecting which skill plays. 0 = none.")]
    public string skillTrigger = "SkillNo";

    #endregion

    #region Attacking

    [Header("Attack Settings")]
    [Tooltip("Declared for the Arachno Tank. Currently unused — see Phase 10.3.")]
    public bool isRanged = false;
    [Tooltip("Damage per basic attack.")]
    public int damage = 15;
    [Tooltip("For melee this should be the root-motion travel distance +10-15%. " +
             "It's the range at which the enemy decides to swing, NOT the range " +
             "the swing reaches — that's hitRadius below.")]
    public float attackRange = 1f;
    [Tooltip("Seconds between attacks. Stamped when the attack STARTS, so an " +
             "interrupted swing still respects it.")]
    public float attackCooldown = 1.5f;
    [Tooltip("Cone the player must be inside for the enemy to consider attacking.")]
    public float attackAngle = 90f;
    [Tooltip("Disruption dealt by a melee hit, independent of damage. The player's " +
             "staggerThreshold decides whether it breaks their action — 20 by " +
             "default, so 15 here means melee hurts but doesn't interrupt.")]
    public float staggerForce = 15f;

    #endregion

    #region Melee Hit Window

    [Header("Melee Hit")]
    [Tooltip("Overlap radius around the weapon during the strike. Scale this with " +
             "the enemy: 0.6 suits a mutant, a Giant's wide swing needs 1.2-1.5 " +
             "or hits pass straight through the player.")]
    public float hitRadius = 0.6f;

    [Tooltip("How long the strike stays live, seconds. Must cover the swing's " +
             "contact frames — too short and slow heavy attacks whiff, too long " +
             "and the enemy hits you after you've already dodged. ~0.15 for a fast " +
             "claw, 0.25-0.35 for a heavy two-hander.")]
    public float hitWindowDuration = 0.15f;

    #endregion

    #region Watchdogs

    [Header("Watchdogs")]
    [Tooltip("Force-completes an attack if its animation event never fires. " +
             "Without it, a clip cut short locks the enemy out of attacking for " +
             "the rest of its life. Set above the longest attack clip.")]
    public float maxAttackDuration = 3f;

    [Tooltip("Force-completes a skill that never reaches OnSkillComplete — an " +
             "early exit, a retimed clip, a missing event. Set above the longest " +
             "skill this enemy has, including its wind-up and VFX lifetime.")]
    public float maxSkillDuration = 10f;

    #endregion

    #region Skills

    [Header("Skill Settings")]
    [Tooltip("Cooldowns start at one full cooldown from spawn, so an enemy can't " +
             "open an encounter with a skill the instant it sees you.")]
    public SkillsScriptableObject[] Skills;

    #endregion

    #region References

    [Header("References")]
    [Tooltip("What melee overlaps and range checks look for.")]
    public LayerMask playerLayer;

    #endregion

    #region Ranged (not yet implemented)

    [Header("Ranged Attacks Properties")]
    [ConditionalField(nameof(isRanged))] public Vector3 ShootpointPosition;
    [ConditionalField(nameof(isRanged))] public bool hasLaserSight;

    [ConditionalField(nameof(hasLaserSight), nameof(isRanged))]
    public LaserSightConfigScriptableObject laserSightConfig;

    [ConditionalField(nameof(isRanged))] public PoolableObject ProjectilePrefab;

    #endregion

    #region Setup

    /// <summary>
    /// Applies this config to an enemy on spawn.
    ///
    /// ⚠ StartRoutine() MUST be the last call. It rebuilds every piece of
    /// per-skill state from the Skills array assigned just above, and restarts
    /// the range-polling coroutines — which a pooled enemy has none of, because
    /// deactivating a GameObject kills its coroutines permanently.
    /// </summary>
    public void Setup_CombatConfig(Enemy enemy)
    {
        AI_Combat combat = enemy.AI_Combat;

        // ── Animator ─────────────────────────────────────────────────────
        combat.useRootMotion = useRootMotion;
        combat.attackTrigger = attackTrigger;
        combat.attacksCount  = attacksCount;
        combat.skillTrigger  = skillTrigger;

        // ── Attacking ────────────────────────────────────────────────────
        combat.isRanged       = isRanged;
        combat.damage         = damage;
        combat.attackRange    = attackRange;
        combat.attackCooldown = attackCooldown;
        combat.attackAngle    = attackAngle;
        combat.staggerForce   = staggerForce;

        // ── Melee hit window ─────────────────────────────────────────────
        combat.hitRadius         = hitRadius;
        combat.hitWindowDuration = hitWindowDuration;

        // ── Watchdogs ────────────────────────────────────────────────────
        combat.maxAttackDuration = maxAttackDuration;
        combat.maxSkillDuration  = maxSkillDuration;

        // ── Skills & references ──────────────────────────────────────────
        combat.Skills      = Skills;
        combat.playerLayer = playerLayer;

        // ── LAST: rebuild derived state and restart polling ──────────────
        combat.StartRoutine();
    }

    #endregion

    // The ranged implementation (shootpoint, laser sight, projectile) is still
    // commented out in version control history rather than here — it was cut
    // with the Arachno Tank. The serialized fields above are kept so existing
    // assets don't lose their values if it comes back.
}