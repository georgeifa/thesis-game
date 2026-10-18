using UnityEngine;
using UnityEngine.Events;

public enum PadState { Locked, Idle, Malfunction, Ready, Teleporting }

// The way off the planet.
//
// Not a MissionObjective, deliberately. Its states don't map onto
// Dormant/Active/Completed/Failed, and it must not sit in the objective list
// where EndMission would mark it Failed - "you didn't extract" isn't a missed
// objective, it's how the run ended.
//
// The thing that makes it not a HoldZone: the malfunction timer runs whether
// or not the player is standing there. The pad is doing the work, not the
// player. So you activate it, go and fight somewhere else, and come back - and
// the return trip is the tense part instead of the standing.
public class ExtractionPad : MonoBehaviour, IInteractable
{
    [Header("Timing")]
    [Tooltip("How long the pad takes to come good after you call it. You do NOT have to stay.")]
    [SerializeField] float malfunctionTime = 45f;
    [Tooltip("How long it stays Ready with nobody on it before dropping back to Idle.")]
    [SerializeField] float readyWindow = 30f;
    [Tooltip("Once this starts it can't be cancelled.")]
    [SerializeField] float teleportTime = 3f;

    [Header("Zone")]
    [SerializeField] float radius = 5f;

    [Header("Prompt")]
    [SerializeField] string activatePrompt = "CALL EXTRACTION";

    [Header("Placeholder feedback")]
    [Tooltip("Swapped per state until week 7 puts sparks and a beacon on it.")]
    [SerializeField] Renderer padRenderer;
    [SerializeField] Material lockedMaterial;
    [SerializeField] Material idleMaterial;
    [SerializeField] Material malfunctionMaterial;
    [SerializeField] Material readyMaterial;

    [Header("Events")]
    public UnityEvent onCalled;
    public UnityEvent onReady;
    public UnityEvent onTeleportStart;

    Transform player;
    PlayerHealthManager playerHealth;

    public PadState State { get; private set; } = PadState.Locked;

    // Seconds left in whatever the current state is counting down. 0 when it
    // isn't counting.
    public float Timer { get; private set; }

    public string Prompt => activatePrompt;

    void Start()
    {
        MissionManager m = MissionManager.Instance;

        if (m == null || m.Player == null)
        {
            Debug.LogError($"{name}: no MissionManager or no player - the pad will never work.");
            enabled = false;
            return;
        }

        player = m.Player;
        playerHealth = player.GetComponent<PlayerHealthManager>();

        Apply(PadState.Locked);
    }

    void Update()
    {
        MissionManager m = MissionManager.Instance;
        if (m == null || !m.IsRunning) return;

        switch (State)
        {
            case PadState.Locked:
                if (m.ExtractionOpen) Apply(PadState.Idle);
                break;

            case PadState.Idle:
                break;   // waiting for the player to press E

            case PadState.Malfunction:
                // No presence check. This is the whole point of the pad.
                Timer -= Time.deltaTime;
                if (Timer <= 0f)
                {
                    Apply(PadState.Ready);
                    Timer = readyWindow;
                    onReady?.Invoke();
                }
                break;

            case PadState.Ready:
                if (PlayerOnPad())
                {
                    Apply(PadState.Teleporting);
                    Timer = teleportTime;
                    onTeleportStart?.Invoke();
                    break;
                }

                Timer -= Time.deltaTime;
                if (Timer <= 0f) Apply(PadState.Idle);   // wandered off, start again
                break;

            case PadState.Teleporting:
                // Committed. Stepping off doesn't cancel it, and dying doesn't
                // either - GameManager.EndRun guards re-entry, so whichever
                // ending lands first is the one that counts.
                Timer -= Time.deltaTime;
                if (Timer <= 0f)
                {
                    m.CompleteExtraction();
                    enabled = false;   // the pad's job is done
                }
                break;
        }
    }

    bool PlayerOnPad()
    {
        if (player == null) return false;

        // A corpse lying on the pad is not someone standing on it.
        if (playerHealth != null && playerHealth.IsDead) return false;

        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= radius * radius;
    }

    public bool CanInteract()
    {
        return State == PadState.Idle && PlayerOnPad();
    }

    public void Interact()
    {
        if (!CanInteract()) return;

        Apply(PadState.Malfunction);
        Timer = malfunctionTime;
        onCalled?.Invoke();
    }

    void Apply(PadState next)
    {
        State = next;
        Timer = 0f;

        if (padRenderer == null) return;

        Material m = next switch
        {
            PadState.Locked      => lockedMaterial,
            PadState.Idle        => idleMaterial,
            PadState.Malfunction => malfunctionMaterial,
            _                    => readyMaterial    // Ready and Teleporting both read as "go"
        };

        if (m != null) padRenderer.sharedMaterial = m;
    }

    void OnDrawGizmos()
    {
        Gizmos.color = State switch
        {
            PadState.Ready       => Color.green,
            PadState.Teleporting => Color.cyan,
            PadState.Malfunction => Color.red,
            PadState.Idle        => Color.yellow,
            _                    => new Color(1f, 1f, 1f, 0.35f)
        };

        MissionObjective.DrawFlatCircle(transform.position, radius);
    }
}