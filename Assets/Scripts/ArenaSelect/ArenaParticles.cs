using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Single world-space ParticleSystem reconfigured per arena instead of swapping systems -
// keeps the mobile budget at exactly one system / 60 particles regardless of how many
// arenas ship. Apply() resets every module it might have touched last time before
// setting up the new style, so switching arenas never leaves a stale rotation/noise/
// velocity setting bleeding into the next style.
//
// NOTE: CloudMist's "two lower corners" spawn area (per the design spec) can't be done
// with a single shape module without leaving a dead gap in the middle that reads as a
// bug, not a choice - see ArenaSelectSceneBuilder's placement note. This reconfigures
// the same one shape to span the lower edge instead, which still reads as "particles
// drifting up from low in the frame" and keeps the one-ParticleSystem budget intact.

/// <summary>
/// Reconfigures the arena select screen's ambient ParticleSystem and lightning flash
/// overlay to match the currently selected arena's VFX profile.
/// </summary>
public class ArenaParticles : MonoBehaviour
{
    private const int MaxParticles = 60;

    private const float FlashUpDuration = 0.05f;
    private const float FlashDownDuration = 0.12f;
    private const float FlashPeakAlpha = 0.5f;
    private const float FlashGapMin = 4f;
    private const float FlashGapMax = 7f;
    private const float FlashBetweenPopsMin = 0.05f;
    private const float FlashBetweenPopsMax = 0.15f;
    private const int FlashPopsMin = 2;
    private const int FlashPopsMax = 5; // Random.Range(int,int) max is exclusive -> 2..4 pops.

    [SerializeField]
    private ParticleSystem particles;

    [SerializeField]
    [Tooltip("Full-screen Image tinted glow; only animated while ParticleStyle is CloudMist.")]
    private Image lightningFlash;

    private bool validRefs = true;
    private Color lightningColor = Color.white;
    private Coroutine lightningRoutine;

    private void Awake()
    {
        if (particles == null)
        {
            Debug.LogError("ArenaParticles: particles is not assigned.");
            validRefs = false;
        }

        if (lightningFlash != null)
        {
            Color c = lightningFlash.color;
            c.a = 0f;
            lightningFlash.color = c;
        }
    }

    private void OnDisable()
    {
        StopLightningLoop();
    }

    /// <summary>Reconfigures the shared particle system (and lightning overlay) for style, tinted by the arena's glow/accent.</summary>
    public void Apply(ArenaParticleStyle style, Color glow, Color accent)
    {
        if (!validRefs)
        {
            return;
        }

        StopLightningLoop();

        ParticleSystem.MainModule main = particles.main;
        ParticleSystem.EmissionModule emission = particles.emission;
        ParticleSystem.ShapeModule shape = particles.shape;
        ParticleSystem.VelocityOverLifetimeModule velocity = particles.velocityOverLifetime;
        ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
        ParticleSystem.NoiseModule noise = particles.noise;
        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;

        // Clear every module a previous style might have turned on before configuring
        // the new one, so re-Apply (switching arenas) never leaves e.g. Stardust's
        // rotation spinning underneath SandWisps.
        velocity.enabled = false;
        rotation.enabled = false;
        noise.enabled = false;

        main.maxParticles = MaxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        switch (style)
        {
            case ArenaParticleStyle.Stardust:
                ApplyStardust(main, emission, shape, velocity, rotation, colorOverLifetime, glow);
                break;
            case ArenaParticleStyle.SandWisps:
                ApplySandWisps(main, emission, shape, noise, colorOverLifetime, accent);
                break;
            case ArenaParticleStyle.CloudMist:
                ApplyCloudMist(main, emission, shape, colorOverLifetime, glow);
                StartLightningLoop(glow);
                break;
            default:
                // New ArenaParticleStyle members must be wired in above, or an arena
                // silently gets whatever the previous arena's module state left behind.
                Debug.LogError($"ArenaParticles.Apply: unhandled ArenaParticleStyle '{style}'.");
                break;
        }
    }

