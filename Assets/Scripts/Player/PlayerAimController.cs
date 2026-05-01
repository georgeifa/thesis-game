using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.Pool;


public class PlayerAimController : MonoBehaviour
{
    private PlayerEquipmentManager equipmentManager;

    // Cached components
    private Animator animator;
    private Camera mainCamera;
    //private RaycastWeapon weapon;

    // Animation
    private int aimingLayerIndexUpper;
    private int aimingLayerIndexLower;
    private float currentAimWeight = 0f;

    public bool isAiming = false;

    [SerializeField] private float rotationTimeAim = 2f;

    [Header("Aim Settings")]
    [SerializeField] private Rig aimingRig;
    [SerializeField] private float aimDuration = .3f;
    [SerializeField] private LayerMask groundMask;


    public Rig HandIk;
    public Rig weaponHandIk;


    private void Start()
    {
        animator = GetComponent<Animator>();
        equipmentManager = GetComponent<PlayerEquipmentManager>();

        
        //weapon = GetComponentInChildren<RaycastWeapon>();
        mainCamera = Camera.main; // Cache Camera.main

        // Cache layer indices once
        aimingLayerIndexLower = animator.GetLayerIndex("Aim Movement - Lower Body");
        aimingLayerIndexUpper = animator.GetLayerIndex("Aiming - Upper Body");
    }


    void Update()
    {
        HandleAnimations();
    }

    public void Aim(bool aimingPressed, InputAction mousePosition)
    {
        if (aimingPressed)
        {
            //var (success, position) = Helpers.MousePositionToIsometric(mainCamera, mousePosition, groundMask,weapon.transform.position.y);
            float y = equipmentManager.ActiveGun.GetComponentInChildren<ParticleSystem>().transform.position.y;
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
                aimingRig.weight = Mathf.MoveTowards(aimingRig.weight, 1, Time.deltaTime * aimDuration);
            }
        }
        else if (!transform.forward.Equals(Vector3.forward))
        {
            isAiming = false;
            aimingRig.weight = Mathf.MoveTowards(aimingRig.weight, 0, Time.deltaTime * aimDuration);
        }
    }

    void HandleAnimations()
    {
        // Update aim weights
        float targetWeight = isAiming ? 1f : 0f;
        currentAimWeight = Mathf.MoveTowards(currentAimWeight, targetWeight, Time.deltaTime * aimDuration);

        animator.SetLayerWeight(aimingLayerIndexLower, currentAimWeight);
        animator.SetLayerWeight(aimingLayerIndexUpper, currentAimWeight);
    }

}
