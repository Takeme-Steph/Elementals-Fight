using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

[CreateAssetMenu(menuName = "InputReader")]
public class InputReader : ScriptableObject, PlayerInput.IGroundActions, PlayerInput.IUIActions

{   
    private PlayerInput _playerInput; // reference to the input actions class to be used

    // Gameplay Events
    public event Action<Vector2> MoveEvent = delegate { };
    public event Action JumpEvent = delegate { };
    public event Action JumpCanceledEvent = delegate { };
    public event Action AttackEvent = delegate { };
    public event Action<bool> BlockEvent = delegate { };
    public event Action HeavyAttackEvent = delegate { };



    // UI Events
    public event Action PauseEvent = delegate { };
    public event Action ResumeEvent = delegate { };

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

    private void OnDisable()
    {
        if (_playerInput == null)
        {
            return;
        }

        // PlayerInput is generated and owns native Input System action maps. Leaving
        // either map enabled when this shared ScriptableObject unloads makes its
        // finalizer assert and leaves the input system holding stale callbacks across
        // play-mode restarts / scene changes. Clear both callback sets, disable every
        // map, then dispose the generated asset before allowing it to be collected.
        _playerInput.Ground.SetCallbacks(null);
        _playerInput.UI.SetCallbacks(null);
        _playerInput.Disable();

        // ScriptableObject.OnDisable also runs when the Editor returns to edit mode.
        // The generated wrapper's Dispose() always calls Destroy(asset), which Unity
        // rejects outside play mode. This asset is a runtime-created, non-persistent
        // InputActionAsset, so destroying that exact instance immediately in edit mode
        // is safe and prevents it surviving a script/domain reload.
        if (Application.isPlaying)
        {
            _playerInput.Dispose();
        }
        else
        {
            DestroyImmediate(_playerInput.asset);
        }
        _playerInput = null;
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
        if (context.phase == InputActionPhase.Started)
			AttackEvent.Invoke();
    }
    public void OnHeavyAttack(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Started)
			HeavyAttackEvent.Invoke();
    }
    
public void OnBlock(InputAction.CallbackContext context)
    {
        if (context.phase == InputActionPhase.Started)
			BlockEvent.Invoke(true);

		if (context.phase == InputActionPhase.Canceled)
			BlockEvent.Invoke(false);
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
