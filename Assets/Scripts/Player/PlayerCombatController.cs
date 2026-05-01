using MyBox;
using UnityEngine;
using UnityEngine.InputSystem;


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

    private PlayerAimController aimController;
    private PlayerEquipmentManager equipmentManager;

    private Gun activeGun;

    private int reloadingLayerIndexUpper;

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

    private void HandleReloadStarted()
    {
        Debug.Log("Reload started");

        currentState = CombatState.Reloading;

        // later: trigger animation

        activeGun.FinishReload();
    }

    private void HandleReloadFinished()
    {
        Debug.Log("Reload finished");

        currentState = CombatState.Idle;
    }

    private void HandleAmmoChanged(int clip, int total)
    {
        Debug.Log($"Ammo: {clip}/{total}");

        // later: update UI
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        reloadingLayerIndexUpper = animator.GetLayerIndex("Reloading");

        if (equipmentManager.ActiveGun != null)
        {
            HandleGunChanged(equipmentManager.ActiveGun.GetComponent<Gun>());
        }
    }


    public void Fire(bool firePressed, InputAction mousePosition)
    {
        if (activeGun == null)
        {
            Debug.LogWarning("No active gun yet");
            return;
        }

        if (firePressed && equipmentManager.ActiveGun != null)
        {
            aimController.Aim(firePressed,mousePosition);
            //weapon.StartFiring();
        }

        equipmentManager.ActiveGun.GetComponent<Gun>().Tick(firePressed);
    }

    public void Reload(bool reloadPressed)
    {
        if (reloadPressed && !currentState.Equals(CombatState.Reloading) && equipmentManager.ActiveGun.GetComponent<Gun>().CanReload())
        {
            equipmentManager.ActiveGun.GetComponent<Gun>().StartReloading();
            animator.SetTrigger("Reload");
            //have to change ik0,0
        }
    }

    public void EndReload()
    {
        equipmentManager.ActiveGun.GetComponent<Gun>().FinishReload();
        //have to change ik

    }

    void HandleAnimations()
    {
        animator.SetLayerWeight(reloadingLayerIndexUpper, currentState.Equals(CombatState.Reloading) ? 1f : 0f);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
