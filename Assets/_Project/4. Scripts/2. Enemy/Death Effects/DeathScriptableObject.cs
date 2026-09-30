using System.Collections;
using UnityEngine;

/// <summary>
/// What happens after an enemy's health reaches zero: shutting down the AI,
/// playing whatever the death looks like, and eventually returning the body to
/// its pool.
///
/// This base class owns the parts every death shares — silencing the AI, and
/// sinking the corpse through the floor so it disappears without a pop.
/// Subclasses own the presentation: AnimatorDeath plays a clip, ExplosionDeath
/// removes the body entirely.
///
/// ── WHY THERE ARE NO CACHED COMPONENT FIELDS ────────────────────────────
/// A ScriptableObject is ONE asset shared by every enemy that uses it. The
/// previous version stored `animator`, `AI_Locomotion`, `behaviorGraphAgent`
/// and `deathParticle` as instance fields, so two enemies dying within a
/// second of each other overwrote each other's references — and any code
/// reading them after a yield was operating on whoever died most recently.
///
/// Everything is passed as arguments or read from `enemy` instead. Enemy
/// already caches its own components as public fields, so there is nothing to
/// look up. The rule: a shared asset may hold CONFIGURATION, never per-target
/// STATE.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
public class DeathScriptableObject : ScriptableObject
{
    #region Inspector

    [Header("Sink & Despawn")]
    [Tooltip("How long the corpse stays visible before it starts sinking. This " +
             "is the window the player has to see what they killed.")]
    public float SinkAfter = 2f;
    [Tooltip("Metres per second the corpse sinks through the floor.")]
    public float sinkSpeed = 2f;
    [Tooltip("Multiplier on the agent height to decide how far to sink. Needs to " +
             "clear the tallest part of the model, not just the agent capsule.")]
    public float sinkDistanceMultiplier = 2f;

    #endregion

    #region Death Sequence

    /// <summary>
    /// Entry point, called by Enemy.Die().
    ///
    /// Subclasses override this and call base.Die(...) FIRST, then add their
    /// presentation — the base silences the AI before anything else touches the
    /// body, and starts the despawn timer, which only waits.
    ///
    /// Subclasses must NOT call Initializations() themselves; this does it.
    /// </summary>
    public virtual void Die(Enemy enemy, Transform root, ParticleSystem particleSystem)
    {
        if (enemy == null) return;

        Initializations(enemy, root, particleSystem);
        StartDespawn(enemy);
    }

    /// <summary>
    /// Begins the sink-and-pool timer. Separate from Die so a subclass with its
    /// own multi-stage sequence can start the despawn at the right moment
    /// WITHOUT re-running Initializations — calling base.Die() from inside a
    /// coroutine would silence an already-silent AI and restart the death
    /// particle you may have just stopped.
    /// </summary>
    protected void StartDespawn(Enemy enemy)
    {
        if (enemy == null) return;
        enemy.StartCoroutine(SinkAndDespawn(enemy));
    }

    /// <summary>
    /// Shared setup for every death. Idempotent — safe if a subclass's flow
    /// reaches it more than once.
    /// </summary>
    protected virtual void Initializations(Enemy enemy, Transform root, ParticleSystem particleSystem)
    {
        SilenceAI(enemy);
        PlayDeathParticle(particleSystem);
    }

    /// <summary>
    /// Stops the enemy thinking and moving. Deliberately does NOT re-enable
    /// anything later: Enemy.ResetForSpawn asserts the enabled state on the way
    /// back in, so each death config doesn't need its own undo. See
    /// Enemy_Spawn_Contract.md §1.
    /// </summary>
    protected void SilenceAI(Enemy enemy)
    {
        if (enemy.AI_Locomotion != null)
        {
            enemy.AI_Locomotion.ClearStagger();   // kill lock/watchdog/recovery coroutines
            enemy.AI_Locomotion.Stop();

            // Disabled outright so its Update can't fight the death animation or
            // drag the corpse back to the agent's position mid-sink. A stagger
            // recovery firing on a dying enemy used to do exactly that, which is
            // why bodies sometimes never sank and never returned to the pool.
            enemy.AI_Locomotion.enabled = false;
        }

        if (enemy.AI_Combat != null)
            enemy.AI_Combat.StopRoutine();        // FOV polling on a corpse is waste

        if (enemy.behavior != null)
            enemy.behavior.enabled = false;
    }

    protected void PlayDeathParticle(ParticleSystem particleSystem)
    {
        if (particleSystem == null) return;

        particleSystem.gameObject.SetActive(true);
        particleSystem.Play();
    }

    #endregion

    #region Sink & Return To Pool

    /// <summary>
    /// Waits, then sinks the corpse through the floor and deactivates it —
    /// which is what returns it to the pool via PoolableObject.OnDisable.
    ///
    /// Time-based rather than "while the corpse is above targetY". The old
    /// condition-driven loop could never finish if anything else wrote the
    /// transform, and an unfinished loop meant the enemy was never deactivated,
    /// never pooled, and the spawner slowly ran out of enemies. A loop whose
    /// exit depends on a condition something else can undo is not a loop with
    /// an exit.
    /// </summary>
    private IEnumerator SinkAndDespawn(Enemy enemy)
    {
        yield return new WaitForSeconds(SinkAfter);

        float distance = GetSinkDistance(enemy);
        float duration = distance / Mathf.Max(0.01f, sinkSpeed);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (enemy == null) yield break;

            // Space.World — the local-space version sank the corpse along its
            // own down axis, so anything that died tilted or on a slope slid
            // sideways into the floor instead of straight down.
            enemy.transform.Translate(Vector3.down * sinkSpeed * Time.deltaTime, Space.World);
            yield return null;
        }

        if (enemy != null)
            enemy.gameObject.SetActive(false);
    }

    /// <summary>
    /// How far the corpse has to travel to be completely out of sight. Based on
    /// the agent's capsule height because that is the one dimension every enemy
    /// is guaranteed to have configured.
    /// </summary>
    private float GetSinkDistance(Enemy enemy)
    {
        if (enemy.Agent != null)
            return enemy.Agent.height * sinkDistanceMultiplier;

        // Fallback for anything without an agent — scale is a rough proxy.
        return enemy.transform.localScale.y * 3f;
    }

    #endregion
}