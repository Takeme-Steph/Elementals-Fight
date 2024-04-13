using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackCTRL : MonoBehaviour
{
    [SerializeField] private InputReader input; // Reference the 3rd party input reader
    
    private PlayerStateManager playerState; // reference of the players rigid body.
 

    private void OnEnable()
    {
       
    }
    
    private void OnDisable()
    {
        
    }

    // Start is called before the first frame update
    void Start()
    {
        TryGetComponent<PlayerStateManager>(out playerState);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void Attack()
    {
        if(!playerState.isAttacking)
        {
            playerState.StartAttacking();  
        }
    }

}
