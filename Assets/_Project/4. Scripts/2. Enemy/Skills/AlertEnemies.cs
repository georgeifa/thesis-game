using System.Collections;
using Unity.Behavior;
using UnityEngine;

/// <summary>
/// Calls nearby enemies to the fight: plays an alert animation, waits a
/// telegraph window, then flags every enemy in radius as Alerted.
///
/// This is THE skill the interrupt system exists for. Cut it short during
/// ReactionTime and the alert never lands — the player killed the caller in
/// time. That turns an alarm from a punishment into a decision.
/// </summary>
[CreateAssetMenu(fileName = "AlertEnemies", menuName = "Skills/Utility Skills/AlertEnemies")]
public class AlertEnemies : SkillsScriptableObject
{
    #region Inspector

    [Tooltip("Which layer to search for other enemies.")]
    [SerializeField] private LayerMask TargetLayer;

    [Tooltip("Time between the animation starting and the enemies actually being " +
             "alerted. This is the player's window to kill the caller — it needs a " +
             "visible or audible tell to be fair (Phase 5.3).")]
    public float ReactionTime = 1f;

    [Tooltip("Animator trigger for the alert animation.")]
    public string AlertTrigger = "Alert";

    [Tooltip("Maximum enemies alerted by one call.")]
    public int MaxAlerted = 50;

    [Tooltip("Blackboard boolean set on each alerted enemy.")]
    public string AlertedVariable = "Alerted";

    #endregion

    #region Reusable buffer

    // Allocated once. The overlap itself is synchronous, and the results are
    // consumed before the coroutine yields, so a shared buffer is safe.
    [System.NonSerialized] private Collider[] alertBuffer;

    #endregion

    #region Conditions

    public override bool CanUseSkill(Enemy enemy, GameObject player)
    {
        // Deliberately NOT calling base: alerting doesn't require the player to
        // be in range, only that this enemy isn't already busy. The cooldown is
        // enforced by Enemy.CanUseSkill via UseTime.
        return !enemy.IsUsingSkill;
    }

    #endregion

    #region Execution

    public override IEnumerator Execute(Enemy enemy, GameObject player)
    {
        if (alertBuffer == null || alertBuffer.Length != MaxAlerted)
            alertBuffer = new Collider[Mathf.Max(1, MaxAlerted)];

        enemy.Animator.SetTrigger(AlertTrigger);

        // ── The telegraph window ─────────────────────────────────────────
        // Nothing has happened yet. Being stopped here means the alert is
        // cancelled outright, which is the entire point of the skill.
        yield return new WaitForSeconds(ReactionTime);

        int count = Physics.OverlapSphereNonAlloc(
            enemy.transform.position, Range, alertBuffer, TargetLayer);

        for (int i = 0; i < count; i++)
        {
            if (alertBuffer[i].TryGetComponent(out BehaviorGraphAgent agent))
                agent.SetVariableValue(AlertedVariable, true);

            // Spread across frames so a big group doesn't spike one frame.
            // Side effect worth keeping: an interrupt partway through alerts
            // only the enemies reached so far, so killing the caller late
            // still limits the damage.
            yield return null;
        }

        Complete(enemy);
    }

    /// <summary>
    /// Cut short — release the utility-skill flag so the enemy isn't left
    /// permanently unable to act. The alert simply never landed.
    /// </summary>
    public override void Interrupt(Enemy enemy)
    {
        base.Interrupt(enemy);
        Complete(enemy);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Clears Enemy.IsUsingSkill and stamps the cooldown. Previously nothing
    /// called this: IsUsingSkill was set true by Enemy.UseSkill and never
    /// reset, and CanUseSkill above reads it — so an enemy could alert exactly
    /// once, ever, and then silently refuse every skill for the rest of its life.
    /// Safe to call twice if an animation event also calls it.
    /// </summary>
    private void Complete(Enemy enemy)
    {
        if (enemy == null) return;
        enemy.CompleteUtillitySkill();
    }

    #endregion

    // ⚠ KNOWN GAP (Phase 5.2): nothing ever clears the Alerted blackboard flag
    //   once set. An enemy alerted at minute one is still alerted at minute ten.
    //   Same dead-end shape as the old Patrol bug. Not fixed here — it belongs
    //   with the reinforcements work, along with 5.1 (alert to the ALERTER's
    //   position rather than teleporting knowledge of the player).
}