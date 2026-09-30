using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "SetFOVMode", story: "Set FOV of [Enemy] to alerted [Alerted]", category: "Action", id: "9790460ac2151dfca17d383f9c974782")]
public partial class SetFovModeAction : Action
{
    [SerializeReference] public BlackboardVariable<Enemy> Enemy;
    [Tooltip("On for chase/investigate, off for idle/patrol.")]
    [SerializeReference] public BlackboardVariable<bool> Alerted;

    protected override Status OnStart()
    {
        if (Enemy?.Value == null) return Status.Failure;
 
        if (Alerted.Value) Enemy.Value.FOV.SetAlertedFOV();
        else               Enemy.Value.FOV.SetPatrolFOV();
 
        return Status.Success;
    }
}

