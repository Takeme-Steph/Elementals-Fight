using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputReader inputReader; // Reference the 3rd party input reader
    [SerializeField] private float playerMoveSpeed = 7f; // Player's movement speed.
    [SerializeField] private float playerJumpForce = 200f;

    private CharacterPhysics characterPhysics;
    private PlayerStateMachine stateMachine;
    private SceneHandler sceneHandler;

    private bool jump;
    private bool isAttacking;
    private bool isHeavyAttacking;

    private Vector2 moveDirection;

    private void OnEnable()
    {
        // Subscribe to events
        inputReader.MoveEvent += HandleMove;
        inputReader.JumpEvent += HandleJump;
        inputReader.JumpCanceledEvent += HandleCanceledJump;
        inputReader.AttackEvent += HandleAttack;
        inputReader.BlockEvent += HandleBlock;
        inputReader.HeavyAttackEvent += HandleHeavyAttack;

        TryGetComponent<PlayerStateMachine>(out stateMachine);
        if (stateMachine == null)
        {
            Debug.LogError(gameObject.name + ": PlayerStateMachine component not found.");
        }
    }

    private void OnDisable()
    {
        // Unsubscribe from events
        inputReader.MoveEvent -= HandleMove;
        inputReader.JumpEvent -= HandleJump;
        inputReader.JumpCanceledEvent -= HandleCanceledJump;
        inputReader.AttackEvent -= HandleAttack;
        inputReader.BlockEvent -= HandleBlock;
        inputReader.HeavyAttackEvent -= HandleHeavyAttack;
    }

    void Start()
    {
        if (!TryGetComponent<CharacterPhysics>(out characterPhysics))
            Debug.LogError(gameObject.name + " has no CharacterPhysics component attached");

        sceneHandler = SceneHandler.Instance;
        if (sceneHandler == null)
            Debug.LogError("No SceneHandler.Instance found. Game will not run.");
    }

    void Update()
    {
        // Defensive fallback: if this ran before SceneHandler's Awake() for
        // any reason, keep retrying instead of permanently giving up.
        if (sceneHandler == null)
        {
            sceneHandler = SceneHandler.Instance;
            if (sceneHandler == null) return;
        }

        if (!sceneHandler.isGameOver && sceneHandler.activeMatch)
        {
            Jump();
            Attack();
        }
    }

    void FixedUpdate()
    {
        if (sceneHandler == null)
        {
            sceneHandler = SceneHandler.Instance;
            if (sceneHandler == null) return;
        }

        // Movement (translate + bounds-keeping) and ground detection both
        // live in CharacterPhysics, shared across any controller type
        // (human or AI) driving this character.
        characterPhysics.MoveHorizontal(moveDirection, playerMoveSpeed);
    }

    private void Jump()
    {
        if (jump && stateMachine.CanJump)
        {
            stateMachine.RequestJump();
            characterPhysics.ApplyJumpForce(playerJumpForce);
        }
        jump = false;
    }

    private void Attack()
    {
        if (isHeavyAttacking)
        {
            isHeavyAttacking = false;
            isAttacking = false;
            if (stateMachine.CanAttack)
            {
                stateMachine.RequestAttack(true);
            }
            return;
        }

        if (isAttacking)
        {
            isAttacking = false;
            if (stateMachine.CanAttack)
            {
                stateMachine.RequestAttack(false);
            }
        }
    }

    private void HandleMove(Vector2 direction)
    {
        moveDirection = direction;
    }

    private void HandleJump()
    {
        jump = true;
    }

    private void HandleCanceledJump()
    {
        jump = false;
    }

    private void HandleAttack()
    {
        isAttacking = true;
    }

    private void HandleHeavyAttack()
    {
        isHeavyAttacking = true;
    }

    private void HandleBlock(bool isHeld)
    {
        stateMachine.RequestBlock(isHeld);
    }
}
