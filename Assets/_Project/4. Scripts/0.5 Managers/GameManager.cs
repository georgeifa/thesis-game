using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Run-level state: how many soldiers remain, when the next deploys, and when
/// the run ends. Lives live here because they outlast any single soldier.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Lives")]
    [SerializeField] private int startingLives = 4;

    [Header("Deployment")]
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private DropPod dropPodPrefab;
    [Tooltip("Pause after death before the reinforcement launches.")]
    [SerializeField] private float launchDelay = 1.5f;
    [Tooltip("Leave spent pods in the world as scenery.")]
    [SerializeField] private bool keepSpentPods = true;
    [Tooltip("If not kept, seconds before removal.")]
    [SerializeField] private float spentPodLifetime = 5f;

    [Header("References")]
    [SerializeField] private PlayerDeathHandler player;
    [SerializeField] private CameraManager deploymentCamera;

    private int livesRemaining;

    public int LivesRemaining => livesRemaining;
    public bool IsGameOver { get; private set; }

    public event Action<int> OnLivesChanged;
    public event Action OnGameOver;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        livesRemaining = startingLives;
        OnLivesChanged?.Invoke(livesRemaining);

        player.OnSoldierDied += HandleSoldierDied;
    }

    private void OnDestroy()
    {
        if (player != null)
            player.OnSoldierDied -= HandleSoldierDied;
    }

    private void HandleSoldierDied()
    {
        livesRemaining--;
        OnLivesChanged?.Invoke(livesRemaining);

        if (livesRemaining > 0)
            StartCoroutine(DeployAfterDelay());
        else
            EndRun();
    }

    private IEnumerator DeployAfterDelay()
    {
        yield return new WaitForSeconds(launchDelay);

        Vector3 pos = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion rot = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;

        DropPod pod = Instantiate(dropPodPrefab);
        pod.SetDissolveTargets(player.GetVisualRenderers());

        deploymentCamera.FollowPod(pod.transform);

        pod.Drop(
            pos,
            onSoldierPlaced:   () => player.PlaceSoldier(pos, rot),
            onSoldierReleased: () => ReleaseSoldier(pod));
    }

    private void ReleaseSoldier(DropPod pod)
    {
        deploymentCamera.ReturnToPlayer();
        player.ActivateSoldier();
        
        if (!keepSpentPods)
            Destroy(pod.gameObject, spentPodLifetime);
    }

    private void EndRun()
    {
        IsGameOver = true;
        OnGameOver?.Invoke();
        Debug.Log("GAME OVER — no soldiers remaining");
    }
}