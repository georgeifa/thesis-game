using System.Collections.Generic;
using UnityEngine;

// Runs the mission: owns the objective list, the mission clock, and the
// decision about when extraction opens. Nothing else ticks objectives.
public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance { get; private set; }

    [Header("Setup")]
    [SerializeField] Transform player;
    [Tooltip("Order matters for the HUD. Leave empty and it finds them all, which is fine for grey-box testing.")]
    [SerializeField] List<MissionObjective> objectives = new List<MissionObjective>();

    [Header("Mission clock")]
    [SerializeField] float missionDuration = 600f;
    [SerializeField] float firstWarningAt = 120f;
    [SerializeField] float finalWarningAt = 30f;

    [Header("Extraction")]
    [Tooltip("Extraction also opens this many seconds in, even if nothing has been resolved yet. Stops a player who can't finish objective one being stranded.")]
    [SerializeField] float extractionOpensAt = 240f;

    public bool IsRunning { get; private set; }
    public bool ExtractionOpen { get; private set; }
    public bool Won { get; private set; }

    public float TimeLeft { get; private set; }
    public float Elapsed => missionDuration - TimeLeft;

    public IReadOnlyList<MissionObjective> Objectives => objectives;

    // The pad needs it too, and one wired reference is one fewer to forget.
    public Transform Player => player;
    public int ObjectivesTotal => objectives.Count;
    public int ObjectivesCompleted { get; private set; }

    bool warnedFirst;
    bool warnedFinal;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        if (player == null)
        {
            Debug.LogError($"{name}: no player assigned - objectives will never see the player.");
            enabled = false;
            return;
        }

        if (objectives.Count == 0)
        {
            objectives.AddRange(FindObjectsByType<MissionObjective>());
            Debug.LogWarning($"{name}: objective list was empty, found {objectives.Count} in the scene. " +
                             "Fill the list in the inspector for a defined order.");
        }

        foreach (MissionObjective o in objectives)
            o.Setup(player);

        TimeLeft = missionDuration;
        IsRunning = true;
    }

    void Update()
    {
        if (!IsRunning) return;

        // GameManager owns losing by running out of soldiers. Polling its flag
        // rather than subscribing - one less subscription with a lifetime,
        // same reasoning as the objectives.
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver)
        {
            IsRunning = false;
            return;
        }

        float dt = Time.deltaTime;

        TimeLeft -= dt;
        CheckWarnings();

        foreach (MissionObjective o in objectives)
            o.Tick(dt);

        // After the tick, so an objective that resolved this frame opens
        // extraction on the same frame rather than the next one.
        if (!ExtractionOpen) CheckExtractionGate();

        if (TimeLeft <= 0f)
        {
            TimeLeft = 0f;
            EndMission("OUT OF TIME");
        }
    }

    void CheckWarnings()
    {
        if (!warnedFirst && TimeLeft <= firstWarningAt)
        {
            warnedFirst = true;
            // TODO: HUD warning in week 4.
            Debug.Log($"[Mission] {firstWarningAt:0} seconds remaining");
        }

        if (!warnedFinal && TimeLeft <= finalWarningAt)
        {
            warnedFinal = true;
            Debug.Log($"[Mission] {finalWarningAt:0} seconds remaining");
        }
    }

    void CheckExtractionGate()
    {
        if (Elapsed >= extractionOpensAt)
        {
            OpenExtraction("timer");
            return;
        }

        // Resolved, not completed. A player who can't finish the first
        // objective still gets a way off the planet.
        foreach (MissionObjective o in objectives)
        {
            if (o.IsResolved)
            {
                OpenExtraction($"'{o.displayName}' resolved");
                return;
            }
        }
    }

    void OpenExtraction(string why)
    {
        ExtractionOpen = true;
        Debug.Log($"[Mission] Extraction open ({why})");
    }

    // Called from Enemy.Die(). Static and null-safe for the same reason
    // RunStats' reporters are: the call site is one line that can never throw
    // and needs no reference.
    //
    // Deliberately NOT a static event on Enemy that Eliminate subscribes to.
    // A static event is the shape that accumulated ten FieldOfView subscribers
    // across respawns, and it would need unsubscribing on resolve, on destroy
    // and on scene load. Pushing the kill in has no lifetime at all.
    public static void ReportEnemyKilled(Vector3 position)
    {
        MissionManager m = Instance;
        if (m == null || !m.IsRunning) return;

        // Only one objective type cares about kills. When a second one does,
        // that is when this earns a virtual on the base class.
        foreach (MissionObjective o in m.objectives)
            if (o is Eliminate e) e.RegisterKill(position);
    }

    // Called by the extraction pad when the teleport sequence finishes.
    public void CompleteExtraction()
    {
        Won = true;
        EndMission("EXTRACTION COMPLETE");
    }

    public void EndMission(string headline)
    {
        if (!IsRunning) return;
        IsRunning = false;

        // Everything unfinished counts as failed - never started, still locked
        // behind a prerequisite, or halfway through. Fail() ignores anything
        // already resolved, so this is safe to run over the whole list.
        foreach (MissionObjective o in objectives)
            o.Fail();

        ObjectivesCompleted = 0;
        foreach (MissionObjective o in objectives)
            if (o.IsComplete) ObjectivesCompleted++;

        Debug.Log($"[Mission] {headline} - objectives {ObjectivesCompleted}/{ObjectivesTotal}");

        if (GameManager.Instance != null)
            GameManager.Instance.EndRun(headline);
    }
}