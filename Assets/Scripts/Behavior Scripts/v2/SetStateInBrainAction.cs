using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Set State in Brain", story: "Set the [AI_State] in [Enemy] brain script", category: "Action", id: "b4755ac1324d21f1ddaf20b10384623f")]
public partial class SetStateInBrainAction : Action
{
    [SerializeReference] public BlackboardVariable<AIState> AI_State;
    [SerializeReference] public BlackboardVariable<Enemy> Enemy;

    protected override Status OnStart()
    {
        if(AI_State == null || Enemy == null) return Status.Failure;

        Enemy.Value.CurrentState = AI_State.Value;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

