using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A thrown grenade shared by the player and grenade-throwing enemies.
/// Flies as a physics object; the fuse starts on FIRST contact and counts down
/// (driving a blink light that speeds up as it nears zero), then explodes:
/// radius damage to every IDamagable on TargetLayer, VFX + decal, returns to pool.
/// </summary>
public class Grenade : MonoBehaviour
{
    public float ExplodeAfter;
    public int Damage;
    public float BlastRadius;
    public PoolableObject BlastVFX;
    public LayerMask TargetLayer;   // was PlayerLayer — who this grenade can damage

    [Header("Blink Light (optional)")]
    [SerializeField] private Light blinkLight;      // child light; leave empty for no blink
    [SerializeField] private float maxBlinkInterval = 0.4f;  // slow blink at fuse start
    [SerializeField] private float minBlinkInterval = 0.04f; // frantic blink near the end

    private Rigidbody rb;
    private bool hasFuseStarted;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        if (BlastVFX != null)
            BlastVFX.GetComponent<AdjustableExplosion>().Adjust(BlastRadius);
    }

    /// <summary>
    /// Throws the grenade with an initial velocity. Called by the player throw
    /// (and usable by enemies). The fuse still starts on first contact.
    /// </summary>
    public void Launch(Vector3 velocity)
    {
        if (rb == null) rb = GetComponent<Rigidbody>();

        hasFuseStarted = false;
        rb.linearVelocity  = velocity;
        rb.angularVelocity = Random.insideUnitSphere * 2f;

        if (blinkLight != null)
            blinkLight.enabled = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Only the FIRST contact starts the fuse — later bounces are ignored,
        // so the countdown (and explosion) can't be triggered twice.
        if (hasFuseStarted) return;
        hasFuseStarted = true;

        StartCoroutine(FuseCountdown());
    }

    private IEnumerator FuseCountdown()
    {
        float remaining = ExplodeAfter;
        float blinkTimer = 0f;
        bool lightOn = false;

        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;

            if (blinkLight != null)
            {
                // Interval shrinks linearly with the remaining fuse fraction:
                // full interval at the start, minimum interval at the end.
                float t = Mathf.Clamp01(remaining / ExplodeAfter);
                float interval = Mathf.Lerp(minBlinkInterval, maxBlinkInterval, t);

                blinkTimer += Time.deltaTime;
                if (blinkTimer >= interval)
                {
                    blinkTimer = 0f;
                    lightOn = !lightOn;
                    blinkLight.enabled = lightOn;
                }
            }

            yield return null;
        }

        Explode();
    }

    private void Explode()
    {
        if (blinkLight != null)
            blinkLight.enabled = false;

        // Fire the pooled VFX + decal at our position.
        if (BlastVFX != null)
        {
            BlastVFX.transform.SetPositionAndRotation(transform.position, transform.rotation);
            BlastVFX.gameObject.SetActive(true);
            BlastVFX.GetComponent<SpawnExplosionDecal>().SpawnDecal();
        }

        // Damage every unique IDamagable in the blast radius (multi-target).
        Collider[] colliders = new Collider[16];
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, BlastRadius, colliders, TargetLayer);

        HashSet<IDamagable> hit = new();
        for (int i = 0; i < count; i++)
        {
            IDamagable target = colliders[i].GetComponentInParent<IDamagable>();
            if (target == null || hit.Contains(target)) continue;
            hit.Add(target);

            target.TakeDamage(Damage);
        }

        gameObject.SetActive(false); // return to pool
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, BlastRadius);
    }
}