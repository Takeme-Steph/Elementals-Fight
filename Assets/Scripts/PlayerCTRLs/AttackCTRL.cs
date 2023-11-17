using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackCTRL : MonoBehaviour
{
    [SerializeField] private InputReader input; // Reference the 3rd party input reader
    
    public bool _attack;
    private PlayerStateManager playerState; // reference of the players rigid body.
 

    private void OnEnable()
    {
        // subscribe to events
        input.AttackEvent += HandleAttack;
    }
    
    private void OnDisable()
    {
        // Unsubscribe to events
        input.AttackEvent -= HandleAttack;
    }

    // Start is called before the first frame update
    void Start()
    {
        TryGetComponent<PlayerStateManager>(out playerState);
    }

    // Update is called once per frame
    void Update()
    {
        Attack();
    }

    private void Attack()
    {
        if(_attack & !playerState._isAttacking)
        {
            _attack = false;
            playerState.StartAttacking();  
        }
    }


    private void HandleAttack()
    {
        _attack = true;
    }
}
