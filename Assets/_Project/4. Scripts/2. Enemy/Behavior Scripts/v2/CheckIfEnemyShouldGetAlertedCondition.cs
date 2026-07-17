using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Check If Enemy Should Get Alerted", story: "Check if Enemy should get [Alerted] (need [CurrentState] )", category: "Conditions", id: "d24c4556d37ecb12e8f7a3e926994c4c")]
public partial class CheckIfEnemyShouldGetAlertedCondition : Condition
{
    [SerializeReference] public BlackboardVariable<AIState> CurrentState;
    [SerializeReference] public BlackboardVariable<bool> Alerted;

    public override bool IsTrue()
    {
        return 
        (CurrentState == AIState.Idle || CurrentState == AIState.Patrol) 
        && Alerted ;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
