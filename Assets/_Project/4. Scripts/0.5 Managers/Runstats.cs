using UnityEngine;

/// <summary>
/// Counts what happened during a run, for the results screen.
///
/// ── WHY A SINGLETON WITH STATIC REPORTERS ───────────────────────────────
/// The things worth counting happen in a dozen unrelated places — the gun, the
/// bullet trail, enemy death, the grenade throw. Threading a reference through
/// all of them would mean touching every one of those classes' constructors
/// and configs for something that is pure bookkeeping.
///
/// The static Report* methods are null-safe: if no RunStats exists in the
/// scene they quietly do nothing, so a call site is one line that can never
/// throw and never needs a guard. That is the whole reason they're static —
/// not convenience, but keeping the instrumentation from becoming a
/// dependency that other systems have to care about.
///
/// NOT DontDestroyOnLoad: a run's stats belong to that run, and reloading the
/// scene to retry should start from zero.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
public class RunStats : MonoBehaviour
{
    public static RunStats Instance { get; private set; }

    #region Counters

    [Header("Combat (read-only, shown for debugging)")]
    [SerializeField] private int shotsFired;
    [SerializeField] private int shotsHit;
    [SerializeField] private int enemiesKilled;
    [SerializeField] private int damageDealt;
    [SerializeField] private int soldiersLost;

    public int ShotsFired     => shotsFired;
    public int ShotsHit       => shotsHit;
    public int EnemiesKilled  => enemiesKilled;
    public int DamageDealt    => damageDealt;

    /// <summary>
    /// Always equal to starting lives on the results screen — a constant, not a
    /// statistic. Kept because it is the meaningful number on a MISSION COMPLETE
    /// screen, where finishing with soldiers in reserve is the thing worth
    /// reporting. Do not show it on the failure screen.
    /// </summary>
    public int SoldiersLost   => soldiersLost;

    /// <summary>
    /// 0–1. Counted per PROJECTILE, not per trigger pull — a shotgun blast of
    /// eight pellets is eight shots. That keeps the number honest about how
    /// much lead reached a target, at the cost of making shotguns look less
    /// accurate than they feel.
    /// </summary>
    public float Accuracy => shotsFired > 0 ? (float)shotsHit / shotsFired : 0f;

    #endregion

    #region Timing

    private float startTime;
    private float endTime = -1f;

    /// <summary>Seconds the run lasted. Frozen once the run ends.</summary>
    public float RunDuration => (endTime >= 0f ? endTime : Time.time) - startTime;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        ResetRun();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void ResetRun()
    {
        shotsFired = shotsHit = enemiesKilled = 0;
        damageDealt = soldiersLost = 0;

        startTime = Time.time;
        endTime = -1f;
    }

    /// <summary>Freezes RunDuration. Called when the run ends.</summary>
    public void StopTimer()
    {
        if (endTime < 0f) endTime = Time.time;
    }

    #endregion

    #region Reporters
    // Null-safe by design — see the class summary. Call them from anywhere
    // without a reference or a guard.

    public static void ReportShotFired(int projectiles = 1)
    {
        if (Instance != null) Instance.shotsFired += projectiles;
    }

    public static void ReportShotHit(int projectiles = 1)
    {
        if (Instance != null) Instance.shotsHit += projectiles;
    }

    public static void ReportEnemyKilled()
    {
        if (Instance != null) Instance.enemiesKilled++;
    }

    public static void ReportDamageDealt(int amount)
    {
        if (Instance != null) Instance.damageDealt += Mathf.Max(0, amount);
    }

    public static void ReportSoldierLost()
    {
        if (Instance != null) Instance.soldiersLost++;
    }

    #endregion

    #region Formatting
    // Kept here rather than in the UI so every screen that shows these — the
    // results screen now, an end-of-mission summary later — formats them the
    // same way.

    public string AccuracyText => shotsFired > 0 ? $"{Accuracy * 100f:0}%" : "—";

    public string DurationText
    {
        get
        {
            int total = Mathf.Max(0, Mathf.FloorToInt(RunDuration));
            return $"{total / 60:00}:{total % 60:00}";
        }
    }

    #endregion
}