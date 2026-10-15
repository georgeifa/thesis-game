using UnityEngine;

/// <summary>
/// Returns a pooled object to its pool a fixed time after it is activated.
///
/// ── WHY THIS EXISTS ─────────────────────────────────────────────────────
/// Every pooled VFX in this project has had the same question hanging over it:
/// *who gives this back?* The answers so far have all been different, and two of
/// them were wrong — the slam shockwave was never returned at all, and the
/// grenade's explosion was returned before it was used.
///
/// A one-shot effect knows how long it lasts and nothing else needs to. Putting
/// the answer on the prefab means the caller acquires and forgets, which is the
/// only version of this that cannot be got wrong by a caller written later.
///
/// ⚠ EXACTLY ONE THING MAY RETURN A POOLED OBJECT. If a prefab carries this
/// component, no caller may also deactivate it — a double return would put the
/// same instance in the available list twice and hand it to two users at once.
/// (ObjectPool.ReturnObjectToPool now refuses duplicates, but relying on that
/// guard means two systems both believe they own the object, which is a
/// different bug wearing the same hat.)
///
/// OnEnable restarts the timer, so this works correctly across reuse — the
/// object is activated again from the pool and the clock starts again.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(PoolableObject))]
[DisallowMultipleComponent]
public class ReturnToPoolAfter : MonoBehaviour
{
    [Tooltip("Seconds the effect is allowed to live once activated. Must be at " +
             "least as long as the visible effect, or it vanishes mid-play.")]
    [SerializeField] private float lifetime = 3f;

    private void OnEnable()
    {
        // Cancel first: the object may have been returned early by something
        // else and re-acquired, and a stale invocation would cut the new life
        // short.
        CancelInvoke(nameof(Despawn));
        Invoke(nameof(Despawn), Mathf.Max(0.01f, lifetime));
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(Despawn));
    }

    /// <summary>
    /// PoolableObject.OnDisable is the actual return path — deactivating is all
    /// this needs to do.
    /// </summary>
    private void Despawn()
    {
        gameObject.SetActive(false);
    }
}