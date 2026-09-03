using UnityEngine;
using UnityEngine.UI;

// Palette-driven background: a tinted gradient, three drifting soft-circle blobs, a
// horizon glow, a pulsing halo behind the deity, a light pillar and a rotating dashed
// pedestal ring, plus a particle system whose start colour follows the palette.

/// <summary>
/// Recolours and continuously animates the character-select backdrop to match the
/// currently selected fighter's palette.
/// </summary>
public class AmbientBackdrop : MonoBehaviour
{
    // Distinct drift frequencies (Hz) and amplitudes (canvas units) per blob so the
    // three never fall into visible sync.
    private static readonly float[] BlobFreqX = { 0.11f, 0.07f, 0.09f };
    private static readonly float[] BlobFreqY = { 0.09f, 0.11f, 0.07f };
    private static readonly float[] BlobAmpX = { 90f, 110f, 70f };
    private static readonly float[] BlobAmpY = { 70f, 60f, 100f };

    private const float HaloPulsePeriod = 3.5f;
    private const float HaloScaleLow = 1f;
    private const float HaloScaleHigh = 1.08f;
    private const float HaloAlphaLow = 0.28f;
    private const float HaloAlphaHigh = 0.45f;

    private const float PedestalDashedDegPerSec = 12f;

    [SerializeField]
    [Tooltip("Full-screen vertical gradient, tinted to the palette's Deep colour.")]
    private Image baseGradient;

    [SerializeField]
    [Tooltip("Three soft-circle blobs (Glow / Primary / Secondary) that drift slowly.")]
    private Image[] blobs;

    [SerializeField]
    [Tooltip("Bottom-right glow ellipse, tinted Glow.")]
    private Image horizon;

    [SerializeField]
    [Tooltip("Soft circle behind the deity model, tinted Primary, pulses scale + alpha.")]
    private Image halo;

    [SerializeField]
    [Tooltip("Thin vertical light bar, tinted Secondary.")]
    private Image pillar;

    [SerializeField]
    [Tooltip("Static gold pedestal ring - not recoloured by the palette.")]
    private Image pedestalRing;

    [SerializeField]
    [Tooltip("Dashed pedestal ring, tinted Primary, rotates slowly.")]
    private Image pedestalDashed;

    [SerializeField]
    [Tooltip("Ambient sparks; start colour follows Primary.")]
    private ParticleSystem particles;

    [SerializeField]
    [Tooltip("Exponential ease rate for palette colour changes (higher = snappier).")]
    private float colorLerpSpeed = 6f;

    // Managed set: baseGradient, blob0, blob1, blob2, horizon, halo, pillar, pedestalDashed.
    private Image[] managed;
    private Color[] current;
    private Color[] target;

    private Vector2[] blobBasePos;
    private RectTransform haloRect;
    private RectTransform pedestalDashedRect;

    private void Awake()
    {
        if (baseGradient == null || horizon == null || halo == null || pillar == null || pedestalDashed == null)
        {
            Debug.LogError("AmbientBackdrop: one or more required Image references are not assigned.");
        }

        if (blobs == null || blobs.Length == 0)
        {
            Debug.LogError("AmbientBackdrop: blobs is empty.");
            blobs = new Image[0];
        }
    }

    private void Start()
    {
        EnsureInitialised();
    }

    // Script execution order is not guaranteed between this component and
    // CharacterSelectController, whose Start() calls ApplyPalette. If this ran
    // second the first palette call would find no arrays and silently bail,
    // leaving every blob at its white build-time placeholder colour.
    private void EnsureInitialised()
    {
        if (managed != null)
        {
            return;
        }

        managed = new Image[5 + blobs.Length];
        managed[0] = baseGradient;
        for (int i = 0; i < blobs.Length; i++)
        {
            managed[1 + i] = blobs[i];
        }
        managed[1 + blobs.Length] = horizon;
        managed[2 + blobs.Length] = halo;
        managed[3 + blobs.Length] = pillar;
        managed[4 + blobs.Length] = pedestalDashed;

        current = new Color[managed.Length];
        target = new Color[managed.Length];
        for (int i = 0; i < managed.Length; i++)
        {
            Color c = managed[i] != null ? managed[i].color : Color.white;
            current[i] = c;
            target[i] = c;
        }

        blobBasePos = new Vector2[blobs.Length];
        for (int i = 0; i < blobs.Length; i++)
        {
            if (blobs[i] == null)
            {
                continue;
            }

            blobs[i].raycastTarget = false;
            blobBasePos[i] = blobs[i].rectTransform.anchoredPosition;
        }

        if (halo != null)
        {
            haloRect = halo.rectTransform;
        }

        if (pedestalDashed != null)
        {
            pedestalDashedRect = pedestalDashed.rectTransform;
        }
    }

