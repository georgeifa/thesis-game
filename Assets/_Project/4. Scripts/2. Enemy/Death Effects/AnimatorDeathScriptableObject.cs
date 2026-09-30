using UnityEngine;

/// <summary>
/// Death by animation: play a death clip and let the corpse sink.
///
/// Cheaper than a ragdoll and more readable — an authored death has a clear
/// silhouette where physics gives you whatever it gives you. The trade is
/// variety, which is why Phase 11.1 proposes a hybrid: animate the common case,
/// ragdoll the heavy hits.
/// </summary>
[CreateAssetMenu(fileName = "Animator Death Config", menuName = "Deaths/Animator/Default Animator Death")]
public class AnimatorDeathScriptableObject : DeathScriptableObject
{
    #region Inspector

    [Tooltip("Animator trigger that plays the death clip.")]
    public string deathTrigger = "Die";

    #endregion

    public override void Die(Enemy enemy, Transform root, ParticleSystem particleSystem)
    {
        if (enemy == null) return;

        // Silences the AI and starts the despawn timer. Runs first so the death
        // clip plays over a body nothing else is driving.
        base.Die(enemy, root, particleSystem);

        if (enemy.Animator == null) return;

        // The animator itself stays enabled — unlike the ragdoll path, the clip
        // IS the death. Note that SilenceAI disabled AI_Locomotion, so a death
        // clip with root motion will not travel; if you author one that should,
        // it needs its own handling.
        enemy.Animator.SetTrigger(deathTrigger);
    }
}