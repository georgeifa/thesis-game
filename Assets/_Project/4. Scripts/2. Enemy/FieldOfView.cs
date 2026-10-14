using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// An enemy's eyes. Polls on an interval rather than every frame — detection at
/// 10Hz is indistinguishable from per-frame in play, and costs a tenth as much
/// across a crowd.
///
/// ── TWO KINDS OF POLL ───────────────────────────────────────────────────
/// PERCEPTION (this component's own routine) answers "can I see the player at
/// all", using the live `radius` and `DetectionAngle` fields. Those fields
/// change with the enemy's state — a hunting enemy gets a wider cone and more
/// range — and because the loop re-reads them every tick, switching modes takes
/// effect on the next poll with no restart.
///
/// COMBAT polls are started by AI_Combat, which passes an explicit range and
/// angle per weapon or skill. Those are fixed per poll and unaffected by
/// alerted/patrol mode. That is what `isForCombat` selects between.
///
/// ⚠ The two kinds ask DIFFERENT QUESTIONS and must not share behaviour beyond
/// the loop itself. Perception asks "do I know where the player is". Combat asks
/// "is the player in front of me, within reach". Anything that answers one must
/// be gated off the other — see the Close-range Awareness region.
///
/// ── POOLING ─────────────────────────────────────────────────────────────
/// ⚠ The perception routine MUST be restarted on spawn. Deactivating a
/// GameObject kills its coroutines permanently, so a routine started from
/// Start() is gone after the enemy's first death — and `playerDetected` then
/// stays frozen at its final value forever. An enemy that died chasing came
/// back believing it could still see the player, went straight to Chase, and no
/// blackboard reset could talk it out of it because a graph node copies this
/// flag over the blackboard every tick.
///
/// Setup_FOVConfig calls Restart() at the end of configuration. See
/// Enemy_Spawn_Contract.md.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
[DisallowMultipleComponent]
public class FieldOfView : MonoBehaviour
{
    #region Inspector

    [Tooltip("Seconds between perception polls. 0.1 is plenty; lower is wasted work.")]
    public float interval = 0.1f;

    [Tooltip("Current sight range. Swapped between patrol and alerted values at " +
             "runtime — do not treat as a constant.")]
    public float radius;
    [Tooltip("Current cone width. Same: swapped at runtime.")]
    [Range(0, 360)]
    public int DetectionAngle;

    [Tooltip("Assigned from the Enemy Config.")]
    public GameObject playerRef;
    public LayerMask targetMask;
    public LayerMask obstructionMask;

    [Tooltip("Result of the last PERCEPTION poll. Read by the behavior graph. " +
             "Combat polls must never write this.")]
    public bool playerDetected;

    #endregion

    #region State

    // Set from FOVConfig, not from the live fields — capturing the live values
    // at Start would break for pooled enemies, whose Start runs before the
    // config has ever been applied.
    private float patrolRadius;
    private int patrolDetectionAngle;
    private float alertedRadius;
    private int alertedDetectionAngle;

    // Close-range awareness, swapped alongside the cone. See the region below.
    private float patrolAwarenessRadius;
    private float alertedAwarenessRadius;
    private float awarenessRadius;

    private readonly Collider[] awarenessBuffer = new Collider[4];

    private Coroutine perceptionRoutine;

    /// <summary>
    /// The actual line-of-sight test, supplied by FieldOfViewCheckScriptableObject.
    ///
    /// A plain delegate field rather than an event, deliberately: the config
    /// assigns it on every spawn, and with `event` the only way to do that is
    /// `+=`, which accumulates a new subscriber per respawn. After ten deaths an
    /// enemy would run ten identical overlap checks per poll and only the last
    /// return value would count.
    /// </summary>
    public Func<Transform, float, LayerMask, float, LayerMask, bool> PerformFOVCheck;

    #endregion

    #region Lifecycle

    /// <summary>
    /// Fallback for enemies placed directly in a scene. Spawned enemies are
    /// restarted by Setup_FOVConfig instead, which is the first moment the
    /// radius and angle are correct.
    /// </summary>
    private void Start() => Restart();

    /// <summary>
    /// Restarts perception for a fresh life: clears the stale detection result
    /// and relaunches the poll. Safe to call repeatedly.
    /// </summary>
    public void Restart()
    {
        StopRoutine();

        // Clearing this matters as much as restarting the loop. Left true, the
        // enemy is blind AND convinced it can see you.
        playerDetected = false;

        // The radius/angle arguments are vestigial for a perception poll — the
        // loop re-reads the live fields each tick when isForCombat is false.
        // They are passed anyway to keep one signature for both uses.
        perceptionRoutine = StartCoroutine(FOVRoutine(
            Mathf.Max(0.02f, interval),
            isForCombat: false,
            transform,
            radius,
            targetMask,
            DetectionAngle,
            obstructionMask,
            (result) => playerDetected = result));
    }

