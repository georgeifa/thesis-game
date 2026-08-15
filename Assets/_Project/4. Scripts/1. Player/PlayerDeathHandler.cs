using System;
using UnityEngine;

/// <summary>
/// The player's side of dying and redeploying. On death the body is left as a
/// ragdoll corpse and the soldier is shut down; on deployment a fresh soldier
/// is placed with the same gear.
///
/// The player GameObject is reused — only the corpse is spawned — so nothing
/// holding a player reference (camera, enemies, managers) breaks.
/// </summary>
public class PlayerDeathHandler : MonoBehaviour
{
    [Header("Corpse")]
    [SerializeField] private GameObject corpsePrefab;
    [Tooltip("0 = corpses stay forever.")]
    [SerializeField] private float corpseLifetime = 0f;
    [Tooltip("Push away from the killing hit.")]
    [SerializeField] private float deathForce = 6f;
    [Tooltip("How much of the push goes upward.")]
    [SerializeField] private float upwardBias = 0.25f;
    [Tooltip("Bone the force is applied to.")]
    [SerializeField] private string torsoBoneName = "mixamorig:Hips";

    [Header("Visual")]
    [Tooltip("The player's Model object — hidden on death, shown on deployment.")]
    [SerializeField] private GameObject playerVisual;

    [Header("Debug")]
    [SerializeField] private bool enableDebugDamage = true;
    [SerializeField] private KeyCode debugDamageKey = KeyCode.K;
    [SerializeField] private int debugDamageAmount = 25;

    private PlayerHealthManager health;
    private PlayerCombatController combat;
    private PlayerControls movement;
    private PlayerInputManager input;
    private PlayerEquipmentManager equipment;
    private CharacterController controller;
    private PlayerAimController aim;

    private Animator animator;

    /// <summary>Raised when this soldier dies — the GameManager decides what follows.</summary>
    public event Action OnSoldierDied;

    /// <summary>Renderers for the materialisation dissolve. Includes inactive — the model is hidden while dead.</summary>
    public Renderer[] GetVisualRenderers() => playerVisual.GetComponentsInChildren<Renderer>(true);

    private void Awake()
    {
        health     = GetComponent<PlayerHealthManager>();
        combat     = GetComponent<PlayerCombatController>();
        movement   = GetComponent<PlayerControls>();
        input      = GetComponent<PlayerInputManager>();
        equipment  = GetComponent<PlayerEquipmentManager>();
        aim        = GetComponent<PlayerAimController>();
        controller = GetComponent<CharacterController>();
        animator   = GetComponent<Animator>();
    }

    private void Start() => health.OnDeath += Die;

    private void OnDestroy()
    {
        if (health != null) health.OnDeath -= Die;
    }

    private void Update()
    {
        if (enableDebugDamage && Input.GetKeyDown(debugDamageKey))
            DebugDamageFromCursor();
    }

    #region Death

    private void Die()
    {
        SpawnCorpse();

        // Disabling the components stops their Update loops, which halts the
        // combat state machine, movement and input.
        input.enabled      = false;
        combat.enabled     = false;
        movement.enabled   = false;
        controller.enabled = false;

        if (playerVisual != null)
            playerVisual.SetActive(false);

        OnSoldierDied?.Invoke();
    }

    private void SpawnCorpse()
    {
        if (corpsePrefab == null) return;

        GameObject corpse = Instantiate(corpsePrefab, transform.position, transform.rotation);

        if (corpseLifetime > 0f)
            Destroy(corpse, corpseLifetime);

        ApplyDeathForce(corpse);
    }

    private void ApplyDeathForce(GameObject corpse)
    {
        if (deathForce <= 0f) return;

        Rigidbody torso = FindTorso(corpse);
        if (torso == null) return;

        // IncomingDirection points from the hit toward the player, so the body
        // is pushed the way the shot was travelling. VelocityChange ignores
        // mass, so the result doesn't depend on the ragdoll's mass setup.
        Vector3 push = (health.IncomingDirection + Vector3.up * upwardBias).normalized;
        torso.AddForce(push * deathForce, ForceMode.VelocityChange);
    }

    // Only the torso is pushed — forcing every rigidbody makes the corpse move
    // as a rigid lump instead of letting the limbs trail.
    private Rigidbody FindTorso(GameObject corpse)
    {
        Rigidbody heaviest = null;

        foreach (Rigidbody rb in corpse.GetComponentsInChildren<Rigidbody>())
        {
            if (rb.name.Contains(torsoBoneName)) return rb;
            if (heaviest == null || rb.mass > heaviest.mass) heaviest = rb;
        }

        return heaviest;   // fallback: the heaviest body is almost always the torso
    }

    #endregion

    #region Deployment

    /// <summary>
    /// Places a fresh soldier and makes them visible, but not yet playable.
    /// Called when materialisation starts, so the character stands on the
    /// platform while the dissolve sweeps in.
    /// </summary>
    public void PlaceSoldier(Vector3 position, Quaternion rotation)
    {
        // The controller is already disabled from Die(); it would otherwise
        // fight the teleport, so it stays off until the transform is set.
        transform.SetPositionAndRotation(position, rotation);
        controller.enabled = true;

        health.ResetHealth();
        equipment.ResetLoadout();

        // The previous soldier may have died mid-reload. Rebind returns every
        // layer and parameter to default so the new one doesn't resume it.
        animator.Rebind();
        animator.Update(0f);

        combat.ResetToIdle();
        aim.ResetAim();

        if (playerVisual != null)
            playerVisual.SetActive(true);
    }

    /// <summary>Hands control to the player. Called when materialisation finishes.</summary>
    public void ActivateSoldier()
    {
        input.enabled    = true;
        combat.enabled   = true;
        movement.enabled = true;
    }

    #endregion

    // Fakes a hit from the cursor's ground position so the death force can be
    // tuned before enemies deal damage.
    private void DebugDamageFromCursor()
    {
        Vector3 hitFrom = transform.position - transform.forward * 2f;

        if (Camera.main != null)
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (new Plane(Vector3.up, transform.position).Raycast(ray, out float dist))
                hitFrom = ray.GetPoint(dist);
        }

        health.GetHitDirection(hitFrom);
        health.TakeDamage(debugDamageAmount);
    }
}