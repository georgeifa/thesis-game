using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;
using UnityEngine.AI;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Agent chases Player", story: "[Enemy] chases [Player]", category: "Action", id: "e3891a780b83bbc9598c4502f62ee396")]
public partial class AgentChasesPlayerAction : Action
{
    [SerializeReference] public BlackboardVariable<Enemy> Enemy;
    [SerializeReference] public BlackboardVariable<GameObject> Player;
   


    [Tooltip("Defines how often to update destination. Higher for more performancem, lower for more accuracy.")]
    [SerializeReference] public BlackboardVariable<float> UpdateInterval = new BlackboardVariable<float>(0.5f);
    [Tooltip("Variable to determine if agent should / can follow the player.")]
    [SerializeReference] public BlackboardVariable<bool> FollowPlayer;

    [SerializeReference] public BlackboardVariable<SkillsScriptableObject> SkillToUse;

    private float StoppingDistance;
    private float timer = 0f;


    protected override Status OnStart()
    {
        if(Enemy?.Value == null || Player?.Value == null) return Status.Failure;


        StoppingDistance = Enemy.Value.Agent.stoppingDistance;
        return Status.Running;
    }

    protected override Status OnUpdate()
    {  
        // The state may have moved on (lost sight → Investigate) while this node was
        // Running. A Running node parks the branch, so it has to bail out itself.
        if (Enemy.Value.CurrentState != AIState.Chase) return Status.Failure;

        if (FollowPlayer)
        {
            if(Enemy.Value.AI_Combat.EnemyIn)
                return Status.Success;

            for(int i=0; i<Enemy.Value.AI_Combat.EnemyInSkill.Count;i++)
            {
                if(Enemy.Value.AI_Combat.EnemyInSkill[i]){
                    SkillsScriptableObject skill = Enemy.Value.AI_Combat.GetSeparateSkill(i);
                    if(Enemy.Value.AI_Combat.CanUseSkill(skill,Player)){
                        SkillToUse.Value = skill;
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

        Enemy.Value.AI_Locomotion.SetDestination(Player.Value.transform.position);
            
        // Draw debug line
        Debug.DrawLine(
                Enemy.Value.transform.position + Vector3.up, 
                Player.Value.transform.position + Vector3.up, 
                Color.yellow, 
                UpdateInterval
            );
    }

    Status CheckDistance()
    {        
        float distance = Vector3.Distance(Enemy.Value.transform.position, Player.Value.transform.position);

        if (distance <= StoppingDistance)
        {
            return Status.Success;
        }

        return Status.Running;
    }

    protected override void OnEnd()
    {
        Enemy.Value.AI_Locomotion.ResetPath();
    }
}

