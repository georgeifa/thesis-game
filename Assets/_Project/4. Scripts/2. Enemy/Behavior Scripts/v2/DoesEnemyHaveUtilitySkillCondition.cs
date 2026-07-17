using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Does Enemy Have Utility Skill", story: "Check if [Enemy] has Utility Skill", category: "Conditions", id: "299bfba8b668c907f3e52038071651f6")]
public partial class DoesEnemyHaveUtilitySkillCondition : Condition
{
    [SerializeReference] public BlackboardVariable<Enemy> Enemy;

    public override bool IsTrue()
    {
        if(Enemy == null)   return false;

        return Enemy.Value.hasUtilitySkill;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
