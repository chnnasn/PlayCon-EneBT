using UnityEngine;
using Tools;
using UnityEngine.InputSystem;

public class PlayerInputSystem : LazySingleton<PlayerInputSystem>
{
    public CharacterInput inputActions;
    protected override void Awake()
    {
        base.Awake();
        if (inputActions == null )
            inputActions = new CharacterInput();
    }
    private void OnEnable()
    {
        inputActions?.Enable();
    }
    private void OnDisable()
    {
        inputActions?.Disable();
    }
    //属性封装
    public Vector2 PlayerMove
    {
        get => inputActions.Player.Movement.ReadValue<Vector2>();
    }
    public Vector2 CameraLook
    {  
        get=> inputActions.Player.CameraLook.ReadValue<Vector2>();
    }
    public bool Run
    {
        get => inputActions.Player.Run.triggered;
    }
    public bool Run_Continue
    {
        get => inputActions.Player.Run.phase == InputActionPhase.Performed;
    }
    public bool Walk
    { 
        get => inputActions.Player.Walk.triggered;
    }

    public bool TryGetPointerPosition(out Vector2 pointerPosition)
    {
        pointerPosition = default;
        if (inputActions?.Player.MousePo == null)
            return false;

        pointerPosition = inputActions.Player.MousePo.ReadValue<Vector2>();
        return true;
    }

    public bool TryWarpPointerPosition(Vector2 screenPosition)
    {
        if (Mouse.current == null)
            return false;

        Mouse.current.WarpCursorPosition(screenPosition);
        return true;
    }
    
}