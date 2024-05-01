using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputReader inputReader; // Reference the 3rd party input reader
    //[SerializeField] private int playerHP = 100; // The player's health points.
    [SerializeField] private float playerMoveSpeed = 7f; // Player's movement speed.
    [SerializeField] private float playerJumpForce = 100f; // Player's jump force.
    [SerializeField] private AttackCTRL attackController;

    private Rigidbody playerRigidbody; // Reference of the player's rigid body.
    private PlayerStateManager playerStateManager;
    private SceneHandler sceneHandler;

    private bool isJumping;
    private bool isAttacking;
    //private bool isGrounded;
    //private bool isMoving;

    private Vector2 moveDirection;

    private void OnEnable()
    {
        // Subscribe to events
        inputReader.MoveEvent += HandleMove;
        inputReader.JumpEvent += HandleJump;
        inputReader.JumpCanceledEvent += HandleCanceledJump;
        inputReader.AttackEvent += HandleAttack;

        TryGetComponent<PlayerStateManager>(out playerStateManager);
        if (playerStateManager == null)
        {
            Debug.LogError(gameObject.name + ": PlayerStateManager component not found.");
        }
        TryGetComponent<AttackCTRL>(out attackController);
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

        // Initialize variables
        playerMoveSpeed = 7f;
        playerJumpForce = 100f;

        // Try to get scene handler script and throw an error message if not found
        if (!GameObject.Find("GameManager").TryGetComponent<SceneHandler>(out sceneHandler))
            Debug.LogError("No scene handler script found in scene. Game will not run");

        
    }

    void Update()
    {
        if (!sceneHandler.isGameOver)
        {
            Move();
            Jump();
            KeepInBounds();
            Attack();
        }
    }

    
    private void OnCollisionEnter(Collision collision)
    {
        // Set player on ground flag when the player collides with the ground
        if (collision.gameObject.CompareTag("Ground"))
        {
            // refactor to use raycasting to hadle jump event
            playerStateManager.BeGrounded();
            playerStateManager.ResetAnimationFlags();
            playerStateManager.BeIdle();
        }
    }


    private void Move()
    {
        if (moveDirection == Vector2.zero)
        {
            playerStateManager.StopWalking();
            return;
        }

        else if (transform.position.x > sceneHandler.safeZoneLeftX &&
            transform.position.x < sceneHandler.safeZoneRightX)
        {
            playerStateManager.StartWalking(moveDirection.x, playerMoveSpeed);
            transform.Translate(new Vector3(moveDirection.x, 0, 0) * (playerMoveSpeed * Time.deltaTime), Space.World);
        }
    }

    private void Jump()
    {
        if (isJumping && playerStateManager.isOnGround)
        {
            playerStateManager.StartJumping();
            playerRigidbody.AddForce(Vector3.up * playerJumpForce * Time.deltaTime, ForceMode.Impulse);
            isJumping = false;
        }
    }

    private void Attack()
    {
        if (isAttacking)
        {
            isAttacking = false;
            attackController.Attack();
        }
    }

    private void HandleMove(Vector2 direction)
    {
        moveDirection = direction;
    }

    private void HandleJump()
    {
        isJumping = true;
    }

    private void HandleCanceledJump()
    {
        isJumping = false;
    }

    private void HandleAttack()
    {
        isAttacking = true;
    }

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
}
