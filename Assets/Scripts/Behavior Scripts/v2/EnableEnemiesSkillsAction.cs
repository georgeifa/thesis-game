using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using System.Collections;
using UnityEngine.Animations;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Enable Enemies Skills", story: "Start countdown to enable enemy's skills", category: "Action", id: "89d2e35735c33129024e50450df93e1e")]
public partial class EnableEnemiesSkillsAction : Action
{

    [SerializeReference] public BlackboardVariable<AI_Combat> AI_Combat;
    [SerializeReference] public BlackboardVariable<float> EnableAfter;

    protected override Status OnStart()
    {
        if(AI_Combat == null) return Status.Failure;

        AI_Combat.Value.StartCoroutine(UnlockEnemySkills());
        return Status.Success;
    }

    IEnumerator UnlockEnemySkills()
    {
        yield return new WaitForSeconds(EnableAfter);
        AI_Combat.Value.skillsUnlocked = true;
    }
}

