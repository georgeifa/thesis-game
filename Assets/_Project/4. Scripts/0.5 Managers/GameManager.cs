using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Run-level state: how many soldiers remain, when the next deploys, and when
/// the run ends. Lives live here because they outlast any single soldier.
///
/// ── WHY THE RUN ENDS IN TWO STEPS ───────────────────────────────────────
/// `IsGameOver` is set the instant the last soldier falls — it is the GATE that
/// stops anything else from reacting. `OnGameOver` fires a few seconds later —
/// it is the PRESENTATION. Separating them means the death animation finishes
/// before the results screen appears, without leaving a window in which a
/// second death could still be processed.
///
/// Firing both at once cuts the fall off mid-animation and reads as a crash
/// rather than a defeat.
/// ────────────────────────────────────────────────────────────────────────
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    #region Inspector

    [Header("Lives")]
    [SerializeField] private int startingLives = 4;

    [Header("Deployment")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private DropPod dropPodPrefab;
    [Tooltip("Pause after death before the reinforcement launches.")]
    [SerializeField] private float launchDelay = 1.5f;
    [Tooltip("Leave spent pods in the world as scenery.")]
    [SerializeField] private bool keepSpentPods = true;
    [Tooltip("If not kept, seconds before removal.")]
    [SerializeField] private float spentPodLifetime = 5f;

    [Header("Run end")]
    [Tooltip("Seconds between the last soldier falling and the results screen " +
             "appearing. Long enough for the death animation to finish.")]
    [SerializeField] private float gameOverDelay = 2.5f;

    [Header("References")]
    [SerializeField] private PlayerDeathHandler player;
    [SerializeField] private CameraManager deploymentCamera;

    #endregion

    #region State

    private int livesRemaining;

    public int LivesRemaining => livesRemaining;
    public bool IsGameOver { get; private set; }

    public event Action<int> OnLivesChanged;
    public event Action OnGameOver;

    #endregion

    #region Lifecycle

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        livesRemaining = startingLives;
        OnLivesChanged?.Invoke(livesRemaining);

        // Guarded rather than assumed: this one subscription is the entire
        // reason the game is losable. Unassigned, every other system keeps
        // working and the run simply never ends — a silent gate of exactly the
        // kind that has cost this project days.
        if (player == null)
        {
            Debug.LogError($"{name}: no PlayerDeathHandler assigned — the run can never end.");
            return;
        }

        player.OnSoldierDied += HandleSoldierDied;
    }

    private void OnDestroy()
    {
        if (player != null)
            player.OnSoldierDied -= HandleSoldierDied;

        // Symmetry with RunStats. On a scene reload Unity destroys the old
        // scene before the new one's Awake runs, so this is belt-and-braces —
        // but a static that outlives its object is how singletons rot.
        if (Instance == this) Instance = null;
    }

    #endregion

    #region Soldier loss

    private void HandleSoldierDied()
    {
        // Re-entry guard. Without it a second OnSoldierDied — a damage source
        // landing on the same frame as the fatal one, or anything firing during
        // the game-over delay — drives livesRemaining negative and invokes
        // OnGameOver twice. The results screen would then re-populate from a
        // timer that has already stopped.
        if (IsGameOver) return;

        RunStats.ReportSoldierLost();

        livesRemaining--;
        OnLivesChanged?.Invoke(livesRemaining);

        if (livesRemaining > 0)
            StartCoroutine(DeployAfterDelay());
        else
            EndRun();
    }

    #endregion

    #region Deployment

    private IEnumerator DeployAfterDelay()
    {
        yield return new WaitForSeconds(launchDelay);

        Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        DropPod pod = Instantiate(dropPodPrefab);
        pod.SetDissolveTargets(player.GetVisualRenderers());

        deploymentCamera.FollowPod(pod.transform);

        pod.Drop(
            pos,
            onSoldierPlaced:   () => player.PlaceSoldier(pos, rot),
            onSoldierReleased: () => ReleaseSoldier(pod));
    }

    private void ReleaseSoldier(DropPod pod)
    {
        deploymentCamera.ReturnToPlayer();
        player.ActivateSoldier();

        if (!keepSpentPods)
            Destroy(pod.gameObject, spentPodLifetime);
    }

    #endregion

    #region Run end

    private void EndRun()
    {
        // Gate first, immediately — see the class summary.
        IsGameOver = true;

        // Stopped here rather than on the results screen, so the reported
        // duration is time-of-death and not time-of-death-plus-delay.
        // StopTimer is idempotent, so the screen's own call is a harmless no-op.
        RunStats.Instance?.StopTimer();

        Debug.Log("GAME OVER — no soldiers remaining");

        StartCoroutine(ShowResultsAfterDelay());
    }

    private IEnumerator ShowResultsAfterDelay()
    {
        // Scaled time is correct: timeScale is still 1 here. GameOverScreen.Show
        // is what zeroes it, and it does so last, after the panel is up.
        yield return new WaitForSeconds(gameOverDelay);

        OnGameOver?.Invoke();
    }

    #endregion
}