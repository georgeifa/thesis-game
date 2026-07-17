using UnityEngine;

[CreateAssetMenu(fileName = "Animator Death Config", menuName = "Deaths/Animator/Default Animator Death")]
public class AnimatorDeathScriptableObject : DeathScriptableObject
{
    public override void Die(Enemy enemy, Transform root, ParticleSystem particleSystem)
    {
        base.Initializations(enemy,root,particleSystem);
        animator.SetTrigger("Die");
        base.Die(enemy,root,particleSystem);
    }
}
