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

    public void TakeDamage(float damage)
    {
        if(!playerState.isInvincible)
        {
            playerState.BeHit();
            playerHealth -= damage;

            if(playerHealth <= 0)
            {
                sceneHandler.MatchOver();
            }
        }

        
    }
}
