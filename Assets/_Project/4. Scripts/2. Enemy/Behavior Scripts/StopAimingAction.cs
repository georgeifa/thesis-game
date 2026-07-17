using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "StopAiming", story: "[Agent] Stops Aiming", category: "Action", id: "b98167deba70824dc319527d3e13924b")]
public partial class StopAimingAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;

    protected override Status OnStart()
    {
        if(Agent == null) return Status.Failure;

        EnemyCombatHandler combatHandler = Agent.Value.GetComponent<EnemyCombatHandler>();

        if(combatHandler.isAttacking) combatHandler.isAttacking = false;
        return Status.Success;
    }
}

