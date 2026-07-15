using UnityEngine;

// Base class for all player states. Concrete states override only the
// hooks that are relevant to them; everything else is a safe no-op.
public abstract class PlayerState
{
    protected readonly PlayerStateMachine Machine;

    protected PlayerState(PlayerStateMachine machine)
    {
        Machine = machine;
    }

    public abstract PlayerStateType Type { get; }

    // Permissions - what this state allows the rest of the game to do.
    public virtual bool AllowsMovement => false;
    public virtual bool AllowsJump => false;
    public virtual bool AllowsAttack => false;
    public virtual bool AllowsBlock => false;
    public virtual bool IsInvincible => false;

    // Lifecycle
    public virtual void Enter() { }
    public virtual void Exit() { }
    public virtual void Tick() { }

    // Input / event hooks
    public virtual void OnMove(Vector2 direction, float speed) { }
    public virtual void OnJumpPressed() { }
    public virtual void OnJumpCanceled() { }
    public virtual void OnAttackPressed() { }
    public virtual void OnBlockPressed(bool isHeld) { }
    public virtual void OnGrounded() { }
    public virtual void OnLeftGround() { }
    public virtual void OnHit(float damage) { }

    // Called when the driving animation reaches its end (via animation event).
    public virtual void OnAnimationComplete() { }
}
