using UnityEngine;

// Knocked down - not tied to an animation event, just a fixed recovery timer.
// Intended for big hits / low health, once you wire up something that calls
// PlayerStateMachine.EnterKnockdown().
public class KnockdownState : PlayerState
{
    private const float RecoveryDuration = 1.5f;

    private float elapsed;

    public KnockdownState(PlayerStateMachine machine) : base(machine) { }

    public override PlayerStateType Type => PlayerStateType.Knockdown;

    public override bool IsInvincible => true;

    public override void Enter()
    {
        elapsed = 0f;
        Machine.Animator.SetBool(Machine.AnimIDKnockdown, true);
    }

    public override void Exit()
    {
        Machine.Animator.SetBool(Machine.AnimIDKnockdown, false);
    }

    public override void Tick()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= RecoveryDuration)
        {
            Machine.ChangeState(Machine.Idle);
        }
    }

    // While knocked down, further hits don't re-trigger anything -
    // IsInvincible already covers that at the PlayerManager level.
}
