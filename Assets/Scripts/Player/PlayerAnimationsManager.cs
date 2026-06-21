using UnityEngine;
using UnityEngine.Animations.Rigging;

public class PlayerAnimationsManager : MonoBehaviour
{
    // ─────────────────────────────────────────────
    //  Inspector References
    // ─────────────────────────────────────────────

    [Header("Animator Layers")]
    [SerializeField] private string actionsLayerName = "Actions";

    [Header("Hand IK")]
    // Both hands hand off to the animation during a scripted action.
    // The reload clip already contains the correct hand motion, so IK just
    // blends out at the start and back in at the end — no target switching.
    [SerializeField] private TwoBoneIKConstraint leftHandIK;
    [SerializeField] private TwoBoneIKConstraint rightHandIK;

    [Header("Magazine Reparenting")]
    // The mag is reparented to the left hand on grab and back to the gun's
    // MagSocket on seat. The mag sits at local zero under MagSocket, so
    // reseating is just "parent + zero" — no offset or recorded home needed.
    [SerializeField] private Transform magHandSocket;

    [Header("Blend Speed")]
    [SerializeField] private float ikBlendSpeed = 25f;
    [SerializeField] private float layerBlendSpeed = 6f;

    // ─────────────────────────────────────────────
    //  Private State
    // ─────────────────────────────────────────────

    private Animator animator;
    private PlayerCombatController combatController;
    private PlayerEquipmentManager equipmentManager;

    private int actionsLayerIndex;

    // Blend targets (the value each hand IK weight is moving toward)
    private float leftHandIKTargetWeight  = 1f;
    private float rightHandIKTargetWeight  = 1f;

    // Cached from the active gun
    private GameObject magazineModel;
    private GameObject droppedMagPrefab;
    private float droppedMagLifetime;
    private Transform magSocket;

    // Animator parameter hashes
    private static readonly int ReloadHash = Animator.StringToHash("Reload");

    // ─────────────────────────────────────────────
    //  Unity Lifecycle
    // ─────────────────────────────────────────────

    private void Awake()
    {
        animator         = GetComponent<Animator>();
        combatController = GetComponent<PlayerCombatController>();
        equipmentManager = GetComponent<PlayerEquipmentManager>();

        equipmentManager.OnGunChanged += UpdateGunReferences;
    }

    private void Start()
    {
        actionsLayerIndex = animator.GetLayerIndex(actionsLayerName);

        if (equipmentManager.ActiveGun != null)
            UpdateGunReferences(equipmentManager.ActiveGun.GetComponent<Gun>());
    }

    private void Update()
    {
        UpdateActionsLayerWeight();
        BlendLeftHandIK();
        BlendRightHandIK();
    }

    private void OnDestroy()
    {
        if (equipmentManager != null)
            equipmentManager.OnGunChanged -= UpdateGunReferences;
    }

    // ─────────────────────────────────────────────
    //  Actions Layer Weight
    // ─────────────────────────────────────────────

    private void UpdateActionsLayerWeight()
    {
        float target  = combatController.currentState == CombatState.Reloading ? 1f : 0f;
        float current = animator.GetLayerWeight(actionsLayerIndex);
        animator.SetLayerWeight(actionsLayerIndex,
            Mathf.MoveTowards(current, target, Time.deltaTime * layerBlendSpeed));
    }

    // ─────────────────────────────────────────────
    //  Hand IK (simple on/off blends)
    // ─────────────────────────────────────────────

    private void BlendLeftHandIK()
    {
        if (leftHandIK == null) return;
        leftHandIK.weight = Mathf.MoveTowards(
            leftHandIK.weight, leftHandIKTargetWeight, Time.deltaTime * ikBlendSpeed);
    }

    private void BlendRightHandIK()
    {
        if (rightHandIK == null) return;
        rightHandIK.weight = Mathf.MoveTowards(
            rightHandIK.weight, rightHandIKTargetWeight, Time.deltaTime * ikBlendSpeed);
    }

    private void SetLeftHandIK(float weight)  => leftHandIKTargetWeight  = weight;
    private void SetRightHandIK(float weight) => rightHandIKTargetWeight = weight;

    // ─────────────────────────────────────────────
    //  Magazine Reparenting
    // ─────────────────────────────────────────────

    /// <summary>
    /// Parents the mag to the left hand, keeping its current world position so
    /// it doesn't jump — it grabs from wherever it currently sits on the gun
    /// and rides the (animation-driven) hand from there.
    /// </summary>
    private void DetachMagToHand()
    {
        if (magazineModel == null || magHandSocket == null) return;
        magazineModel.transform.SetParent(magHandSocket, worldPositionStays: false);
        magazineModel.transform.localPosition = Vector3.zero;
        magazineModel.transform.localRotation = Quaternion.identity;
    }

