using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class AI_Locomotion : MonoBehaviour
{
    [Header("Movement")]
    public float walkSpeed = 1.5f;
    public float chaseSpeed = 4.5f;
    public float rotationSpeed = 180f;
    
    [Header("Turn Settings")]
    public float turnSlowFactor = 0.5f;
    public float maxTurnAngle = 45f;
    
    [Header("Animation Parameters")]
    public string speedParam = "Speed";
    public string isMovingParam = "IsMoving";
    public string LookingAroundParam = "LookingAround";


    
    private NavMeshAgent agent;
    private Animator animator;
    private Enemy Enemy;
    
    private float currentSpeed = 0f;
    private float speedDamp = 0f;
    private bool useRootMotion = false;
    
    void Awake()
    {
        Enemy = GetComponent<Enemy>();
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        
        agent.updateRotation = false;
        agent.updatePosition = false;
    }
    
    void Update()
    {
        int a = Random.Range(1,3);
        if (useRootMotion) return;
        
        if (agent.desiredVelocity.magnitude > 0.1f && !agent.isStopped)
        {
            UpdateMovement();
        }
        else
        {
            currentSpeed = 0f;
            animator.SetBool(isMovingParam, false);
        }
        
        // Sync transform with agent
        if (!agent.isStopped)
            transform.position = agent.nextPosition;
    }
    
    void UpdateMovement()
    {
        // Calculate turn angle
        float turnAngle = Vector3.Angle(transform.forward, agent.desiredVelocity.normalized);
        
        
        // Apply turn-based speed reduction
        float speedMultiplier = turnAngle > maxTurnAngle ? turnSlowFactor : 1f;
        float targetSpeed = agent.desiredVelocity.magnitude * speedMultiplier;
        
        // Smooth speed
        currentSpeed = Mathf.SmoothDamp(currentSpeed, targetSpeed, ref speedDamp, 0.1f);
        
        // Align velocity during sharp turns
        if (turnAngle > 30f)
        {
            agent.velocity = transform.forward * currentSpeed;
        }
        else
        {
            agent.velocity = agent.desiredVelocity.normalized * currentSpeed;
        }
        
        // Rotate toward target
        Quaternion targetRot = Quaternion.LookRotation(agent.desiredVelocity.normalized);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        
        // Update animator
        float animSpeed = currentSpeed / GetCurrentMaxSpeed();
        animator.SetFloat(speedParam, animSpeed);
        animator.SetBool(isMovingParam, animSpeed > 0.1f);
    }
    
    void OnAnimatorMove()
    {
        if (animator.applyRootMotion)
        {
            transform.position += animator.deltaPosition;
            transform.rotation *= animator.deltaRotation;
            agent.nextPosition = transform.position;
        }
    }
    
    // ========== PUBLIC METHODS ==========
    
    public void SetDestination(Vector3 destination)
    {
        if (!useRootMotion && !agent.isStopped)
            agent.SetDestination(destination);
    }
    
    public void Stop()
    {
        agent.isStopped = true;
        agent.velocity = Vector3.zero;
        currentSpeed = 0f;
        animator.SetBool(isMovingParam, false);
    }
    
    public void Resume() => agent.isStopped = false;

    public void Reset() {ResetPath(); Resume(); } 

    public void ResetPath() => agent.ResetPath();
    
    private float GetCurrentMaxSpeed()
    {
        switch (Enemy.CurrentState)
        {
            case AIState.Chase: return chaseSpeed;
            case AIState.Patrol: return walkSpeed;
            default: return walkSpeed;
        }
    }
    
    public void SetRootMotionMode(bool enable)
    {
        useRootMotion = enable;
        agent.isStopped = enable;
        animator.applyRootMotion = enable;
        
        if (!enable)
        {
            agent.nextPosition = transform.position;
            agent.velocity = Vector3.zero;
        }
    }

    public void FinishLookingAround()
    {
        animator.SetInteger(LookingAroundParam,0);
    }
}