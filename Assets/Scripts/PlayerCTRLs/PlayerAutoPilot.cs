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
    private bool wantsToBlock;
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

        // Block is a held state, not a one-shot action, so this needs to be
        // reasserted continuously rather than only at decision time -
        // RequestBlock is idempotent (safe to call every frame with the
        // same value), same as PlayerController relies on for input.
        stateMachine.RequestBlock(wantsToBlock);

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

    private void MakeDecision()
    {
        GameObject opponent = sceneHandler.GetMainPlayer();
        if (opponent == null)
        {
            moveDirection = Vector2.zero;
            return;
        }

        float delta = opponent.transform.position.x - transform.position.x;
        float distance = Mathf.Abs(delta);

        if (distance > attackRange)
        {
            // Out of range - close the distance, don't block while approaching.
            moveDirection = new Vector2(Mathf.Sign(delta), 0f);
            wantsToBlock = false;
        }
        else
        {
            // In range - stop closing and pick an action.
            moveDirection = Vector2.zero;

            float roll = Random.value;
            if (roll < blockChance)
            {
                wantsToBlock = true;
            }
            else
            {
                wantsToBlock = false;
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
