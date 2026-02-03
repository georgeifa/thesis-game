using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "RotateToFace", story: "[Self] rotates to face [Target]", category: "Action", id: "78ef847cafce9ad4fbab1a798c3d0e1c")]
public partial class RotateToFaceAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<FieldOfView> FOV;
    [SerializeReference] public BlackboardVariable<float> rotationSpeed;


    private Transform ownerTransform;
    private Transform playerTransform;


    protected override Status OnStart()
    {
        if (Self == null || Target == null)
            return Status.Failure;

        ownerTransform = Self.Value.transform;
        playerTransform = Target.Value.transform;

        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        Vector3 directionToPlayer = playerTransform.position - ownerTransform.position;
        directionToPlayer.y = 0; // Keep rotation horizontal
        
        if(FOV.Value.playerDetected) return Status.Success;
        
        // Rotate towards player
        Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
        ownerTransform.rotation = Quaternion.Slerp(
            ownerTransform.rotation, 
            targetRotation, 
            rotationSpeed * Time.deltaTime
        );
        
        return Status.Running;
    }

    protected override void OnEnd()
    {
    }
}

