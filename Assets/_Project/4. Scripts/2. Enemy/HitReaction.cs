using UnityEngine;

/// <summary>
/// Procedural hit reaction. Tilts a bone away from the impact and settles it
/// back, on top of whatever the animator is playing.
///
/// The decay is computed analytically rather than integrated, so it cannot
/// diverge — an explicit spring with a clamped offset can inject energy and
/// overflow into NaN, which corrupts the bone transform permanently.
///
/// Works in LOCAL space against the pose the animator wrote this frame, so
/// the offset can never compound across frames.
/// </summary>
public class HitReaction : MonoBehaviour
{
    [Header("Bone")]
    [Tooltip("Bone to tilt — chest or upper spine. Empty falls back to the humanoid Chest.")]
    [SerializeField] private Transform hitBone;

    [Header("Impulse")]
    [Tooltip("Degrees of tilt per unit of stagger force.")]
    [SerializeField] private float degreesPerForce = 0.4f;
    [Tooltip("Hard ceiling on the tilt.")]
    [SerializeField] private float maxAngle = 12f;
    [Tooltip("Random spread on the impact direction, degrees.")]
    [SerializeField] private float directionJitter = 25f;

    [Header("Settle")]
    [Tooltip("How long the reaction lasts, seconds.")]
    [SerializeField] private float duration = 0.35f;

    // Axis of the current tilt (unit) and its starting magnitude in degrees.
    private Vector3 axis;
    private float amplitude;
    private float elapsed;

    private Animator animator;

    private void Awake()
    {
        animator = GetComponent<Animator>();

        if (hitBone == null && animator != null && animator.isHuman)
            hitBone = animator.GetBoneTransform(HumanBodyBones.Chest);
    }

    public void Configure(float degrees, float max, float jitter, float dur)
    {
        degreesPerForce = degrees;
        maxAngle        = max;
        directionJitter = jitter;
        duration        = dur;
    }

    /// <summary>
    /// Shoves the bone away from the impact. Direction is the push direction
    /// (attacker toward this enemy), horizontal.
    /// </summary>
    public void ApplyImpulse(Vector3 direction, float force)
    {
        if (hitBone == null || force <= 0f) return;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        direction.Normalize();

        if (directionJitter > 0f)
            direction = Quaternion.AngleAxis(
                Random.Range(-directionJitter, directionJitter), Vector3.up) * direction;

        // Tilting along a horizontal push means rotating about the axis
        // perpendicular to it.
        Vector3 newAxis = Vector3.Cross(Vector3.up, direction);
        if (newAxis.sqrMagnitude < 0.0001f) return;

        axis = newAxis.normalized;
        amplitude = Mathf.Min(force * degreesPerForce, maxAngle);
        elapsed = 0f;      // a new hit replaces the previous reaction
    }

    private void LateUpdate()
    {
         if (hitBone == null || amplitude <= 0f) return;

        elapsed += Time.deltaTime;
        if (elapsed >= duration) { amplitude = 0f; return; }

        float t = elapsed / duration;
        float decay = 1f - t;
        float angle = amplitude * decay;

        if (Mathf.Abs(angle) < 0.01f) return;
        // Local space, against the pose the animator wrote this frame — so
        // this starts fresh each frame instead of stacking on its own output.
        Vector3 localAxis = hitBone.parent != null
            ? hitBone.parent.InverseTransformDirection(axis)
            : axis;

        hitBone.localRotation = Quaternion.AngleAxis(angle, localAxis) * hitBone.localRotation;
    }

    /// <summary>Clears the reaction — call when pooling or on death.</summary>
    public void ResetReaction()
    {
        amplitude = 0f;
        elapsed = 0f;
    }
}