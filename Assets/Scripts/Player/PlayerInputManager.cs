using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputManager : MonoBehaviour
{
    private PlayerInput playerInput;

    private PlayerAimController aimController;
    private PlayerCombatController combatController;
    private PlayerControls movementController;
    private PlayerEquipmentManager equipmentManager;
    
    private InputAction aimAction;
    private InputAction mousePosition;
    private InputAction fireAction;
    private InputAction reloadAction;
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    bool aimingPressed;
    bool firePressed;
    bool reloadPressed;
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
        aimAction = playerInput.actions["Aim"];
        fireAction = playerInput.actions["Attack"];
        reloadAction = playerInput.actions["Reload"];
        mousePosition = playerInput.actions["Cursor Position"];       
    }

    void ReadInput()
    {
        aimingPressed = aimAction.IsPressed();
        firePressed = fireAction.IsPressed();
        reloadPressed = reloadAction.IsPressed();

    }
    // Update is called once per frame
    void Update()
    {
        ReadInput();

        combatController.Reload(reloadPressed);
        if (!combatController.currentState.Equals(CombatState.Reloading))
        {
            aimController.Aim(aimingPressed || firePressed,mousePosition);
            combatController.Fire(firePressed,mousePosition);
        }
    }
}
