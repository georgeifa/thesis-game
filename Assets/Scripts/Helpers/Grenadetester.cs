using UnityEngine;

/// <summary>
/// TEMPORARY test harness for the Grenade. Put this on an empty GameObject,
/// assign the grenade prefab, the VFX prefab, and a throw origin. Press T to
/// launch a grenade forward+up so you can watch the arc, blink, and explosion.
/// Delete once the real player throw is wired up.
/// </summary>
public class GrenadeTester : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject grenadePrefab;   // prefab with Grenade + Rigidbody + Collider
    [SerializeField] private Transform throwOrigin;      // where it spawns from (defaults to this object)

    [Header("Grenade Config (mirrors SetupGrenade)")]
    [SerializeField] private float blastRadius = 4f;
    [SerializeField] private int   damage      = 120;
    [SerializeField] private float explodeAfter = 2.5f;
    [SerializeField] private LayerMask targetLayer;      // set to EnemyParent for testing on enemies

    [Header("Throw")]
    [SerializeField] private float throwForce = 8f;
    [SerializeField] private float upForce    = 4f;
    [SerializeField] private KeyCode throwKey = KeyCode.T;

    private ObjectPool grenadePool;
    private ObjectPool vfxPool;

    private void Start()
    {
        if (throwOrigin == null) throwOrigin = transform;

        // Build the pools once, same API as Gun.Initialize / SetupGrenade.
        grenadePool = ObjectPool.CreateInstance(grenadePrefab.GetComponent<PoolableObject>(), 10);
    }

    private void Update()
    {
        if (Input.GetKeyDown(throwKey))
            ThrowOne();
    }

    private void ThrowOne()
    {
        PoolableObject obj = grenadePool.GetObject();
        obj.transform.SetPositionAndRotation(throwOrigin.position, throwOrigin.rotation);
        obj.gameObject.SetActive(true);

        Grenade grenade = obj.GetComponent<Grenade>();

        // Configure like SetupGrenade does.
        grenade.BlastRadius  = blastRadius;
        grenade.Damage       = damage;
        grenade.ExplodeAfter = explodeAfter;
        grenade.TargetLayer  = targetLayer;

        // Simple forward+up throw so we can see the arc.
        Vector3 velocity = throwOrigin.forward * throwForce + Vector3.up * upForce;
        grenade.Launch(velocity);

        Debug.Log("Grenade thrown");
    }
}