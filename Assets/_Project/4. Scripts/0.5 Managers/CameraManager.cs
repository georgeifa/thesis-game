using UnityEngine;
using Unity.Cinemachine;

/// <summary>
/// Switches the active camera to the drop pod during a deployment, then back
/// to the player. Cinemachine blends between them automatically.
/// </summary>
public class CameraManager : MonoBehaviour
{
    [SerializeField] private CinemachineCamera dropCamera;
    [Tooltip("Above the gameplay camera's priority.")]
    [SerializeField] private int activePriority = 20;
    [SerializeField] private int inactivePriority = 0;

    public void FollowPod(Transform pod)
    {
        dropCamera.Follow = pod;
        dropCamera.Priority = activePriority;
    }

    public void ReturnToPlayer()
    {
        dropCamera.Priority = inactivePriority;
        dropCamera.Follow = null;
    }
}