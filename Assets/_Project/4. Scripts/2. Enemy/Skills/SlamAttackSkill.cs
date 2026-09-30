using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A ground slam: a wind-up, then an instantaneous damage sphere around the
/// enemy with a shockwave VFX scaled to match its radius.
///
/// The wind-up is the point of the attack — it is the player's window to move
/// out of range, and (now that skills are interruptible) to stagger the enemy
/// out of the slam entirely.
/// </summary>
[CreateAssetMenu(fileName = "Slam Attack Skill", menuName = "Skills/Combat Skills/Slam Attack Skill")]
public class SlamAttackSkill : SkillsScriptableObject
{
    #region Inspector

    [Header("Slam")]
    [Tooltip("Seconds between the animation starting and the shockwave landing. " +
             "Nothing is spawned during this window, so interrupting here is free — " +
             "which is exactly what makes the slam dodgeable and punishable.")]
    public float WindUpTime = 1f;
    [Tooltip("Who the slam can hit. The damage radius itself is the inherited Range field.")]
    public LayerMask PlayerLayerMask;

    [Header("VFX")]
    [Tooltip("Shockwave effect. Scaled uniformly to Range so the visual matches the hitbox.")]
    public PoolableObject VFX_Prefab;
    [Tooltip("Offset from the enemy's origin, in the enemy's local space.")]
    public Vector3 SpawnOffset;
    [Tooltip("How long the shockwave lives before it is returned to the pool.")]
    public float VFXLifetime = 2f;

    [Header("Pooling")]
    [Tooltip("Instances in the shockwave pool. Shared by every enemy using this asset.")]
    public int PoolSize = 5;

    #endregion

    #region Pooling

    // Created once and reused. The previous version called CreateInstance on
    // every slam, which built a fresh pool GameObject plus PoolSize instances
    // each time and never reclaimed the old ones.
    [System.NonSerialized] private ObjectPool vfxPool;
    [System.NonSerialized] private int poolSession = -1;

    // A pool owns scene objects, which die with play mode. ScriptableObject
    // fields are NOT reset when "Reload Domain" is off in Enter Play Mode
    // Options, so a cached pool can survive into the next session pointing at
    // destroyed objects. Bumping a counter before each run self-invalidates it.
    private static int session;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void NewSession() => session++;

    private void EnsurePool()
    {
        if (poolSession == session && vfxPool != null) return;

        vfxPool = VFX_Prefab != null ? ObjectPool.CreateInstance(VFX_Prefab, PoolSize) : null;
        poolSession = session;
    }

    #endregion

    #region Reusable buffers

    // The overlap runs synchronously inside one block with no yield in the
    // middle, so two enemies slamming on the same frame can never interleave
    // and a single shared buffer is safe. Same reasoning as FieldOfViewCheck.
    private readonly Collider[] overlapBuffer = new Collider[16];
    private readonly HashSet<IDamagable> alreadyHit = new();

    #endregion

    #region Execution

    public override IEnumerator Execute(Enemy enemy, GameObject player)
    {
        EnsurePool();

        // ── Wind-up ──────────────────────────────────────────────────────
        yield return new WaitForSeconds(WindUpTime);

        // ── Impact ───────────────────────────────────────────────────────
        PoolableObject shockwave = SpawnShockwave(enemy);
        if (shockwave != null)
            enemy.SetActiveSkillInstance(shockwave, vfxPool);

        ApplyDamage(enemy);

        // ── Let it play out, then give it back ───────────────────────────
        // The previous version never returned the shockwave to the pool at
        // all: it was parented to the enemy and left there. After PoolSize
        // slams GetObject() returned null and every later slam was invisible.
        yield return new WaitForSeconds(VFXLifetime);

        enemy.ReleaseActiveSkillInstance();
    }

    // Interrupt() is inherited — the base releases the shockwave, which is all
    // this skill holds. Interrupted during the wind-up there is nothing to
    // release and the call is a no-op.

    #endregion

    #region Helpers

    private PoolableObject SpawnShockwave(Enemy enemy)
    {
        if (vfxPool == null) return null;

        PoolableObject instance = vfxPool.GetObject();
        if (instance == null)
        {
            Debug.LogWarning($"{name}: shockwave pool exhausted — increase PoolSize.");
            return null;
        }

        instance.transform.SetParent(enemy.transform, false);
        instance.transform.localPosition += SpawnOffset;

        // Uniform scale to Range so the effect reads as the actual hitbox
        // rather than decoration that happens to be nearby.
        instance.transform.localScale = Vector3.one * Range;
        instance.gameObject.SetActive(true);

        return instance;
    }

    /// <summary>
    /// One damage event per target in the sphere, applied the instant the
    /// shockwave lands. StaggerForce is passed through, so a slam can stagger
    /// the player the same way the player's grenade can stagger the Giant.
    /// </summary>
    private void ApplyDamage(Enemy enemy)
    {
        alreadyHit.Clear();

        int count = Physics.OverlapSphereNonAlloc(
            enemy.transform.position, Range, overlapBuffer, PlayerLayerMask);

        for (int i = 0; i < count; i++)
        {
            // GetComponentInParent checks the collider's own object first, so
            // this works whether IDamagable sits on the collider or above it.
            IDamagable target = overlapBuffer[i].GetComponentInParent<IDamagable>();

            // A target caught by two colliders (torso + arm) is damaged once.
            if (target == null || !alreadyHit.Add(target)) continue;

            target.TakeDamage(Damage, enemy.transform.position, StaggerForce);
        }
    }

    #endregion

    // ⚠ COMPLETION: this skill does not call OnSkillComplete() — it relies on
    //   an animation event on the slam clip calling AI_Combat.OnSkillComplete().
    //   That was true of the original too. If the Giant's clip has no such
    //   event, isUsingSkill leaks and he never attacks again. Worth verifying
    //   once, since the symptom is silent.
}