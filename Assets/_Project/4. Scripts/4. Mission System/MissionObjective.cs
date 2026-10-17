using UnityEngine;
using UnityEngine.Events;

public enum ObjectiveState { Locked, Dormant, Active, Completed, Failed }

public enum Activation { Interact, OnEnter, OnMissionStart }

// Base for every mission objective. Handles the state machine, the optional
// time limit and the prerequisite lock. Subclasses only decide what Progress
// means and what happens each frame while active.
public abstract class MissionObjective : MonoBehaviour, IInteractable
{
    [Header("Info")]
    public string displayName = "Objective";
    [TextArea(1, 2)] public string description;

    [Tooltip("Verb shown on the interact prompt. Falls back to the display name if blank.")]
    public string interactPrompt = "";

    [Header("Rules")]
    public Activation activation = Activation.Interact;

    [Tooltip("Seconds from activation to failure. 0 = no limit.")]
    public float timeLimit = 0f;

    [Tooltip("Stays Locked until this one completes. Empty = starts unlocked.")]
    public MissionObjective prerequisite;

    [Tooltip("How close the player has to be. Measured flat, height ignored.")]
    public float radius = 6f;

    [Header("Events")]
    [Tooltip("Fires once when this completes. The bomb hooks its detonation here.")]
    public UnityEvent onCompleted;

    public ObjectiveState State { get; private set; } = ObjectiveState.Dormant;
    public float TimeLeft { get; private set; }

    public bool IsComplete => State == ObjectiveState.Completed;
    public bool IsResolved => State == ObjectiveState.Completed || State == ObjectiveState.Failed;

    // 0-1. What the HUD draws. Means something different per subclass.
    public abstract float Progress { get; }

    // Most objectives sit on the thing they're about, but the bomb's defend
    // zone wants to be centred on the charge, not on wherever the script sits.
    public virtual Vector3 Centre => transform.position;

    protected Transform player;
    PlayerHealthManager playerHealth;

    public void Setup(Transform playerTransform)
    {
        player = playerTransform;
        playerHealth = playerTransform.GetComponent<PlayerHealthManager>();

        State = prerequisite != null ? ObjectiveState.Locked : ObjectiveState.Dormant;
        TimeLeft = 0f;
        OnSetup();
    }

    protected virtual void OnSetup() { }

    // Called by MissionManager, not Update. That way objectives can't run
    // before the mission starts or after it ends, and the order is fixed.
    public void Tick(float dt)
    {
        switch (State)
        {
            case ObjectiveState.Locked:
                // Note: if the prerequisite FAILS this stays locked forever,
                // which is what we want. MissionManager fails everything
                // unresolved at the end anyway, so it tallies correctly.
                if (prerequisite.IsComplete) State = ObjectiveState.Dormant;
                break;

            case ObjectiveState.Dormant:
                if (activation == Activation.OnMissionStart) Activate();
                else if (activation == Activation.OnEnter && PlayerInRange()) Activate();
                break;

            case ObjectiveState.Active:
                if (timeLimit > 0f)
                {
                    TimeLeft -= dt;
                    if (TimeLeft <= 0f)
                    {
                        TimeLeft = 0f;
                        Fail();
                        return;
                    }
                }
                OnTick(dt);
                break;
        }
    }

    protected abstract void OnTick(float dt);

    public void Activate()
    {
        if (State != ObjectiveState.Dormant) return;

        State = ObjectiveState.Active;
        TimeLeft = timeLimit;
        OnActivate();
    }

    protected virtual void OnActivate() { }

    protected void Complete()
    {
        if (IsResolved) return;

        State = ObjectiveState.Completed;
        TimeLeft = 0f;
        OnResolved();

        // Last, so whatever this triggers sees an objective that's already
        // settled. The bomb's blast can kill the player and the objective
        // still counts.
        onCompleted?.Invoke();
    }

    // Public because MissionManager fails everything outstanding when the
    // player extracts or the clock runs out, including ones never started.
    public void Fail()
    {
        if (IsResolved) return;

        State = ObjectiveState.Failed;
        TimeLeft = 0f;
        OnResolved();
    }

    protected virtual void OnResolved() { }

    public bool PlayerInRange()
    {
        if (player == null) return false;

        // A dead soldier's transform stays exactly where they fell - the player
        // GameObject is reused and only the corpse is spawned. Without this the
        // antenna keeps broadcasting over a body, and the pad would accept a
        // corpse as someone standing on it.
        if (playerHealth != null && playerHealth.IsDead) return false;

        Vector3 d = player.position - Centre;
        d.y = 0f;   // flat - the player and a prop's pivot sit at different heights
        return d.sqrMagnitude <= radius * radius;
    }

    public virtual string Prompt =>
        string.IsNullOrEmpty(interactPrompt) ? displayName : interactPrompt;

    public virtual bool CanInteract()
    {
        return State == ObjectiveState.Dormant
            && activation == Activation.Interact
            && PlayerInRange();
    }

    public virtual void Interact()
    {
        if (CanInteract()) Activate();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = State switch
        {
            ObjectiveState.Completed => Color.green,
            ObjectiveState.Failed    => Color.red,
            ObjectiveState.Active    => Color.yellow,
            _                        => new Color(1f, 1f, 1f, 0.35f)
        };

        // A flat ring, not a sphere. PlayerInRange zeroes the Y before
        // comparing, so the real check is an infinitely tall cylinder - a wire
        // sphere draws a shape the code doesn't actually use and makes you
        // think you're losing range at height.
        DrawFlatCircle(Centre, radius);
    }

    // Rolled by hand instead of Handles.DrawWireDisc, which needs a
    // UnityEditor using - and a stray one of those in a runtime script already
    // cost us a broken build once.
    static void DrawFlatCircle(Vector3 centre, float r)
    {
        const int steps = 40;
        Vector3 prev = centre + new Vector3(r, 0f, 0f);

        for (int i = 1; i <= steps; i++)
        {
            float a = i / (float)steps * Mathf.PI * 2f;
            Vector3 next = centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);

            Gizmos.DrawLine(prev, next);
            prev = next;
        }
    }
}