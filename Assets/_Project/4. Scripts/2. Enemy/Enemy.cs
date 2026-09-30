using FIMSpace.FProceduralAnimation;
using Unity.Behavior;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// The AI states an enemy can be in.
///
/// ⚠ APPEND NEW VALUES AT THE END. Unity Behavior serialises blackboard enums
/// by INDEX, so inserting a value in the middle silently reassigns every state
/// in every graph asset.
/// </summary>
[BlackboardEnum]
public enum AIState
{
    Idle,
    Patrol,
    Chase,
    Attack,
    UsingSkill,
    Investigate,
    LookAround
}

/// <summary>
/// The identity of an enemy: its health, its components, and its lifecycle.
///
/// ── WHAT THIS CLASS IS AND ISN'T ────────────────────────────────────────
/// It does NOT decide behaviour — the behavior graph does that. It does not
/// move (AI_Locomotion), fight (AI_Combat), see (FieldOfView) or react
/// visually (HitReaction). It is the shared centre those systems hang off:
/// the thing that has health, can be damaged, can die, and can be recycled.
///
/// Its one piece of real logic is damage handling, because that is the point
/// where several systems have to be coordinated in a specific order.
///
/// ── POOLING ─────────────────────────────────────────────────────────────
/// Enemies are POOLED. They are reused, never recreated, so every field,
/// coroutine, component state and animator parameter survives death and comes
/// back with the next enemy. The Pool Lifecycle region is the contract that
/// keeps that from leaking. See Enemy_Spawn_Contract.md before changing it.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BehaviorGraphAgent), typeof(NavMeshAgent), typeof(FieldOfView))]
public class Enemy : PoolableObject, IDamagable
{
    #region Blackboard Keys
    // ────────────────────────────────────────────────────────────────────────
    //  SetVariableValue takes a STRING and fails SILENTLY on a typo or a
    //  renamed variable — no exception, no warning, the write simply doesn't
    //  land. Naming them once here means a typo is a compile error, and
    //  renaming a blackboard variable is one edit rather than a search.
    // ────────────────────────────────────────────────────────────────────────

    public const string BB_Player            = "Player";
    public const string BB_Enemy             = "Enemy";
    public const string BB_AIState           = "AI State";
    public const string BB_PlayerDetected    = "Player Detected";
    public const string BB_Alerted           = "Alerted";
    public const string BB_FollowPlayer      = "Follow Player";
    public const string BB_LastKnownPosition = "Last Known Position";
    public const string BB_SightLostTime     = "Sight Lost Time";
    public const string BB_SkillToUse       = "Skill To Use";

    #endregion

    #region Inspector — Identity

    [Tooltip("Mirrors the blackboard's AI State. The graph's first branch syncs " +
             "blackboard → this every tick, which is why code that wants to CHANGE " +
             "the state must write the blackboard, not this field.")]
    public AIState CurrentState;

    [Tooltip("Maximum health. Set from the Enemy Config on spawn.")]
    public int Health = 100;

    [SerializeField]
    [Tooltip("Serialized so it's visible while debugging in play mode.")]
    private int currentHealth;

    [HideInInspector]
    [Tooltip("Stagger force at or above this interrupts the current action and " +
             "plays the reaction animation. Set from the Enemy Config.")]
    public float StaggerThreshold = 25f;

    #endregion

    #region Inspector — Animator

    [Space]
    [Header("Animator Settings")]
    [Tooltip("Fired on spawn to snap the state machine back to locomotion.")]
    public string ResetParam = "Reset";
    [Tooltip("1 = hit from the front, 2 = from behind. Consumed by directional " +
             "hit reaction clips.")]
    public string HitDirectionParam = "HitDirection";
    [Tooltip("Plays the authored stagger reaction.")]
    public string StaggerTrigger = "Stagger";
    [Tooltip("Alert call animation, used by the AlertEnemies skill.")]
    public string AlertTrigger = "Alert";

    #endregion

    #region Inspector — Components

    [Space]
    [Header("Components")]
    public BehaviorGraphAgent behavior;
    public NavMeshAgent Agent;
    public Animator Animator;
    public AI_Locomotion AI_Locomotion;
    public FieldOfView FOV;
    public AI_Combat AI_Combat;
    public HitReaction HitReaction;


