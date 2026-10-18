using UnityEngine;

// Kill N enemies inside the zone. The outpost objective.
//
// Kills are pushed in by MissionManager.ReportEnemyKilled rather than this
// subscribing to anything - see the note there.
public class Eliminate : MissionObjective
{
    [Header("Eliminate")]
    public int killsRequired = 12;

    int killed;

    public int Killed => killed;
    public int Remaining => Mathf.Max(0, killsRequired - killed);

    public override float Progress => killsRequired > 0 ? (float)killed / killsRequired : 1f;

    protected override void OnSetup()
    {
        killed = 0;
    }

    // Nothing to do per frame - progress only moves when something dies. The
    // base still ticks it for the OnEnter check and the time limit.
    protected override void OnTick(float dt) { }

    public void RegisterKill(Vector3 where)
    {
        if (State != ObjectiveState.Active) return;

        // Only kills inside the zone count, or you could clear the outpost
        // from the other side of the map.
        if (!IsInZone(where)) return;

        killed++;

        if (killed >= killsRequired)
            Complete();
    }
}