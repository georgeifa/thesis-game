using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Agent Can Attack", story: "[Enemy] is allowed to Attack", category: "Conditions", id: "64e703186c138ab19a7d8067140decdb")]
public partial class AgentCanAttackCondition : Condition
{
    [SerializeReference] public BlackboardVariable<Enemy> Enemy;

    public override bool IsTrue()
    {
        if(Enemy?.Value == null)
        {
            Debug.LogError("No Enemy script found in Behaviour Agent");
            return false;
        }

        return Enemy.Value.AI_Combat.CanAttack() && Enemy.Value.AI_Combat.EnemyIn;
    }
}
