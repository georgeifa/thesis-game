using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "Player is in Attack Sight", story: "Player is in attack sight of Agent in [FOV]", category: "Conditions", id: "60ab6d3ef52727aa216a6473defc4e08")]
public partial class PlayerIsInAttackSightCondition : Condition
{

    [SerializeReference] public BlackboardVariable<FieldOfView> FOV;

    public override bool IsTrue()
    {
        if(FOV == null) return false;
        return FOV.Value.playerDetected;
;
    }
}