    public void StopRoutine()
    {
        if (perceptionRoutine == null) return;

        StopCoroutine(perceptionRoutine);
        perceptionRoutine = null;
    }

    #endregion

    #region Polling

    /// <summary>
    /// The shared poll loop, used for both perception and AI_Combat's range checks.
    /// </summary>
    /// <param name="isForCombat">
    /// True: use the radius and angle passed in, fixed for the life of the poll
    /// (attack range, skill range), cone test ONLY. False: re-read this
    /// component's live radius and DetectionAngle every tick, so alerted/patrol
    /// switching applies without restarting, and allow close-range awareness.
    /// </param>
    /// <param name="onComplete">Receives the result of every poll.</param>
    public IEnumerator FOVRoutine(float interval, bool isForCombat, Transform transform,
                                  float radius, LayerMask targetMask, float DetectionAngle, LayerMask obstructionMask,
                                  Action<bool> onComplete)
    {
        WaitForSeconds wait = new WaitForSeconds(interval);

        while (true)
        {
            yield return wait;

            float checkAngle  = isForCombat ? DetectionAngle : this.DetectionAngle;
            float checkRadius = isForCombat ? radius : this.radius;

            // Null-guarded: the check is supplied by the config, and a poll can
            // tick before configuration on the first frame after a spawn.
            bool seen = PerformFOVCheck != null &&
                        PerformFOVCheck(transform, checkRadius, targetMask, checkAngle, obstructionMask);

            // ⚠ PERCEPTION ONLY. A combat poll is asking whether the player is
            // in front of this enemy and within reach; proximity answers "yes"
            // while the enemy is facing the wrong way, which makes it swing at
            // nothing. Keep this gated.
            if (!isForCombat && !seen)
                seen = CheckProximityAwareness();

            // NOTE: this loop does NOT assign playerDetected. The perception
            // caller does that through onComplete; a combat caller writing it
            // would stomp the perception result with an attack-range answer ten
            // times a second.
            onComplete?.Invoke(seen);
        }
    }

    #endregion

    #region Close-range Awareness
    // The cone is a VISION model. It is correct for distance and wrong for
    // contact: an enemy you are standing beside knows you are there whether or
    // not it is facing you. Without this, circling an attacking enemy drops its
    // aggro — not because it lost you, but because its head didn't turn.
    //
    // Swapped with the cone between modes, and normally 0 while patrolling:
    // approaching an unaware enemy from behind should work. Once it is already
    // fighting you, standing behind it should not make you disappear.

    /// <summary>Called by the config. Usually 0 — stealth approach stays viable.</summary>
    public void SetPatrolAwareness(float radius) => patrolAwarenessRadius = radius;

    /// <summary>Called by the config. Roughly the enemy's reach plus a little.</summary>
    public void SetAlertedAwareness(float radius) => alertedAwarenessRadius = radius;

    private bool CheckProximityAwareness()
    {
        if (awarenessRadius <= 0f) return false;

        int count = Physics.OverlapSphereNonAlloc(
            transform.position, awarenessRadius, awarenessBuffer, targetMask);

        for (int i = 0; i < count; i++)
        {
            Vector3 toTarget = awarenessBuffer[i].transform.position - transform.position;
            float dist = toTarget.magnitude;

            if (dist < 0.01f) return true;

            // Still respects walls — you can break contact by putting cover between us.
            if (!Physics.Raycast(transform.position, toTarget / dist, dist, obstructionMask))
                return true;
        }
        return false;
    }
    #endregion

    #region Alerted / Patrol Modes

    /// <summary>Called by the config. Patrol values are the enemy's baseline.</summary>
    public void SetPatrolRadius_Angle(float radius, int angle)
    {
        patrolRadius = radius;
        patrolDetectionAngle = angle;
    }

    /// <summary>Called by the config. Used while chasing or investigating.</summary>
    public void SetAlertedRadius_Angle(float radius, int angle)
    {
        alertedRadius = radius;
        alertedDetectionAngle = angle;
    }

    /// <summary>
    /// Wide cone and long range — the enemy is actively hunting, so running
    /// wide shouldn't break contact. Takes effect on the next poll.
    /// </summary>
    public void SetAlertedFOV()
    {
        radius = alertedRadius;
        DetectionAngle = alertedDetectionAngle;
        awarenessRadius = alertedAwarenessRadius;
    }

    /// <summary>
    /// Back to the patrol cone and range. Also called on spawn — an enemy that
    /// died alerted would otherwise respawn hunting, and spot the player across
    /// the arena on its first poll.
    /// </summary>
    public void SetPatrolFOV()
    {
        radius = patrolRadius;
        DetectionAngle = patrolDetectionAngle;
        awarenessRadius = patrolAwarenessRadius;
    }

    #endregion
}