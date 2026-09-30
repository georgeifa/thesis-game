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
    [SerializeReference] public BlackboardVariable<AIState> CurrentAIState;



    protected override Status OnStart()
    {
        // .Value, not the wrapper — the BlackboardVariable object always exists.
        if (Enemy?.Value == null || Player?.Value == null || Skill?.Value == null)
            return Status.Failure;

        if (Skill.Value.skillType == SkillType.Utility)
        {
            if (!Enemy.Value.CanUseSkill(Player.Value)) return Status.Success;

            Enemy.Value.UseSkill(Skill.Value, Player.Value);
        }
        else
        {
            if (!Enemy.Value.AI_Combat.CanUseSkill(Skill.Value, Player.Value)) return Status.Success;

            Enemy.Value.AI_Combat.UseSkill(Skill.Value, Player.Value);
        }

        FollowPlayer.Value = false;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        // A parked node survives into the enemy's next life. Bail if the state
        // moved on or the selection was cleared underneath us.
        if (Enemy?.Value == null || Skill?.Value == null) return Status.Failure;
        if (CurrentAIState.Value != AIState.UsingSkill) return Status.Failure;

        bool stillRunning = Skill.Value.skillType == SkillType.Utility
            ? Enemy.Value.IsUsingSkill
            : Enemy.Value.AI_Combat.isUsingSkill;

        return stillRunning ? Status.Running : Status.Success;
    }

    protected override void OnEnd()
    {
        if (FollowPlayer != null) FollowPlayer.Value = true;
        if (Skill != null) Skill.Value = null;
    }
}

