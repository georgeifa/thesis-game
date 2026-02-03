using UnityEngine;

public class AITestController : MonoBehaviour
{
    [Header("Test Settings")]
    public GameObject playerTarget;  // Drag your Player GameObject here
    public float updateInterval = 0.5f;  // How often to update destination
    public bool followPlayer = true;
    public float stopDistance = 2f;  // Distance at which to stop
    
    private AI_Locomotion locomotion;
    private float timer = 0f;
    
    void Start()
    {
        locomotion = GetComponent<AI_Locomotion>();
        
        if (locomotion == null)
        {
            Debug.LogError("AI_Locomotion component not found!");
            enabled = false;
            return;
        }
        
        if (playerTarget == null)
        {
            Debug.LogError("Player target not assigned!");
            enabled = false;
            return;
        }
        
        Debug.Log("AI Test Controller Started");
        Debug.Log("Controls: F = Toggle Follow, G = GoToRandom, Space = Stop");
    }
    
    void Update()
    {
        // Handle test controls
        HandleTestInput();
        
        // Follow player logic
        if (followPlayer)
        {
            timer += Time.deltaTime;
            
            // Update destination at intervals (not every frame for performance)
            if (timer >= updateInterval)
            {
                UpdateDestination();
                timer = 0f;
            }
            
            // Check if we're close enough to stop
            CheckDistance();
        }
    }
    
    void HandleTestInput()
    {
        // Toggle follow player
        if (Input.GetKeyDown(KeyCode.F))
        {
            followPlayer = !followPlayer;
            Debug.Log($"Follow Player: {followPlayer}");
            
            if (!followPlayer)
            {
                locomotion.Stop();
            }
        }
        
        // Go to random point
        if (Input.GetKeyDown(KeyCode.G))
        {
            GoToRandomPoint();
        }
        
        // Stop movement
        if (Input.GetKeyDown(KeyCode.Space))
        {
            locomotion.Stop();
            followPlayer = false;
            Debug.Log("Movement stopped");
        }
        
        // Quick teleport player for testing
        if (Input.GetKeyDown(KeyCode.T))
        {
            Vector3 randomPoint = new Vector3(
                Random.Range(-10f, 10f),
                0,
                Random.Range(-10f, 10f)
            );
            playerTarget.transform.position = randomPoint;
            Debug.Log($"Player teleported to: {randomPoint}");
        }
    }
    
    void UpdateDestination()
    {
        if (playerTarget != null && locomotion != null)
        {
            locomotion.SetDestination(playerTarget.transform.position);
            
            // Optional: Draw debug line
            Debug.DrawLine(
                transform.position + Vector3.up, 
                playerTarget.transform.position + Vector3.up, 
                Color.yellow, 
                updateInterval
            );
        }
    }
    
    void CheckDistance()
    {
        if (playerTarget == null) return;
        
        float distance = Vector3.Distance(transform.position, playerTarget.transform.position);
        
        if (distance <= stopDistance)
        {
            Debug.Log($"Reached stop distance: {distance}");
        }
    }
    
    void GoToRandomPoint()
    {
        Vector3 randomPoint = transform.position + new Vector3(
            Random.Range(-10f, 10f),
            0,
            Random.Range(-10f, 10f)
        );
        
        locomotion.SetDestination(randomPoint);
        Debug.Log($"Going to random point: {randomPoint}");
        
        // Draw the path
        Debug.DrawLine(transform.position + Vector3.up, randomPoint + Vector3.up, Color.green, 5f);
    }
    
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        
        // Draw stop distance sphere
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stopDistance);
        
        // Draw forward vector
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, transform.forward * 2);
        
        // Draw line to player
        if (playerTarget != null)
        {
            Gizmos.color = followPlayer ? Color.green : Color.gray;
            Gizmos.DrawLine(transform.position, playerTarget.transform.position);
        }
    }
}