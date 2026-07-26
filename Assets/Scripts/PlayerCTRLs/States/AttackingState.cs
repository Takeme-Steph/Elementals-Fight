using UnityEngine;

public class AttackingState : PlayerState
{
    // Safety-net: force-exit after this long so the player can never get
    // permanently stuck unable to attack again.
    private const float MaxDuration = 1.0f;

    private float elapsed;

    public AttackingState(PlayerStateMachine machine) : base(machine) { }

    public override PlayerStateType Type => PlayerStateType.Attacking;

    // Set by the state that transitions in, just before Enter() runs.
    public bool PendingHeavy;

    public override bool IsInvincible => false;

    public override void Exit()
    {
        // Explicitly clear this rather than relying on it never being
        // touched elsewhere - an Animator state with Write Defaults enabled
        // can reset it unpredictably, and the next attack correctly setting
        // it isn't a substitute for actually clearing it when we leave.
        Machine.Animator.SetBool(Machine.AnimIDIsHeavyAttack, false);
    }

    public override void Enter()
    {
        elapsed = 0f;
        Machine.Animator.SetBool(Machine.AnimIDIsHeavyAttack, PendingHeavy);
        Machine.Animator.SetTrigger(Machine.AnimIDAttack);
    }

    public override void Tick()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= MaxDuration)
        {
            Machine.ChangeState(Machine.Idle);
        }
    }

    // Called by an animation event placed at the actual impact frame of the
    // attack clip (via PlayerStateMachine.PerformAttack()) - not fired at the
    // start of the animation, so the hit lands in sync with when the weapon/
    // limb visually connects, not when the button was pressed.
    public void PerformAttack()
    {
        // Guard against the Animator finishing the tail end of this clip's
        // playback (during its own transition/blend) after our FSM has
        // already moved away from Attacking - e.g. because this attack got
        // interrupted by an incoming hit. Without this, an attack "started"
        // before getting hit could still land afterward, even though the
        // character is now in Hitstun/Knockback.
        if (Machine.CurrentStateType != PlayerStateType.Attacking) return;

        Machine.AttackController.Attack(PendingHeavy);
    }

    // Called by the attack animation's end-of-clip animation event
    // (via PlayerStateMachine.StopAttacking()).
    public override void OnAnimationComplete()
    {
        Machine.ChangeState(Machine.Idle);
    }
}
