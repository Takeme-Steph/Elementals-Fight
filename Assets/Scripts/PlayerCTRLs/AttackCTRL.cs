using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackCTRL : MonoBehaviour
{
    [SerializeField] private InputReader input; // Reference the 3rd party input reader
    
    private PlayerStateManager playerState; // reference of the players rigid body.
    public Collider[] attackColliders;// colletion of the player's attack colliders
 

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

    //Handle player attack
    public void Attack()
    {
        if(!playerState.isAttacking)
        {
            // initiate attack state and get hitboxes that overlap with the attack hitboxes
            playerState.StartAttacking(); 
            Collider col = attackColliders[0];
            Collider[] cols = Physics.OverlapBox(col.bounds.center,col.bounds.extents,col.transform.rotation,
                LayerMask.GetMask("Hitbox"));
            
            //ignore player hitboxes and deal damage to opponent
            foreach (Collider c in cols)
            {
                GameObject parentObject = FindTopmostParent(c.transform.gameObject);
                if(parentObject.transform == transform) {continue;}
                Debug.Log(c.name);
                float damage = 10;

                parentObject.SendMessage("TakeDamage", damage);
            }
        }
    }


    // Function to find the topmost parent of a child object
    public GameObject FindTopmostParent(GameObject child)
    {
        // If the parent is null, then this is the topmost parent
        if (child.transform.parent == null)
        {
            return child;
        }
        else
        {
            // Recursively check the parent of the current object
            return FindTopmostParent(child.transform.parent.gameObject);
        }
    }

}