    #endregion

    #region Inspector — Utility Skills

    [Space]
    [Header("Utility Skills")]
    [Tooltip("Skills invoked directly by the graph rather than through AI_Combat " +
             "— the alert call and the kamikaze detonation.")]
    public bool hasUtilitySkill;
    public SkillsScriptableObject UtilitySkill;
    public bool IsUsingSkill;

    #endregion

    #region Inspector — Death

    [Space]
    [Header("Death Components")]
    [SerializeField]
    [Tooltip("Root transform handed to the death config — the object to sink.")]
    private Transform root;
    [SerializeField]
    private ParticleSystem deathParticle;
    [Tooltip("Animator death. Owns everything that happens after health reaches zero.")]
    public DeathScriptableObject DeathSO;

    #endregion

    #region Runtime State

    /// <summary>Live VFX for the current skill activation. See the Skill VFX region.</summary>
    [HideInInspector] public PoolableObject ActiveSkillInstance;
    [HideInInspector] public ObjectPool ActiveSkillPool;

    // Where the last hit came from. Feeds the investigate reaction, and is
    // available for directional ragdoll deaths (Phase 11.3).
    private Vector3 lastDamageSource;
    private bool hasDamageSource;

    private Renderer[] renderers;

    // Utility-skill cooldown stamp.
    private float UseTime;

    // Re-entrancy guard: Die() can be reached both from the OnDeath event and
    // directly (DieOnApproach), and dying twice would run the death config twice.
    private bool isDying;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => Health;
    public bool IsDead => isDying || currentHealth <= 0;

    #endregion

    #region Events

    public event IDamagable.TakeDamageEvent OnTakeDamage;
    public event IDamagable.DeathEvent OnDeath;

    #endregion