    /// <summary>Retargets every palette-driven colour to match def; instant skips the ease.</summary>
    public void ApplyPalette(CharacterDefinition def, bool instant)
    {
        if (def == null)
        {
            Debug.LogError("AmbientBackdrop.ApplyPalette: def is null.");
            return;
        }

        EnsureInitialised();

        int idx = 0;
        SetTarget(idx++, def.Deep);
        for (int i = 0; i < blobs.Length; i++)
        {
            // Secondary is often near-white (pearl, silver), which reads as a grey
            // smudge at blob size; pull it most of the way toward Primary.
            Color blobTint = i == 0 ? def.Glow : (i == 1 ? def.Primary : Color.Lerp(def.Secondary, def.Primary, 0.65f));
            SetTarget(idx++, blobTint);
        }
        SetTarget(idx++, def.Glow);
        SetTarget(idx++, def.Primary);
        SetTarget(idx++, def.Secondary);
        SetTarget(idx++, def.Primary);

        if (particles != null)
        {
            ParticleSystem.MainModule main = particles.main;
            Color sparkColor = def.Primary;
            sparkColor.a = 0.9f;
            main.startColor = new ParticleSystem.MinMaxGradient(sparkColor);
        }

        if (instant)
        {
            for (int i = 0; i < managed.Length; i++)
            {
                current[i] = target[i];
                if (managed[i] != null)
                {
                    managed[i].color = current[i];
                }
            }
        }
    }

    private void SetTarget(int index, Color rgbSource)
    {
        // Preserve each element's own designed alpha (set by the scene builder); the
        // palette only ever changes hue, never transparency.
        float alpha = target[index].a;
        target[index] = new Color(rgbSource.r, rgbSource.g, rgbSource.b, alpha);
    }

    private void Update()
    {
        if (managed == null)
        {
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
        float t = Time.unscaledTime;

        // Palette ease: exponential decay toward target, framerate-independent.
        float lerpFactor = 1f - Mathf.Exp(-colorLerpSpeed * dt);
        for (int i = 0; i < managed.Length; i++)
        {
            if (managed[i] == null)
            {
                continue;
            }

            current[i] = Color.Lerp(current[i], target[i], lerpFactor);
            managed[i].color = current[i];
        }

        // Blob drift.
        for (int i = 0; i < blobs.Length; i++)
        {
            if (blobs[i] == null)
            {
                continue;
            }

            float dx = Mathf.Sin(t * BlobFreqX[i % BlobFreqX.Length] * Mathf.PI * 2f) * BlobAmpX[i % BlobAmpX.Length];
            float dy = Mathf.Cos(t * BlobFreqY[i % BlobFreqY.Length] * Mathf.PI * 2f) * BlobAmpY[i % BlobAmpY.Length];
            blobs[i].rectTransform.anchoredPosition = blobBasePos[i] + new Vector2(dx, dy);
        }

        // Halo pulse: scale and alpha breathe together, overriding the palette-eased alpha.
        if (haloRect != null && halo != null)
        {
            float phase = (Mathf.Sin(t * (Mathf.PI * 2f / HaloPulsePeriod)) + 1f) * 0.5f;
            float scale = Mathf.Lerp(HaloScaleLow, HaloScaleHigh, phase);
            haloRect.localScale = new Vector3(scale, scale, 1f);

            Color c = halo.color;
            c.a = Mathf.Lerp(HaloAlphaLow, HaloAlphaHigh, phase);
            halo.color = c;
            current[2 + blobs.Length] = c;
        }

        // Pedestal dashed ring rotation.
        if (pedestalDashedRect != null)
        {
            Vector3 euler = pedestalDashedRect.localEulerAngles;
            euler.z += PedestalDashedDegPerSec * dt;
            pedestalDashedRect.localEulerAngles = euler;
        }
    }
}
