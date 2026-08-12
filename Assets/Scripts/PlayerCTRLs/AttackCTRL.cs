using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Carries what actually happened in a hit, so the receiving PlayerManager
// can decide between a normal hitstun reaction and a knockback, and which
// direction to physically push the target.
public class HitInfo
{
    public float damage;
    public bool causesKnockback;
    public float knockbackDirectionX; // -1 or 1, horizontal push direction

    public HitInfo(float damage, bool causesKnockback, float knockbackDirectionX)
    {
        this.damage = damage;
        this.causesKnockback = causesKnockback;
        this.knockbackDirectionX = knockbackDirectionX;
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
    // attack is actually allowed to happen, so no guard needed for match state here.
    public void Attack(bool isHeavy)
    {
        // Some characters (e.g. Ninja, pending his model replacement) still ship with
        // no hitbox marker rig at all - attackColliders[0] used to throw an
        // IndexOutOfRangeException on their very first attack. Degrade to a
        // logged no-op instead so a missing rig fails loudly in the console
        // rather than crashing the match.
        if (attackColliders == null || attackColliders.Length == 0)
        {
            Debug.LogError($"{name}: AttackCTRL.attackColliders is empty - this character is missing its hitbox marker rig, attack will deal no damage.");
            return;
        }

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

            float knockbackDirectionX = Mathf.Sign(parentObject.transform.position.x - transform.position.x);
            parentObject.SendMessage("Hit", new HitInfo(damage, isHeavy, knockbackDirectionX));
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
