using UnityEngine;

// AI opponent controller. Mirrors PlayerController's responsibilities but
// decides its own actions instead of reading input - both go through the
// same PlayerStateMachine / CharacterPhysics, so the AI gets exactly the
// same movement, attack, block, and jump behaviour a human would.
public class PlayerAutoPilot : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float jumpForce = 20f;

    [Header("Combat range")]
    [SerializeField] private float attackRange = 2.5f;

    [Header("Decision timing")]
    [SerializeField] private float decisionInterval = 0.6f;
    [SerializeField] private float decisionIntervalVariance = 0.25f;

    [Header("Behaviour weights (0-1)")]
    [SerializeField] private float blockChance = 0.25f;
    [SerializeField] private float heavyAttackChance = 0.3f;
    [SerializeField] private float jumpChance = 0.05f;

    private CharacterPhysics characterPhysics;
    private PlayerStateMachine stateMachine;
    private SceneHandler sceneHandler;

    private Vector2 moveDirection;
    private float decisionTimer;

    private void Start()
    {
        if (!TryGetComponent(out characterPhysics))
            Debug.LogError(gameObject.name + " has no CharacterPhysics component attached");

        if (!TryGetComponent(out stateMachine))
            Debug.LogError(gameObject.name + " has no PlayerStateMachine component attached");

        sceneHandler = SceneHandler.Instance;
        if (sceneHandler == null)
            Debug.LogError("No SceneHandler.Instance found. Game will not run.");

        decisionTimer = decisionInterval;
    }

    private void Update()
    {
        if (sceneHandler == null)
        {
            sceneHandler = SceneHandler.Instance;
            if (sceneHandler == null) return;
        }

        if (sceneHandler.isGameOver || !sceneHandler.activeMatch) return;

        // Fixed: movement used to only get recalculated at the (slow, ~0.6-1s)
        // decision tick, same as the attack/block choice. That meant the AI
        // kept walking toward the player for up to a full decision interval
        // after already being in range or physically touching them - it
        // wasn't "unable to tell it was close enough", it just hadn't
        // re-checked yet. Distance/movement now updates every frame, fully
        // decoupled from the slower attack/block/jump decision timer, which
        // stays deliberately unhurried so the AI doesn't feel robotic.
        UpdateMovement();

        decisionTimer -= Time.deltaTime;
        if (decisionTimer <= 0f)
        {
            MakeDecision();
            decisionTimer = decisionInterval + Random.Range(-decisionIntervalVariance, decisionIntervalVariance);
        }
    }

    private void FixedUpdate()
    {
        if (sceneHandler == null || characterPhysics == null) return;
        characterPhysics.MoveHorizontal(moveDirection, moveSpeed);
    }

    // Continuous, every-frame: purely "should I still be closing the
    // distance right now". Nothing here is a considered decision, it's just
    // physically reacting to where the opponent currently is.
    private void UpdateMovement()
    {
        GameObject opponent = sceneHandler.GetMainPlayer();
        if (opponent == null)
        {
            moveDirection = Vector2.zero;
            return;
        }

        float delta = opponent.transform.position.x - transform.position.x;
        float distance = Mathf.Abs(delta);

        moveDirection = distance > attackRange ? new Vector2(Mathf.Sign(delta), 0f) : Vector2.zero;
    }

    // Slower, deliberate: what to actually DO once in range - attack, block,
    // or occasionally jump. Kept on its own timer so the AI reads as making
    // considered choices rather than reacting instantly every frame.
    private void MakeDecision()
    {
        GameObject opponent = sceneHandler.GetMainPlayer();
        if (opponent == null) return;

        float delta = opponent.transform.position.x - transform.position.x;
        float distance = Mathf.Abs(delta);

        if (distance > attackRange)
        {
            stateMachine.RequestBlock(false);
        }
        else
        {
            float roll = Random.value;
            if (roll < blockChance)
            {
                stateMachine.RequestBlock(true);
            }
            else
            {
                stateMachine.RequestBlock(false);
                if (stateMachine.CanAttack)
                {
                    bool heavy = Random.value < heavyAttackChance;
                    stateMachine.RequestAttack(heavy);
                }
            }
        }

        // Small, occasional chance to jump regardless of range - independent
        // of the block/attack roll above so it doesn't crowd out other actions.
        if (Random.value < jumpChance && stateMachine.CanJump)
        {
            stateMachine.RequestJump();
            characterPhysics.ApplyJumpForce(jumpForce);
        }
    }
}
