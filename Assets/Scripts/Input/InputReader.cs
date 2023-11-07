using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "InputReader")]
public class InputReader : ScriptableObject, PlayerInput.IGroundActions, PlayerInput.IUIActions

{   
    private PlayerInput _playerInput; // reference to the player's input system

    // Gameplay Events
    public event Action<Vector2> MoveEvent;
    public event Action JumpEvent;
    public event Action JumpCanceledEvent;

    // UI Events
    public event Action PauseEvent;
    public event Action ResumeEvent;

    private void OnEnable()
    {
        if (_playerInput == null)
        {
            _playerInput = new PlayerInput(); //initialize player's input system
            _playerInput.Ground.SetCallbacks(this);
            _playerInput.UI.SetCallbacks(this);

            EnableGroundInput(); // enable ground input map by default

        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        MoveEvent.Invoke(context.ReadValue<Vector2>());
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
			JumpEvent.Invoke();

		if (context.phase == InputActionPhase.Canceled)
			JumpCanceledEvent.Invoke();
    }
    public void OnAttack(InputAction.CallbackContext context)
    {
        
    }
    public void OnPause(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
			PauseEvent.Invoke();
    }
    public void OnResume(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Performed)
			ResumeEvent.Invoke();
    }

    // Enable Ground input map
    public void EnableGroundInput()
	{
		_playerInput.UI.Disable();
		_playerInput.Ground.Enable();
	}

    // Enable UI input map
    public void EnableUIInput()
	{
		_playerInput.UI.Enable();
		_playerInput.Ground.Disable();
	}
}
