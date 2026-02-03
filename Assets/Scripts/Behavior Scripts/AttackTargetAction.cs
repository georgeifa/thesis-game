using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using Unity.VisualScripting;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Attack Target", story: "[Self] attacks [Target]", category: "Action", id: "d7c228747d8e40e80ba9cc8425aa78fc")]
public partial class AttackTargetAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<bool> attackComplete;
    [SerializeReference] public BlackboardVariable<EnemyCombatHandler> CombatHandler;


    protected override Status OnStart()
    {
        if(Self==null || Target == null) return Status.Failure;
        if(!CombatHandler.Value.ReadyToAttack) return Status.Failure;
        attackComplete.Value = false;
        CombatHandler.Value.Attack(Target.Value);

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if(!attackComplete.Value)
            return Status.Running;
        return Status.Success;
    }

    protected override void OnEnd()
    {
        attackComplete.Value = false;
    }
}

