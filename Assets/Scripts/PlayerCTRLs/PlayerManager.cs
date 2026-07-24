using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public bool isCTRLPlayer; // flag if this is a player controlled character
    public float playerMaxHealth = 100;
    public float playerHealth = 100;
    private PlayerStateMachine stateMachine;
    private CharacterPhysics characterPhysics;
    public int matchWinCount;
    private SceneHandler sceneHandler;
    private bool gotHit;
    private float hitPoints;
    private bool hitCausesKnockback;
    private float hitKnockbackDirectionX;

    // Magnitude of the physical push applied on a knockback-causing hit.
    [SerializeField] private float knockbackForce = 8f;

    // Chip damage taken while blocking - partial mitigation, not full
    // invincibility, so block-breakers/combos can bypass it later without
    // needing to rework this system.
    [SerializeField] private float blockDamageMultiplier = 0.2f;

    // Start is called before the first frame update
    void Start()
    {
        TryGetComponent<PlayerStateMachine>(out stateMachine);
        TryGetComponent<CharacterPhysics>(out characterPhysics);

        // Fixed: this used to do TryGetComponent<SceneHandler>(out sceneHandler)
        // on this same GameObject - but SceneHandler lives on GameManager, not
        // on the player, so this always silently returned false and left
        // sceneHandler null. sceneHandler.MatchOver() would have crashed the
        // instant anyone's health actually reached zero.
        sceneHandler = SceneHandler.Instance;
        if (sceneHandler == null)
            Debug.LogError("No SceneHandler.Instance found. Game will not run.");
    }

    void LateUpdate()
    {
        if (sceneHandler == null)
        {
            sceneHandler = SceneHandler.Instance;
            if (sceneHandler == null) return;
        }

        TakeDamage(hitPoints);
    }

    private void TakeDamage(float damage)
    {
        if (!stateMachine.IsInvincible && gotHit)
        {
            bool isBlocking = stateMachine.CurrentStateType == PlayerStateType.Blocking;

            if (isBlocking)
            {
                // Blocked hit: reduced chip damage, stay in Blocking - no
                // hitstun/knockdown interruption.
                playerHealth -= damage * blockDamageMultiplier;
            }
            else
            {
                if (hitCausesKnockback)
                {
                    stateMachine.EnterKnockback();
                    if (characterPhysics != null)
                        characterPhysics.ApplyImpulse(new Vector3(hitKnockbackDirectionX * knockbackForce, 0f, 0f));
                }
                else
                {
                    stateMachine.EnterHitstun();
                }

                playerHealth -= damage;
            }

            gotHit = false;
            hitPoints = 0;
            hitCausesKnockback = false;

            if (playerHealth <= 0)
            {
                sceneHandler.MatchOver();
            }
        }
    }

    public void Hit(HitInfo info)
    {
        gotHit = true;
        hitPoints = info.damage;
        hitCausesKnockback = info.causesKnockback;
        hitKnockbackDirectionX = info.knockbackDirectionX;
    }
}
