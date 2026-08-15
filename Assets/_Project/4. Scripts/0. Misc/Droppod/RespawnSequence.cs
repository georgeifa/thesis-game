using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// The materialisation phase of a deployment: the force-field effect around
/// the pod and the character's dissolve-in, both scaled to one duration.
/// </summary>
public class RespawnSequence : MonoBehaviour
{
    // The shader materialises at 1 and is solid at 0. These never change, so
    // they are constants rather than inspector fields.
    private const float Hidden = 1f;
    private const float Solid  = 0f;

    private static readonly int DissolveAmountID = Shader.PropertyToID("_DissolveAmount");

    [Header("Timing")]
    [Tooltip("How long the materialisation takes. The effect and dissolve are both scaled to this.")]
    [SerializeField] private float sequenceDuration = 2.5f;
    [Tooltip("Seconds the effect takes to VISUALLY complete at normal speed. Measure with the Particle Effect panel.")]
    [SerializeField] private float effectAuthoredLength = 5f;

    [Header("References")]
    [Tooltip("Effect prefab instance. Disabled until the sequence runs.")]
    [SerializeField] private GameObject forceFieldEffect;

    private Renderer[] dissolveRenderers;
    private MaterialPropertyBlock propertyBlock;

    /// <summary>Renderers using the materializing shader. Wired at runtime — the player is a scene object.</summary>
    public void SetDissolveRenderers(Renderer[] renderers) => dissolveRenderers = renderers;

    /// <summary>
    /// Runs the materialisation. onStarted fires once the character is hidden
    /// (place and show the soldier there); onComplete fires when it is solid.
    /// Callbacks rather than events — there is exactly one listener, so this
    /// avoids the subscribe/unsubscribe dance.
    /// </summary>
    public void Play(Action onStarted, Action onComplete)
    {
        StartCoroutine(Sequence(onStarted, onComplete));
    }

    private IEnumerator Sequence(Action onStarted, Action onComplete)
    {
        // Hide first, THEN announce, so the soldier is never solid for a frame.
        SetDissolve(Hidden);
        onStarted?.Invoke();

        StartForceField();

        for (float t = 0f; t < sequenceDuration; t += Time.deltaTime)
        {
            SetDissolve(Mathf.Lerp(Hidden, Solid, t / sequenceDuration));
            yield return null;
        }

        SetDissolve(Solid);
        onComplete?.Invoke();
    }

    // Playing at authored/desired speed makes the effect finish exactly when
    // the sequence does, so the countdown ring stays truthful.
    private void StartForceField()
    {
        if (forceFieldEffect == null) return;

        float speed = effectAuthoredLength / sequenceDuration;
        forceFieldEffect.SetActive(true);

        foreach (ParticleSystem ps in forceFieldEffect.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.simulationSpeed = speed;
            ps.Clear();
            ps.Play();
        }
    }

    // Property blocks avoid instantiating a material copy per renderer.
    private void SetDissolve(float value)
    {
        if (dissolveRenderers == null) return;

        propertyBlock ??= new MaterialPropertyBlock();

        foreach (Renderer r in dissolveRenderers)
        {
            if (r == null) continue;

            r.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(DissolveAmountID, value);
            r.SetPropertyBlock(propertyBlock);
        }
    }
}