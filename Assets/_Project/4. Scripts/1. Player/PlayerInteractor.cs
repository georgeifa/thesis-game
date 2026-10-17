using UnityEngine;
using UnityEngine.InputSystem;

// Sits on the player. Each frame it looks for the nearest thing that will
// accept an interaction right now, and presses it when you hit E.
//
// It does NOT decide whether you're close enough - every IInteractable checks
// its own range, because the ranges differ (3m for a generator, 6m for the
// antenna). scanRadius is just a net to catch candidates.
//
// TODO: the prompt is only readable from code for now. Real HUD prompt is
// Week 4 - until then the debug overlay draws it.
public class PlayerInteractor : MonoBehaviour
{
    [Header("Input")]
    [Tooltip("Drag the Interact action from the Input Actions asset.")]
    [SerializeField] InputActionReference interactAction;

    [Header("Scan")]
    [Tooltip("How far out we look. Generous on purpose - the interactables do the real range check.")]
    [SerializeField] float scanRadius = 12f;
    [SerializeField] LayerMask interactableMask;

    readonly Collider[] hits = new Collider[16];

    public IInteractable Current { get; private set; }
    public string CurrentPrompt => Current != null ? Current.Prompt : null;

    void OnEnable()
    {
        if (interactAction == null)
        {
            Debug.LogError($"{name}: no Interact action assigned - interacting will do nothing.");
            return;
        }

        interactAction.action.performed += OnInteractPressed;
        interactAction.action.Enable();
    }

    void OnDisable()
    {
        // Unsubscribe but don't Disable the action - something else may be
        // using the same map. Enabling twice is harmless, leaving a dead
        // subscriber is not.
        if (interactAction != null)
            interactAction.action.performed -= OnInteractPressed;

        Current = null;
    }

    void Update()
    {
        Current = FindNearest();
    }

    IInteractable FindNearest()
    {
        int n = Physics.OverlapSphereNonAlloc(transform.position, scanRadius, hits, interactableMask);

        IInteractable best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < n; i++)
        {
            // InParent so the collider can sit on a child of the prop.
            IInteractable candidate = hits[i].GetComponentInParent<IInteractable>();
            if (candidate == null || !candidate.CanInteract()) continue;

            float d = (hits[i].transform.position - transform.position).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                best = candidate;
            }
        }

        return best;
    }

    void OnInteractPressed(InputAction.CallbackContext ctx)
    {
        // Scan fresh instead of using Current. Input callbacks run before
        // Update, so Current is always a frame behind - which matters on the
        // one frame where you press E the instant you walk into range.
        FindNearest()?.Interact();
    }
}