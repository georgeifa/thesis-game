using System.Collections;
using MyBox;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;


public enum CombatState
{
    None,
    Idle,
    Shooting,
    Reloading,
    SwitchingWeapon
}

public class PlayerCombatController : MonoBehaviour
{
    public CombatState currentState;

    private Animator animator;

    private Gun activeGun;
    private PlayerAimController aimController;
    private PlayerEquipmentManager equipmentManager;
    private PlayerInputManager inputManager;
    private PlayerAnimationsManager animationsManager;


    private bool wantsToShoot;
    private bool reloadPressed;
    private bool switchPressed;
    private EquipmentSlot nextSlot;

    // timing
    [SerializeField] private float equipTime = 0.4f;
    [SerializeField] private float switchCooldown = 0.15f;

    // for semi-auto behavior
    private bool wasHoldingFireLastFrame;

#region Initializations
    private void Awake()
    {
        InitializeComponents();
        equipmentManager.OnGunChanged += HandleGunChanged;
    }

    private void InitializeComponents()
    {
        if (equipmentManager == null)
            equipmentManager = GetComponent<PlayerEquipmentManager>();
        if (equipmentManager == null) { Debug.LogError("PlayerEquipment is missing!"); return; }

        if (inputManager == null)
            inputManager = GetComponent<PlayerInputManager>();
        if (inputManager == null) { Debug.LogError("PlayerInputManager is missing!"); return; }

        if (aimController == null)
            aimController = GetComponent<PlayerAimController>();
        if (aimController == null) { Debug.LogError("PlayerAimController is missing!"); return; }

        if (animator == null)
            animator = GetComponent<Animator>();
        if (animator == null) { Debug.LogError("Animator is missing!"); return; }

        if (animationsManager == null)
            animationsManager = GetComponent<PlayerAnimationsManager>();
        if (animationsManager == null) { Debug.LogError("PlayerAnimationsManager is missing!"); return; }
    }

    void Start()
    {
        if (equipmentManager.ActiveGun != null)
            HandleGunChanged(equipmentManager.ActiveGun.GetComponent<Gun>());
    }
#endregion

#region Input Calls

    public void SetFire(bool pressed) => wantsToShoot = pressed;
    public void SetReload()           => reloadPressed = true;

#endregion

    private void Update()
    {
        HandleState();
        UpdateGun();

        reloadPressed = false;
        switchPressed = false;

        wasHoldingFireLastFrame = wantsToShoot;
    }

    private void UpdateGun()
    {
        if (activeGun == null) return;

        bool allowShooting = currentState == CombatState.Shooting;
        activeGun.Tick(allowShooting);
    }

    private void HandleState()
    {
        switch (currentState)
        {
            case CombatState.None:            SetState(CombatState.Idle); break;
            case CombatState.Idle:            HandleIdle();               break;
            case CombatState.Shooting:        HandleShooting();           break;
            case CombatState.Reloading:       HandleReloading();          break;
            case CombatState.SwitchingWeapon: /* locked */                break;
        }
    }

    private void HandleIdle()
    {
        if (switchPressed)      { StartCoroutine(SwitchRoutine()); return; }
        if (reloadPressed)      { SetState(CombatState.Reloading); return; }
        if (CanStartShooting()) { SetState(CombatState.Shooting);       }
    }

    private void HandleShooting()
    {
        if (!wantsToShoot || !aimController.isAiming) { SetState(CombatState.Idle);      return; }
        if (reloadPressed)                            { SetState(CombatState.Reloading); return; }
        if (switchPressed)                            { StartCoroutine(SwitchRoutine()); return; }
    }

    private void HandleReloading()
    {
        // NOTE: the Reloading -> Idle exit is owned by the animation event
        // OnReloadAnimationEnd() (Frame O). We do NOT exit here based on the
        // gun's IsReloading flag, because ammo is refilled mid-animation at
        // Frame E and that would cut the animation short.

        // Interrupt: shooting cancels the reload (Helldivers behaviour)
        if (wantsToShoot && aimController.isAiming)
        {
            CancelReload();
            SetState(CombatState.Shooting);
            return;
        }

        // Interrupt: weapon switch cancels the reload
        if (switchPressed)
        {
            CancelReload();
            StartCoroutine(SwitchRoutine());
            return;
        }
    }

    private void SetState(CombatState newState)
    {
        if (currentState == newState) return;

        ExitState(currentState);
        currentState = newState;
        EnterState(newState);
    }

    private void EnterState(CombatState state)
    {
        switch (state)
        {
            case CombatState.Shooting:
                break;

            case CombatState.Reloading:
                StartReload();
                break;

            case CombatState.SwitchingWeapon:
                activeGun?.ForceStop();
                break;
        }
    }

