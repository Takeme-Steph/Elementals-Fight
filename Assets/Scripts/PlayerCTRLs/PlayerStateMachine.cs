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
    }

    private void Start()
    {
        current = Idle;
        current.Enter();
    }

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
    public void RequestAttack() => current.OnAttackPressed();
    public void RequestBlock(bool isHeld) => current.OnBlockPressed(isHeld);

    public void SetGrounded(bool grounded)
    {
        IsOnGround = grounded;
        if (grounded) current.OnGrounded();
        else current.OnLeftGround();
    }

    // Called by PlayerManager when a non-invincible hit lands.
    public void EnterHitstun()
    {
        current.OnHit(0f);
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
    }
}
