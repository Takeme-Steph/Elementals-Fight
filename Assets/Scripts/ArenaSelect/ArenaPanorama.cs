using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Full-screen layered backdrop for ArenaSelect: two identical layer sets (A/B), each
// its own nested Canvas so the idle drift only ever rebuilds that one subtree instead
// of the whole Backdrop canvas. Swapping arenas paints the currently-inactive set with
// the new arena's palette, springs it in over the old one, then swaps which set is
// "active" (the one Update() keeps drifting) - same cross-fade trick as a two-layer
// video dissolve, done entirely with transform/colour so it stays cheap on mobile.

/// <summary>
/// Palette-driven panoramic backdrop for the arena select screen.
/// </summary>
public class ArenaPanorama : MonoBehaviour
{
    // Idle drift (both layers, active one only): a slow "breathing" scale bounce plus
    // a gentle side-to-side pan, transform only per the mobile constraints.
    private const float DriftPeriod = 14f;
    private const float DriftScaleAmount = 0.06f;
    private const float DriftPanAmount = 18f;

    // Arena-swap pop-in spring (incoming layer): scale 1.18 -> 1.0, alpha 0 -> 1.
    private const float PopStartScale = 1.18f;
    private const float PopStiffness = 140f;
    private const float PopDamping = 16f;

    // Outgoing layer ease: scale -> 0.92, alpha -> 0, plain ease-out over a fixed time.
    private const float OldEaseDuration = 0.35f;
    private const float OldEndScale = 0.92f;

    [System.Serializable]
    private class Layer
    {
        [Tooltip("Whole-layer root; carries its own nested Canvas and CanvasGroup so scale/alpha here never touches the other layer.")]
        public RectTransform root;
        public CanvasGroup group;

        [Tooltip("GradientV sprite tinted skyTop, drawn over skyBottomFill.")]
        public Image sky;
        [Tooltip("Solid fill tinted skyBottom, sits behind sky.")]
        public Image skyBottomFill;
        [Tooltip("SoftCircle glow tinted horizon.")]
        public Image horizonGlow;
        [Tooltip("Far silhouette band, tinted deep, drifts slower than bandNear.")]
        public Image bandFar;
        [Tooltip("Near silhouette band, tinted deep, drifts faster than bandFar.")]
        public Image bandNear;
        [Tooltip("Full-width readability band flush with the top edge, tinted deep - keeps header text legible over a pale sky (e.g. Olympus).")]
        public Image bandTop;
        [Tooltip("Full-width readability band flush with the bottom edge, tinted deep - keeps the dock/buttons legible over a pale sky.")]
        public Image bandBottom;
        [Tooltip("Vignette sprite (transparent centre, dark edge) tinted deep.")]
        public Image vignette;
    }

    [SerializeField]
    private Layer layerA = new Layer();

    [SerializeField]
    private Layer layerB = new Layer();

    private bool validRefs = true;
    private bool activeIsA = true;
    private bool isTransitioning;
    private Coroutine showRoutine;

    private void Awake()
    {
        bool aOk = layerA != null && layerA.root != null && layerA.group != null;
        bool bOk = layerB != null && layerB.root != null && layerB.group != null;

        if (!aOk || !bOk)
        {
            Debug.LogError("ArenaPanorama: layerA/layerB root and group must be assigned.");
            validRefs = false;
        }
    }

    /// <summary>Paints and cross-fades to def's palette; instant skips straight to the resting state.</summary>
    public void Show(ArenaDefinition def, bool instant)
    {
        if (!validRefs)
        {
            return;
        }

        if (def == null)
        {
            Debug.LogError("ArenaPanorama.Show: def is null.");
            return;
        }

        if (showRoutine != null)
        {
            StopCoroutine(showRoutine);
            showRoutine = null;
        }

        if (instant)
        {
            isTransitioning = false;
            Layer active = activeIsA ? layerA : layerB;
            Layer idle = activeIsA ? layerB : layerA;

            Paint(active, def);
            SetTransform(active, 1f, 1f);
            SetTransform(idle, OldEndScale, 0f);
            return;
        }

        showRoutine = StartCoroutine(ShowRoutine(def));
    }

