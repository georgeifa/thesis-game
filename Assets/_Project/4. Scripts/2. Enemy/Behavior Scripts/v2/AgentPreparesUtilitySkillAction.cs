using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Agent Prepares Utility Skill", story: "[Enemy] gets ready to use [UtilitySkill]", category: "Action", id: "a2dcef0b63238be6f148a96641ef0936")]
public partial class AgentPreparesUtilitySkillAction : Action
{
    [SerializeReference] public BlackboardVariable<Enemy> Enemy;
    [SerializeReference] public BlackboardVariable<SkillsScriptableObject> UtilitySkill;

    protected override Status OnStart()
    {
        if(Enemy == null || UtilitySkill == null) return Status.Failure;

        UtilitySkill.Value = Enemy.Value.UtilitySkill;
        return Status.Success;
    }

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

