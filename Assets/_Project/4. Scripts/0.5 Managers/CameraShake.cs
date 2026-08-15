using UnityEngine;

/// <summary>
/// Positional camera shake. Put this on the camera (or a parent of it) and
/// call Shake() from anything that should hit hard — pod impacts, explosions,
/// heavy weapons.
///
/// Works on a local offset that decays to zero, so it doesn't fight whatever
/// is driving the camera's actual position (follow scripts, etc.) as long as
/// this sits on a child of the thing being moved.
/// </summary>
public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    [Header("Defaults")]
    [SerializeField] private float defaultDuration = 0.3f;
    [SerializeField] private float defaultMagnitude = 0.3f;
    [Tooltip("Higher = faster, more jittery shake.")]
    [SerializeField] private float frequency = 25f;

    private Vector3 baseLocalPosition;
    private float shakeTimeRemaining;
    private float shakeDuration;
    private float shakeMagnitude;

    // Random offsets so X and Y don't move in lockstep.
    private float seedX;
    private float seedY;

    private void Awake()
    {
        Instance = this;
        baseLocalPosition = transform.localPosition;

        seedX = Random.value * 100f;
        seedY = Random.value * 100f;
    }

    private void LateUpdate()
    {
        if (shakeTimeRemaining <= 0f)
        {
            transform.localPosition = baseLocalPosition;
            return;
        }

        shakeTimeRemaining -= Time.deltaTime;

        // Fade out over the shake's life so it settles instead of stopping dead.
        float strength = shakeMagnitude * (shakeTimeRemaining / shakeDuration);

        // Perlin noise gives smooth, non-repeating motion — random values per
        // frame would look like static.
        float t = Time.time * frequency;
        float offsetX = (Mathf.PerlinNoise(seedX, t) - 0.5f) * 2f;
        float offsetY = (Mathf.PerlinNoise(seedY, t) - 0.5f) * 2f;

        transform.localPosition = baseLocalPosition + new Vector3(offsetX, offsetY, 0f) * strength;
    }

    /// <summary>Shake with the inspector defaults.</summary>
    public void Shake() => Shake(defaultDuration, defaultMagnitude);

    /// <summary>
    /// Shake for a duration at a magnitude. A stronger shake overrides a weaker
    /// one already in progress; a weaker one won't cut off a big impact.
    /// </summary>
    public void Shake(float duration, float magnitude)
    {
        if (shakeTimeRemaining > 0f && magnitude < shakeMagnitude) return;

        shakeDuration = duration;
        shakeTimeRemaining = duration;
        shakeMagnitude = magnitude;
    }

    /// <summary>Shake scaled by distance — far-off explosions rattle less.</summary>
    public void ShakeAtPosition(Vector3 worldPos, float duration, float magnitude, float maxDistance)
    {
        float dist = Vector3.Distance(transform.position, worldPos);
        if (dist > maxDistance) return;

        float falloff = 1f - (dist / maxDistance);
        Shake(duration, magnitude * falloff);
    }
}