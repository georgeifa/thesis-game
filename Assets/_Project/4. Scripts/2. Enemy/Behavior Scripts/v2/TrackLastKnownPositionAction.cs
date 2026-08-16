using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "TrackLastKnownPosition", story: "Remember where [Player] was last seen", category: "Action", id: "b27e118007013c6c043f2398dae1a125")]
public partial class TrackLastKnownPositionAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Player;
    [SerializeReference] public BlackboardVariable<bool> PlayerDetected;
    [SerializeReference] public BlackboardVariable<Vector3> LastKnownPosition;

    protected override Status OnUpdate()
    {
        if (PlayerDetected.Value && Player.Value != null)
        {
            LastKnownPosition.Value = Player.Value.transform.position;
        }
 
        return Status.Success;
    }
}