    // Spawns from a box shell ringing the screen edge, drifts slowly inward with a
    // gentle rotation, colour ramps white -> glow.
    private static void ApplyStardust(ParticleSystem.MainModule main, ParticleSystem.EmissionModule emission, ParticleSystem.ShapeModule shape, ParticleSystem.VelocityOverLifetimeModule velocity, ParticleSystem.RotationOverLifetimeModule rotation, ParticleSystem.ColorOverLifetimeModule colorOverLifetime, Color glow)
    {
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = Color.white;

        emission.rateOverTime = 12f;

        shape.shapeType = ParticleSystemShapeType.BoxShell;
        // Rings the whole 2.4:1 view at ortho size 5 (half-height 5 -> full height 10,
        // 2.4:1 -> full width ~24; 26/12 gives a touch of overscan past both edges).
        shape.scale = new Vector3(26f, 12f, 0.1f);
        shape.position = Vector3.zero;
        // Explicit every time Apply() runs - SandWisps/CloudMist leave shape.rotation at
        // (-90,0,0) to stand their box on end; left over on the shared ParticleSystem,
        // that rotation collapses this box's Y extent (12) down into Z, leaving a flat
        // ~0.1-unit-tall shell that reads as a single horizontal line across the screen.
        shape.rotation = Vector3.zero;

        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        // Negative radial velocity pulls every particle toward the system origin at its
        // own angle - the cheapest way to get "drift inward from wherever you spawned"
        // without per-particle scripting.
        velocity.radial = new ParticleSystem.MinMaxCurve(-0.15f, -0.05f);

        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(glow, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.4f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;
    }

    // Spawns along a thin band low in frame, rises 0.6-1.2 u/s with sideways noise,
    // tinted warm toward the arena's accent.
    private static void ApplySandWisps(ParticleSystem.MainModule main, ParticleSystem.EmissionModule emission, ParticleSystem.ShapeModule shape, ParticleSystem.NoiseModule noise, ParticleSystem.ColorOverLifetimeModule colorOverLifetime, Color accent)
    {
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
        main.startColor = accent;

        emission.rateOverTime = 14f;

        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(18f, 0.4f, 0.1f);
        shape.position = new Vector3(0f, -6f, 0f);
        // Box shape emits along its local +Z by default; rotate so that becomes "up".
        shape.rotation = new Vector3(-90f, 0f, 0f);

        noise.enabled = true;
        noise.separateAxes = true;
        noise.strengthX = 0.6f;
        noise.strengthY = 0.05f;
        noise.strengthZ = 0f;
        noise.frequency = 0.4f;

        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(accent, 0f), new GradientColorKey(accent, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.8f, 0.4f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;
    }

    // Large, slow, low-alpha particles drifting up from low in frame; the accompanying
    // LightningFlash overlay (see StartLightningLoop) is what carries this style's
    // identity, the particles themselves are deliberately understated.
    private static void ApplyCloudMist(ParticleSystem.MainModule main, ParticleSystem.EmissionModule emission, ParticleSystem.ShapeModule shape, ParticleSystem.ColorOverLifetimeModule colorOverLifetime, Color glow)
    {
        main.startLifetime = new ParticleSystem.MinMaxCurve(10f, 16f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.9f, 1.6f);
        main.startColor = Color.white;

        emission.rateOverTime = 4f;

        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(20f, 0.5f, 0.1f);
        shape.position = new Vector3(0f, -7f, 0f);
        shape.rotation = new Vector3(-90f, 0f, 0f);

        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(glow, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.28f, 0.5f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;
    }

    private void StartLightningLoop(Color glow)
    {
        if (lightningFlash == null)
        {
            return;
        }

        lightningColor = glow;
        lightningRoutine = StartCoroutine(LightningLoop());
    }

    private void StopLightningLoop()
    {
        if (lightningRoutine != null)
        {
            StopCoroutine(lightningRoutine);
            lightningRoutine = null;
        }

        SetFlashAlpha(0f);
    }

    private IEnumerator LightningLoop()
    {
        while (true)
        {
            float gap = Random.Range(FlashGapMin, FlashGapMax);
            float gapT = 0f;
            while (gapT < gap)
            {
                gapT += Time.unscaledDeltaTime;
                yield return null;
            }

            int pops = Random.Range(FlashPopsMin, FlashPopsMax);
            for (int i = 0; i < pops; i++)
            {
                float upT = 0f;
                while (upT < FlashUpDuration)
                {
                    upT += Time.unscaledDeltaTime;
                    SetFlashAlpha(Mathf.Lerp(0f, FlashPeakAlpha, Mathf.Clamp01(upT / FlashUpDuration)));
                    yield return null;
                }

                float downT = 0f;
                while (downT < FlashDownDuration)
                {
                    downT += Time.unscaledDeltaTime;
                    SetFlashAlpha(Mathf.Lerp(FlashPeakAlpha, 0f, Mathf.Clamp01(downT / FlashDownDuration)));
                    yield return null;
                }

                SetFlashAlpha(0f);

                float betweenDuration = Random.Range(FlashBetweenPopsMin, FlashBetweenPopsMax);
                float betweenT = 0f;
                while (betweenT < betweenDuration)
                {
                    betweenT += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }
    }

    private void SetFlashAlpha(float alpha)
    {
        if (lightningFlash == null)
        {
            return;
        }

        Color c = lightningColor;
        c.a = alpha;
        lightningFlash.color = c;
    }
}
