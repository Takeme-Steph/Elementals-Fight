using UnityEngine;

public class BlockingState : PlayerState
{
    public BlockingState(PlayerStateMachine machine) : base(machine) { }

    public override PlayerStateType Type => PlayerStateType.Blocking;

    public override bool AllowsMovement => false;
    public override bool AllowsJump => true; // jump-cancel out of block
    public override bool AllowsAttack => false;
    public override bool AllowsBlock => true;

    // Chip-damage mitigation while blocking is handled in PlayerManager,
    // which checks CurrentStateType == Blocking directly.

public override void Enter()
    {
        Machine.Animator.SetBool(Machine.AnimIDBlock, true);
    }

    public override void Exit()
    {
        Machine.Animator.SetBool(Machine.AnimIDBlock, false);
    }

    public override void OnBlockPressed(bool isHeld)
    {
        if (!isHeld)
            Machine.ChangeState(Machine.Idle);
    }

    public override void OnJumpPressed()
    {
        Machine.ChangeState(Machine.Jumping);
    }

    
}
