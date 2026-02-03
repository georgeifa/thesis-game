using System;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Animations.Rigging;
using UnityEngine.InputSystem;
using UnityEngine.Pool;

[RequireComponent(typeof(CharacterController),typeof(PlayerInput),typeof(Animator))]
public class PlayerControls : MonoBehaviour
{
    // Cached components
    private CharacterController controller;
    private PlayerInput playerInput;
    private Animator animator;
    private PlayerAimController aimController;

    // Movement variables
    private Vector3 playerVelocity;
    private bool groundedPlayer;
    
    // Input actions
    private InputAction moveAction;
    private InputAction sprintAction;

    // State flags
    private bool isWalking;
    private bool isRunning;

    // Animation
    private float forwardAmount;
    private float turnAmount;

    // Cached calculations
    private Vector3 cachedMoveDirection;
    private Vector2 cachedInput;

    // Serialized fields
    [Header("Movement Settings")]
    [SerializeField] private float playerSpeed = 2.0f;
    [SerializeField] private float jumpHeight = 1.0f;
    [SerializeField] private float gravityValue = -9.81f;
    [SerializeField] private float sprintMultiplier = 2f;
    [SerializeField] private float aimSpeedMultiplier = .5f;
    
    [Header("Rotation Settings")]
    [SerializeField] private float rotationTimeIdle = 2f;
    [SerializeField] private float rotationTimeRunning = 1f;

    // Animation parameter hashes - FOR BETTER OPTIMIZATION
    private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
    private static readonly int PlayerSpeedHash = Animator.StringToHash("playerSpeed");
    private static readonly int ForwardAmountHash = Animator.StringToHash("Forward Amount");
    private static readonly int TurnAmountHash = Animator.StringToHash("Turn Amount");

    private void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        aimController = GetComponent<PlayerAimController>();
        controller = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();

        InitializeActions();
    }

    void InitializeActions()
    {
        moveAction = playerInput.actions["Move"];
        sprintAction = playerInput.actions["Sprint"];
    }

    void Update()
    {
        // Cache grounded check
        groundedPlayer = controller.isGrounded;
        
        // Reset y velocity when grounded
        if (groundedPlayer && playerVelocity.y < 0)
        {
            playerVelocity.y = -0.5f; // Small negative value to stay grounded
        }

        // Process input and movement
        MovingHandler();
        HandleAnimations();
    }

    void MovingHandler()
    {
        if (aimController.isAiming)
            MoveWhileAiming();
        else
            Move();
    }

    void MoveWhileAiming()
    {
        isRunning = false;
        cachedInput = moveAction.ReadValue<Vector2>();
        
        // Reuse vector to avoid allocation
        cachedMoveDirection.Set(cachedInput.x, 0, cachedInput.y);
        cachedMoveDirection = cachedMoveDirection.ToIso();

        // Apply gravity
        playerVelocity.y += gravityValue * Time.deltaTime;

        // Combine movements
        Vector3 finalMove = (aimSpeedMultiplier * playerSpeed * cachedMoveDirection) + (playerVelocity.y * Vector3.up);

        isWalking = cachedInput.x != 0 || cachedInput.y != 0;

        controller.Move(finalMove * Time.deltaTime);
    }

    void Move()
    {
        cachedInput = moveAction.ReadValue<Vector2>();
        
        // Reuse vector to avoid allocation
        cachedMoveDirection.Set(cachedInput.x, 0, cachedInput.y);
        cachedMoveDirection = cachedMoveDirection.ToIso();

        if (cachedMoveDirection != Vector3.zero)
        {
            float finalRotationTime = sprintAction.IsPressed() ? rotationTimeRunning : rotationTimeIdle;
            transform.forward = Vector3.Slerp(transform.forward, cachedMoveDirection, finalRotationTime * Time.deltaTime);
        }

        // Jump logic
        if (Input.GetButtonDown("Jump") && groundedPlayer)
        {
            playerVelocity.y = Mathf.Sqrt(jumpHeight * -2.0f * gravityValue);
        }

        // Apply gravity
        playerVelocity.y += gravityValue * Time.deltaTime;

        float finalSpeed = sprintAction.IsPressed() ? playerSpeed * sprintMultiplier : playerSpeed;

        // Combine movements
        Vector3 finalMove = (cachedMoveDirection * finalSpeed) + (playerVelocity.y * Vector3.up);

        isWalking = cachedInput.x != 0 || cachedInput.y != 0;
        isRunning = sprintAction.IsPressed() && isWalking;

        controller.Move(finalMove * Time.deltaTime);
    }

    void HandleAnimations()
    {
        // Use hashed parameters for better performance
        animator.SetBool(IsGroundedHash, groundedPlayer);
        animator.SetBool(IsWalkingHash, isWalking);

        // Set movement speed
        float targetSpeed = 0f;
        if (isWalking)
            targetSpeed = isRunning ? 1f : 0.5f;

        animator.SetFloat(PlayerSpeedHash, targetSpeed, 0.1f, Time.deltaTime);

        // Handle walking while aiming animations
        if (aimController.isAiming)
        {
            Vector3 localMove = Helpers.CalculateLocalMove(cachedInput,transform);
            turnAmount = localMove.x;
            forwardAmount = localMove.z;

            animator.SetFloat(ForwardAmountHash, forwardAmount, 0.1f, Time.deltaTime);
            animator.SetFloat(TurnAmountHash, turnAmount, 0.1f, Time.deltaTime);
        }
    }
    
}