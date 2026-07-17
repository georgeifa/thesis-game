using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : MonoBehaviour
{
    private PlayerInput playerInput;

    [SerializeField] private string aimActionName;
    [SerializeField] private string mousePositionActionName;
    [SerializeField] private string fireActionName;
    [SerializeField] private string reloadActionName;
    [SerializeField] private string weaponSwapActionName;
    [SerializeField] private string throwActionName;




    private PlayerAimController aimController;
    private PlayerCombatController combatController;
    private PlayerControls movementController;
    private PlayerEquipmentManager equipmentManager;
    
    private InputAction aimAction;
    private InputAction mousePosition;
    private InputAction fireAction;
    private InputAction reloadAction;
    private InputAction scrollAction;
    private InputAction throwAction;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    bool aimingPressed;
    bool firePressed;
    bool reloadPressed;
    bool throwPressed;
    Vector2 scroll;
    void Start()
    {
        playerInput = GetComponent<PlayerInput>();

        aimController = GetComponent<PlayerAimController>();
        combatController = GetComponent<PlayerCombatController>();
        movementController = GetComponent<PlayerControls>();
        equipmentManager = GetComponent<PlayerEquipmentManager>();

        InitializeInputActions();
    }

    void InitializeInputActions()
    {
        aimAction = playerInput.actions[aimActionName];
        fireAction = playerInput.actions[fireActionName];
        reloadAction = playerInput.actions[reloadActionName];
        mousePosition = playerInput.actions[mousePositionActionName];  
        scrollAction = playerInput.actions[weaponSwapActionName];  
        throwAction = playerInput.actions[throwActionName];
    }

    void ReadInput()
    {
        aimingPressed = aimAction.IsPressed();
        firePressed = fireAction.IsPressed();
        reloadPressed = reloadAction.IsPressed();
        scroll = scrollAction.ReadValue<Vector2>();
        throwPressed = throwAction.IsPressed();
    }
    // Update is called once per frame
    void Update()
    {
        ReadInput();

        if(reloadPressed)
            combatController.SetReload();
            
        if (combatController.currentState != CombatState.Throwing)
            aimController.Aim(aimingPressed || firePressed,mousePosition);
        combatController.SetFire(firePressed);

        if(scroll.y != 0)
            combatController.ToggleWeapon();

        if (combatController.currentState == CombatState.Throwing)
            aimController.RotateTowardsCursor(mousePosition);
        
        combatController.SetThrowHeld(throwPressed);
    }

    public InputAction GetMousePosition()
    {
        return mousePosition;
    }
}
