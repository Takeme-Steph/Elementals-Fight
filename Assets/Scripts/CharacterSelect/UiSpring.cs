using UnityEngine;

// Small allocation-free damped-spring integrator shared by every animated widget in
// CharacterSelect. Everything here is a pure function over caller-owned state (current
// value + ref velocity) so nothing needs per-instance heap objects or coroutines.

/// <summary>
/// Semi-implicit (symplectic) Euler damped spring, the same integration scheme as most
/// UI tween libraries: cheap, stable for the stiffness/damping ranges used across this
/// screen, and allocation-free.
/// </summary>
public static class UiSpring
{
    /// <summary>Advances a scalar spring by one step and returns the new value.</summary>
    public static float Step(float current, float target, ref float velocity, float stiffness, float damping, float dt)
    {
        // Clamp dt at the call sites (not here) so a single hitch never blows up the
        // integrator; this function trusts whatever dt it is given.
        float force = (target - current) * stiffness - velocity * damping;
        velocity += force * dt;
        return current + velocity * dt;
    }

    public static Vector2 Step(Vector2 current, Vector2 target, ref Vector2 velocity, float stiffness, float damping, float dt)
    {
        Vector2 force = (target - current) * stiffness - velocity * damping;
        velocity += force * dt;
        return current + velocity * dt;
    }

    public static Vector3 Step(Vector3 current, Vector3 target, ref Vector3 velocity, float stiffness, float damping, float dt)
    {
        Vector3 force = (target - current) * stiffness - velocity * damping;
        velocity += force * dt;
        return current + velocity * dt;
    }

    /// <summary>
    /// True once a spring is close enough to target and slow enough that callers can
    /// stop ticking it (and, for graphics, stop calling SetVerticesDirty every frame).
    /// </summary>
    public static bool Settled(float current, float target, float velocity, float eps = 0.001f)
    {
        return Mathf.Abs(target - current) < eps && Mathf.Abs(velocity) < eps;
    }
}
