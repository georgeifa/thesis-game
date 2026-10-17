using UnityEngine;

// Stand in the zone until the bar fills. Used by the antenna broadcast, the
// bomb's defend phase, and "hold this ground".
public class HoldZone : MissionObjective
{
    [Header("Hold")]
    [Tooltip("Seconds of standing in the zone to finish it.")]
    public float fillTime = 60f;

    [Tooltip("How long you can be outside before progress starts dropping.")]
    public float grace = 3f;

    [Tooltip("Drain speed as a fraction of fill speed. 0.5 means losing ground takes twice as long as gaining it.")]
    [Range(0f, 2f)] public float drainRate = 0.5f;

    float progress;
    float timeOutside;

    public override float Progress => progress;

    // The HUD uses these to colour the bar and pick a verb.
    public bool PlayerInside { get; private set; }
    public bool Draining => State == ObjectiveState.Active && !PlayerInside && timeOutside >= grace;

    protected override void OnSetup()
    {
        progress = 0f;
        timeOutside = 0f;
        PlayerInside = false;
    }

    protected override void OnActivate()
    {
        // You're normally standing in it when you press E, so don't let the
        // grace timer start counting on the first frame.
        PlayerInside = PlayerInRange();
        timeOutside = 0f;
    }

    protected override void OnTick(float dt)
    {
        PlayerInside = PlayerInRange();

        if (PlayerInside)
        {
            timeOutside = 0f;
            progress += dt / fillTime;

            if (progress >= 1f)
            {
                progress = 1f;
                Complete();
            }
            return;
        }

        timeOutside += dt;
        if (timeOutside < grace) return;

        progress -= dt * drainRate / fillTime;

        // Stops at zero instead of failing. The time limit is the only way to
        // lose this - two fail conditions on one bar and the player can't tell
        // which one got them.
        if (progress < 0f) progress = 0f;
    }
}