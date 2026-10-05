using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// The player's health, damage response and death state.
///
/// Implements IDamagable, so every damage source that already works against
/// enemies — melee overlaps, enemy grenades, the slam, the kamikaze blast —
/// reaches the player through exactly the same call with no special cases.
/// That symmetry is the point of the interface: there is one damage pipeline
/// in the game, not a player one and an enemy one.
///
/// ── HOW A HIT READS ─────────────────────────────────────────────────────
/// The player has no stagger animation and no ragdoll — deliberately. Taking
/// control away from the player feels punishing in a way that taking it away
/// from an enemy does not. A hit is instead communicated three ways:
///
///   1. A procedural spine tilt (the same HitReaction component the enemies
///      use, tuned smaller — the camera is closer and the player fills more
///      screen).
///   2. Camera shake scaled by the hit's force, via the same Cinemachine
///      impulse pattern the drop pod uses.
///   3. Above a threshold, whatever the player was doing is CUT SHORT. A
///      reload interrupted by a mutant's claw is the player-side mirror of
///      staggering an enemy out of its slam.
///
/// Losing your action is the real cost, and it's a cost the player can play
/// around rather than merely suffer.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
public class PlayerHealthManager : MonoBehaviour, IDamagable
{
    #region Inspector — Health

    [Header("Health")]
    [SerializeField] private int _MaxHealth = 100;
    [SerializeField]
    [Tooltip("Serialized only so it's visible while debugging in play mode.")]
    private int _Health;

    #endregion

    #region Inspector — Damage Feedback

    [Header("Feedback")]
    [Tooltip("Procedural spine tilt. Same component as the enemies, smaller values " +
             "— suggested degreesPerForce 1.5, maxAngle 15, duration 0.35.")]
    [SerializeField] private HitReaction hitReaction;

    [Tooltip("Camera shake. Reuses the impulse source pattern already set up for " +
             "the drop pod landing.")]
    [SerializeField] private CinemachineImpulseSource impulseSource;

    [Tooltip("Shake magnitude per unit of stagger force.")]
    [SerializeField] private float shakePerForce = 0.01f;

    [Tooltip("At or above this, the current action (reload, switch, throw) is " +
             "interrupted. Below it, the hit is felt but doesn't disrupt. " +
             "Reference: melee 15, slam 20, enemy grenade 35.")]
    [SerializeField] private float staggerThreshold = 20f;

    [Tooltip("Minimum seconds between camera shakes, so a flamethrower's rapid " +
             "ticks don't turn the screen into a blender.")]
    [SerializeField] private float minShakeInterval = 0.15f;

    #endregion

    #region Inspector — References

    [Header("References")]
    [Tooltip("Told to abort whatever the player was doing on a heavy hit.")]
    [SerializeField] private PlayerCombatController combat;

    #endregion

    #region State

    public int CurrentHealth { get => _Health;    private set => _Health = value; }
    public int MaxHealth     { get => _MaxHealth; private set => _MaxHealth = value; }

    /// <summary>0–1, for health bars (HUD hook, §8.1).</summary>
    public float HealthFraction => MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0f;

    public bool IsDead { get; private set; }

    [Header("State")]
    [Tooltip("True from death until the new soldier is handed control. Deployment " +
            "takes several seconds during which the player has no input, so being " +
            "targetable then is a free kill for the enemies.")]
    public bool IsInvulnerable { get; private set; }

    public void SetInvulnerable(bool value) => IsInvulnerable = value;

    // Where the last hit came from, for directional feedback.
    private Vector3 lastDamageSource;
    private bool hasDamageSource;
    private float lastShakeTime = -99f;

    #endregion

    #region Events

    public event IDamagable.TakeDamageEvent OnTakeDamage;
    public event IDamagable.DeathEvent OnDeath;

    /// <summary>
    /// Fires on every health change with the new 0–1 fraction. Separate from
    /// OnTakeDamage because a health bar wants the resulting VALUE, while a
    /// damage number wants the AMOUNT — and healing has to update the bar too.
    /// </summary>
    public event System.Action<float> OnHealthChanged;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        if (combat == null)        combat        = GetComponent<PlayerCombatController>();
        if (hitReaction == null)   hitReaction   = GetComponent<HitReaction>();
        if (impulseSource == null) impulseSource = GetComponent<CinemachineImpulseSource>();

