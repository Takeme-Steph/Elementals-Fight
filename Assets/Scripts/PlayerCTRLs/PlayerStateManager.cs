using UnityEngine;

public class PlayerStateManager : MonoBehaviour
{
    // Player state flags
    public bool isAttacking;
    public bool isJumping; // track when the player is jumping
    public bool isIdle;
    public bool isWalking;

    public bool isOnGround; // track when the player is on ground

    // Animation
    private Animator animator; // Reference to the player's Animator component
    private bool hasAnimator;

    // Animation IDs
    private int animIDAttack;
    private int animIDWalk;
    private int animIDIdle;
    private int animIDJump;
    private int animIDGrounded;
    private int animIDDirection;
    private int animIDSpeed;

    void OnEnable()
    {
        hasAnimator = TryGetComponent(out animator);
        if (!hasAnimator)
            Debug.LogError(gameObject.name + " has no animator controller attached");

        AssignAnimationIDs(); // assign animations to the animation IDs
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void StartAttacking()
    {
        ResetAnimationFlags();
        isAttacking = true;
        PlayAnimation(animIDAttack); // Trigger attack animation
    }

    public void StopAttacking()
    {
        isAttacking = false;
        BeIdle();
    }

    public void StartJumping()
    {
        ResetAnimationFlags();
        PlayAnimation(animIDJump); // Trigger jump animation
        isJumping = true;
    }

    public void StopJumping()
    {
        isJumping = false;
        BeIdle();
    }

    public void BeIdle()
    {
        ResetAnimationFlags();
        isIdle = true;
        animator.SetBool(animIDIdle, true);
    }

    public void StartWalking(float moveDirection, float speed)
    {
        if (!isWalking)
        {
            ResetAnimationFlags();
            isWalking = true;
            animator.SetFloat(animIDDirection, moveDirection); // Set player walk direction
            animator.SetFloat(animIDSpeed, speed);
            animator.SetBool(animIDWalk, true);
            
        }
    }

    public void StopWalking()
    {
        if (isWalking)
        {
            ResetAnimationFlags();
            BeIdle();
        }
    }

    public void BeGrounded()
    {
        isOnGround = true;
        animator.SetBool(animIDGrounded, true);
    }

    public void LeaveGround()
    {
        isOnGround = false;
        animator.SetBool(animIDGrounded, false);
    }

    private void AssignAnimationIDs()
    {
        animIDWalk = Animator.StringToHash("isWalking");
        animIDIdle = Animator.StringToHash("isIdle");
        animIDJump = Animator.StringToHash("Jump");
        animIDGrounded = Animator.StringToHash("isGrounded");
        animIDDirection = Animator.StringToHash("Direction");
        animIDAttack = Animator.StringToHash("Attack");
        animIDSpeed = Animator.StringToHash("Speed");
    }

    private void PlayAnimation(int animationID)
    {
        if (!hasAnimator)
            return;

        animator.SetTrigger(animationID);
    }
    
    public void ResetAnimationFlags()
    {
        isWalking = false;
        isJumping = false;
        isAttacking = false;
        isIdle = false;
        animator.SetBool(animIDWalk, false);
        animator.SetBool(animIDIdle, false);
        animator.SetFloat(animIDSpeed, 0);
        // Add any other animation flags you want to reset here
    }
}
