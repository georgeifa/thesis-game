using System.Collections;
using UnityEngine;

/// <summary>
/// The kamikaze's payload: on reaching the player, the enemy simply dies —
/// and its DeathScriptableObject (ExplosionDeath) does the damage.
///
/// The skill deliberately owns no logic beyond "die now". Putting the blast in
/// the death config rather than here means the spiderbot explodes identically
/// whether it detonates on purpose or is shot down, which is one behaviour to
/// tune instead of two.
/// </summary>
[CreateAssetMenu(fileName = "Die On Approach", menuName = "Skills/Utility Skills/Die On Approach")]
public class DieOnApproach : SkillsScriptableObject
{
    #region Execution

    public override IEnumerator Execute(Enemy enemy, GameObject player)
    {
        // One frame before dying, for a subtle but real ordering reason:
        // AI_Combat assigns skillRoutine from the RETURN value of
        // StartCoroutine, and StartCoroutine runs the body synchronously up to
        // the first yield. Dying before that first yield means Die() calls
        // InterruptAction() while skillRoutine still holds the previous value.
        // Harmless today because this skill is non-interruptible, but it is
        // the kind of ordering that breaks silently if that ever changes.
        yield return null;

        if (enemy == null) yield break;

        // Enemy.Die() calls AI_Combat.InterruptAction() as its first line.
        // That routes back into InterruptSkill(), which returns immediately
        // because Interruptible is false — so this coroutine is never stopped
        // by the very death it is causing.
        enemy.Die();
    }

    #endregion

    #region Safety

    /// <summary>
    /// A committed kamikaze cannot be talked out of it. Forced here rather
    /// than left as a checkbox: if someone ticks Interruptible on the asset,
    /// staggering the spiderbot at the moment of detonation would stop the
    /// coroutine mid-Die() and leave a half-dead enemy.
    /// </summary>
    private void OnValidate()
    {
        Interruptible = false;
    }

    private void OnEnable()
    {
        // OnValidate only runs in the editor; this covers builds.
        Interruptible = false;
    }

    #endregion
}