        CurrentHealth = MaxHealth;
    }

    /// <summary>Full health, alive again. Called when a new soldier deploys.</summary>
    public void ResetHealth()
    {
        CurrentHealth = MaxHealth;
        IsDead = false;

        // A new soldier doesn't inherit the last one's hit.
        hasDamageSource = false;
        lastDamageSource = Vector3.zero;

        hitReaction?.ResetReaction();

        OnHealthChanged?.Invoke(HealthFraction);
    }

    /// <summary>Restores health, clamped to max. For pickups / stims later.</summary>
    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;

        CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
        OnHealthChanged?.Invoke(HealthFraction);
    }

    #endregion

    #region Taking Damage

    /// <summary>
    /// The single entry point for anything that hurts the player.
    ///
    /// ORDER MATTERS, and mirrors Enemy.TakeDamage deliberately:
    ///   1. Remember the source, so feedback can be directional
    ///   2. Apply damage and announce it
    ///   3. Bail on death — the death sequence owns everything after this, and
    ///      shaking the camera for a corpse fights the drop-pod sequence
    ///   4. Feedback and interruption
    /// </summary>
    /// <param name="sourcePosition">
    /// World position the damage came FROM — the attacker or the blast centre,
    /// not the impact point on the player. Every feedback decision below is
    /// about which way the threat is, so the attacker is the meaningful answer.
    /// </param>
    /// <param name="staggerForce">
    /// Disruption, independent of damage. A grenade that barely scratches you
    /// can still break your reload; a flamethrower tick that hurts a lot
    /// deliberately doesn't, or you'd never act while burning.
    /// </param>
    public void TakeDamage(int damage, Vector3 sourcePosition, float staggerForce = 0f)
    {
        if (IsDead || IsInvulnerable) return;

        lastDamageSource = sourcePosition;
        hasDamageSource = true;

        int damageTaken = Mathf.Clamp(damage, 0, CurrentHealth);
        if (damageTaken > 0)
        {
            CurrentHealth -= damageTaken;

            OnTakeDamage?.Invoke(damageTaken);
            OnHealthChanged?.Invoke(HealthFraction);

            if (CurrentHealth <= 0)
            {
                IsDead = true;
                OnDeath?.Invoke();
                return;
            }
        }

        ApplyFeedback(sourcePosition, staggerForce);
    }

    #endregion

    #region Feedback

    /// <summary>
    /// Everything the player sees and feels when hit, short of dying.
    /// </summary>
    private void ApplyFeedback(Vector3 sourcePosition, float force)
    {
        if (force <= 0f) return;

        Vector3 direction = transform.position - sourcePosition;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) direction = -transform.forward;
        direction.Normalize();

        // ── Always: the body reacts ──────────────────────────────────────
        // Unlike the enemies, there is no authored stagger clip competing with
        // this, so the tilt runs at every tier.
        hitReaction?.ApplyImpulse(direction, force);

        // ── Always (rate-limited): the camera reacts ─────────────────────
        if (impulseSource != null && Time.time >= lastShakeTime + minShakeInterval)
        {
            lastShakeTime = Time.time;
            impulseSource.GenerateImpulseWithVelocity(direction * force * shakePerForce);
        }

        // ── Heavy hits: you lose what you were doing ─────────────────────
        // The player-side mirror of AI_Combat.InterruptAction(). This is the
        // part that makes taking a hit a tactical event rather than a number
        // going down.
        if (force >= staggerThreshold)
            combat?.InterruptAction();
    }

    #endregion

    #region Hit Direction
    // Kept for directional feedback that doesn't exist yet — a damage vignette
    // pointing at the threat (§2.2), or a directional hit-reaction clip.

    /// <summary>
    /// Direction the damage came FROM, pointing at the player, flat on the
    /// ground plane. Falls back to the player's back if nothing has hit yet.
    /// </summary>
    public Vector3 IncomingDirection
    {
        get
        {
            if (!hasDamageSource) return -transform.forward;

            Vector3 dir = transform.position - lastDamageSource;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.001f ? dir.normalized : -transform.forward;
        }
    }

    /// <summary>
    /// Signed angle of the hit relative to facing. 0 = front, ±180 = behind,
    /// +90 = right, -90 = left. Feeds a directional vignette when that exists.
    /// </summary>
    public float HitAngle
    {
        get
        {
            if (!hasDamageSource) return 180f;

            Vector3 toSource = lastDamageSource - transform.position;
            toSource.y = 0f;
            if (toSource.sqrMagnitude < 0.001f) return 180f;

            return Vector3.SignedAngle(transform.forward, toSource.normalized, Vector3.up);
        }
    }

    #endregion

    // NOTE: TakeDamage(int) and GetHitDirection(Vector3) are gone. They were
    // the old two-call pairing, folded into the single TakeDamage above — the
    // same fold done on the enemy side. If the compiler flags a call site, that
    // is an unmigrated damage source being surfaced, not a regression.
}