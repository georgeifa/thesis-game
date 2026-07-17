using System;
using UnityEngine;

[CreateAssetMenu(fileName = "FOV Config", menuName = "Enemy/FOV Config")]
public class FOVConfigScriptableObject : ScriptableObject
{
    public float interval = 0.1f;

    public float radius;
    [Range(0,360)]
    public int DetectionAngle;

    public LayerMask targetMask;
    public LayerMask obstructionMask;

    public void Setup_FOVConfig(Enemy enemy, GameObject player)
    {
        enemy.FOV.interval = interval;
        enemy.FOV.radius = radius;
        enemy.FOV.DetectionAngle = DetectionAngle;

        enemy.FOV.targetMask = targetMask;
        enemy.FOV.obstructionMask = obstructionMask;
        enemy.FOV.playerRef = player;

        enemy.FOV.PerformFOVCheck += FieldOfViewCheck;
        enemy.FOV.ResetAngle += ResetFOVAngle;
        enemy.FOV.MaxAngle += MaximizeFOVAngle;

    }

    private bool FieldOfViewCheck(Transform Transform, float Radius, LayerMask TargetMask, float Angle)
    {
        Collider[] rangeChecks = new Collider[10];
        float n_Radius = Helpers.RangeWithColliderOffset(Transform.gameObject, Radius);
        int numColliders = Physics.OverlapSphereNonAlloc(Transform.position, n_Radius, rangeChecks, TargetMask);
        
        // Early exit if no targets found
        if (numColliders == 0)
            return false;

        Transform target = rangeChecks[0].transform;
        Vector3 directionToTarget = target.position - Transform.position;
        
        float sqrDistanceToTarget = directionToTarget.sqrMagnitude;
        float sqrRadius = n_Radius * n_Radius;
        
        // Check if target is within radius using squared distance
        if (sqrDistanceToTarget > sqrRadius)
            return false;

        directionToTarget.Normalize();
        
        float dot = Vector3.Dot(Transform.forward, directionToTarget);
        
        float detectionDotThreshold = Mathf.Cos(Angle * 0.5f * Mathf.Deg2Rad);
        
        // Check if target is within field of view
        if (dot < detectionDotThreshold)
            return false;

        // Check line of sight with actual distance
        float distanceToTarget = Mathf.Sqrt(sqrDistanceToTarget);
        if (Physics.Raycast(Transform.position, directionToTarget, distanceToTarget, obstructionMask))
            return false;

        // If we got here, player is detected and visible
        return true;
    }

    private int ResetFOVAngle()
    {
        return DetectionAngle;
    }

    private int MaximizeFOVAngle()
    {
        return 360;
    }


}
