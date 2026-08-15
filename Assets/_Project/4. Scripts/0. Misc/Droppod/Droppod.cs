using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// The reinforcement pod. Falls to the spawn point, kills what it lands on,
/// opens, then runs the materialisation. Purely presentational — it reports
/// the two beats the GameManager cares about and does no deploying itself.
/// </summary>
[RequireComponent(typeof(Animator))]
public class DropPod : MonoBehaviour
{
    [Header("Descent")]
    [SerializeField] private float dropHeight = 60f;
    [SerializeField] private float dropSpeed = 90f;
    [Tooltip("Random tilt during descent, degrees.")]
    [SerializeField] private float descentTilt = 5f;

    [Header("Impact")]
    [SerializeField] private int impactDamage = 500;
    [SerializeField] private float impactRadius = 2.5f;
    [SerializeField] private LayerMask impactTargetLayer;
    [SerializeField] private float impactShakeMagnitude = 0.5f;
    [Tooltip("Pause after landing before the doors open.")]
    [SerializeField] private float openDelay = 0.4f;

    [Header("References")]
    [SerializeField] private GameObject descentTrail;
    [SerializeField] private ParticleSystem impactDust;
    [SerializeField] private ParticleSystem impactSparks;
    [SerializeField] private string openTrigger = "Open";

    private Animator podAnimator;
    private CinemachineImpulseSource impulseSource;
    private RespawnSequence respawnSequence;

    private Vector3 landingPoint;
    private bool doorsOpened;

    private void Awake()
    {
        podAnimator = GetComponent<Animator>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
        respawnSequence = GetComponent<RespawnSequence>();

    }

    /// <summary>Renderers the materialisation dissolves in. Set before Drop().</summary>
    public void SetDissolveTargets(Renderer[] renderers)
    {
        if (respawnSequence != null)
            respawnSequence.SetDissolveRenderers(renderers);
    }

    /// <summary>
    /// Drops the pod. onSoldierPlaced fires when the soldier should appear
    /// (start of materialisation); onSoldierReleased when they can be played.
    /// </summary>
    public void Drop(Vector3 landing, Action onSoldierPlaced, Action onSoldierReleased)
    {
        landingPoint = landing;
        transform.position = landing + Vector3.up * dropHeight;

        // Random yaw and tilt so repeated drops don't look identical.
        transform.rotation = Quaternion.Euler(
            UnityEngine.Random.Range(-descentTilt, descentTilt),
            UnityEngine.Random.Range(0f, 360f),
            UnityEngine.Random.Range(-descentTilt, descentTilt));

        StartCoroutine(DropSequence(onSoldierPlaced, onSoldierReleased));
    }

    private IEnumerator DropSequence(Action onPlaced, Action onReleased)
    {
        // ── Descent ──
        if (descentTrail != null) descentTrail.SetActive(true);

        while (transform.position.y > landingPoint.y)
        {
            transform.position += Vector3.down * dropSpeed * Time.deltaTime;
            yield return null;
        }

        // ── Impact ──
        transform.position = landingPoint;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        if (descentTrail != null) descentTrail.SetActive(false);
        if (impactDust != null) impactDust.Play();
        if (impactSparks != null) impactSparks.Play();
        if (impulseSource != null)
            impulseSource.GenerateImpulseWithVelocity(Vector3.down * impactShakeMagnitude);

        ApplyImpactDamage();

        yield return new WaitForSeconds(openDelay);

        // ── Doors ──
        podAnimator.SetTrigger(openTrigger);

        // The animation event calls SetDoorsToOpen. Time-limited so a missing
        // event degrades the look rather than soft-locking the run.
        float timeout = Time.time + 5f;
        yield return new WaitUntil(() => doorsOpened || Time.time > timeout);

        if (!doorsOpened)
            Debug.LogWarning("DropPod: doors-open event never fired — continuing anyway.");

        // ── Materialise ──
        if (respawnSequence != null)
        {
            respawnSequence.Play(onPlaced, onReleased);
        }
        else
        {
            onPlaced?.Invoke();
            onReleased?.Invoke();
        }
    }

    /// <summary>Animation event on the door-opening clip.</summary>
    public void SetDoorsToOpen() => doorsOpened = true;

    // Anything caught under the pod dies. Same overlap pattern as the grenade.
    private void ApplyImpactDamage()
    {
        if (impactDamage <= 0) return;

        Collider[] hits = Physics.OverlapSphere(landingPoint, impactRadius, impactTargetLayer);
        var alreadyHit = new HashSet<IDamagable>();

        foreach (Collider col in hits)
        {
            IDamagable target = col.GetComponentInParent<IDamagable>();
            if (target == null || !alreadyHit.Add(target)) continue;

            target.GetHitDirection(landingPoint);
            target.TakeDamage(impactDamage);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, impactRadius);
    }
}