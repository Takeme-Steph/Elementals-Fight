using UnityEngine;

// Central FSM controller for a player character. Replaces the old
// PlayerStateManager. Other scripts (PlayerController, AttackCTRL,
// PlayerManager) talk to this instead of touching bool flags directly.
public class PlayerStateMachine : MonoBehaviour
{
    [SerializeField] private AttackCTRL attackController;
    public AttackCTRL AttackController => attackController;

    private Animator animator;
    public Animator Animator => animator;
    private bool hasAnimator;

    // Animation IDs (same hashes as before, plus new ones for Block/Knockdown).
    public int AnimIDAttack { get; private set; }
    public int AnimIDWalk { get; private set; }
    public int AnimIDIdle { get; private set; }
    public int AnimIDJump { get; private set; }
    public int AnimIDGrounded { get; private set; }
    public int AnimIDDirection { get; private set; }
    public int AnimIDSpeed { get; private set; }
    public int AnimIDHit { get; private set; }
    public int AnimIDBlock { get; private set; }
    public int AnimIDKnockdown { get; private set; }
    public int AnimIDIsHeavyAttack { get; private set; }

    // States - constructed once and reused (avoids per-transition allocation).
    public IdleState Idle { get; private set; }
    public WalkingState Walking { get; private set; }
    public JumpingState Jumping { get; private set; }
    public AttackingState Attacking { get; private set; }
    public BlockingState Blocking { get; private set; }
    public HitstunState Hitstun { get; private set; }
    public KnockdownState Knockdown { get; private set; }

    private PlayerState current;
    public PlayerStateType CurrentStateType => current.Type;

    public bool IsOnGround { get; private set; }

    // Convenience passthroughs so other scripts can ask "am I allowed to X"
    // without needing to know about states at all.
    public bool CanMove => current.AllowsMovement;
    public bool CanJump => current.AllowsJump;
    public bool CanAttack => current.AllowsAttack;
    public bool CanBlock => current.AllowsBlock;
    public bool IsInvincible => current.IsInvincible;

    private void Awake()
    {
        hasAnimator = TryGetComponent(out animator);
        if (!hasAnimator)
            Debug.LogError(gameObject.name + " has no animator controller attached");

        if (attackController == null)
            TryGetComponent(out attackController);

        AssignAnimationIDs();

        Idle = new IdleState(this);
        Walking = new WalkingState(this);
        Jumping = new JumpingState(this);
        Attacking = new AttackingState(this);
        Blocking = new BlockingState(this);
        Hitstun = new HitstunState(this);
        Knockdown = new KnockdownState(this);

        // Fixed: this used to be set in Start(), but Awake() is what Unity
        // guarantees runs synchronously the instant this object is instantiated
        // (Start() is deferred until before the next Update). If a character is
        // spawned at runtime (e.g. by LoadCharacter), a queued/catch-up
        // FixedUpdate could run before Start() ever fired, leaving 'current'
        // null and crashing PlayerController.Move(). Setting it here removes
        // that race entirely.
        current = Idle;
        current.Enter();
    }

    // Initialization moved to Awake() - see comment there.

    private void Update()
    {
        current.Tick();
    }

    public void ChangeState(PlayerState next)
    {
        if (next == current) return;
        current.Exit();
        current = next;
        current.Enter();
    }

    // --- Input entry points, called from PlayerController / InputReader ---

    public void Move(Vector2 direction, float speed) => current.OnMove(direction, speed);
    public void RequestJump() => current.OnJumpPressed();
    public void CancelJump() => current.OnJumpCanceled();
    public void RequestAttack(bool isHeavy) => current.OnAttackPressed(isHeavy);
    public void RequestBlock(bool isHeld) => current.OnBlockPressed(isHeld);

    public void SetGrounded(bool grounded)
    {
        IsOnGround = grounded;

        // Fixed: the Animator Controller's own Jump state transitions
        // (Jump -> Idle, Jump -> Walk) require an "isGrounded" Animator
        // parameter to be true - but nothing was ever setting it, so the
        // Animator stayed visually stuck on Jump forever even after the FSM
        // correctly moved on to Idle/Walking underneath it.
        Animator.SetBool(AnimIDGrounded, grounded);

        if (grounded) current.OnGrounded();
        else current.OnLeftGround();
    }

    // Called by PlayerManager when a non-invincible hit lands.
    // Fixed: this used to route through current.OnHit(), and every state's
    // OnHit() called back into this method - infinite recursion / stack overflow
    // the first time a hit ever landed. Now it transitions directly.
    public void EnterHitstun()
    {
        ChangeState(Hitstun);
    }

    // Explicit entry point for knockdown, to be wired up to heavy attacks /
    // low-health hits later.
    public void EnterKnockdown()
    {
        ChangeState(Knockdown);
    }

    // --- Backward-compatible names for existing Animation Events ---
    // Your attack/hit-reaction animation clips already call these by name;
    // keeping the same method names means you don't need to touch those clips.

    public void StopAttacking() => current.OnAnimationComplete();
    public void EndHit() => current.OnAnimationComplete();

    private void AssignAnimationIDs()
    {
        AnimIDWalk = Animator.StringToHash("isWalking");
        AnimIDIdle = Animator.StringToHash("isIdle");
        AnimIDJump = Animator.StringToHash("Jump");
        AnimIDGrounded = Animator.StringToHash("isGrounded");
        AnimIDDirection = Animator.StringToHash("Direction");
        AnimIDAttack = Animator.StringToHash("Attack");
        AnimIDSpeed = Animator.StringToHash("Speed");
        AnimIDHit = Animator.StringToHash("Hit");
        AnimIDBlock = Animator.StringToHash("isBlocking");
        AnimIDKnockdown = Animator.StringToHash("isKnockedDown");
        AnimIDIsHeavyAttack = Animator.StringToHash("isHeavyAttack");
    }
}
