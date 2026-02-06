using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.Pool;


public class PlayerAimController : MonoBehaviour
{
    [SerializeField]
    private PlayerGunSelector GunSelector;

    // Cached components
    private PlayerInput playerInput;
    private Animator animator;
    private Camera mainCamera;
    //private RaycastWeapon weapon;

    // Animation
    private int aimingLayerIndexUpper;
    private int aimingLayerIndexLower;
    private int reloadingLayerIndexUpper;
    private float currentAimWeight = 0f;

    private InputAction aimAction;
    private InputAction mousePosition;
    private InputAction fireAction;
    private InputAction reloadAction;

    private bool isReloading;
    public bool isAiming = false;

    [SerializeField] private float rotationTimeAim = 2f;

    [Header("Aim Settings")]
    [SerializeField] private float aimDuration = .3f;
    [SerializeField] private LayerMask groundMask;


    public Rig HandIk;
    public Rig weaponHandIk;


    private void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>();
        //weapon = GetComponentInChildren<RaycastWeapon>();
        mainCamera = Camera.main; // Cache Camera.main

        // Cache layer indices once
        aimingLayerIndexLower = animator.GetLayerIndex("Aim Movement - Lower Body");
        aimingLayerIndexUpper = animator.GetLayerIndex("Aiming - Upper Body");
        reloadingLayerIndexUpper = animator.GetLayerIndex("Reloading");


        InitializeActions();
    }

    void InitializeActions()
    {
        aimAction = playerInput.actions["Aim"];
        fireAction = playerInput.actions["Attack"];
        reloadAction = playerInput.actions["Reload"];
        mousePosition = playerInput.actions["Cursor Position"];
        
    }


    void Update()
    {
        Reload();
        if (!isReloading)
        {
            Aim();
            Fire();
        }
        HandleAnimations();
    }

    void Aim()
    {
        bool aimingPressed = aimAction.IsPressed() || fireAction.IsPressed();

        if (aimingPressed)
        {
            //var (success, position) = Helpers.MousePositionToIsometric(mainCamera, mousePosition, groundMask,weapon.transform.position.y);
            float y = GunSelector.ActiveGun.GetInGameModel().GetComponentInChildren<ParticleSystem>().transform.position.y;
            var (success, position) = Helpers.MousePositionToIsometric(mainCamera, mousePosition, groundMask,y);
            
            if (success)
            {
                //weapon.SetAimPosition(position);
                var direction = position - transform.position;


                direction.y = 0;


                // Only rotate if direction is significant
                if (direction.sqrMagnitude > 0.01f)
                {
                    transform.forward = Vector3.Slerp(transform.forward, direction, rotationTimeAim * Time.deltaTime);
                }

                isAiming = true;
            }
        }
        else if (!transform.forward.Equals(Vector3.forward))
        {
            isAiming = false;
        }
    }


    void Fire()
    {
        bool firePressed = fireAction.IsPressed();

        if (firePressed && GunSelector.ActiveGun != null)
        {
            Aim();
            //weapon.StartFiring();
        }
        GunSelector.ActiveGun.Tick(firePressed);

    }

    void Reload()
    {
        bool reloadPressed = reloadAction.IsPressed();

        if (reloadPressed && !isReloading && GunSelector.ActiveGun.CanReload())
        {
            GunSelector.ActiveGun.StartReloading();
            isReloading = true;
            animator.SetTrigger("Reload");
            //have to change ik0,0

        }
    }

    public void EndReload()
    {
        isReloading = false;
        GunSelector.ActiveGun.EndReload();
        //have to change ik

    }

    void HandleAnimations()
    {
        // Update aim weights
        float targetWeight = isAiming ? 1f : 0f;
        currentAimWeight = Mathf.MoveTowards(currentAimWeight, targetWeight, Time.deltaTime * aimDuration);

        animator.SetLayerWeight(aimingLayerIndexLower, currentAimWeight);
        animator.SetLayerWeight(aimingLayerIndexUpper, currentAimWeight);

        animator.SetLayerWeight(reloadingLayerIndexUpper, isReloading ? 1f : 0f);
    }

}