    #region Pool Lifecycle
    // ────────────────────────────────────────────────────────────────────────
    //  THE RULE (Enemy_Spawn_Contract.md §1):
    //    A temporary suspension restores what it WAS.
    //    A life boundary establishes what it SHOULD BE.
    //
    //  THE SPLIT (§3):
    //    OnDisable  → releases what this enemy OWNS: pooled objects, running
    //                 coroutines, the behavior graph instance, subscriptions.
    //                 These must be given up while the enemy still exists to
    //                 give them up.
    //    ResetForSpawn → asserts VALUES: flags, numbers, component
    //                 enabled-states. Cheap, idempotent, one place.
    // ────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        // Serialized references win; GetComponent is the fallback so a prefab
        // with an unassigned field still works.
        if (behavior == null)      behavior      = GetComponent<BehaviorGraphAgent>();
        if (Agent == null)         Agent         = GetComponent<NavMeshAgent>();
        if (Animator == null)      Animator      = GetComponent<Animator>();
        if (FOV == null)           FOV           = GetComponent<FieldOfView>();
        if (AI_Combat == null)     AI_Combat     = GetComponent<AI_Combat>();
        if (AI_Locomotion == null) AI_Locomotion = GetComponent<AI_Locomotion>();
        if (HitReaction == null)   HitReaction   = GetComponent<HitReaction>();
    }

    private void OnEnable()
    {
        // Called here AND from SetupEnemy. ResetForSpawn is deliberately
        // idempotent so that's safe: a spawner that forgets to configure an
        // enemy still gets a clean one, and configuring twice costs nothing.
        ResetForSpawn();
    }

    public override void OnDisable()
    {
        // ── Release what this enemy owns ─────────────────────────────────
        OnDeath -= Die;

        ReleaseActiveSkillInstance();   // pooled VFX back to its pool
        AI_Combat.ResetCombatState();   // stops skill + hit-window coroutines
        AI_Locomotion.ClearStagger();   // stops lock + watchdog coroutines
        HitReaction?.ResetReaction();

        // ⚠ MUST BE LAST. PoolableObject.OnDisable is what returns this object
        // to its pool. Without it enemies are never reclaimed, the pool drains,
        // and the spawner silently stops producing enemies.
        base.OnDisable();
    }

    /// <summary>
    /// Puts a recycled enemy back the way it started. Called from OnEnable and
    /// again at the top of EnemyScriptableObject.SetupEnemy, before the
    /// blackboard is configured, so the graph is live when its variables land.
    ///
    /// Safe to call any number of times.
    /// </summary>
    public void ResetForSpawn()
    {
        isDying = false;

        // ── Components the death path disabled ───────────────────────────
        // DeathScriptableObject.DisableMovement() turns these off and never
        // turns them back on. Asserted here rather than mirrored there,
        // because every death config would otherwise need its own undo — and
        // there will be more death configs than there are spawn paths.
        behavior.enabled      = true;
        AI_Combat.enabled     = true;
        AI_Locomotion.enabled = true;

        // ── Event subscription ───────────────────────────────────────────
        // Unsubscribe-then-subscribe rather than subscribe: this method runs
        // more than once per life, and a double subscription would run the
        // whole death sequence twice.
        OnDeath -= Die;
        OnDeath += Die;

        // ── Per-life values ──────────────────────────────────────────────
        ResetHealth();
        IsUsingSkill    = false;
        UseTime         = 0f;
        hasDamageSource = false;
        lastDamageSource = Vector3.zero;

        AI_Combat.ResetCombatState();
        AI_Locomotion.ClearStagger();
        AI_Locomotion.SetRootMotionMode(false);

        // Guarded: an agent that hasn't been placed on the NavMesh yet logs an
        // error on ResetPath. On the very first spawn the spawner may not have
        // positioned this enemy yet.
        if (Agent != null && Agent.isOnNavMesh)
            AI_Locomotion.ResetLocomotion();

        // ── Presentation ─────────────────────────────────────────────────
        SetVisible(true);
        HitReaction?.ResetReaction();
        ReleaseActiveSkillInstance();
        ClearQueuedTriggers();

        // NOTE: the FOV is deliberately NOT reset here. FOVConfig's
        // Setup_FOVConfig assigns radius and DetectionAngle from the config
        // later in SetupEnemy, which restores the patrol values as a side
        // effect. Calling SetPatrolFOV() here would run BEFORE that on the
        // first spawn, when the patrol values haven't been captured yet — and
        // would set the radius to zero, leaving a permanently blind enemy.
        // Verify Setup_FOVConfig assigns BOTH fields.
    }

    /// <summary>Full health, alive.</summary>
    public void ResetHealth() => currentHealth = Health;

    /// <summary>
    /// Animator.SetTrigger QUEUES. A trigger set on a frame the animator can't
    /// consume — the enemy died, the component was disabled, no valid
    /// transition existed — stays queued and fires on the NEXT enemy's first
    /// frame. Stagger something and kill it in the same instant, and the
    /// recycled enemy plays a reaction on spawn for no reason.
    /// </summary>
    private void ClearQueuedTriggers()
    {
        Animator.ResetTrigger(StaggerTrigger);
        Animator.ResetTrigger(AlertTrigger);

        Animator.SetInteger(HitDirectionParam, 0);
        Animator.SetTrigger(ResetParam);   // the intended reset, fired last
    }

    #endregion

    #region Taking Damage

    /// <summary>
    /// The single entry point for anything that hurts this enemy — bullets,
    /// grenades, melee overlaps, explosions, the drop pod.
    ///
    /// ORDER MATTERS and is not arbitrary:
    ///   1. Remember the source, so a shot from behind can be investigated
    ///   2. Set the hit direction BEFORE health changes, so nothing clears the
    ///      animator parameter after it's been set (a bug this used to have)
    ///   3. Apply damage, and bail on death — no point staggering a corpse
    ///   4. Stagger, then react
    /// </summary>
    /// <param name="sourcePosition">World position the damage came FROM (the
    /// attacker), not the impact point — "where did this come from" is only
    /// meaningful about the shooter.</param>
    /// <param name="staggerForce">Disruption, independent of damage. Compared
    /// against StaggerThreshold.</param>
    public void TakeDamage(int damage, Vector3 sourcePosition, float staggerForce = 0f)
    {
        if (IsDead) return;

        lastDamageSource = sourcePosition;
        hasDamageSource = true;

        SetHitDirection(sourcePosition);

        int damageTaken = Mathf.Clamp(damage, 0, currentHealth);
        if (damageTaken > 0)
        {
            currentHealth -= damageTaken;
            OnTakeDamage?.Invoke(damageTaken);

            if (currentHealth <= 0)
            {
                OnDeath?.Invoke();
                return;
            }
        }

        ApplyStagger(sourcePosition, staggerForce);
        ReactToDamage();
    }

    /// <summary>
    /// Front or back, in the enemy's own space. Private and called before the
    /// health change — it used to be a separate public pre-call, and
    /// TakeDamage then cleared the parameter it had just set.
    /// </summary>
    private void SetHitDirection(Vector3 sourcePosition)
    {
        Vector3 local = transform.InverseTransformPoint(sourcePosition);
        Animator.SetInteger(HitDirectionParam, local.z > 0 ? 1 : 2);
    }

    #endregion

    #region Stagger

    /// <summary>
    /// Two tiers, and deliberately never both at once.
    ///
    /// Below the threshold: a procedural bone tilt and nothing else. There is
    /// no animation at this tier, so the tilt IS the feedback — it has to be
    /// visible or light hits read as landing on nothing.
    ///
    /// At or above: the authored reaction animation owns the body, the current
    /// action is interrupted, and movement locks. The tilt is cleared rather
    /// than layered, because a procedural offset fighting an authored clip is
    /// what made staggers look rubbery.
    /// </summary>
    private void ApplyStagger(Vector3 sourcePosition, float force)
    {
        // Guards the case where StaggerThreshold is 0 — without it, `0 >= 0`
        // makes every damage event a full stagger.
        if (force <= 0f) return;

        Vector3 direction = transform.position - sourcePosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = -transform.forward;
        direction.Normalize();

        if (force >= StaggerThreshold)
        {
            HitReaction?.ResetReaction();

            // Interrupt FIRST: it resets root motion and re-enables the agent,
            // so doing it after the lock would immediately undo the lock.
            AI_Combat.InterruptAction();
            Animator.SetTrigger(StaggerTrigger);
            AI_Locomotion.LockForStagger();
        }
        else
        {
            HitReaction?.ApplyImpulse(direction, force);
        }
    }

    #endregion

    #region Reacting to Damage

    /// <summary>
    /// Being shot tells an unaware enemy roughly where the attacker is, so it
    /// investigates instead of standing there until the player wanders into
    /// its cone.
    ///
    /// Writes the BLACKBOARD rather than CurrentState directly — the graph's
    /// first branch syncs blackboard → CurrentState every tick, so a direct
    /// write would be overwritten on the next frame.
    /// </summary>
    private void ReactToDamage()
    {
        if (behavior == null || !hasDamageSource) return;

        // Already engaged: it knows where the player is. Interrupting a chase
        // to investigate the place the shot came from would be a downgrade.
        if (CurrentState == AIState.Chase
            || CurrentState == AIState.Attack
            || CurrentState == AIState.UsingSkill)
            return;

        behavior.SetVariableValue(BB_LastKnownPosition, lastDamageSource);
        behavior.SetVariableValue(BB_AIState, AIState.Investigate);
    }

    #endregion

    #region Utility Skills
    // ────────────────────────────────────────────────────────────────────────
    //  ⚠ This is a SECOND entry point for using a skill, parallel to
    //  AI_Combat.UseSkill. They have different preconditions (this checks
    //  IsUsingSkill + UseTime; AI_Combat checks isAttacking, isUsingSkill and
    //  per-skill cooldowns) and different completion methods. Which rules
    //  apply depends on which door the call came through.
    //
    //  Scheduled for collapse during the cleanup pass. Until then, the
    //  transitional SkillsScriptableObject.UseSkill shim must stay, because
    //  this path depends on it.
    // ────────────────────────────────────────────────────────────────────────

    public bool CanUseSkill(GameObject Player)
    {
        if (UtilitySkill == null) return false;

        return UtilitySkill.CanUseSkill(this, Player)
            && UseTime + UtilitySkill.Cooldown < Time.time;
    }

    public void UseSkill(SkillsScriptableObject Skill, GameObject Player)
    {
        IsUsingSkill = true;
        Skill.UseSkill(this, Player);
    }

    /// <summary>
    /// Clears the utility-skill flag and stamps the cooldown. Must be reached
    /// on EVERY exit path including interruption — AlertEnemies could
    /// previously only fire once ever, because nothing called this.
    /// </summary>
    public void CompleteUtillitySkill()
    {
        UseTime = Time.time;
        IsUsingSkill = false;
    }

    #endregion

    #region Skill VFX
    // ────────────────────────────────────────────────────────────────────────
    //  Per-activation state lives HERE and not on the SkillsScriptableObject,
    //  because the SO is one asset shared by every enemy using that skill. Two
    //  Giants slamming at once would overwrite each other's handle; interrupt
    //  one and you'd release the OTHER's VFX while the first leaked forever.
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registers the live VFX for the current skill activation. Releases any
    /// previous one first — a skill with a wind-up followed by a main effect
    /// (BreathType) registers twice, and the wind-up must not be orphaned.
    /// </summary>
    public void SetActiveSkillInstance(PoolableObject instance, ObjectPool pool)
    {
        ReleaseActiveSkillInstance();

        ActiveSkillInstance = instance;
        ActiveSkillPool = pool;
    }

    /// <summary>
    /// Returns the live skill VFX to its pool. Safe at any point — before the
    /// skill started, mid-way, after it finished, twice in a row.
    ///
    /// Both normal completion and interruption call this, deliberately: one
    /// teardown path, exercised on every single cast, so the interrupt case is
    /// never the untested one.
    /// </summary>
    public void ReleaseActiveSkillInstance()
    {
        if (ActiveSkillInstance == null) { ActiveSkillPool = null; return; }

        if (ActiveSkillInstance.TryGetComponent(out ParticleSystem ps))
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Reparenting is illegal while this GameObject is being deactivated —
        // activeSelf is already false by the time OnDisable runs. Release should
        // have happened in Die() before this point; skipping the reparent here
        // keeps a missed path from throwing.
        if (gameObject.activeSelf)
            ActiveSkillPool?.ResetParent(ActiveSkillInstance);

        ActiveSkillInstance.gameObject.SetActive(false);

        ActiveSkillInstance = null;
        ActiveSkillPool = null;
    }

    // ClearActiveSkillInstance() was removed deliberately: nulling the fields
    // without returning the object orphans a pooled VFX, and every former
    // caller wanted Release. If the compiler flags a call site, it's a bug
    // being surfaced, not a regression.

    #endregion

    #region Death

    /// <summary>
    /// Reached either through the OnDeath event or directly by a skill
    /// (DieOnApproach). Everything after this belongs to the DeathSO.
    /// </summary>
    public void Die()
    {
        if (isDying) return;
        isDying = true;

        // Cut short whatever was in progress — otherwise the attack animation
        // or skill coroutine plays on over the death and the enemy visibly
        // finishes its swing before dropping.
        //
        // Safe for the kamikaze: DieOnApproach is Interruptible = false, so
        // InterruptSkill returns without stopping the coroutine that called
        // this method.
        AI_Combat.InterruptAction();
        ReleaseActiveSkillInstance();
        AI_Combat.StopRoutine();          // FOV polling has no business running on a corpse
        AI_Locomotion.ClearStagger();     // kills the lock, watchdog and recovery coroutines
        HitReaction?.ResetReaction();

        AI_Locomotion.Stop();
        AI_Locomotion.enabled = false;    // the death config owns the transform now

        currentHealth = 0;
        DeathSO.Die(this, root, deathParticle);
    }

    /// <summary>
    /// Hides or shows the body without deactivating it — used by ExplosionDeath,
    /// which consumes the bot in its own blast but needs the GameObject alive a
    /// moment longer to host the cleanup coroutine.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (renderers == null) renderers = GetComponentsInChildren<Renderer>(true);

        foreach (Renderer r in renderers)
            if (r != null) r.enabled = visible;
    }

    #endregion
}