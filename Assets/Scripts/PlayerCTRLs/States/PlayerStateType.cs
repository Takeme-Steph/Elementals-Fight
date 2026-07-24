public enum PlayerStateType
{
    Idle,
    Walking,
    Jumping,
    Attacking,
    Blocking,
    Hitstun,
    Knockback
    // Knockdown will come later as a distinct, heavier state (block
    // breakers, combos, fatalities) - separate from this lighter stagger.
}
