using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A per-prefab pool of reusable GameObjects.
///
/// ── THE STATIC REGISTRY AND ITS LIFETIME ────────────────────────────────
/// Pools are shared by prefab through a static dictionary, so every caller
/// asking for the same prefab gets the same pool rather than building its own.
/// That is what stops pools multiplying — a bug this project has already had in
/// four separate places.
///
/// ⚠ But `static` means the registry survives things the pool GameObjects do
/// not. A scene load destroys every pooled object and every pool Parent, and
/// leaves the dictionary pointing at all of them. The next GetObject then
/// touches a destroyed transform and throws.
///
/// A SCENE LOAD IS A LIFE BOUNDARY, exactly like an enemy respawn — and the
/// same rule applies: a life boundary establishes what things SHOULD BE. Two
/// mechanisms enforce it here, deliberately overlapping:
///
///   1. OWNERSHIP — the registry is cleared when a scene unloads.
///   2. VALIDATION — CreateInstance checks the pool it is about to hand back
///      and rebuilds it if its Parent has been destroyed.
///
/// (1) alone would be enough if event ordering were guaranteed. (2) makes the
/// failure impossible rather than merely handled, and costs one null check per
/// pool acquisition. Given that this bug class has cost this project more time
/// than any other, that is a good trade.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
public class ObjectPool
{
    #region Instance state

    private GameObject Parent;
    private PoolableObject Prefab;
    private int Size;
    private List<PoolableObject> AvailableObjectsPool;

    /// <summary>True once this pool's objects have been destroyed out from under it.</summary>
    private bool IsStale => Parent == null;

    #endregion

    #region Static registry

    private static readonly Dictionary<PoolableObject, ObjectPool> ObjectPools =
        new Dictionary<PoolableObject, ObjectPool>();

    /// <summary>
    /// Incremented every time pooled objects become invalid — on play-mode start
    /// and on every scene unload.
    ///
    /// Anything that CACHES an ObjectPool reference in a field (skill and death
    /// ScriptableObjects, which are assets and therefore outlive the scene)
    /// should store the Session it cached at and re-acquire when this differs.
    /// One counter in one place, rather than a RuntimeInitializeOnLoadMethod in
    /// every class that caches.
    /// </summary>
    public static int Session { get; private set; }

    /// <summary>
    /// Runs once when play mode starts — including when "Reload Domain" is
    /// disabled in Enter Play Mode Options, which is the case where statics
    /// carry over from the previous session.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void InitialiseStaticState()
    {
        InvalidateAll();

        // Unsubscribe first: with domain reload disabled this method runs again
        // on a static that still holds the previous session's subscription, and
        // += alone would accumulate one handler per play session. Same shape as
        // the FieldOfView PerformFOVCheck event leak.
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static void OnSceneUnloaded(Scene scene) => InvalidateAll();

    private static void InvalidateAll()
    {
        ObjectPools.Clear();
        Session++;
    }

    #endregion

    #region Construction

    private ObjectPool(PoolableObject Prefab, int Size)
    {
        this.Prefab = Prefab;
        this.Size = Size;
        AvailableObjectsPool = new List<PoolableObject>(Size);
    }

    /// <summary>
    /// Returns the shared pool for this prefab, building it if it does not exist
    /// or if the one on record has been destroyed.
    ///
    /// Cheap enough to call every time you need the pool — it is a dictionary
    /// lookup plus a null check. Callers do not need to cache the result, and a
    /// caller that does cache it must also check <see cref="Session"/>.
    /// </summary>
    public static ObjectPool CreateInstance(PoolableObject Prefab, int Size)
    {
        if (Prefab == null)
        {
            Debug.LogError("ObjectPool.CreateInstance called with a null prefab.");
            return null;
        }

        if (ObjectPools.TryGetValue(Prefab, out ObjectPool existing))
        {
            // The guard that makes the scene-reload failure impossible rather
            // than merely unlikely. A stale pool is dropped and rebuilt here,
            // silently and correctly, whatever cleared or failed to clear the
            // registry.
            if (!existing.IsStale) return existing;

            ObjectPools.Remove(Prefab);
        }

        ObjectPool pool = new ObjectPool(Prefab, Size);

        pool.Parent = new GameObject($"{Prefab.name} Pool");
        pool.CreateObjects();

        ObjectPools.Add(Prefab, pool);

        return pool;
    }

    private void CreateObjects()
    {
        for (int i = 0; i < Size; i++)
        {
            CreateObject();
        }
    }

    private void CreateObject()
    {
        PoolableObject poolableObject = GameObject.Instantiate(
            Prefab, Vector3.zero, Quaternion.identity, Parent.transform);

        poolableObject.Parent = this;

        // PoolableObject.OnDisable is what adds it to AvailableObjectsPool.
        poolableObject.gameObject.SetActive(false);
    }

    #endregion

    #region Acquire / release

    public PoolableObject GetObject(Vector3 Position, Quaternion Rotation)
    {
        // Defensive, and the line that actually threw before: an entry can be
        // destroyed by something outside the pool's control. Dropping the
        // corpses turns a hard exception into a pool that quietly grows by one.
        while (AvailableObjectsPool.Count > 0 && AvailableObjectsPool[0] == null)
        {
            AvailableObjectsPool.RemoveAt(0);
        }

        if (AvailableObjectsPool.Count == 0) // auto-expand if out of objects
        {
            if (IsStale)
            {
                Debug.LogError($"ObjectPool for {Prefab} is stale — its objects were " +
                               "destroyed. Re-acquire the pool via CreateInstance.");
                return null;
            }

            CreateObject();
        }

        PoolableObject instance = AvailableObjectsPool[0];
        AvailableObjectsPool.RemoveAt(0);

        instance.transform.position = Position;
        instance.transform.rotation = Rotation;

        instance.gameObject.SetActive(true);

        return instance;
    }

    public PoolableObject GetObject()
    {
        return GetObject(Vector3.zero, Quaternion.identity);
    }

    public void ResetParent(PoolableObject Object)
    {
        if (Object == null || Parent == null) return;

        Object.transform.SetParent(Parent.transform);
    }

    public void ReturnObjectToPool(PoolableObject Object)
    {
        if (Object == null) return;

        // Refuse a double return. Without this, an object deactivated twice —
        // once by whatever owns it and once by something that thought it did —
        // sits in the available list twice and is handed to two callers at once.
        // The second caller's position write then teleports the first caller's
        // effect, which reads as "the VFX sometimes appears in the wrong place".
        //
        // Contains() is O(n) over a list of PoolSize, so a handful of
        // comparisons. Cheap enough that correctness wins.
        if (AvailableObjectsPool.Contains(Object)) return;

        AvailableObjectsPool.Add(Object);
    }

    #endregion
}