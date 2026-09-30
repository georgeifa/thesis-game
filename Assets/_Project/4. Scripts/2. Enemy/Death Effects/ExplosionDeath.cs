using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The kamikaze's death: a short fuse, a blast that damages everything nearby,
/// and the bot is gone — consumed by its own explosion rather than leaving a
/// body.
///
/// ── TWO DESIGN DECISIONS ────────────────────────────────────────────────
///
/// 1. THE BLAST LIVES IN THE DEATH CONFIG, NOT IN THE DieOnApproach SKILL.
///    So the spiderbot explodes identically whether it detonates on purpose or
///    is shot down on approach. One behaviour to tune instead of two — and
///    being killed just before contact still costs the player, which is what
///    makes the kamikaze worth reacting to early rather than ignoring.
///
/// 2. NO CORPSE.
///    A machine that detonates shouldn't leave an intact chassis lying there.
///    It also removes the whole ragdoll path for this enemy: no bone arrays, no
///    freeze timer, no sink. The body is hidden at the moment of detonation and
///    returned to the pool once the VFX has played out.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Explosion Death", menuName = "Deaths/Explosion Death")]
public class ExplosionDeath : DeathScriptableObject
{
    #region Inspector

    [Header("Explosion")]
    [Tooltip("Damage dealt to everything in the blast.")]
    public int Damage = 40;
    [Tooltip("Blast radius.")]
    public float Radius = 3f;
    [Tooltip("Fuse: seconds between death and detonation. This is the player's " +
             "window to get clear, so it has to be long enough to react to. The " +
             "enemy's death particle plays for the whole fuse as the telegraph.")]
    public float Delay = 1f;
    [Tooltip("Stagger dealt by the blast, independent of damage.")]
    public float StaggerForce = 40f;
    [Tooltip("What the blast can damage. Include the player and, if you want " +
             "chain reactions, other enemies.")]
    public LayerMask DamagableLayermask = -1;

    [Header("VFX")]
    public PoolableObject ExplosionPrefab;
    [Tooltip("How long the explosion effect lives before returning to its pool.")]
    public float VFXLifetime = 3f;
    [Tooltip("Instances in the explosion pool. Needs to cover the most " +
             "simultaneous detonations you expect.")]
    public int PoolSize = 5;

    #endregion

    #region Pooling

    // Created once and reused. The previous version called CreateInstance on
    // every detonation — a new pool GameObject plus PoolSize instances each
    // time, none reclaimed — AND never returned the instance it took, so after
    // PoolSize explosions there was no VFX at all.
    [System.NonSerialized] private ObjectPool explosionPool;
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
        if (poolSession == session && explosionPool != null) return;

        explosionPool = ExplosionPrefab != null
            ? ObjectPool.CreateInstance(ExplosionPrefab, PoolSize)
            : null;

        poolSession = session;
    }

    #endregion

    #region Reusable buffer

    // The overlap is synchronous and consumed before the next yield, so a
    // shared buffer is safe even with several simultaneous blasts.
    [System.NonSerialized] private Collider[] blastBuffer;

    #endregion

    #region Death Sequence

    public override void Die(Enemy enemy, Transform root, ParticleSystem particleSystem)
    {
        if (enemy == null) return;

        // Silence IMMEDIATELY, and deliberately do NOT call base.Die — the
        // sink-and-despawn flow doesn't apply here, because there is no corpse
        // to sink. The fuse coroutine owns the despawn instead.
        //
        // Silencing now matters: the fuse is a full second, and a still-thinking
        // enemy would keep pathing toward the player after it was already dead.
        Initializations(enemy, root, particleSystem);

        enemy.StartCoroutine(FuseThenDetonate(enemy));
    }

    private IEnumerator FuseThenDetonate(Enemy enemy)
    {
        // ── Fuse ─────────────────────────────────────────────────────────
        // The enemy's own death particle (started by Initializations) plays
        // through this window. That's the telegraph — without a visible tell,
        // the blast is a punishment rather than something to react to.
        yield return new WaitForSeconds(Delay);

        if (enemy == null) yield break;

        // ── Detonation ───────────────────────────────────────────────────
        PoolableObject vfx = SpawnExplosion(enemy.transform.position);
        ApplyBlastDamage(enemy.transform.position);

        // The bot is consumed by its own blast. Hiding rather than deactivating,
        // because this coroutine is hosted on the enemy — deactivating here
        // would kill the coroutine before it could return the VFX to its pool.
        enemy.SetVisible(false);

        // ── Cleanup ──────────────────────────────────────────────────────
        yield return new WaitForSeconds(VFXLifetime);

        if (vfx != null) vfx.gameObject.SetActive(false);   // back to its pool

        if (enemy != null) enemy.gameObject.SetActive(false);   // back to its pool
    }

    #endregion

    #region Blast

    private PoolableObject SpawnExplosion(Vector3 origin)
    {
        EnsurePool();
        if (explosionPool == null) return null;

        PoolableObject instance = explosionPool.GetObject();
        if (instance == null)
        {
            Debug.LogWarning($"{name}: explosion pool exhausted — increase PoolSize.");
            return null;
        }

        instance.transform.position = origin;
        instance.gameObject.SetActive(true);

        if (instance.TryGetComponent(out ParticleSystem particle))
            particle.Play();

        return instance;
    }

    private void ApplyBlastDamage(Vector3 origin)
    {
        blastBuffer ??= new Collider[32];

        int count = Physics.OverlapSphereNonAlloc(
            origin, Radius, blastBuffer, DamagableLayermask);

        // A target caught by several of its own colliders takes the blast once.
        HashSet<IDamagable> alreadyHit = new();

        for (int i = 0; i < count; i++)
        {
            // GetComponentInParent checks the collider's own object first, so
            // this works whether IDamagable sits on the collider or above it.
            IDamagable target = blastBuffer[i].GetComponentInParent<IDamagable>();
            if (target == null || !alreadyHit.Add(target)) continue;

            target.TakeDamage(Damage, origin, StaggerForce);
        }
    }

    #endregion

    // The old `if (enemy.AI_Combat.isUsingSkill) OnSkillComplete();` is gone.
    // It existed because DieOnApproach is non-interruptible, so InterruptAction
    // leaves isUsingSkill set when the kamikaze kills itself. Enemy.OnDisable →
    // AI_Combat.ResetCombatState() now clears it as part of the spawn contract,
    // which covers every route to death rather than this one.
}