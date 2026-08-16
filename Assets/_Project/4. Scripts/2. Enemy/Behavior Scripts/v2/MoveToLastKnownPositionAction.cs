using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using MyBox;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "MoveToLastKnownPosition", story: "[Self] investigates the last known position of player", category: "Action", id: "eca10cee00bf3c2d55a65644a068a5f0")]
public partial class MoveToLastKnownPositionAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Vector3> LastKnownPosition;
    [SerializeReference] public BlackboardVariable<bool> PlayerDetected;
 
    [Tooltip("How close counts as arrived.")]
    [SerializeReference] public BlackboardVariable<float> ArrivalDistance;

    [Tooltip("Give up walking after this long and look around instead.")]
    [SerializeReference] public BlackboardVariable<float> InvestigateTimeout;

    private float giveUpTime;

    private Enemy enemy;

    protected override Status OnStart()
    {
        if (Self.Value == null) return Status.Failure;

        enemy = Self.Value.GetComponent<Enemy>();
        if (enemy == null) return Status.Failure;

        giveUpTime = Time.time + InvestigateTimeout.Value;
 
        enemy.AI_Locomotion.Resume();
        enemy.AI_Locomotion.SetDestination(LastKnownPosition.Value);
 
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        if (enemy == null) return Status.Failure;
 
        // Reacquired — bail out so the graph can return to Chase.
        if (PlayerDetected.Value) return Status.Failure;
 
        float distance = Vector3.Distance(
            enemy.transform.position, LastKnownPosition.Value);
 
        if (distance <= ArrivalDistance.Value)
            return Status.Success;

        // The position may be unreachable — off-navmesh, behind geometry, on a ledge.
        // Success rather than Failure so the enemy still looks around before giving up.
        if (Time.time >= giveUpTime) return Status.Success;
 
        // Re-issue in case the path was cleared (e.g. by an interrupted action).
        if(!enemy.Agent.hasPath)
            enemy.AI_Locomotion.SetDestination(LastKnownPosition.Value);
        return Status.Running;
    }

    protected override void OnEnd()
    {
        enemy.AI_Locomotion.ResetPath();
    }
}

