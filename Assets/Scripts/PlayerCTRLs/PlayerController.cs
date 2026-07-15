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

    private Vector2 moveDirection;

    private void OnEnable()
    {
        // Subscribe to events
        inputReader.MoveEvent += HandleMove;
        inputReader.JumpEvent += HandleJump;
        inputReader.JumpCanceledEvent += HandleCanceledJump;
        inputReader.AttackEvent += HandleAttack;

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
    }

    void Start()
    {
        // Try to get player rigidbody and throw an error message if not found
        if (!TryGetComponent<Rigidbody>(out playerRigidbody))
            Debug.LogError(gameObject.name + " has no rigidbody attached");

        // Try to get scene handler script and throw an error message if not found
        if (!GameObject.Find("GameManager").TryGetComponent<SceneHandler>(out sceneHandler))
            Debug.LogError("No scene handler script found in scene. Game will not run");
    }

    void Update()
    {
        if (sceneHandler == null) return; // guard against the Find() above failing

        if (!sceneHandler.isGameOver && sceneHandler.activeMatch)
        {
            Jump();
            KeepInBounds();
            Attack();
        }
    }

    void FixedUpdate()
    {
        Move();
        GroundCheck();
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
        if (isAttacking)
        {
            isAttacking = false;
            if (stateMachine.CanAttack)
            {
                stateMachine.RequestAttack();
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

    //Check when the player hits the ground
    private void GroundCheck()
    {
        RaycastHit groundHit;
        Physics.Raycast(transform.position, transform.TransformDirection(Vector3.down), out groundHit, 0.1f, sceneHandler.groundLayerMask);
        Color rayColor;
        if (groundHit.collider != null)
        {
            rayColor = Color.green;
            if (!stateMachine.IsOnGround) stateMachine.SetGrounded(true);
        }
        else
        {
            rayColor = Color.red;
            if (stateMachine.IsOnGround) stateMachine.SetGrounded(false);
        }
        Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.down) * 0.1f, rayColor);
    }
}
