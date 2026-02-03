using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Die On Approach", menuName = "Skills/Utility Skills/Die On Approach")]
public class DieOnApproach : SkillsScriptableObject
{
    public override void UseSkill(Enemy enemy, GameObject player)
    {
        DieWhenClose(enemy);
    }

    public override bool CanUseSkill(Enemy enemy, GameObject player)
    {
        return base.CanUseSkill(enemy, player);
    }

    private void DieWhenClose(Enemy enemy)
    {
        enemy.Die();
    }
}
