using UnityEngine;

// Light stagger reaction to a heavy hit - not a full knockdown (that's a
// future, heavier state for block breakers/combos/fatalities). Timer-based
// recovery, same pattern as HitstunState/AttackingState's safety-net.
public class KnockbackState : PlayerState
{
    private const float RecoveryDuration = 1.5f;

    private float elapsed;

    public KnockbackState(PlayerStateMachine machine) : base(machine) { }

    public override PlayerStateType Type => PlayerStateType.Knockback;

    public override bool IsInvincible => true;

    public override void Enter()
    {
        elapsed = 0f;
        Machine.Animator.SetBool(Machine.AnimIDKnockback, true);
    }

    public override void Exit()
    {
        Machine.Animator.SetBool(Machine.AnimIDKnockback, false);
    }

    public override void Tick()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= RecoveryDuration)
        {
            Machine.ChangeState(Machine.Idle);
        }
    }
}
