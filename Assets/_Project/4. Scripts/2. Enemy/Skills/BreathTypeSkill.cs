using System.Collections;
using UnityEngine;

/// <summary>
/// A sustained breath/flame attack: a wind-up VFX while the enemy tracks the
/// player, then a damaging particle effect for a fixed duration.
///
/// Both VFX are pooled objects parented to the enemy's SkillsVFX. Each is
/// registered on the ENEMY as it is acquired, so a stagger that stops this
/// coroutine mid-way can still release them — the SO is shared across every
/// enemy using this skill and cannot hold per-activation state.
/// </summary>
[CreateAssetMenu(fileName = "Breath Type Skill", menuName = "Skills/Combat Skills/Breath Type Skill")]
public class BreathTypeSkill : SkillsScriptableObject
{
    #region Inspector

    [Header("Breath")]
    [Tooltip("How long the damaging effect lasts.")]
    public float Duration = 3f;
    [Tooltip("Seconds between damage ticks, passed to the AreaDamage on the VFX.")]
    public float TickRate = .5f;
    [Tooltip("The damaging particle effect. Needs an AreaDamage in its children.")]
    public PoolableObject Prefab;

    [Header("Wind-up")]
    [Tooltip("Charge time before the breath starts. This is the player's window to interrupt.")]
    public float WindUpTime = 1.5f;
    [Tooltip("Charge-up particle effect.")]
    public PoolableObject windUpPrefab;

    [Header("Timing")]
    [Tooltip("Delay before the first VFX, so the animation has started. Nothing is " +
             "allocated during this window, so interrupting here is free.")]
    public float AnimationLeadIn = .5f;

    [Header("Tracking")]
    [Tooltip("Face the player during the wind-up.")]
    public bool TrackDuringWindUp = true;
    [Tooltip("Keep facing the player while breathing. Off means the player can " +
             "walk out of the cone — that is what makes the attack dodgeable.")]
    public bool TrackDuringBreath = false;

    [Header("Pooling")]
    [Tooltip("Instances per pool. Shared across every enemy using this skill asset.")]
    public int PoolSize = 5;

    #endregion

    #region Pools

    // Created once and reused. The previous version called
    // ObjectPool.CreateInstance on EVERY activation, which builds a fresh pool
    // GameObject and PoolSize new instances each time and never reclaims the
    // old ones — the same leak as §10.2 in the completion plan.
    [System.NonSerialized] private ObjectPool windUpPool;
    [System.NonSerialized] private ObjectPool breathPool;
    [System.NonSerialized] private int poolSession = -1;

    // A pool owns scene GameObjects, which are destroyed when play mode ends.
    // ScriptableObject fields are NOT reset when "Reload Domain" is disabled in
    // Enter Play Mode Options, so a cached pool can survive into the next play
    // session pointing at destroyed objects. Bumping a session counter before
    // each run makes the cache self-invalidating either way.
    private static int session;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void NewSession() => session++;

    private void EnsurePools()
    {
        if (poolSession == session && windUpPool != null && breathPool != null) return;

        windUpPool = windUpPrefab != null ? ObjectPool.CreateInstance(windUpPrefab, PoolSize) : null;
        breathPool = Prefab != null ? ObjectPool.CreateInstance(Prefab, PoolSize) : null;
        poolSession = session;
    }

    #endregion

    #region Execution

    public override IEnumerator Execute(Enemy enemy, GameObject player)
    {
        EnsurePools();

        Transform vfxParent = enemy.AI_Combat.SkillsVFX != null
            ? enemy.AI_Combat.SkillsVFX.transform
            : enemy.transform;

        // Nothing acquired yet — an interrupt during the lead-in costs nothing.
        yield return new WaitForSeconds(AnimationLeadIn);

        // ── Wind-up ──────────────────────────────────────────────────────
        PoolableObject windUp = Spawn(windUpPool, vfxParent);
        if (windUp != null)
            enemy.SetActiveSkillInstance(windUp, windUpPool);

        for (float time = 0f; time < WindUpTime; time += Time.deltaTime)
        {
            if (TrackDuringWindUp) FacePlayer(enemy, player);
            yield return null;
        }

        // ── Breath ───────────────────────────────────────────────────────
        PoolableObject breath = Spawn(breathPool, vfxParent);

        // Registering the breath releases the wind-up automatically, so the
        // two can never both be live and only one handle is ever tracked.
        if (breath != null)
        {
            if (breath.TryGetComponent(out AreaDamage areaDamage)
                || (areaDamage = breath.GetComponentInChildren<AreaDamage>()) != null)
            {
                areaDamage.Damage = Damage;
                areaDamage.TickRate = TickRate;
            }
            else
            {
                Debug.LogWarning($"{name}: breath prefab has no AreaDamage — it will deal no damage.");
            }

            enemy.SetActiveSkillInstance(breath, breathPool);
        }
        else
        {
            // Pool exhausted: the wind-up is still registered and would hang
            // around for the whole duration otherwise.
            enemy.ReleaseActiveSkillInstance();
        }

        for (float time = 0f; time < Duration; time += Time.deltaTime)
        {
            if (TrackDuringBreath) FacePlayer(enemy, player);
            yield return null;
        }

        // ── Normal completion ────────────────────────────────────────────
        // The same teardown an interrupt would run, so there is exactly one
        // code path that returns a pooled VFX and it is exercised every cast.
        enemy.ReleaseActiveSkillInstance();

        enemy.AI_Combat.OnSkillComplete();
    }

    /// <summary>
    /// Interrupt() is inherited: the base implementation calls
    /// enemy.ReleaseActiveSkillInstance(), which is all this skill needs
    /// whether it was cut during the wind-up, during the breath, or before
    /// either VFX existed.
    /// </summary>

    #endregion

    #region Helpers

    private PoolableObject Spawn(ObjectPool pool, Transform parent)
    {
        if (pool == null) return null;

        PoolableObject instance = pool.GetObject();
        if (instance == null) return null;

        instance.transform.SetParent(parent, false);
        instance.gameObject.SetActive(true);

        if (instance.TryGetComponent(out ParticleSystem particle))
            particle.Play();

        return instance;
    }

    /// <summary>
    /// Turns to face the player on the horizontal plane only. A plain LookAt
    /// at the player's position pitches the whole enemy when the two pivots
    /// sit at different heights.
    /// </summary>
    private void FacePlayer(Enemy enemy, GameObject player)
    {
        if (player == null) return;

        Vector3 direction = player.transform.position - enemy.transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;

        enemy.transform.rotation = Quaternion.LookRotation(direction);
    }

    #endregion
}