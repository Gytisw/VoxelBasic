using System;
using UnityEngine;

public class PlayerInput : MonoBehaviour
{
    public event Action OnMouseClick, OnFly;
    public bool RunningPressed { get; private set; }
    public Vector3 MovementInput { get; private set; }
    public Vector2 MousePosition { get; private set; }
    public bool IsJumping { get; private set; }
    
    // Update is called once per frame
    void Update()
    {
        GetMouseClick();
        GetMousePosition();
        GetMovementInput();
        GetJumpInput();
        GetRunInput();
        GetFlyInput();
    }

    private void GetFlyInput()
    {
        if (Input.GetKeyDown(KeyCode.V))
        {
            OnFly?.Invoke();
        }
    }

    private void GetRunInput()
    {
        RunningPressed = Input.GetKey(KeyCode.LeftShift);
    }

    private void GetJumpInput()
    {
        IsJumping = Input.GetButton("Jump");
    }

    private void GetMovementInput()
    {
        MovementInput = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
    }

    private void GetMousePosition()
    {
        MousePosition = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
    }

    private void GetMouseClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            OnMouseClick?.Invoke();
        }
        if (Input.GetMouseButtonDown(1))
        {
            OnMouseClick?.Invoke();
        }
    }
}
