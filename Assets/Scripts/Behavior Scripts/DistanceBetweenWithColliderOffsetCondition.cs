using System;
using Unity.Behavior;
using UnityEngine;

[Serializable, Unity.Properties.GeneratePropertyBag]
[Condition(name: "distance between with collider offset", story: "distance between [Self] and [Target] [Operator] [Threshold]", category: "Conditions", id: "081802f026129c5cb0087d68152ead59")]
public partial class DistanceBetweenWithColliderOffsetCondition : Condition
{
    [SerializeReference] public BlackboardVariable<GameObject> Self;
    [SerializeReference] public BlackboardVariable<Transform> Target;
    [Comparison(comparisonType: ComparisonType.All)]
    [SerializeReference] public BlackboardVariable<ConditionOperator> Operator;
    [SerializeReference] public BlackboardVariable<float> Threshold;

    public override bool IsTrue()
    {
        if (Self.Value == null || Target.Value == null) return false;

        float distance = Vector3.Distance(Self.Value.transform.position, Target.Value.position);

        float Threshold_w_offset = Helpers.RangeWithColliderOffset(Self.Value,Threshold);     

        return ConditionUtils.Evaluate(distance, Operator, Threshold_w_offset);
    }
}
