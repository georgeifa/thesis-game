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


    private bool wantsToShoot;
    private bool reloadPressed;
    private bool switchPressed;
    private EquipmentSlot nextSlot;

    // timing
    [SerializeField] private float equipTime = 0.4f;

    [SerializeField] private float switchCooldown = 0.15f;

    // for semi-auto behavior
    private bool wasHoldingFireLastFrame;

    private int reloadingLayerIndexUpper;

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

        if (equipmentManager == null)
        {
            Debug.LogError("PlayerEquipment is missing!");
            return;
        }

        if (inputManager == null)
            inputManager = GetComponent<PlayerInputManager>();

        if (inputManager == null)
        {
            Debug.LogError("PlayerInputManager is missing!");
            return;
        }

        if (aimController == null)
            aimController = GetComponent<PlayerAimController>();

        if (aimController == null)
        {
            Debug.LogError("PlayerAimController is missing!");
            return;
        }

        if (animator == null)
            animator = GetComponent<Animator>();

        if (animator == null)
        {
            Debug.LogError("Animator is missing!");
            return;
        }
    }

    void Start()
    {
        reloadingLayerIndexUpper = animator.GetLayerIndex("Reloading");

        if (equipmentManager.ActiveGun != null)
        {
            HandleGunChanged(equipmentManager.ActiveGun.GetComponent<Gun>());
        }
    }

#endregion

#region Input Calls

    public void SetFire(bool pressed)
    {
        wantsToShoot = pressed;
    }

    public void SetReload()
    {
        reloadPressed = true;
    }

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
            case CombatState.None:
                SetState(CombatState.Idle);
                break;

            case CombatState.Idle:
                HandleIdle();
                break;

            case CombatState.Shooting:
                HandleShooting();
                break;

            case CombatState.Reloading:
                HandleReloading();
                break;

            case CombatState.SwitchingWeapon:
                // locked
                break;
        }
    }

    private void HandleIdle()
    {
        if (switchPressed)
        {
            StartCoroutine(SwitchRoutine());
            return;
        }

        if (reloadPressed)
        {
            SetState(CombatState.Reloading);
            return;
        }

        if (CanStartShooting())
        {
            SetState(CombatState.Shooting);
        }
    }

    private void HandleShooting()
    {
        if (!wantsToShoot || !aimController.isAiming)
        {
            SetState(CombatState.Idle);
            return;
        }

        if (reloadPressed)
        {
            SetState(CombatState.Reloading);
            return;
        }

        if (switchPressed)
        {
            StartCoroutine(SwitchRoutine());
            return;
        }
    }

    private void HandleReloading()
    {
        if (activeGun != null && !activeGun.IsReloading)
        {
            SetState(CombatState.Idle);
            return;
        }

        if (wantsToShoot && aimController.isAiming)
        {
            CancelReload();
            SetState(CombatState.Shooting);
            return;
        }

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
                Reload();
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

        if (activeGun != null)
        {
            activeGun.OnReloadStarted -= HandleReloadStarted;
            activeGun.OnReloadFinished -= HandleReloadFinished;
            activeGun.OnAmmoChanged -= HandleAmmoChanged;
        }
    }

#region Weapon Switching

    public void ToggleWeapon()
    {
        if (currentState == CombatState.SwitchingWeapon)
            return;

        EquipmentSlot next = equipmentManager.GetNextWeaponSlot();

        if (!equipmentManager.CanSwitchTo(next))
            return;

        RequestSwitch(next);
    }

    private void RequestSwitch(EquipmentSlot slot)
    {
        switchPressed = true;
        nextSlot = slot;
    }

    private void HandleGunChanged(Gun newGun)
    {
        if (activeGun != null)
        {
            activeGun.OnReloadStarted -= HandleReloadStarted;
            activeGun.OnReloadFinished -= HandleReloadFinished;
            activeGun.OnAmmoChanged -= HandleAmmoChanged;
        }

            activeGun = newGun;

        if (activeGun == null) return;

        // subscribe to new gun
        activeGun.OnReloadStarted += HandleReloadStarted;
        activeGun.OnReloadFinished += HandleReloadFinished;
        activeGun.OnAmmoChanged += HandleAmmoChanged;
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
        {
            aimController.Aim(wantsToShoot,mousePosition);
            //weapon.StartFiring();
        }

        equipmentManager.ActiveGun.GetComponent<Gun>().Tick(wantsToShoot);
    }

    private bool CanStartShooting()
    {
        if (!wantsToShoot) return false;

        // require new click after swap
        if (wasHoldingFireLastFrame) return false;

        return true;
    }

#endregion

#region Reload

    private void HandleReloadStarted()
    {
        Debug.Log("Reload started");

        // later: trigger animation

        EndReload();
    }

    public void Reload()
    {
        if (reloadPressed && activeGun != null && activeGun.CanReload())
        {
            reloadPressed = false;
            activeGun.StartReloading();
            animator.SetTrigger("Reload");
            //have to change ik0,0
        }
    }

    private void CancelReload()
    {
        if (activeGun == null) return;

        activeGun.CancelReload();   // tell gun to stop animation/process
    }

    private void HandleReloadFinished()
    {
        Debug.Log("Reload finished");

        SetState(CombatState.Idle);
    }

    public void EndReload()
    {
        activeGun.FinishReload();
        //have to change ik
    }

#endregion

    void HandleAnimations()
    {
        animator.SetLayerWeight(reloadingLayerIndexUpper, currentState.Equals(CombatState.Reloading) ? 1f : 0f);
    }
}
