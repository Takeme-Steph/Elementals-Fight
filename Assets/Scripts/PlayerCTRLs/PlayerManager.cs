using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    public bool isCTRLPlayer; // flag if this is a player controlled character
    public float playerMaxHealth = 100;
    public float playerHealth = 100;
    private PlayerStateManager playerState;
    public int matchWinCount;
    private SceneHandler sceneHandler;
    private bool gotHit;
    private float hitPoints;
    // Start is called before the first frame update
    void Start()
    {
        TryGetComponent<PlayerStateManager>(out playerState);

        TryGetComponent<SceneHandler>(out sceneHandler);
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    void LateUpdate()
    {
        TakeDamage(hitPoints);
    }

    private void TakeDamage(float damage)
    {
        if(!playerState.isInvincible && gotHit)
        {
            playerState.BeHit();
            playerHealth -= damage;
            gotHit = false;
            hitPoints = 0;

            if(playerHealth <= 0)
            {
                sceneHandler.MatchOver();
            }
        }

    }
    public void Hit(float damage)
    {
        gotHit = true;
        hitPoints = damage;
    }
}
