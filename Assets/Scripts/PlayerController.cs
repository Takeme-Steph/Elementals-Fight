using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputReader input;
    [SerializeField] private float playerMoveSpeed = 3; //this is players movement speed.
    [SerializeField] private float playerJumpForce = 5; //this is players jump force.
    [SerializeField] private float envMaxSizeX = 65.0f; //max lenght of the play envirenment.
    [SerializeField] private float envMinSizeX = -34.0f; //min lenght of the play environment.
    private float bufferX = 0.1f; // buffer amount to substract/ add when player reaches edge of the screen.
    
    public bool isOnGround; // track when the player is on ground
    private Rigidbody _playerRb; //reference of the players rigid body.
    private Vector2 _moveDirection;

    private bool _isJumping; // track when the player is jumping

    // Start is called before the first frame update
    void Start()
    {
        _playerRb = GetComponent<Rigidbody>(); //get player rigidbody
        // subsribe to events
        input.MoveEvent += HandleMove;
        input.JumpEvent += HandleJump;
        input.JumpCanceledEvent += HandleCanceledJump; 
    }
    private void OnDisable()
    {
        // Unsubsribe to events
        input.MoveEvent -= HandleMove;
        input.JumpEvent -= HandleJump;
        input.JumpCanceledEvent -= HandleCanceledJump; 
    }

    // Update is called once per frame
    void Update()
    {
        Move();
        Jump();
    }

    private void Move()
    {
        if(_moveDirection == Vector2.zero)
        {
            return;
        }

        // Only allow movement on the x axis
        // Reset player position if they move out of bounds
        if (transform.position.x < envMinSizeX)
        {
            transform.position = new Vector3(envMinSizeX + bufferX, 0, transform.position.z);
        }
        else if (transform.position.x >= envMaxSizeX)
        {
            transform.position = new Vector3(envMaxSizeX - bufferX, 0, transform.position.z);
        }
        else
        {
            transform.position += new Vector3(_moveDirection.x,0, 0) * (playerMoveSpeed * Time.deltaTime);
        }
    }

    private void Jump()
    {
        if(_isJumping && isOnGround)
        {
            _playerRb.AddForce(Vector3.up * playerJumpForce, ForceMode.Impulse); // imediately jump
            isOnGround = false; // set the player on ground check to false
        }
    }

    private void HandleMove(Vector2 direction)
    {
        _moveDirection = direction;
    }

    private void HandleJump()
    {
        _isJumping = true;
    }

    private void HandleCanceledJump()
    {
        _isJumping = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        isOnGround = true;
    }
}
