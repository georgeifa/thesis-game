using System;
using Unity.Behavior;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.UIElements;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Can Use Skill Check", story: "[Self] can use skill from [Combat] on [Target]", category: "Conditions", id: "ea2a7bc7ed913ac280b9c61f24a7f7f3")]
public partial class CanUseSkillCheckCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    [SerializeReference] public BlackboardVariable<EnemyCombatHandler> Combat;

    private Enemy enemy;
    public override bool IsTrue()
    {
        if(Self == null || Target == null  || Combat == null || Combat.Value.Skill == null) return false;
        return Combat.Value.Skill.CanUseSkill(enemy,Target.Value);
    }

    public override void OnStart()
    {
        enemy = Self.Value.GetComponent<Enemy>();
    }

}
