using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Agent attack Target", story: "[Enemy] Attacks the [Target]", category: "Action", id: "0dfe6dd62b3b44bd5136bf987d011e2f")]
public partial class AgentAttackTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<Enemy> Enemy;
    [SerializeReference] public BlackboardVariable<GameObject> Target;

    AI_Combat AI_Combat;

    protected override Status OnStart()
    {
        if(Enemy?.Value == null || Target?.Value == null) return Status.Failure;

        AI_Combat = Enemy.Value.AI_Combat;

        AI_Combat.Attack();
        return Status.Running;
    }

    protected override Status OnUpdate()
    {

        if(AI_Combat.isAttacking) return Status.Running;

        return Status.Success;
    }

    protected override void OnEnd()
    {
        if (AI_Combat != null && AI_Combat.isAttacking)
            AI_Combat.OnAttackComplete();
    }
}

