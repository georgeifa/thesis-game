using UnityEngine;
using UnityEngine.AI;

[CreateAssetMenu(fileName = "NavMeshAgent Config", menuName = "Enemy/NavMeshAgent Config")]
public class NavMeshAgentConfigurationScriptableObject : ScriptableObject
{
    [Header("NavMeshAgent Properties")]
    [Tooltip("0: Humanoid")]
    public int AgentTypeID = 0;
    public float BaseOffset = 0;

    public float Speed = 3f;
    public float AngularSpeed = 120;
    public float Acceleration = 8;
    public float StoppingDistance = 0.5f;
    public bool AutoBraking = true;

    [Space]
    [Header("Obstacle Avoidance")]
    public float Radius = 0.5f;
    public float Height = 2f;
    public ObstacleAvoidanceType ObstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;
    public int AvoidancePriority = 50;

    [Header("Pathfinding")]
    // -1 means everything
    public int AreaMask = -1;

    public void Setup_NavMeshAgentConfig(Enemy enemy)
    {
        enemy.Agent.agentTypeID = AgentTypeID;
        enemy.Agent.baseOffset = BaseOffset;

        enemy.Agent.speed = Speed;
        enemy.Agent.angularSpeed = AngularSpeed;
        enemy.Agent.acceleration = Acceleration;
        enemy.Agent.stoppingDistance = StoppingDistance;
        enemy.Agent.autoBraking = AutoBraking;

        enemy.Agent.radius = Radius;
        enemy.Agent.height = Height;
        enemy.Agent.obstacleAvoidanceType = ObstacleAvoidanceType;
        enemy.Agent.avoidancePriority = AvoidancePriority;

        enemy.Agent.areaMask = AreaMask;
    }
}
