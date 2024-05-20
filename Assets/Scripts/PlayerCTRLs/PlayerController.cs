using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputReader inputReader; // Reference the 3rd party input reader
    //[SerializeField] private int playerHP = 100; // The player's health points.
    [SerializeField] private float playerMoveSpeed = 7f; // Player's movement speed.
    [SerializeField] private float playerJumpForce = 200f; 
    [SerializeField] private AttackCTRL attackController;


    private Rigidbody playerRigidbody;
    private PlayerStateManager playerStateManager;
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
        if (jump && playerStateManager.isOnGround)
        {
            playerStateManager.StartJumping();
            //playerRigidbody.velocity = Vector3.up * playerJumpForce * Time.deltaTime;
            playerRigidbody.AddForce(Vector3.up * playerJumpForce * Time.deltaTime, ForceMode.Impulse);
        }
        jump = false;
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
        //bool hitGround;
        //Physics.Raycast(boxCollider.bounds.center, transform.TransformDirection(Vector3.down), boxCollider.bounds.extents.y);
        Physics.Raycast(transform.position, transform.TransformDirection(Vector3.down), out groundHit, 0.1f, sceneHandler.groundLayerMask);
        Color rayColor;
        if (groundHit.collider != null)
        {
            rayColor = Color.green;
            if(!playerStateManager.isOnGround) {playerStateManager.BeGrounded();}
            if (playerStateManager.isJumping) {playerStateManager.StopJumping();}
            //hitGround = true;
        }
        else
        {
            rayColor = Color.red;
            playerStateManager.LeaveGround();
            //hitGround = false;
        }
        Debug.DrawRay(transform.position, transform.TransformDirection(Vector3.down)*0.1f, rayColor);
        //return hitGround;
    }
}
