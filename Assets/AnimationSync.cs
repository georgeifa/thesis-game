using UnityEngine;

public class AnimationSync : MonoBehaviour
{
    [Header("References")]
    public Transform animatedModel;  // Drag your animated mesh here
    public bool syncPosition = true;
    public bool syncRotation = false;
    
    [Header("Debug")]
    public Vector3 lastWorldPosition;
    public Vector3 totalMovement;
    
    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;
    private Animator animator;
    
    void Start()
    {
        // Auto-find animator if not assigned
        if (animatedModel == null)
        {
            animator = GetComponentInChildren<Animator>();
            if (animator != null) animatedModel = animator.transform;
        }
        else
        {
            animator = animatedModel.GetComponent<Animator>();
        }
        
        if (animatedModel != null)
        {
            initialLocalPosition = animatedModel.localPosition;
            initialLocalRotation = animatedModel.localRotation;
            lastWorldPosition = animatedModel.position;
            
            // CRITICAL: Disable root motion for generic animations
            if (animator != null)
            {
                animator.applyRootMotion = false;
            }
            
            Debug.Log($"Initialized: {animatedModel.name} | Local Pos: {initialLocalPosition}");
        }
        else
        {
            Debug.LogError("No animated model found!");
            enabled = false;
        }
    }
    
    void Update()
    {
        if (animatedModel == null) return;
        
        // OPTION 1: Direct sync (simplest)
        SyncMethod1();
        
        // OR OPTION 2: Smooth sync (uncomment to use)
        // SyncMethod2();
    }
    
    // Method 1: Direct parent following
    void SyncMethod1()
    {
        // Get current world position of animated model
        Vector3 currentWorldPos = animatedModel.position;
        
        // Calculate movement delta
        Vector3 delta = currentWorldPos - transform.position;
        
        // Apply to parent (this GameObject with collider)
        transform.position += delta;
        
        // Reset model to initial local position
        animatedModel.localPosition = initialLocalPosition;
        
        // Handle rotation if needed
        if (syncRotation)
        {
            Quaternion deltaRot = animatedModel.rotation * Quaternion.Inverse(transform.rotation);
            transform.rotation *= deltaRot;
            animatedModel.localRotation = initialLocalRotation;
        }
    }
    
    // Method 2: Track and apply movement
    void SyncMethod2()
    {
        Vector3 currentWorldPos = animatedModel.position;
        
        // How much has the model moved since last frame?
        Vector3 frameMovement = currentWorldPos - lastWorldPosition;
        
        // Apply that movement to the parent
        transform.position += frameMovement;
        totalMovement += frameMovement;
        
        // Always reset model to its initial local position
        animatedModel.localPosition = initialLocalPosition;
        animatedModel.localRotation = initialLocalRotation;
        
        // Update tracking
        lastWorldPosition = animatedModel.position;
    }
    
    // Method 3: Physics-friendly version (for Rigidbody)
    void SyncWithRigidbody()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            Vector3 currentWorldPos = animatedModel.position;
            Vector3 targetVelocity = (currentWorldPos - transform.position) / Time.deltaTime;
            
            rb.linearVelocity = targetVelocity;
            animatedModel.localPosition = initialLocalPosition;
        }
    }
    
    void OnDrawGizmosSelected()
    {
        if (animatedModel != null && Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, animatedModel.position);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(animatedModel.position, 0.1f);
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 0.15f);
        }
    }
}
