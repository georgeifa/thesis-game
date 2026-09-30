using System.Collections;
using UnityEngine;

/// <summary>
/// Lobs a grenade at the player using the shared arc solver.
///
/// Unlike the other skills this one holds nothing after it fires: the grenade
/// is launched and lives its own life on its own fuse. That makes the skill
/// safely interruptible without any cleanup — see the note in Execute.
/// </summary>
[CreateAssetMenu(fileName = "Throw Grenade Skill", menuName = "Skills/Combat Skills/Throw Grenade Skill")]
public class ThrowGrenadeSkill : SkillsScriptableObject
{
    #region Inspector

    [Header("Additional Range Properties")]
    [Tooltip("Closer than this and the enemy would blast itself — it picks something else instead.")]
    public float minRange;

    [Header("Grenade Properties")]
    public PoolableObject grenadePrefab;
    [Tooltip("Fuse length once launched.")]
    public float grenadeExplodeAfter;
    public ExplosionScriptableObject ExplosionVFX;
    [Tooltip("Who the explosion damages. The enemy's own layer is excluded here, " +
             "which is why enemies don't kill each other with grenades.")]
    public LayerMask playerLayer;

    [Header("Throw Properties")]
    [Tooltip("Arc height bounds, passed to Helpers.CalculateArcVelocity. The solver " +
             "scales the arc with distance between these two.")]
    public float minArcHeight;
    public float maxArcHeight;
    [Tooltip("Delay before the grenade leaves the hand, matching the throw animation.")]
    public float ThrowDelay = .1f;

    [Header("Pooling")]
    public int PoolSize = 5;

    #endregion

    #region Pooling

    // Both pools were previously created inside the coroutine — the grenade
    // pool per throw, and the explosion-VFX pool per throw as well, inside
    // SetupGrenade. Two fresh pools and ten new objects every single grenade.
    [System.NonSerialized] private ObjectPool grenadePool;
    [System.NonSerialized] private ObjectPool explosionPool;
    [System.NonSerialized] private int poolSession = -1;

    private static int session;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void NewSession() => session++;

    private void EnsurePools()
    {
        if (poolSession == session && grenadePool != null && explosionPool != null) return;

        grenadePool = grenadePrefab != null
            ? ObjectPool.CreateInstance(grenadePrefab, PoolSize) : null;

        explosionPool = ExplosionVFX != null && ExplosionVFX.explosionPrefab != null
            ? ObjectPool.CreateInstance(ExplosionVFX.explosionPrefab, PoolSize) : null;

        poolSession = session;
    }

    #endregion

    #region Conditions

    public override bool CanUseSkill(Enemy enemy, GameObject player)
    {
        float distance = Vector3.Distance(enemy.transform.position, player.transform.position);

        // Inside minRange the enemy would catch its own blast.
        return base.CanUseSkill(enemy, player) && distance >= minRange;
    }

    #endregion

    #region Execution

    public override IEnumerator Execute(Enemy enemy, GameObject player)
    {
        EnsurePools();

        // Interrupting during this delay stops the throw entirely and nothing
        // has been taken from the pool yet. After the delay, everything below
        // runs in a single frame with no yield in it — so there is no moment
        // where a grenade exists but has not been launched. That is why this
        // skill can stay Interruptible = true with an empty Interrupt().
        yield return new WaitForSeconds(ThrowDelay);

        Transform origin = enemy.AI_Combat.SkillsVFX != null
            ? enemy.AI_Combat.SkillsVFX.transform
            : enemy.transform;

        Vector3 throwPoint = origin.position;

        if (grenadePool == null)
        {
            Debug.LogWarning($"{name}: no grenade prefab assigned.");
            yield break;
        }

        PoolableObject instance = grenadePool.GetObject();
        if (instance == null)
        {
            // Previously this was unchecked and threw a NullReferenceException
            // the moment the pool ran dry mid-fight.
            Debug.LogWarning($"{name}: grenade pool exhausted — increase PoolSize.");
            yield break;
        }

        instance.transform.SetPositionAndRotation(throwPoint, Quaternion.identity);

        Grenade grenade = instance.GetComponent<Grenade>();
        if (grenade == null)
        {
            Debug.LogWarning($"{name}: grenade prefab has no Grenade component.");
            instance.gameObject.SetActive(false);
            yield break;
        }

        ConfigureGrenade(grenade);

        Vector3 velocity = Helpers.CalculateArcVelocity(
            throwPoint, player.transform.position, minArcHeight, maxArcHeight);

        // Launch() also clears hasFuseStarted — a pooled grenade would
        // otherwise detonate on its first frame after reuse.
        grenade.Launch(velocity);

        // NOTE: the grenade is deliberately NOT registered via
        // SetActiveSkillInstance. It is independent once launched, and
        // registering it would let a later interrupt deactivate a live grenade
        // in mid-air.
    }

    // Interrupt() is inherited and finds nothing registered, so it is a no-op.

    #endregion

    #region Helpers

    private void ConfigureGrenade(Grenade grenade)
    {
        grenade.BlastRadius  = Range;
        grenade.Damage       = Damage;
        grenade.ExplodeAfter = grenadeExplodeAfter;
        grenade.TargetLayer  = playerLayer;

        if (explosionPool == null) return;

        PoolableObject blastVFX = explosionPool.GetObject();
        if (blastVFX == null)
        {
            Debug.LogWarning($"{name}: explosion VFX pool exhausted — the grenade will " +
                             "detonate with no visual.");
            return;
        }

        blastVFX.gameObject.SetActive(false);
        ExplosionVFX.SetupExplosion(blastVFX.gameObject);
        grenade.BlastVFX = blastVFX;
    }

    #endregion

    // ⚠ ONE THING I COULD NOT VERIFY: the BlastVFX handed to the grenade is
    //   pulled from the pool here but returned by Grenade itself after the
    //   explosion. If Grenade doesn't deactivate it, the explosion pool drains
    //   the same way the shockwave pool did in SlamAttackSkill. Worth checking
    //   Grenade.Explode for a SetActive(false) on BlastVFX.
}