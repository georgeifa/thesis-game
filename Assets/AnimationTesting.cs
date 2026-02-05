using UnityEngine;
using UnityEngine.InputSystem;

public class AnimationTesting : MonoBehaviour
{

    private PlayerInput playerInput;
    private Animator animator;

    private bool isReloading;

    private InputAction aimAction;
    private InputAction mousePosition;
    private InputAction fireAction;
    private InputAction reloadAction;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerInput = GetComponent<PlayerInput>();
        animator = GetComponent<Animator>();

        aimAction = playerInput.actions["Aim"];
        fireAction = playerInput.actions["Attack"];
        reloadAction = playerInput.actions["Reload"];
        mousePosition = playerInput.actions["Cursor Position"];
    }

    // Update is called once per frame
    void Update()
    {
        Reload();
        Aim();
    }

    void Aim()
    {
        bool aimingPressed = aimAction.IsPressed();

        animator.SetBool("isAiming",aimingPressed);

    }


    void Reload()
    {
        bool reloadPressed = reloadAction.IsPressed();

        if (reloadPressed && !isReloading)
        {
            isReloading = true;
            animator.SetTrigger("Reload");

        }
    }
}