    /// <summary>
    /// Parents the mag back to the gun's MagSocket at local zero, which is its
    /// authored seated position. No offset needed — the socket is the home.
    /// </summary>
    private void ReseatMag()
    {
        if (magazineModel == null || magSocket == null) return;
        magazineModel.transform.SetParent(magSocket);
        magazineModel.transform.localPosition = Vector3.zero;
        magazineModel.transform.localRotation = Quaternion.identity;
    }

    // ─────────────────────────────────────────────
    //  Gun Reference Update (fired on weapon switch)
    // ─────────────────────────────────────────────

    private void UpdateGunReferences(Gun newGun)
    {
        if (newGun == null) return;

        magazineModel = newGun.References.MagazineModel;
        magSocket     = newGun.References.MagSocket;
        droppedMagPrefab = newGun.gunData.DroppedMagPrefab;
        droppedMagLifetime = newGun.gunData.droppedMagLifetime;

        if (magazineModel != null)
            magazineModel.SetActive(true);
    }

    // ─────────────────────────────────────────────
    //  Animator Trigger
    // ─────────────────────────────────────────────

    public void TriggerReload() => animator.SetTrigger(ReloadHash);

    // ─────────────────────────────────────────────
    //  Reload Abort / Cleanup
    // ─────────────────────────────────────────────

    /// <summary>
    /// Called by PlayerCombatController when a reload is interrupted.
    /// Returns the gun to the holder, reseats the mag, restores both hands.
    /// </summary>
    public void AbortReload()
    {
        equipmentManager.ReturnWeaponToHolder();
        ReseatMag();

        SetRightHandIK(1f);
        SetLeftHandIK(1f);

        if (magazineModel != null)
            magazineModel.SetActive(true);
    }

    // ─────────────────────────────────────────────
    //  Animation Events (placed on the reload clip)
    // ─────────────────────────────────────────────

    // ── Frame 0 ──────────────────────────────────
    /// <summary>
    /// Reload begins. Parent the gun into the right hand and hand BOTH hands
    /// over to the animation (IK → 0). The clip drives the hand motion; no IK
    /// fights it, so the hands follow the animation cleanly.
    /// </summary>
    public void Reload_HandOffToAnimation()
    {
        equipmentManager.AttachWeaponToHand();
        SetRightHandIK(0f);
        SetLeftHandIK(0f);
    }

    // ── Frame X ──────────────────────────────────
    /// <summary>
    /// Hand has reached the magazine — reparent the mag to the left hand so it
    /// pulls free with the hand as the animation drives it.
    /// </summary>
    public void Reload_GrabMag() => DetachMagToHand();

    // ── Frame Z ──────────────────────────────────
    /// <summary>
    /// Spawn a physics clone of the mag at its current position and hide the
    /// real mesh; the clone handles the visual drop to the ground.
    /// </summary>
    public void Reload_DropMag()
    {
        Debug.Log("Here, magazine model" + magazineModel);
        Debug.Log("Here, droppedMagPrefab" + droppedMagPrefab);

        if (magazineModel == null || droppedMagPrefab == null) return;

        GameObject dropped = Instantiate(
            droppedMagPrefab,
            magazineModel.transform.position,
            magazineModel.transform.rotation);

        Rigidbody rb = dropped.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity  = transform.forward * -0.5f + Vector3.up * 0.3f;
            rb.angularVelocity = Random.insideUnitSphere * 3f;
        }

        dropped.layer = LayerMask.NameToLayer("Debris");
        Destroy(dropped, droppedMagLifetime);

        magazineModel.SetActive(false);
    }

    // ── Frame E ──────────────────────────────────
    /// <summary>
    /// Re-enable the mag mesh as the "new" magazine from the hip and refill
    /// ammo. Safe even if interrupted after this point. The mesh is still
    /// parented to the hand here, so it appears in the hand as the new mag.
    /// </summary>
    public void Reload_GrabNewMag()
    {
        if (magazineModel != null)
            magazineModel.SetActive(true);

        combatController.OnNewMagGrabbed();
    }

    // ── Frame Y (second) ─────────────────────────
    /// <summary>
    /// Mag is seated back into the gun — reparent it to the gun's MagSocket.
    /// </summary>
    public void Reload_SeatMag() => ReseatMag();

    // ── Frame O (last frame) ─────────────────────
    /// <summary>
    /// Reload complete. Return the gun to the holder, hand both hands back to
    /// IK, and tell the combat controller the reload is finished.
    /// </summary>
    public void Reload_HandBackToIK()
    {
        equipmentManager.ReturnWeaponToHolder();

        SetRightHandIK(1f);
        SetLeftHandIK(1f);

        combatController.OnReloadAnimationEnd();
    }
}