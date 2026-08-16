using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "LookAround", story: "[Self] looks around for the player", category: "Action", id: "a008b7e0f118805cd70728e811374cc8")]
public partial class LookAroundAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;

    [SerializeReference] public BlackboardVariable<bool> PlayerDetected;
    
    [Tooltip("How long to search before giving up.")]
    [SerializeReference] public BlackboardVariable<float> SearchDuration;

    private Enemy enemy;
    private float endTime;


    protected override Status OnStart()
    {
         if (Self.Value == null) return Status.Failure;
        enemy = Self.Value.GetComponent<Enemy>();
        if (enemy == null) return Status.Failure;

        enemy.AI_Locomotion.Stop();

        enemy.AI_Locomotion.SetRootMotionMode(true);
 
        // AI_Locomotion.FinishLookingAround() sets this back to 0 in OnEnd.
        enemy.Animator.SetInteger(enemy.AI_Locomotion.LookingAroundParam, 1);
 
        endTime = Time.time + SearchDuration.Value;
        return Status.Running;
    }
 
    protected override Status OnUpdate()
    {
        if (enemy == null) return Status.Failure;
 
        // Spotted them again — abandon the search.
        if (PlayerDetected.Value) return Status.Failure;
 
        return Time.time >= endTime ? Status.Success : Status.Running;
    }
 
    protected override void OnEnd()
    {
        if (enemy == null) return;
 
        enemy.AI_Locomotion.FinishLookingAround();
        enemy.AI_Locomotion.Resume();
    }
}

