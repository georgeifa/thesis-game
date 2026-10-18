using UnityEngine;
using UnityEngine.Events;

// One generator. Walk up, press E once, it's on. No hold.
//
// Not an objective itself - the objective is "restore power" and this is one
// of its three switches. That split is the whole reason IInteractable exists:
// the interactor has to talk to this and to MissionObjective, which are
// otherwise unrelated types.
public class GeneratorTarget : MonoBehaviour, IInteractable
{
    [SerializeField] string prompt = "START GENERATOR";
    [SerializeField] float radius = 3f;

    [Tooltip("Fires when this one comes on - lights, sound, a material swap.")]
    [SerializeField] UnityEvent onActivated;

    RestorePowerObjective owner;
    Transform player;

    public bool IsOn { get; private set; }

    public string Prompt => prompt;

    public void Setup(RestorePowerObjective objective, Transform playerTransform)
    {
        owner = objective;
        player = playerTransform;
        IsOn = false;
    }

    public bool CanInteract()
    {
        if (IsOn || player == null) return false;

        // Only while the objective is running, so generators can't be switched
        // on before the mission starts or after it's over.
        if (owner == null || owner.State != ObjectiveState.Active) return false;

        // No dead-player check here on purpose - PlayerDeathHandler disables
        // PlayerInteractor, so nothing asks us while the soldier is down.
        Vector3 d = player.position - transform.position;
        d.y = 0f;
        return d.sqrMagnitude <= radius * radius;
    }

    public void Interact()
    {
        if (!CanInteract()) return;

        IsOn = true;
        onActivated?.Invoke();
        owner.TargetActivated();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = IsOn ? Color.green : new Color(1f, 1f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}