using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputReader inputReader; // Reference the 3rd party input reader
    [SerializeField] private float playerMoveSpeed = 7f; // Player's movement speed.
    [SerializeField] private float playerJumpForce = 200f;

    private Rigidbody playerRigidbody;
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
        // Try to get player rigidbody and throw an error message if not found
        if (!TryGetComponent<Rigidbody>(out playerRigidbody))
            Debug.LogError(gameObject.name + " has no rigidbody attached");

        // Fixed: this used to look up GameManager by name via GameObject.Find(),
        // which raced against script execution order and could silently fail,
        // leaving sceneHandler null forever and crashing GroundCheck() every
        // FixedUpdate. SceneHandler.Instance is set in its own Awake(), which
        // is guaranteed to run before this Start(), so this is reliable.
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
            KeepInBounds();
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

        // Ground detection moved to GroundDetector, which runs regardless of
        // which controller (this one, or PlayerAutoPilot) is active.
        Move();
    }

    private void OnCollisionEnter(Collision collision)
    {

    }

    private void Move()
    {
        if (moveDirection == Vector2.zero)
        {
            stateMachine.Move(Vector2.zero, 0f);
            return;
        }

        if (!stateMachine.CanMove) return;

        if (transform.position.x > sceneHandler.safeZoneLeftX &&
            transform.position.x < sceneHandler.safeZoneRightX)
        {
            stateMachine.Move(moveDirection, playerMoveSpeed);
            transform.Translate(new Vector3(moveDirection.x, 0, 0) * (playerMoveSpeed * Time.deltaTime), Space.World);
        }
    }

    private void Jump()
    {
        if (jump && stateMachine.CanJump)
        {
            stateMachine.RequestJump();
            // Fixed: ForceMode.Impulse already represents an instantaneous change,
            // multiplying by Time.deltaTime was making jump height frame-rate dependent.
            playerRigidbody.AddForce(Vector3.up * playerJumpForce, ForceMode.Impulse);
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


    //Keep the player within the safe playable area of the scene
    private void KeepInBounds()
    {
        if (transform.position.x <= sceneHandler.safeZoneLeftX)
        {
            transform.position = new Vector3(sceneHandler.safeZoneLeftX + sceneHandler.resetBuffer, 0, transform.position.z);
        }
        else if (transform.position.x >= sceneHandler.safeZoneRightX)
        {
            transform.position = new Vector3(sceneHandler.safeZoneRightX - sceneHandler.resetBuffer, 0, transform.position.z);
        }
        else if (transform.position.y < sceneHandler.ground.transform.position.y)
        {
            transform.position = new Vector3(transform.position.x, sceneHandler.ground.transform.position.y + sceneHandler.resetBuffer, transform.position.z);
        }
        else if (transform.position.z != sceneHandler.ground.transform.position.z)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, sceneHandler.ground.transform.position.z);
        }
    }

    // Ground detection now lives in GroundDetector.cs, shared across any
    // controller type (human or AI) driving this character.
}
