using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerStateManager : MonoBehaviour
{
    // Player state flags
    public bool _isAttacking;
    public bool _isOnGround; // track when the player is on ground
    public bool _isJumping; // track when the player is jumping
    public bool _isIdle;
    public bool _isWalking;

    // Animation
    private Animator _animator; // Reference to the player's Animator component
    private bool _hasAnimator;

    private int _animIDAttack;
    private int _animIDHit;
    private int _animIDDie;
    private int _animIDWalk;
    private int _animIDRun;
    private int _animIDIdle;
    private int _animIDJump;
    private int _animIDGrounded;
    private int _animIDDirection;

    
    void OnEnable()
    {
        _hasAnimator = TryGetComponent(out _animator);
        // Return error if no animator controller found for the character
        if(!_hasAnimator){Debug.Log(gameObject.name + "has no animator controller attached");}
        AssignAnimationIDs(); // assign animations to the animation IDs
    }

    // Update is called once per frame
    void Update()
    {
        
    }


    public void StartAttacking()
    {
        _isIdle = false;
        _isAttacking = true;
        _animator.SetBool(_animIDIdle, false); // turn off player idle animation
        _animator.SetTrigger(_animIDAttack);//Trigger attack animation
    }

    public void StopAttacking()
    {
        _isAttacking = false;
    }

    public void StartJumping()
    {
        _isIdle = false;
        _isJumping = true;
        _isOnGround = false;
        _animator.SetBool(_animIDIdle, false); // turn off player idle animation
        _animator.SetBool(_animIDGrounded, false); // turn off the on ground animation flag
        _animator.SetTrigger(_animIDJump); // trigger jump animation
    }

    public void StopJumping()
    {
        _isJumping = false;
    }

    public void BeIdle()
    {
        _isIdle = true;
        _animator.SetBool(_animIDIdle, true);
    }

    public void StartWalking(float _moveDirection)
    {
        _isIdle = false;
        _isWalking = true;
        _animator.SetBool(_animIDIdle, false); // turn off player idle animation
        _animator.SetFloat(_animIDDirection, _moveDirection); // Set player walk direction
        _animator.SetBool(_animIDWalk, true); // Turn on player walking animation
    }

    public void StopWalking()
    {
        _isWalking = false;
        _animator.SetBool(_animIDWalk, false); // Turn off player walking animation
    }

    public void BeGrounded()
    {
        _isOnGround = true;
        _animator.SetBool(_animIDGrounded, true);
    }




    private void AssignAnimationIDs()
    {
        _animIDWalk = Animator.StringToHash("isWalking") ;
        _animIDRun = Animator.StringToHash("isRuning");
        _animIDIdle = Animator.StringToHash("isIdle");
        _animIDJump = Animator.StringToHash("Jump");
        _animIDGrounded = Animator.StringToHash("isGrounded");
        _animIDDirection = Animator.StringToHash("Direction");
        _animIDAttack = Animator.StringToHash("Attack");

        _animIDHit = Animator.StringToHash("Hit");
        _animIDDie = Animator.StringToHash("Die");

    }
}