    private IEnumerator ShowRoutine(ArenaDefinition def)
    {
        isTransitioning = true;

        Layer incoming = activeIsA ? layerB : layerA;
        Layer outgoing = activeIsA ? layerA : layerB;

        Paint(incoming, def);

        float inScale = PopStartScale;
        float inAlpha = 0f;
        float inScaleVel = 0f;
        float inAlphaVel = 0f;
        SetTransform(incoming, inScale, inAlpha);

        float outStartScale = outgoing.root != null ? outgoing.root.localScale.x : 1f;
        float outStartAlpha = outgoing.group != null ? outgoing.group.alpha : 1f;
        float outT = 0f;

        while (!UiSpring.Settled(inScale, 1f, inScaleVel) || !UiSpring.Settled(inAlpha, 1f, inAlphaVel) || outT < OldEaseDuration)
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

            inScale = UiSpring.Step(inScale, 1f, ref inScaleVel, PopStiffness, PopDamping, dt);
            inAlpha = UiSpring.Step(inAlpha, 1f, ref inAlphaVel, PopStiffness, PopDamping, dt);
            SetTransform(incoming, inScale, Mathf.Clamp01(inAlpha));

            if (outT < OldEaseDuration)
            {
                outT += dt;
                float p = Mathf.Clamp01(outT / OldEaseDuration);
                float eased = 1f - Mathf.Pow(1f - p, 3f);
                SetTransform(outgoing, Mathf.Lerp(outStartScale, OldEndScale, eased), Mathf.Lerp(outStartAlpha, 0f, eased));
            }

            yield return null;
        }

        SetTransform(incoming, 1f, 1f);
        SetTransform(outgoing, OldEndScale, 0f);

        activeIsA = !activeIsA;
        isTransitioning = false;
        showRoutine = null;
    }

    private static void Paint(Layer layer, ArenaDefinition def)
    {
        if (layer.sky != null)
        {
            layer.sky.color = def.SkyTop;
        }

        if (layer.skyBottomFill != null)
        {
            layer.skyBottomFill.color = def.SkyBottom;
        }

        if (layer.horizonGlow != null)
        {
            layer.horizonGlow.color = Tint(layer.horizonGlow.color, def.Horizon);
        }

        if (layer.bandFar != null)
        {
            layer.bandFar.color = Tint(layer.bandFar.color, def.Deep);
        }

        if (layer.bandNear != null)
        {
            layer.bandNear.color = Tint(layer.bandNear.color, def.Deep);
        }

        if (layer.bandTop != null)
        {
            layer.bandTop.color = Tint(layer.bandTop.color, def.Deep);
        }

        if (layer.bandBottom != null)
        {
            layer.bandBottom.color = Tint(layer.bandBottom.color, def.Deep);
        }

        if (layer.vignette != null)
        {
            layer.vignette.color = Tint(layer.vignette.color, def.Deep);
        }
    }

    /// <summary>Replaces rgb, keeps whatever alpha the builder gave that element.</summary>
    private static Color Tint(Color current, Color rgbSource)
    {
        return new Color(rgbSource.r, rgbSource.g, rgbSource.b, current.a);
    }

    private static void SetTransform(Layer layer, float scale, float alpha)
    {
        if (layer.root != null)
        {
            layer.root.localScale = new Vector3(scale, scale, 1f);
        }

        if (layer.group != null)
        {
            layer.group.alpha = alpha;
        }
    }

    private void Update()
    {
        if (!validRefs || isTransitioning)
        {
            return;
        }

        Layer active = activeIsA ? layerA : layerB;
        if (active.root == null)
        {
            return;
        }

        float t = Time.unscaledTime;
        float phase = (Mathf.Sin(t * (Mathf.PI * 2f / DriftPeriod)) + 1f) * 0.5f;
        float scale = 1f + DriftScaleAmount * phase;
        float panX = Mathf.Cos(t * (Mathf.PI * 2f / DriftPeriod)) * DriftPanAmount;

        active.root.localScale = new Vector3(scale, scale, 1f);
        active.root.anchoredPosition = new Vector2(panX, 0f);
    }
}
