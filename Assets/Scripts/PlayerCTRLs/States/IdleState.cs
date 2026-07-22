using UnityEngine;

public class IdleState : PlayerState
{
    public IdleState(PlayerStateMachine machine) : base(machine) { }

    public override PlayerStateType Type => PlayerStateType.Idle;

    public override bool AllowsMovement => true;
    public override bool AllowsJump => true;
    public override bool AllowsAttack => true;
    public override bool AllowsBlock => true;

    public override void Enter()
    {
        Machine.Animator.SetBool(Machine.AnimIDIdle, true);
    }

    public override void Exit()
    {
        Machine.Animator.SetBool(Machine.AnimIDIdle, false);
    }

    public override void OnMove(Vector2 direction, float speed)
    {
        if (direction != Vector2.zero)
            Machine.ChangeState(Machine.Walking);
    }

    public override void OnJumpPressed()
    {
        Machine.ChangeState(Machine.Jumping);
    }

    public override void OnAttackPressed(bool isHeavy)
    {
        Machine.Attacking.PendingHeavy = isHeavy;
        Machine.ChangeState(Machine.Attacking);
    }

    public override void OnBlockPressed(bool isHeld)
    {
        if (isHeld)
            Machine.ChangeState(Machine.Blocking);
    }

    public override void OnLeftGround()
    {
        // Walked off a ledge - treat as airborne.
        Machine.ChangeState(Machine.Jumping);
    }

    public override void OnHit(float damage)
    {
        Machine.EnterHitstun();
    }
}
