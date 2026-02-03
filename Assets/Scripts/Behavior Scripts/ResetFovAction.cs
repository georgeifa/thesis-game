using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "ResetFOV", story: "Reset [FOV] angle", category: "Action", id: "3be5a0b2313f3e2be638e421c07a3a27")]
public partial class ResetFovAction : Action
{
    [SerializeReference] public BlackboardVariable<FieldOfView> FOV;

    protected override Status OnStart()
    {
        if(FOV == null) return Status.Failure;
        FOV.Value.ResetFOVAngle();
        return Status.Success;
    }
}

