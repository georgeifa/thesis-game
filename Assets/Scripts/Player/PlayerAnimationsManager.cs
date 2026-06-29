using UnityEngine;
using UnityEngine.Animations.Rigging;

/// <summary>
/// Used for actions/methods that affect the visuals of the players character
/// Especially used for the animations in actions like reloading / gun swapping etc
/// Contains event methods that are called from animations
/// </summary>
public class PlayerAnimationsManager : MonoBehaviour
{
    #region Inspector Fields

    [Header("Animator Layers")]
    [SerializeField] private string actionsLayerName = "Actions";

    [Header("Hand IK")]
    // Hand IKs for the the two hands holding the guns
    [SerializeField] private TwoBoneIKConstraint leftHandIK;
    [SerializeField] private TwoBoneIKConstraint rightHandIK;

    [Header("Magazine Reparenting")]
    // The location in the characters hand where the magazine goes 
    [SerializeField] private Transform magHandSocket;

    [Header("Blend Speed")]
    [SerializeField] private float ikBlendSpeed = 25f;
    [SerializeField] private float layerBlendSpeed = 6f;

    #endregion

    #region Private State

    private Animator animator;
    private PlayerCombatController combatController;
    private PlayerEquipmentManager equipmentManager;

    private int actionsLayerIndex;

    // Blend targets — the value each hand IK weight
    private float leftHandIKTargetWeight  = 1f;
    private float rightHandIKTargetWeight = 1f;

    // Cached from the active gun (refreshed on weapon change)
    private GameObject magazineModel;
    private Transform  magSocket;
    private GameObject droppedMagPrefab;
    private float      droppedMagLifetime;

    // Animator parameter hashes
    private static readonly int ReloadHash = Animator.StringToHash("Reload");
    private static readonly int StowHash   = Animator.StringToHash("Stow");
    private static readonly int DrawHash   = Animator.StringToHash("Draw");
    private static readonly int SlotHash   = Animator.StringToHash("Slot");
    private static readonly int CancelActionHash = Animator.StringToHash("CancelAction");
    #endregion

    #region Unity Lifecycle

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

    #endregion

    #region Actions Mehtods

        #region Reload Related Methods

    /// <summary>Fires the reload animation. Called by PlayerCombatController.</summary>
    public void TriggerReload()
    {
        animator.ResetTrigger(CancelActionHash);
        animator.SetTrigger(ReloadHash);
    }
    /// <summary>
    /// Called by PlayerCombatController when a reload is interrupted.
    /// Returns the gun to the holder, reseats the mag, and restores both hands.
    /// </summary>
    public void AbortReload()
    {
        animator.SetTrigger(CancelActionHash);

        equipmentManager.ReturnWeaponToHolder();
        ReseatMag();

        SetRightHandIK(1f);
        SetLeftHandIK(1f);

        if (magazineModel != null)
            magazineModel.SetActive(true);
    }

    #endregion

        #region Switch Related Methods

    /// <summary>
    /// Fires the stow animation for the given slot. Slot is set BEFORE the
    /// trigger so the two-condition transition (trigger + Slot) routes correctly.
    /// </summary>
    public void TriggerStow(EquipmentSlot slot)
    {
        animator.ResetTrigger(CancelActionHash);  // clear any pending cancel from an interrupted reload

        animator.SetInteger(SlotHash, (int)slot);
        animator.SetTrigger(StowHash);
    }

    /// <summary>Fires the draw animation for the given slot (Slot first, then trigger).</summary>
    public void TriggerDraw(EquipmentSlot slot)
    {
        animator.SetInteger(SlotHash, (int)slot);
        animator.SetTrigger(DrawHash);
    }

    #endregion

    #endregion

    #region Animation Events

        #region Reload

    // The reload clip fires these in order. Each is a single visual step;
    // the frame label is the order of the events in the clip

    /// <summary>
    /// Frame 0 — Reload BEGINS.
    /// Disable IKs and attach the weapon to the right hand
    /// </summary>
    public void Reload_HandOffToAnimation()
    {
        if (combatController.currentState != CombatState.Reloading) return;
        equipmentManager.AttachWeaponToHand();
        SetRightHandIK(0f);
        SetLeftHandIK(0f);
    }

    /// <summary>
    /// Frame A — Hand GRABS the magazine. Reparent the mag to the hand so
    /// it pulls free with the hand as the animation drives it
    /// </summary>
    public void Reload_GrabMag()
    {
        if (combatController.currentState != CombatState.Reloading) return;
        DetachMagToHand();
    }

