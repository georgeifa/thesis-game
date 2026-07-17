using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Agent Can Attack", story: "Agent is allowed to Attack from [AI_Combat]", category: "Conditions", id: "64e703186c138ab19a7d8067140decdb")]
public partial class AgentCanAttackCondition : Condition
{
    [SerializeReference] public BlackboardVariable<AI_Combat> AI_Combat;

    public override bool IsTrue()
    {
        if(AI_Combat == null)
        {
            Debug.LogError("No AI_Combat script found in Behaviour Agent");
            return false;
        }

        return AI_Combat.Value.CanAttack() && AI_Combat.Value.EnemyIn;
    }

    public override void OnStart()
    {
    }

    public override void OnEnd()
    {
    }
}
