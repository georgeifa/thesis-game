using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "HasLostSight", story: "Sight of the player has been lost for long enough", category: "Action", id: "19da333bc24a7869169e9255fc487bcb")]
public partial class HasLostSightAction : Action
{
    [SerializeReference] public BlackboardVariable<bool> PlayerDetected;
    [SerializeReference] public BlackboardVariable<float> SightLostTime;
 
    [Tooltip("Seconds out of sight before the enemy gives up chasing.")]
    [SerializeReference] public BlackboardVariable<float> GracePeriod;
 
    protected override Status OnUpdate()
    {
        // Still visible — nothing lost.
        if (PlayerDetected.Value)
        {
            SightLostTime.Value = -1f;
            return Status.Failure;
        }
 
        // First frame without sight: stamp the time and keep chasing for now.
        if (SightLostTime.Value < 0f)
        {
            SightLostTime.Value = Time.time;
            return Status.Failure;
        }
 
        // Out of sight long enough to commit to investigating.
        return Time.time >= SightLostTime.Value + GracePeriod.Value
            ? Status.Success
            : Status.Failure;
    }
}

