using UnityEngine;

public class Debug_RootAttackDistance : MonoBehaviour
{
    [Header("Quick Measurement")]
    public bool autoMeasureOnStart = true;
    public AnimationClip attackAnimation;
    
    private Vector3 startPos;
    private Quaternion startRot;

    private Animator animator;
    
    void Start()
    {
        animator = GetComponent<Animator>();

        animator.applyRootMotion = true;

        if (autoMeasureOnStart && attackAnimation != null)
        {
            MeasureAttackDistance();
        }
    }

    void OnAnimatorMove()
    {
        if (animator.applyRootMotion)
        {
            transform.position += animator.deltaPosition;
            transform.rotation *= animator.deltaRotation;
        }
    }
    
    public void MeasureAttackDistance()
    {
        startPos = transform.position;
        startRot = transform.rotation;
        
        // Record animation
        animator.Play(attackAnimation.name);
        
        // Wait for animation to complete and measure
        Invoke(nameof(RecordMeasurement), attackAnimation.length);
    }
    
    void RecordMeasurement()
    {
        Vector3 endPos = transform.position;
        float forwardDistance = Vector3.Dot(endPos - startPos, startRot * Vector3.forward);
        
        Debug.Log($"<b>Attack Distance Measurement:</b>");
        Debug.Log($"• Animation: {attackAnimation.name}");
        Debug.Log($"• Duration: {attackAnimation.length:F2}s");
        Debug.Log($"• Forward Movement: {forwardDistance:F2}m");
        Debug.Log($"• Total Movement: {Vector3.Distance(startPos, endPos):F2}m");
        
        // Reset position
        transform.position = startPos;
        transform.rotation = startRot;
    }
}
