using UnityEngine;

public class AttackingState : PlayerState
{
    // Safety-net: if the attack animation event never fires (missing event,
    // interrupted animation, retargeted clip, etc.) force-exit after this long
    // so the player can never get permanently stuck unable to attack again.
    private const float MaxDuration = 1.0f;

    private float elapsed;

    public AttackingState(PlayerStateMachine machine) : base(machine) { }

    public override PlayerStateType Type => PlayerStateType.Attacking;

    // Set by the state that transitions in, just before Enter() runs.
    public bool PendingHeavy;

    public override bool IsInvincible => false;

    public override void Enter()
    {
        elapsed = 0f;
        Machine.Animator.SetBool(Machine.AnimIDIsHeavyAttack, PendingHeavy);
        Machine.Animator.SetTrigger(Machine.AnimIDAttack);
        Machine.AttackController.Attack(PendingHeavy);
    }

    public override void Tick()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= MaxDuration)
        {
            Machine.ChangeState(Machine.Idle);
        }
    }

    public override void OnHit(float damage)
    {
        // Getting hit interrupts an attack.
        Machine.EnterHitstun();
    }

    // Called by the attack animation's end-of-clip animation event
    // (via PlayerStateMachine.StopAttacking()).
    public override void OnAnimationComplete()
    {
        Machine.ChangeState(Machine.Idle);
    }
}
