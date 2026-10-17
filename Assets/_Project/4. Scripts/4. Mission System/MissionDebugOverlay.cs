using UnityEngine;
using UnityEngine.InputSystem;

// Scruffy on-screen readout of everything the mission systems are doing.
//
// OnGUI on purpose: no Canvas, no prefab, no wiring, and it gets deleted
// before submission. It allocates every frame and nobody cares. Do not copy
// this pattern for real UI.
//
// F2 toggles it.
public class MissionDebugOverlay : MonoBehaviour
{
    [SerializeField] bool show = true;
    [SerializeField] PlayerInteractor interactor;

    GUIStyle style;

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            show = !show;
    }

    void OnGUI()
    {
        if (!show) return;

        MissionManager m = MissionManager.Instance;
        if (m == null) return;

        if (style == null)
            style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };

        int lines = 5 + m.ObjectivesTotal;
        GUILayout.BeginArea(new Rect(10, 10, 520, 30 + lines * 18), GUI.skin.box);

        GUILayout.Label($"<b>MISSION</b>   {(m.IsRunning ? "running" : "ended")}" +
                        $"   clock {Format(m.TimeLeft)}" +
                        $"   extraction {(m.ExtractionOpen ? "OPEN" : "locked")}", style);

        string prompt = interactor != null ? interactor.CurrentPrompt : null;
        GUILayout.Label(prompt != null ? $"[E]  {prompt}" : "<i>(nothing in range)</i>", style);

        GUILayout.Space(6);

        foreach (MissionObjective o in m.Objectives)
        {
            GUILayout.Label(Describe(o), style);
        }

        GUILayout.EndArea();
    }

    static string Describe(MissionObjective o)
    {
        string line = $"{o.displayName,-22} {o.State,-10} {o.Progress * 100f,3:0}%";

        if (o.State == ObjectiveState.Active && o.timeLimit > 0f)
            line += $"   t-{o.TimeLeft:0.0}";

        // Hold-specific extras, so the three phases of being away are visible.
        if (o is HoldZone h && o.State == ObjectiveState.Active)
            line += h.PlayerInside ? "   IN" : (h.Draining ? "   DRAINING" : "   grace");

        return line;
    }

    static string Format(float seconds)
    {
        int t = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{t / 60:00}:{t % 60:00}";
    }
}