using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Perform Skill", story: "Agent Performs a [Skill]", category: "Action", id: "cc1327b4670610f61eaec40a04a92e13")]
public partial class PerformSkillAction : Action
{
    [SerializeReference] public BlackboardVariable<SkillsScriptableObject> Skill;

    [SerializeReference] public BlackboardVariable<GameObject> Player;
    [SerializeReference] public BlackboardVariable<Enemy> Enemy;

    [SerializeReference] public BlackboardVariable<bool> FollowPlayer;


    protected override Status OnStart()
    {
        if(Enemy == null || Player == null  || Skill == null) return Status.Failure;

        if(Skill.Value.skillType == SkillType.Utility){
            if(Enemy.Value.CanUseSkill(Player.Value))
            { 
                Enemy.Value.UseSkill(Skill.Value,Player.Value);
                FollowPlayer.Value = false;
            }
            else
                return Status.Success;
        }
        else
        {
            if(Enemy.Value.AI_Combat.CanUseSkill(Skill,Player.Value))
            { 
                Enemy.Value.AI_Combat.UseSkill(Skill.Value,Player.Value);
                FollowPlayer.Value = false;
            }
            else
                return Status.Success;
        }
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if(Skill.Value.skillType == SkillType.Utility)
        {
            if (Enemy.Value.IsUsingSkill)
            {
                return Status.Running;
            }
        }
        else
        {
           if (Enemy.Value.AI_Combat.isUsingSkill)
            {
            return Status.Running;
            } 
        }
        return Status.Success;
    }

    protected override void OnEnd()
    {
        FollowPlayer.Value = true;
        Skill.Value = null;
    }
}

