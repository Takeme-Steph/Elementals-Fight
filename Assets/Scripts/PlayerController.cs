using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private InputReader input;
    [SerializeField] private float playerMoveSpeed; //this is players movement speed.
    [SerializeField] private float playerJumpForce; //this is players jump force.
    private Rigidbody _playerRb; //reference of the players rigid body.
    private Vector2 _moveDirection;

    private bool _isJumping;

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
        transform.position += new Vector3(_moveDirection.x,0, 0) 
            * (playerMoveSpeed * Time.deltaTime);
    }

    private void Jump()
    {
        if(_isJumping)
        {
            _playerRb.AddForce(Vector3.up * (100 * playerJumpForce));
            //transform.position += new Vector3(0,1, 0) * (playerJumpForce * Time.deltaTime);
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
}
