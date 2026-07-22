using UnityEngine;

public class JumpingState : PlayerState
{
    public JumpingState(PlayerStateMachine machine) : base(machine) { }

    public override PlayerStateType Type => PlayerStateType.Jumping;

    // Allow air control and air attacks; jumping again mid-air is not allowed
    // (no double jump yet - add here later if you want one).
    public override bool AllowsMovement => true;
    public override bool AllowsAttack => true;
    public override bool AllowsBlock => false;

    public override void Enter()
    {
        Machine.Animator.SetTrigger(Machine.AnimIDJump);
    }

    public override void OnMove(Vector2 direction, float speed)
    {
        Machine.Animator.SetFloat(Machine.AnimIDDirection, direction.x);
    }

    public override void OnAttackPressed(bool isHeavy)
    {
        Machine.Attacking.PendingHeavy = isHeavy;
        Machine.ChangeState(Machine.Attacking);
    }

    public override void OnHit(float damage)
    {
        Machine.EnterHitstun();
    }

    public override void OnGrounded()
    {
        // Landed - go back to whatever grounded state fits current input.
        Machine.ChangeState(Machine.Idle);
    }
}
