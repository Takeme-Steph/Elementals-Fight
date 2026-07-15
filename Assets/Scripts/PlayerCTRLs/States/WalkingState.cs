using UnityEngine;

public class WalkingState : PlayerState
{
    public WalkingState(PlayerStateMachine machine) : base(machine) { }

    public override PlayerStateType Type => PlayerStateType.Walking;

    public override bool AllowsMovement => true;
    public override bool AllowsJump => true;
    public override bool AllowsAttack => true;
    public override bool AllowsBlock => true;

    public override void Enter()
    {
        Machine.Animator.SetBool(Machine.AnimIDWalk, true);
    }

    public override void Exit()
    {
        Machine.Animator.SetBool(Machine.AnimIDWalk, false);
        Machine.Animator.SetFloat(Machine.AnimIDSpeed, 0f);
    }

    public override void OnMove(Vector2 direction, float speed)
    {
        if (direction == Vector2.zero)
        {
            Machine.ChangeState(Machine.Idle);
            return;
        }

        Machine.Animator.SetFloat(Machine.AnimIDDirection, direction.x);
        Machine.Animator.SetFloat(Machine.AnimIDSpeed, speed);
    }

    public override void OnJumpPressed()
    {
        Machine.ChangeState(Machine.Jumping);
    }

    public override void OnAttackPressed()
    {
        Machine.ChangeState(Machine.Attacking);
    }

    public override void OnBlockPressed(bool isHeld)
    {
        if (isHeld)
            Machine.ChangeState(Machine.Blocking);
    }

    public override void OnLeftGround()
    {
        Machine.ChangeState(Machine.Jumping);
    }

    public override void OnHit(float damage)
    {
        Machine.EnterHitstun();
    }
}
