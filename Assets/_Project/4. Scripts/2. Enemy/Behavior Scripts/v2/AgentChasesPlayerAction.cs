using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Agent chases Player", story: "[Agent] chases [Player]", category: "Action", id: "e3891a780b83bbc9598c4502f62ee396")]
public partial class AgentChasesPlayerAction : Action
{
    [SerializeReference] public BlackboardVariable<GameObject> Agent;
    [SerializeReference] public BlackboardVariable<GameObject> Player;
   


    [Tooltip("Defines how often to update destination. Higher for more performancem, lower for more accuracy.")]
    [SerializeReference] public BlackboardVariable<float> UpdateInterval = new BlackboardVariable<float>(0.5f);
    [Tooltip("Variable to determine if agent should / can follow the player.")]
    [SerializeReference] public BlackboardVariable<bool> FollowPlayer;

    [SerializeReference] public BlackboardVariable<SkillsScriptableObject> SeparateSkill;

    private Enemy enemy;
    private float StoppingDistance;
    private float timer = 0f;


    protected override Status OnStart()
    {
        if(Agent == null || Player == null) return Status.Failure;
        enemy = Agent.Value.GetComponent<Enemy>();
        if(enemy == null) return Status.Failure;


        StoppingDistance = Agent.Value.GetComponent<NavMeshAgent>().stoppingDistance;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {  
        // The state may have moved on (lost sight → Investigate) while this node was
        // Running. A Running node parks the branch, so it has to bail out itself.
        if (enemy.CurrentState != AIState.Chase) return Status.Failure;

        if (FollowPlayer)
        {
            if(enemy.AI_Combat.EnemyIn)
                return Status.Success;

            for(int i=0; i<enemy.AI_Combat.EnemyInSkill.Count;i++)
            {
                if(enemy.AI_Combat.EnemyInSkill[i]){
                    SkillsScriptableObject skill = enemy.AI_Combat.GetSeparateSkill(i);
                    if(enemy.AI_Combat.CanUseSkill(skill,Player)){
                        SeparateSkill.Value = skill;
                        return Status.Success;
                    }
                }           
            }

            timer += Time.deltaTime;
            
            // Update destination at intervals (not every frame for performance)
            if (timer >= UpdateInterval.Value)
            {
                UpdateDestination();
                timer = 0f;
            }

            // Check if we're close enough to stop
            return CheckDistance();
        }

        return Status.Failure;
    }

    void UpdateDestination()
    {

        enemy.AI_Locomotion.SetDestination(Player.Value.transform.position);
            
        // Draw debug line
        Debug.DrawLine(
                Agent.Value.transform.position + Vector3.up, 
                Player.Value.transform.position + Vector3.up, 
                Color.yellow, 
                UpdateInterval
            );
    }

    Status CheckDistance()
    {        
        float distance = Vector3.Distance(Agent.Value.transform.position, Player.Value.transform.position);

        if (distance <= StoppingDistance)
        {
            return Status.Success;
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
        enemy.AI_Locomotion.ResetPath();
    }
}

