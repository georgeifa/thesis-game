using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Maximize FOV", story: "Maximize [FOV] angle", category: "Action", id: "2833988363ac997ddc29b25da15cac6c")]
public partial class MaximizeFovAction : Action
{
    [SerializeReference] public BlackboardVariable<FieldOfView> FOV;

    protected override Status OnStart()
    {
        if(FOV == null) return Status.Failure;
        FOV.Value.MaxFOVAngle();
        return Status.Success;
    }
}

