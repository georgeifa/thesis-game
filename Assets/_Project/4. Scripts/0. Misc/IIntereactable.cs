// Anything the player can press E on. Implemented by MissionObjective (the
// antenna, the bomb, the extraction pad) and by GeneratorTarget, which isn't
// an objective at all - which is the reason this is an interface and not just
// a method on MissionObjective.
public interface IInteractable
{
    // Shown in the prompt. "BEGIN BROADCAST", "START GENERATOR".
    string Prompt { get; }

    // False means no prompt and pressing E does nothing. Each implementer
    // decides - usually "player is close enough and I haven't been used yet".
    bool CanInteract();

    void Interact();
}