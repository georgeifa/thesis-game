using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "FOV Detector", story: "Player is in [FOV]", category: "Conditions", id: "4b32bb53d4370fe1e3f6742db9340810")]
public partial class FovDetectorCondition : Condition
{
    [SerializeReference] public BlackboardVariable<FieldOfView> FOV;

    public override bool IsTrue()
    {
        if(FOV == null) return false;
        return FOV.Value.playerDetected;
    }
}
