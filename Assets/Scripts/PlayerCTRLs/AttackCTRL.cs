using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AttackCTRL : MonoBehaviour
{
    public Collider[] attackColliders; // collection of the player's attack colliders

    // Called by PlayerStateMachine when it enters the Attacking state -
    // the state machine already guarantees this is only called when an
    // attack is actually allowed to happen, so no guard needed here.
    public void Attack()
    {
        // initiate attack and get hitboxes that overlap with the attack hitboxes
        Collider col = attackColliders[0];
        Collider[] cols = Physics.OverlapBox(col.bounds.center, col.bounds.extents, col.transform.rotation,
            LayerMask.GetMask("Hitbox"));

        //ignore player hitboxes and deal damage to opponent
        foreach (Collider c in cols)
        {
            GameObject parentObject = FindTopmostParent(c.transform.gameObject);
            if (parentObject.transform == transform) { continue; }
            Debug.Log(c.name);
            float damage = 10;

            parentObject.SendMessage("Hit", damage);
            break;
        }
    }

    // Function to find the topmost parent of a child object (move to util file?)
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
