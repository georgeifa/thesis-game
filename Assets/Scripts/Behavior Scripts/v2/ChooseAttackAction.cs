using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using System.Collections.Generic;
using System.Linq;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Choose Attack", story: "Agent Chooses Between Possible Attacks", category: "Action", id: "dd3abae668e40c1d292ed36e26430614")]
public partial class ChooseAttackAction : Action
{

    [SerializeReference] public BlackboardVariable<AI_Combat> AI_Combat;
    [SerializeReference] public BlackboardVariable<GameObject> Player;
    [SerializeReference] public BlackboardVariable<SkillsScriptableObject> SkillToUse;

/// <summary>
/// Basic Attacks will always have a priority of 5.
/// Skills can have a priority from 1 to 5, with 1 being the highest priority.
/// Priority changes the probability to be selected instead of a basic attack as well as changes the order in which all available skills will be checked.
/// If a skill is a selected then the agent will proceed with executing the skill, otherwise the agent will just proceed with a basic attack.
/// </summary>


    Dictionary<SkillsScriptableObject,int> availableSkills_w_Priorities;
    
    protected override Status OnStart()
    {
        if(AI_Combat == null) return Status.Failure;

        if(SkillToUse.Value != null)
            return Status.Success;

        availableSkills_w_Priorities = new Dictionary<SkillsScriptableObject, int>();
        foreach(SkillsScriptableObject s in AI_Combat.Value.Skills)
        {
            if(AI_Combat.Value.CanUseSkill(s,Player))
                availableSkills_w_Priorities.Add(s,s.Priority);
        }

        if(availableSkills_w_Priorities.Count != 0)
            CheckPriorities();

        return Status.Success;

    }

    private void CheckPriorities()
    {
        var sortedList = availableSkills_w_Priorities
            .OrderBy(kvp => kvp.Value)  // Sort by priority
            .ToList();

        foreach (var skill in sortedList)
        {
            float probability = .5f;
            int priority = skill.Value;
            probability += (5f-priority)/(2f*5f);
            
            float randomSelector = UnityEngine.Random.Range(0f,1f);
            if(randomSelector<=probability){
                SkillToUse.Value = skill.Key;
                break;
            }
        }
    }
}

