using System;
using MyBox;
using Unity.Behavior;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;


/// <summary>
/// ScriptableObject that holds the BASE STATS for an enemy. These can then be modified at object creation time to buff up enemies
/// and to reset their stats if they died or were modified during runtime
/// </summary>
[CreateAssetMenu(fileName = "Enemy Config", menuName = "Enemy/Enemy Config")]
public class EnemyScriptableObject : ScriptableObject
{
    public Enemy Prefab;

    public int Health = 100;

    [Header("Movement")]
    public float walkSpeed = 1.5f;
    public float chaseSpeed = 4.5f;
    public float rotationSpeed = 180f;
    
    [Header("Turn Settings")]
    public float turnSlowFactor = 0.5f;
    public float maxTurnAngle = 45f;

    [Header("Utility Skills")]
    public SkillsScriptableObject UtilitySkill;
    
    [Header("Animation Parameters")]
    public string speedParam = "Speed";
    public string isMovingParam = "IsMoving";
    public string ResetParam = "Reset";
    public string HitDirectionParam = "HitDirection";

    [Space]
    public float UnlockEnemySkillsAfter;


    
    public NavMeshAgentConfigurationScriptableObject NavMeshAgentConfig;
    public FOVConfigScriptableObject FOVConfig;
    public CombatConfigurationScriptableObject CombatConfig;
    public DeathScriptableObject DeathSO;

    public void SetupEnemy(Enemy enemy, GameObject player)
    {
        enemy.CurrentState = AIState.Idle;
        enemy.Health = Health;
        enemy.DeathSO = DeathSO;

        enemy.AI_Locomotion.walkSpeed = walkSpeed;
        enemy.AI_Locomotion.chaseSpeed = chaseSpeed;
        enemy.AI_Locomotion.rotationSpeed = rotationSpeed;


        enemy.AI_Locomotion.turnSlowFactor = turnSlowFactor;
        enemy.AI_Locomotion.maxTurnAngle = maxTurnAngle;

        enemy.UtilitySkill = UtilitySkill;
        enemy.hasUtilitySkill = UtilitySkill != null;

        enemy.ResetParam = ResetParam;
        enemy.HitDirectionParam = HitDirectionParam;

        enemy.AI_Locomotion.speedParam = speedParam;
        enemy.AI_Locomotion.isMovingParam = isMovingParam;



        NavMeshAgentConfig.Setup_NavMeshAgentConfig(enemy);
        FOVConfig.Setup_FOVConfig(enemy,player);
        CombatConfig.Setup_CombatConfig(enemy);


        Setup_BlackboardConfig(enemy,player);

    }

    private void Setup_BlackboardConfig(Enemy enemy, GameObject player)
    {
        enemy.behavior.SetVariableValue("Player",player);
        enemy.behavior.SetVariableValue("UnlockEnemySkillsAfter",UnlockEnemySkillsAfter);
        enemy.behavior.SetVariableValue("FOV",enemy.FOV);
        enemy.behavior.SetVariableValue("AI Combat",enemy.AI_Combat);
        enemy.behavior.SetVariableValue("Enemy",enemy);
        enemy.behavior.SetVariableValue("Follow Player",true);

    }
}