    /// <summary>
    /// Frame B — Spawn a physics clone of the mag at its current position and
    /// hide the real mesh to create the illusion of dropping the magazine to the ground
    /// </summary>
    public void Reload_DropMag()
    {
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

    /// <summary>
    /// Frame C — Reenable the magazine mesh in hand.
    /// To create the illusion of grabbing a new magazine from the waist
    /// </summary>
    public void Reload_GrabNewMag()
    {
        if (magazineModel != null)
            magazineModel.SetActive(true);

        combatController.OnNewMagGrabbed();
    }

    /// <summary>Frame D — Seat the mag back into the gun (reparent to MagSocket).</summary>
    public void Reload_SeatMag() => ReseatMag();

    /// <summary>
    /// Frame E — Reload COMPLETE.
    /// Detach the gun from the right hand and re-enable the IKs
    /// </summary>
    public void Reload_HandBackToIK()
    {
        equipmentManager.ReturnWeaponToHolder();
        SetRightHandIK(1f);
        SetLeftHandIK(1f);

        combatController.OnReloadAnimationEnd();
    }

    #endregion

        #region Weapon Switch

    /// <summary>
    /// Stow begin — attach the active gun to the hand and hand the right hand to
    /// the animation, so the gun follows the hand toward the body.
    /// </summary>
    public void Switch_StowBegin()
    {
        equipmentManager.AttachWeaponToHand();
        SetRightHandIK(0f);
        SetLeftHandIK(0f);
    }

    /// <summary>Stow reparent (mid) — blink the gun from the hand onto its body socket.</summary>
    public void Switch_StowReparent()
    {
        equipmentManager.StowToBodySocket(combatController.SwitchOutgoingSlot);
    }

    /// <summary>Stow complete (end) — swap active slot and start the draw.</summary>
    public void Switch_StowComplete()
    {
        combatController.OnStowComplete();
    }

    /// <summary>Draw begin — hand the right hand to the animation for the draw.</summary>
    public void Switch_DrawBegin()
    {
        SetRightHandIK(0f);
        SetLeftHandIK(0f);

    }

    /// <summary>
    /// Draw reparent (mid) — blink the now-active gun from its body socket into
    /// the hand so it rides the hand out to ready.
    /// </summary>
    public void Switch_DrawReparent()
    {
        equipmentManager.AttachWeaponToHand();
    }

    /// <summary>Draw complete (end) — gun to holder, hand back to IK, switch done.</summary>
    public void Switch_DrawComplete()
    {
        equipmentManager.ReturnWeaponToHolder();
        SetRightHandIK(1f);
        SetLeftHandIK(1f);
        combatController.OnDrawComplete();
    }

    #endregion
   
    #endregion

    #region Helper Methods

        #region Actions Layer Weight

    // Blends the animation Actions layer in and out depending on if we want to do an action or not
    private void UpdateActionsLayerWeight()
    {
        bool actionActive =
            combatController.currentState == CombatState.Reloading ||
            combatController.currentState == CombatState.SwitchingWeapon;

        float target  = actionActive ? 1f : 0f;
        float current = animator.GetLayerWeight(actionsLayerIndex);
        animator.SetLayerWeight(actionsLayerIndex,
            Mathf.MoveTowards(current, target, Time.deltaTime * layerBlendSpeed));
    }

    #endregion

        #region Hand IKs

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

        private void SetLeftHandIK(float weight)
        {
            leftHandIKTargetWeight = weight;
        }
    private void SetRightHandIK(float weight) => rightHandIKTargetWeight = weight;

    #endregion

        #region Magazine Reparenting

    // Attach the mag to the hand so it can follow the animation
    private void DetachMagToHand()
    {
        if (magazineModel == null || magHandSocket == null) return;
        magazineModel.transform.SetParent(magHandSocket, worldPositionStays: false);
        magazineModel.transform.localPosition = Vector3.zero;
        magazineModel.transform.localRotation = Quaternion.identity;
    }

    // Detach the mag from the hands so it can be controlled from the IKs
    private void ReseatMag()
    {

        if (magazineModel == null || magSocket == null) return;
        magazineModel.transform.SetParent(magSocket);
        magazineModel.transform.localPosition = Vector3.zero;
        magazineModel.transform.localRotation = Quaternion.identity;

    }

    #endregion

        #region Gun Reference Caching

    // Refreshes the per-gun references whenever the active weapon changes.
    private void UpdateGunReferences(Gun newGun)
    {
        if (newGun == null) return;

        magazineModel      = newGun.References.MagazineModel;
        magSocket          = newGun.References.MagSocket;
        droppedMagPrefab   = newGun.gunData.DroppedMagPrefab;
        droppedMagLifetime = newGun.gunData.droppedMagLifetime;

        if (magazineModel != null)
            magazineModel.SetActive(true);
    }

    #endregion

    #endregion

}