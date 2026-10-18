using System.Collections.Generic;
using UnityEngine;

// Switch on every generator. One objective, N switches.
//
// Usually the thing every other objective names as its prerequisite - power
// brings comms up, which is what makes the antenna usable. Mechanically that
// just means the rest of the mission stays Locked until this completes, which
// gives the player an unambiguous first move instead of three equal options
// and no reason to pick one.
public class RestorePowerObjective : MissionObjective
{
    [Header("Generators")]
    [SerializeField] List<GeneratorTarget> targets = new List<GeneratorTarget>();

    int on;

    public int On => on;
    public int Total => targets.Count;

    public override float Progress => targets.Count > 0 ? (float)on / targets.Count : 1f;

    protected override void OnSetup()
    {
        on = 0;

        if (targets.Count == 0)
            Debug.LogError($"{name}: no generators assigned - this objective can never complete.");

        foreach (GeneratorTarget t in targets)
            if (t != null) t.Setup(this, player);
    }

    // Push-driven. Nothing to do per frame; the base still ticks this for the
    // time limit, if one is set.
    protected override void OnTick(float dt) { }

    public void TargetActivated()
    {
        if (State != ObjectiveState.Active) return;

        on++;

        if (on >= targets.Count)
            Complete();
    }
}