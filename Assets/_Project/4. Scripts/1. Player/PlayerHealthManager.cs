using UnityEngine;

/// <summary>
/// The player's health and death state. Implements IDamagable so anything that
/// already damages enemies (enemy attacks, enemy grenades) can damage the
/// player through the same interface.
/// </summary>
public class PlayerHealthManager : MonoBehaviour, IDamagable
{
    [SerializeField]
    private int _MaxHealth = 100;
    [SerializeField]
    private int _Health; //just to see in the inspector

    public int CurrentHealth { get => _Health; private set => _Health = value; }
    public int MaxHealth     { get => _MaxHealth;     private set => _MaxHealth = value; }

    /// <summary>0–1, for health bars.</summary>
    public float HealthFraction => MaxHealth > 0 ? (float)CurrentHealth / MaxHealth : 0f;

    public bool IsDead { get; private set; }

    public event IDamagable.TakeDamageEvent OnTakeDamage;
    public event IDamagable.DeathEvent OnDeath;


// ── Hit direction ────────────────────────────
 
    private Vector3 lastHitPoint;
    private bool hasHitPoint;
 
    /// <summary>World position of the most recent hit, if one was reported.</summary>
    public Vector3 LastHitPoint => lastHitPoint;
    public bool HasHitPoint => hasHitPoint;

    private void Awake()
    {
        CurrentHealth = MaxHealth;
    }

    public void TakeDamage(int damage)
    {
        if (IsDead) return;   // already dead — ignore further hits

        int damageTaken = Mathf.Clamp(damage, 0, CurrentHealth);
        if (damageTaken == 0) return;

        CurrentHealth -= damageTaken;
        OnTakeDamage?.Invoke(damageTaken);

        if (CurrentHealth <= 0)
        {
            IsDead = true;
            OnDeath?.Invoke();
        }
    }

        /// <summary>Full health, alive again. Called when a new soldier deploys.</summary>
    public void ResetHealth()
    {
        CurrentHealth = MaxHealth;
        IsDead = false;
    }

    /// <summary>Restores health, clamped to max. For pickups / stims later.</summary>
    public void Heal(int amount)
    {
        if (IsDead) return;
        CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
    }

     /// <summary>
    /// Direction the damage came FROM, pointing at the player. Flat on the
    /// ground plane. Falls back to the player's back if nothing reported a hit.
    /// </summary>
    public Vector3 IncomingDirection
    {
        get
        {
            if (!hasHitPoint) return -transform.forward;
 
            Vector3 dir = transform.position - lastHitPoint;
            dir.y = 0f;
            return dir.sqrMagnitude > 0.001f ? dir.normalized : -transform.forward;
        }
    }
 
    /// <summary>
    /// Signed angle of the hit relative to where the player is facing.
    /// 0 = hit from the front, 180 = from behind, +90 = right, -90 = left.
    /// Not used yet — here for directional hit reactions later.
    /// </summary>
    public float HitAngle
    {
        get
        {
            if (!hasHitPoint) return 180f;
 
            Vector3 toHit = lastHitPoint - transform.position;
            toHit.y = 0f;
            if (toHit.sqrMagnitude < 0.001f) return 180f;
 
            return Vector3.SignedAngle(transform.forward, toHit.normalized, Vector3.up);
        }
    }
 
    /// <summary>
    /// Called by damage sources before TakeDamage to report WHERE the hit came
    /// from. Optional — damage still works without it.
    /// </summary>
    public void GetHitDirection(Vector3 hitPoint)
    {
        lastHitPoint = hitPoint;
        hasHitPoint = true;
    }
 
    // ADD to ResetHealth() so a new soldier doesn't inherit the old one's hit:
    //     hasHitPoint = false;
}