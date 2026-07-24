using UnityEngine;

// Owns the physics-facing side of a character: the Rigidbody reference,
// ground detection, and physics actions like jumping. Both PlayerController
// (human input) and PlayerAutoPilot (AI) go through this instead of each
// independently touching a Rigidbody or duplicating ground-check logic -
// PlayerStateMachine stays purely about state/animation and never needs to
// know anything about physics.
public class CharacterPhysics : MonoBehaviour
{
    [SerializeField] private float rayOriginOffset = 0.15f;
    [SerializeField] private float rayDistance = 0.3f;
    [SerializeField] private LayerMask groundLayerMask;

    private Rigidbody rb;
    private PlayerStateMachine stateMachine;

    private void Awake()
    {
        if (!TryGetComponent(out rb))
            Debug.LogError(gameObject.name + " has no Rigidbody attached");

        TryGetComponent(out stateMachine);
        if (stateMachine == null)
            Debug.LogError(gameObject.name + ": CharacterPhysics requires a PlayerStateMachine on the same GameObject.");

        // Default to SceneHandler's ground layer mask if one isn't explicitly
        // set on this component, so existing setups keep working without
        // needing to re-configure every character prefab by hand.
        if (groundLayerMask == 0 && SceneHandler.Instance != null)
            groundLayerMask = SceneHandler.Instance.groundLayerMask;
    }

    private void FixedUpdate()
    {
        if (stateMachine == null) return;

        KeepInBounds();

        Vector3 origin = transform.position + Vector3.up * rayOriginOffset;
        RaycastHit groundHit;
        bool grounded = Physics.Raycast(origin, transform.TransformDirection(Vector3.down), out groundHit, rayDistance, groundLayerMask);

        Color rayColor = grounded ? Color.green : Color.red;
        Debug.DrawRay(origin, transform.TransformDirection(Vector3.down) * rayDistance, rayColor);

        // Fixed: landing never zeroed out the Rigidbody's downward velocity -
        // only the transform position got corrected when sinking below
        // ground (in KeepInBounds), while gravity kept accelerating the
        // Rigidbody downward every tick regardless. That residual velocity
        // immediately punched the character back through the floor on the
        // very next physics step, deeper each time - a runaway sink that
        // only became visible once jump/knockback velocities got large
        // enough for the effect to be noticeable.
        if (grounded && rb.linearVelocity.y < 0f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }

        // Deliberately unconditional, not edge-triggered: an Animator state with
        // "Write Defaults" enabled can silently reset bool parameters (like
        // isGrounded) back to false the instant it's entered, without our
        // code doing anything wrong. Pushing this every physics tick means
        // the Animator can never drift out of sync with the real ground
        // state, regardless of what any given Animator state's Write
        // Defaults setting does. SetGrounded()/state transitions are already
        // safe to call repeatedly (ChangeState no-ops on same-state).
        stateMachine.SetGrounded(grounded);
    }

    // Applies an instantaneous upward impulse - correct use of ForceMode.Impulse,
    // not scaled by Time.deltaTime (that was the original jump bug).
    public void ApplyJumpForce(float force)
    {
        rb.AddForce(Vector3.up * force, ForceMode.Impulse);
    }

    // General-purpose impulse for future use (knockback, dash, etc.) so
    // neither controller needs direct Rigidbody access for that either.
    public void ApplyImpulse(Vector3 impulse)
    {
        rb.AddForce(impulse, ForceMode.Impulse);
    }

    // Moved here from PlayerController so both human and AI controllers get
    // correct, bounds-aware movement without duplicating translate/clamp
    // logic - a controller just decides direction and speed, this handles
    // actually applying it safely.
    public void MoveHorizontal(Vector2 direction, float speed)
    {
        if (direction == Vector2.zero)
        {
            stateMachine.Move(Vector2.zero, 0f);
            return;
        }

        if (!stateMachine.CanMove) return;

        if (SceneHandler.Instance == null) return;

        if (transform.position.x > SceneHandler.Instance.safeZoneLeftX &&
            transform.position.x < SceneHandler.Instance.safeZoneRightX)
        {
            stateMachine.Move(direction, speed);
            transform.Translate(new Vector3(direction.x, 0, 0) * (speed * Time.deltaTime), Space.World);
        }
    }

    // Keeps the character within the playable area - was previously only
    // called by PlayerController, meaning the AI-controlled opponent could
    // silently drift out of bounds with nothing correcting it. Runs
    // automatically here regardless of which controller is active.
    private void KeepInBounds()
    {
        var sh = SceneHandler.Instance;
        if (sh == null) return;

        if (transform.position.x <= sh.safeZoneLeftX)
        {
            transform.position = new Vector3(sh.safeZoneLeftX + sh.resetBuffer, 0, transform.position.z);
        }
        else if (transform.position.x >= sh.safeZoneRightX)
        {
            transform.position = new Vector3(sh.safeZoneRightX - sh.resetBuffer, 0, transform.position.z);
        }
        else if (transform.position.y < sh.ground.transform.position.y)
        {
            transform.position = new Vector3(transform.position.x, sh.ground.transform.position.y + sh.resetBuffer, transform.position.z);
            // Safety net alongside the FixedUpdate check above, in case this
            // ever fires from a state the ground raycast didn't catch.
            if (rb.linearVelocity.y < 0f)
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        }
        else if (transform.position.z != sh.ground.transform.position.z)
        {
            transform.position = new Vector3(transform.position.x, transform.position.y, sh.ground.transform.position.z);
        }
    }
}
