using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Drives an enemy's movement by combining a NavMeshAgent (which does the
/// pathfinding) with manual control of the transform (which does the moving).
///
/// ── THE CENTRAL IDEA ────────────────────────────────────────────────────
/// The agent runs with updateRotation = false and updatePosition = false, so
/// it plans but never moves anything. Each frame this script reads the agent's
/// intent (agent.desiredVelocity), decides how fast the enemy should actually
/// travel, and writes the transform itself.
///
/// Doing it manually is what allows turn-based slowdown, animation-driven
/// speed, root-motion attacks and stagger locks — none of which are possible
/// if the agent owns the transform. The cost is that agent and transform can
/// drift apart, which is what the Agent Sync region exists to manage.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class AI_Locomotion : MonoBehaviour
{
    #region Inspector

    [Header("Movement")]
    [Tooltip("Speed while patrolling or idle-wandering.")]
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
    [Tooltip("Turn angle beyond which turnSlowFactor kicks in.")]
    public float maxTurnAngle = 45f;

    [Header("Stagger")]
    [Tooltip("Extra movement lock after the stagger clip's animation event, " +
             "covering the transition blend back into locomotion. The clip ends " +
             "before the visible POSE does — this covers the difference.")]
    public float staggerRecovery = 0.15f;
    [Tooltip("Safety net only. The EndStagger animation event is what normally " +
             "releases the lock; this forces a release if that event never fires, " +
             "so a missing event can't freeze an enemy permanently.")]
    public float maxStaggerLockDuration = 2f;

    [Header("Animation Parameters")]
    public string speedParam = "Speed";
    public string isMovingParam = "IsMoving";
    public string LookingAroundParam = "LookingAroundParam";

    #endregion

    #region State

    private NavMeshAgent agent;
    private Animator animator;
    private Enemy Enemy;
    private bool referencesCached;

    // Smoothed travel speed, so the enemy accelerates rather than snapping to
    // full speed. Also the value fed to the animator's blend tree.
    private float currentSpeed;
    private float speedDamp;

    private bool useRootMotion;

    // True while a stagger reaction is playing. Distinct from agentSyncSuspended
    // because other systems ask "is this enemy staggered?", not "is its
    // transform currently owned by something else?"
    private bool isStaggered;
    private Coroutine staggerRoutine;

    // What agent.isStopped was BEFORE the stagger, so the lock restores the
    // enemy's actual prior state instead of assuming "moving". Without this,
    // staggering an enemy that was deliberately stopped (LookAround) sets it
    // walking again when the lock expires.
    private bool staggerWasStopped;

    public bool IsStaggered => isStaggered;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Idempotent reference caching. Called from Awake AND from every public
    /// entry point, because Unity does NOT guarantee Awake ordering across
    /// components: Enemy.OnEnable can run before this component's Awake, which
    /// is exactly how SetRootMotionMode threw a NullReferenceException during
    /// pool warm-up.
    ///
    /// The rule this encodes: never assume a sibling component has woken up.
    /// </summary>
    private void CacheReferences()
    {
        if (referencesCached) return;
        referencesCached = true;

        Enemy    = GetComponent<Enemy>();
        agent    = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        // The agent plans; this script moves. See the class summary.
        agent.updateRotation = false;
        agent.updatePosition = false;
    }

    private void Awake() => CacheReferences();

    private void Update()
    {
        // Something else owns the transform right now — hold the agent to it
        // and do nothing else. See the Agent Sync region.
        if (agentSyncSuspended)
        {
            HoldAgentAtTransform();
            return;
        }

        bool wantsToMove = agent.desiredVelocity.magnitude > 0.1f && !agent.isStopped;

        if (wantsToMove)
        {
            UpdateMovement();
        }
        else
        {
            currentSpeed = 0f;
            SetBool(isMovingParam, false);
        }

        // The normal case: transform follows the agent's simulated position.
        if (!agent.isStopped)
            transform.position = agent.nextPosition;
    }

    /// <summary>
    /// Root-motion frames move the transform, so the agent has to be dragged
    /// along to match. Only runs while applyRootMotion is on.
    /// </summary>
    private void OnAnimatorMove()
    {
        if (animator == null || !animator.applyRootMotion) return;

        transform.position += animator.deltaPosition;
        transform.rotation *= animator.deltaRotation;
        agent.nextPosition = transform.position;
    }

    #endregion

    #region Agent Sync
    // ────────────────────────────────────────────────────────────────────────
    //  WHY THIS EXISTS
    //
    //  Because updatePosition is false, the transform only follows the agent
    //  because Update() copies agent.nextPosition into it. The agent keeps
    //  simulating regardless — residual velocity, local avoidance — so any
    //  period where Update() does NOT copy is a period where the two drift
    //  apart. When copying resumes, the transform snaps to wherever the agent
    //  wandered off to. That was the stagger teleport.
    //
    //  Two systems take ownership of the transform: root motion (the animation
    //  moves it) and the stagger lock (nothing moves it). Both go through the
    //  same suspend/resume pair, so the rule is stated once instead of
    //  remembered in three places.
    //
    //  Stop() is deliberately NOT a suspension. It stops the agent following
    //  its path, but Update() keeps copying, so the two stay married. Nothing
    //  has taken ownership — the enemy is simply standing still.
    // ────────────────────────────────────────────────────────────────────────

    private bool agentSyncSuspended;

    public bool IsAgentSyncSuspended => agentSyncSuspended;

    /// <summary>
    /// Hand the transform to something else. Stops the agent, kills residual
    /// motion, and tells the animator the enemy isn't travelling.
    /// </summary>
    private void SuspendAgentSync()
    {
        agentSyncSuspended = true;

        agent.isStopped = true;
        agent.velocity = Vector3.zero;

        currentSpeed = 0f;
        speedDamp = 0f;
        SetBool(isMovingParam, false);
    }

    /// <summary>
    /// Take the transform back. Resyncs the agent to wherever the transform
    /// actually ended up, so there is nothing left to snap to.
    /// </summary>
    /// <param name="resumePathing">
    /// False restores a stopped state — used by the stagger lock, which must
    /// return the enemy to what it was doing rather than to "moving".
    /// </param>
    private void ResumeAgentSync(bool resumePathing)
    {
        agentSyncSuspended = false;

        // Only meaningful on the NavMesh; off it these throw.
        if (agent.isOnNavMesh)
        {
            agent.nextPosition = transform.position;
            agent.velocity = Vector3.zero;
            agent.isStopped = !resumePathing;
        }
    }

    /// <summary>
    /// Called every frame while suspended. Dragging the agent along rather than
    /// only resyncing on release means anything that displaces the enemy
    /// mid-suspension (a future knockback, a physics nudge) is followed rather
    /// than fought.
    /// </summary>
    private void HoldAgentAtTransform()
    {
        if (agent.isOnNavMesh)
            agent.nextPosition = transform.position;
    }

    #endregion

    #region Movement

    private void UpdateMovement()
    {
        // Re-read every frame: the enemy's max speed depends on its AI state,
        // which can change mid-stride.
        agent.speed = GetCurrentMaxSpeed();

        float turnAngle = Vector3.Angle(transform.forward, agent.desiredVelocity.normalized);

        // Slow down through sharp turns so the enemy doesn't skate sideways.
        float speedMultiplier = turnAngle > maxTurnAngle ? turnSlowFactor : 1f;
        float targetSpeed = agent.desiredVelocity.magnitude * speedMultiplier;

        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedDamp, 0.1f);

        // During a sharp turn, travel along facing rather than along the path,
        // so the enemy pivots instead of drifting toward the corner.
        agent.velocity = turnAngle > 30f
            ? transform.forward * currentSpeed
            : agent.desiredVelocity.normalized * currentSpeed;

        Quaternion targetRot = Quaternion.LookRotation(agent.desiredVelocity.normalized);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRot, rotationSpeed * Time.deltaTime);

        // Normalised so the blend tree reads 0–1 regardless of walk vs chase.
        float animSpeed = currentSpeed / GetCurrentMaxSpeed();
        SetFloat(speedParam, animSpeed);
        SetBool(isMovingParam, animSpeed > 0.1f);
    }

    private float GetCurrentMaxSpeed()
    {
        if (Enemy == null) return walkSpeed;

        switch (Enemy.CurrentState)
        {
            case AIState.Chase: return chaseSpeed;
            case AIState.Patrol: return walkSpeed;
            default: return walkSpeed;
        }
    }

    #endregion

    #region Stagger Lock

    /// <summary>
    /// Freezes movement while the reaction animation plays. No displacement —
    /// knockback was dropped in favour of a clean freeze, so the disruption is
    /// "you lost a second" rather than "you were pushed".
    /// </summary>
    public void LockForStagger()
    {
        CacheReferences();

        // Capture on the FIRST stagger only. Re-staggering while already locked
        // would record isStopped = true and strand the agent on release.
        if (!isStaggered) staggerWasStopped = agent.isStopped;

        if (staggerRoutine != null) StopCoroutine(staggerRoutine);
        staggerRoutine = StartCoroutine(StaggerWatchdog());

        isStaggered = true;
        SuspendAgentSync();
    }

    /// <summary>
    /// Animation event on the LAST FRAME of every stagger clip. Starts the
    /// recovery pad rather than releasing immediately — see staggerRecovery.
    /// </summary>
    public void EndStagger()
    {
        if (!isStaggered) return;

        if (staggerRoutine != null) StopCoroutine(staggerRoutine);
        staggerRoutine = StartCoroutine(StaggerRecovery());
    }

    private IEnumerator StaggerRecovery()
    {
        yield return new WaitForSeconds(staggerRecovery);
        ReleaseStaggerLock();
    }

    /// <summary>
    /// Same idea as AI_Combat.maxAttackDuration: if the animation event never
    /// arrives, recover anyway. Loud, so a missing event is a warning in the
    /// console rather than an enemy that mysteriously stopped moving.
    /// </summary>
    private IEnumerator StaggerWatchdog()
    {
        yield return new WaitForSeconds(maxStaggerLockDuration);

        Debug.LogWarning($"{name}: stagger never ended via animation event — " +
                         "forcing release. Is the EndStagger event on the clip?");
        ReleaseStaggerLock();
    }

    private void ReleaseStaggerLock()
    {
        staggerRoutine = null;
        isStaggered = false;

        // Restore what the enemy WAS doing, not a constant.
        ResumeAgentSync(resumePathing: !staggerWasStopped);
    }

    /// <summary>Hard reset — call when the enemy is pooled or respawned.</summary>
    public void ClearStagger()
    {
        if (staggerRoutine != null) StopCoroutine(staggerRoutine);

        staggerRoutine = null;
        isStaggered = false;
        staggerWasStopped = false;
        agentSyncSuspended = false;
    }

    #endregion

    #region Public Control

    public void SetDestination(Vector3 destination)
    {
        CacheReferences();

        // Ignored while something else owns the transform — issuing a path
        // mid-attack would have the agent straining against the animation.
        if (!useRootMotion && !agent.isStopped && agent.isOnNavMesh)
            agent.SetDestination(destination);
    }

    /// <summary>
    /// Stand still but stay synced. NOT a sync suspension: Update() keeps
    /// copying the agent's position, so nothing drifts.
    /// </summary>
    public void Stop()
    {
        CacheReferences();

        if (agent.isOnNavMesh) agent.isStopped = true;
        agent.velocity = Vector3.zero;
        currentSpeed = 0f;
        SetBool(isMovingParam, false);
    }

    public void Resume()
    {
        CacheReferences();
        if (agent.isOnNavMesh) agent.isStopped = false;
    }

    public void ResetLocomotion()
    {
        CacheReferences();
        if (!agent.isOnNavMesh) return;

        agent.ResetPath();
        agent.isStopped = false;
    }

    public void ResetPath()
    {
        CacheReferences();
        if (agent.isOnNavMesh) agent.ResetPath();
    }

    /// <summary>
    /// Hands the transform to the animation (attacks and skills that travel).
    /// Both directions go through the sync pair, so the resync on exit can't be
    /// forgotten.
    /// </summary>
    public void SetRootMotionMode(bool enable)
    {
        CacheReferences();

        useRootMotion = enable;
        animator.applyRootMotion = enable;

        if (enable) SuspendAgentSync();
        else        ResumeAgentSync(resumePathing: true);
    }

    public void FinishLookingAround()
    {
        CacheReferences();

        SetInt(LookingAroundParam, 0);
        SetRootMotionMode(false);
    }

    #endregion

    #region Animator Guards
    // Parameter names are assigned from the Enemy Config, but this component
    // can be reset BEFORE that config is applied — during pool warm-up the
    // names are still whatever the prefab carries. Writing a parameter the
    // controller doesn't have logs a warning per call, which is noise rather
    // than signal. Guarded here; genuine misconfiguration still surfaces the
    // moment the enemy actually tries to move.

    private void SetBool(string param, bool value)
    {
        if (animator != null && !string.IsNullOrEmpty(param)) animator.SetBool(param, value);
    }

    private void SetFloat(string param, float value)
    {
        if (animator != null && !string.IsNullOrEmpty(param)) animator.SetFloat(param, value);
    }

    private void SetInt(string param, int value)
    {
        if (animator != null && !string.IsNullOrEmpty(param)) animator.SetInteger(param, value);
    }

    #endregion
}