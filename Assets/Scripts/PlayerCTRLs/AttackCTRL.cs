using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Carries what actually happened in a hit, so the receiving PlayerManager
// can decide between a normal hitstun reaction and a full knockdown.
public class HitInfo
{
    public float damage;
    public bool causesKnockdown;

    public HitInfo(float damage, bool causesKnockdown)
    {
        this.damage = damage;
        this.causesKnockdown = causesKnockdown;
    }
}

public class AttackCTRL : MonoBehaviour
{
    public Collider[] attackColliders; // collection of the player's attack colliders

    // Placeholder damage values - tune these once you've playtested pacing.
    [SerializeField] private float lightAttackDamage = 10f;
    [SerializeField] private float heavyAttackDamage = 20f;

    // Called by PlayerStateMachine when it enters the Attacking state -
    // the state machine already guarantees this is only called when an
    // attack is actually allowed to happen, so no guard needed here.
    public void Attack(bool isHeavy)
    {
        // initiate attack and get hitboxes that overlap with the attack hitboxes
        Collider col = attackColliders[0];
        Collider[] cols = Physics.OverlapBox(col.bounds.center, col.bounds.extents, col.transform.rotation,
            LayerMask.GetMask("Hitbox"));

        float damage = isHeavy ? heavyAttackDamage : lightAttackDamage;

        //ignore player hitboxes and deal damage to opponent
        foreach (Collider c in cols)
        {
            GameObject parentObject = FindTopmostParent(c.transform.gameObject);
            if (parentObject.transform == transform) { continue; }
            Debug.Log(c.name);

            parentObject.SendMessage("Hit", new HitInfo(damage, isHeavy));
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
