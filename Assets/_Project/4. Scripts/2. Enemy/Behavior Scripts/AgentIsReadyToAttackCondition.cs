using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Agent is ready to attack", story: "Agent is ready to attack in [CombatHandler]", category: "Conditions", id: "5774a13c18d0c4836827d79c72be4c4e")]
public partial class AgentIsReadyToAttackCondition : Condition
{
    [SerializeReference] public BlackboardVariable<EnemyCombatHandler> CombatHandler;

    public override bool IsTrue()
    {
        if(CombatHandler == null ) return false;
        return CombatHandler.Value.ReadyToAttack;
    }

}
