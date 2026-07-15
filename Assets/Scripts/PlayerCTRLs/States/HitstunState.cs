using UnityEngine;

public class HitstunState : PlayerState
{
    // Same safety-net pattern as AttackingState - if the hit-reaction
    // animation event never fires, force-recover after this long.
    private const float MaxDuration = 0.6f;

    private float elapsed;

    public HitstunState(PlayerStateMachine machine) : base(machine) { }

    public override PlayerStateType Type => PlayerStateType.Hitstun;

    // Brief invincibility while reeling from a hit, so you can't be
    // combo-locked by overlapping hits landing in the same window.
    public override bool IsInvincible => true;

    public override void Enter()
    {
        elapsed = 0f;
        Machine.Animator.SetTrigger(Machine.AnimIDHit);
    }

    public override void Tick()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= MaxDuration)
        {
            Machine.ChangeState(Machine.Idle);
        }
    }

    // Called by the hit-reaction animation's end-of-clip animation event
    // (via PlayerStateMachine.EndHit()).
    public override void OnAnimationComplete()
    {
        Machine.ChangeState(Machine.Idle);
    }
}
