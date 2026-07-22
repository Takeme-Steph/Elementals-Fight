using UnityEngine;

// Dedicated ground-detection component. This exists so that ground/air
// state works correctly regardless of which controller is driving the
// character (PlayerController for human input, PlayerAutoPilot for AI,
// or anything else added later) - none of those need to own this logic
// themselves, and PlayerStateMachine stays purely about state/animation,
// not physics.
public class GroundDetector : MonoBehaviour
{
    [SerializeField] private float rayOriginOffset = 0.15f;
    [SerializeField] private float rayDistance = 0.3f;
    [SerializeField] private LayerMask groundLayerMask;

    private PlayerStateMachine stateMachine;

    private void Awake()
    {
        TryGetComponent(out stateMachine);
        if (stateMachine == null)
            Debug.LogError(gameObject.name + ": GroundDetector requires a PlayerStateMachine on the same GameObject.");

        // Default to SceneHandler's ground layer mask if one isn't explicitly
        // set on this component, so existing setups keep working without
        // needing to re-configure every character prefab by hand.
        if (groundLayerMask == 0 && SceneHandler.Instance != null)
            groundLayerMask = SceneHandler.Instance.groundLayerMask;
    }

    private void FixedUpdate()
    {
        if (stateMachine == null) return;

        Vector3 origin = transform.position + Vector3.up * rayOriginOffset;
        RaycastHit groundHit;
        bool grounded = Physics.Raycast(origin, transform.TransformDirection(Vector3.down), out groundHit, rayDistance, groundLayerMask);

        Color rayColor = grounded ? Color.green : Color.red;
        Debug.DrawRay(origin, transform.TransformDirection(Vector3.down) * rayDistance, rayColor);

        if (grounded && !stateMachine.IsOnGround) stateMachine.SetGrounded(true);
        else if (!grounded && stateMachine.IsOnGround) stateMachine.SetGrounded(false);
    }
}