    private void ExitState(CombatState state)
    {
        switch (state)
        {
            case CombatState.Shooting:
                activeGun?.ForceStop();
                break;
        }
    }

    private void OnDestroy()
    {
        if (equipmentManager != null)
            equipmentManager.OnGunChanged -= HandleGunChanged;

        UnsubscribeGunEvents();
    }

#region Weapon Switching

    public void ToggleWeapon()
    {
        if (currentState == CombatState.SwitchingWeapon) return;

        EquipmentSlot next = equipmentManager.GetNextWeaponSlot();
        if (!equipmentManager.CanSwitchTo(next)) return;

        RequestSwitch(next);
    }

    private void RequestSwitch(EquipmentSlot slot)
    {
        switchPressed = true;
        nextSlot = slot;
    }

    private void HandleGunChanged(Gun newGun)
    {
        UnsubscribeGunEvents();

        activeGun = newGun;
        if (activeGun == null) return;

        activeGun.OnReloadStarted  += HandleReloadStarted;
        activeGun.OnReloadFinished += HandleReloadFinished;
        activeGun.OnAmmoChanged    += HandleAmmoChanged;
    }

    private void UnsubscribeGunEvents()
    {
        if (activeGun == null) return;
        activeGun.OnReloadStarted  -= HandleReloadStarted;
        activeGun.OnReloadFinished -= HandleReloadFinished;
        activeGun.OnAmmoChanged    -= HandleAmmoChanged;
    }

    private IEnumerator SwitchRoutine()
    {
        SetState(CombatState.SwitchingWeapon);

        activeGun?.ForceStop();
        wasHoldingFireLastFrame = true;

        yield return new WaitForSeconds(equipTime);

        equipmentManager.SwitchTo(nextSlot);

        SetState(CombatState.Idle);
    }

#endregion

    private void HandleAmmoChanged(int clip, int total)
    {
        Debug.Log($"Ammo: {clip}/{total}");
        // later: update UI
    }

#region Shooting

    public void Fire()
    {
        InputAction mousePosition = inputManager.GetMousePosition();
        if (activeGun == null)
        {
            Debug.LogWarning("No active gun yet");
            return;
        }

        if (wantsToShoot && equipmentManager.ActiveGun != null)
            aimController.Aim(wantsToShoot, mousePosition);

        equipmentManager.ActiveGun.GetComponent<Gun>().Tick(wantsToShoot);
    }

    private bool CanStartShooting()
    {
        if (!wantsToShoot)           return false;
        if (wasHoldingFireLastFrame) return false; // require a fresh click after swap
        return true;
    }

#endregion

#region Reload

    /// <summary>
    /// Called by EnterState when entering Reloading.
    /// Starts the gun's reload process and triggers the animation.
    /// If the gun can't reload, drops straight back to Idle.
    ///
    /// IMPORTANT: Gun.StartReloading() must NOT auto-finish on a timer.
    /// The reload is now driven entirely by animation events:
    ///   - ammo refills at Frame E  (OnNewMagGrabbed -> FinishReload)
    ///   - state exits at Frame O   (OnReloadAnimationEnd)
    /// </summary>
    private void StartReload()
    {
        if (activeGun == null || !activeGun.CanReload())
        {
            SetState(CombatState.Idle);
            return;
        }

        activeGun.StartReloading();
        animationsManager.TriggerReload();
    }

    /// <summary>
    /// Animation event (Frame E) via PlayerAnimationsManager.
    /// The new mag has been seated — refill ammo now so it counts
    /// even if the rest of the animation is interrupted.
    /// </summary>
    public void OnNewMagGrabbed()
    {
        activeGun?.FinishReload();
    }

    /// <summary>
    /// Animation event (Frame O) via PlayerAnimationsManager.
    /// The reload animation is fully complete — return to Idle.
    /// </summary>
    public void OnReloadAnimationEnd()
    {
        if (currentState == CombatState.Reloading)
            SetState(CombatState.Idle);
    }

    /// <summary>
    /// Cancels an in-progress reload (interrupted by shoot or switch).
    /// Tells the gun to abort and resets all reload rig weights so the
    /// gun snaps back to the hands instead of staying glued mid-animation.
    /// </summary>
    private void CancelReload()
    {
        activeGun?.CancelReload();
        animationsManager.AbortReload();
    }

    // Gun event callbacks — logging only.
    // State transitions are owned by the animation events above.
    private void HandleReloadStarted()  => Debug.Log("Reload started");
    private void HandleReloadFinished() => Debug.Log("Reload finished");

#endregion
} 