using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM 
using UnityEngine.InputSystem;
#endif

public class PlayerController : MonoBehaviour
{
    // Player properties
    [SerializeField] private InputReader input;
    [SerializeField] private int _playerHP = 100; // The player's health points.
    [SerializeField] private float _playerMoveSpeed; //this is players movement speed.
    [SerializeField] private float _playerJumpForce; //this is players jump force.
    public bool _isMainPlayer; // flag if this is the player controlled character

    // hardcoded variables that need to be updated
    
    
    // Flags
    private bool _isOnGround; // track when the player is on ground
    private bool _isJumping; // track when the player is jumping

    private Rigidbody playerRb; // reference of the players rigid body.
    private Vector2 _moveDirection; // Vector 2 value of player's movement via the input system
    private SceneHandler sceneHandler; // Reference the scene handler script  

    // Start is called before the first frame update
    void Start()
    {
        // Try to get player rigidbody and throw an error message if not found
        if(!TryGetComponent<Rigidbody>(out playerRb))
        {
            Debug.Log(gameObject.name + "has no rigidbody attached");
        }
        
        // subscribe to events
        input.MoveEvent += HandleMove;
        input.JumpEvent += HandleJump;
        input.JumpCanceledEvent += HandleCanceledJump; 

        // Initialize variables
        _playerMoveSpeed = 10;
        _playerJumpForce = 7;

        // Tryto get scene handler script and throw an error message if not found
        if(!GameObject.Find("GameManager").TryGetComponent<SceneHandler>(out sceneHandler))
        {
            Debug.Log("No scene handler script found in scene. Game will not run");
        }    
    }

    private void OnDisable()
    {
        // Unsubscribe to events
        input.MoveEvent -= HandleMove;
        input.JumpEvent -= HandleJump;
        input.JumpCanceledEvent -= HandleCanceledJump; 
    }

    // Update is called once per frame
    void Update()
    {   
        // Only run if the gameplay is still ative
        if(!sceneHandler._isGameOver)
        {
            Move();
            Jump();
            KeepInBounds();
        }
    }

    // Move the player on the x axis
    private void Move()
    {
        // Do nothing if player is not moving
        if(_moveDirection == Vector2.zero)
        {
            return;
        }
        // Only allow movement on the x axis if within bounds
        else if(transform.position.x > sceneHandler._safeZoneLeftX && 
            transform.position.x < sceneHandler._safeZoneRightX)
        {
            transform.Translate(new Vector3(_moveDirection.x,0, 0) * (_playerMoveSpeed * Time.deltaTime), 
            Space.World);
        }
    }

    // Make player jump
    private void Jump()
    {
        // check if player is on ground and starts jumping
        if(_isJumping && _isOnGround)
        {
            playerRb.AddForce(Vector3.up * _playerJumpForce, ForceMode.Impulse); // imediately jump
            _isOnGround = false; // set the player on ground check to false
        }
    }

    private void HandleMove(Vector2 direction)
    {
        _moveDirection = direction; // Set the movement direction
    }

    private void HandleJump()
    {
        _isJumping = true;
    }

    private void HandleCanceledJump()
    {
        _isJumping = false;
    }

    private void KeepInBounds()
    {
        // Reset player position if they move out of bounds
        if (transform.position.x <= sceneHandler._safeZoneLeftX)
        {
            transform.position = new Vector3(sceneHandler._safeZoneLeftX + sceneHandler._resetBuffer, 0, 
            transform.position.z);
        }
        else if (transform.position.x >= sceneHandler._safeZoneRightX)
        {
            transform.position = new Vector3(sceneHandler._safeZoneRightX - sceneHandler._resetBuffer, 0, 
            transform.position.z);
        }
        else if (transform.position.y < sceneHandler.ground.transform.position.y)
        {
            transform.position = new Vector3(transform.position.x, sceneHandler.ground.transform.position.y
            + sceneHandler._resetBuffer, transform.position.z);
        }
        else if (transform.position.z != sceneHandler.ground.transform.position.z)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y,
                sceneHandler.ground.transform.position.z);
        }
        else
        {
            return;
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Set player on ground flag when he player collides with the ground
        if(collision.gameObject.CompareTag("Ground"))
        {
            _isOnGround = true;
        }
        
    }
}
