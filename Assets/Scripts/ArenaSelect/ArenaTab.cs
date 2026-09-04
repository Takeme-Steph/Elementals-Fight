using TMPro;
using UnityEngine;
using UnityEngine.UI;

// One dock slot: plate + border ring + rune (sprite or glyph fallback) + name. All the
// idle<->active visual difference (scale, overall alpha, border/rune tint, plate glass
// brightness) is driven off a single 0..1 "activation" spring so every element stays in
// lockstep instead of needing its own timer.

/// <summary>
/// A single arena tab in ArenaTabDock. Content is set once via Paint; activation state
/// is toggled independently via SetActive so the dock can drive many of these off one
/// selected index.
/// </summary>
public class ArenaTab : MonoBehaviour
{
    private const float ActiveScale = 1.15f;
    private const float SpringStiffness = 300f;
    private const float SpringDamping = 20f;
    private const float IdleAlpha = 0.55f;
    private const float ActiveAlpha = 1f;
    private const float PlateActiveAlphaBoost = 1.8f;

    [SerializeField]
    private RectTransform scaleTarget;

    [SerializeField]
    private CanvasGroup group;

    [SerializeField]
    [Tooltip("Glass RoundedRect plate; alpha boosts when active (\"brighter\").")]
    private Image plate;

    [SerializeField]
    [Tooltip("Outline/border overlay; alpha 1 tinted accent when active, dim otherwise.")]
    private Image border;

    [SerializeField]
    [Tooltip("Rune icon; shown when the arena has a runeSprite.")]
    private Image rune;

    [SerializeField]
    [Tooltip("Fallback glyph shown instead of rune when the arena has no runeSprite.")]
    private TMP_Text runeGlyphText;

    [SerializeField]
    private TMP_Text nameText;

    [SerializeField]
    private Button button;

    public Button Button => button;

    private bool validRefs = true;
    private bool isActive;
    private float activation;
    private float activationVelocity;

    private Color plateIdleColor = Color.white;
    private Color borderIdleColor = Color.white;
    private Color runeIdleColor = Color.white;
    private Color accentColor = Color.white;

    private void Awake()
    {
        if (scaleTarget == null || plate == null || border == null || nameText == null || button == null)
        {
            Debug.LogError("ArenaTab: one or more required references are not assigned.");
            validRefs = false;
        }

        if (plate != null)
        {
            plateIdleColor = plate.color;
        }

        if (border != null)
        {
            borderIdleColor = border.color;
        }

        if (rune != null)
        {
            runeIdleColor = rune.color;
        }

        ApplyActivation(0f);
    }

    /// <summary>Writes def's rune/name into the tab. Does not change activation state.</summary>
    public void Paint(ArenaDefinition def)
    {
        if (def == null)
        {
            Debug.LogError("ArenaTab.Paint: def is null.");
            return;
        }

        accentColor = def.Accent;

        bool hasSprite = def.RuneSprite != null;

        if (rune != null)
        {
            rune.sprite = def.RuneSprite;
            rune.enabled = hasSprite;
        }

        if (runeGlyphText != null)
        {
            runeGlyphText.gameObject.SetActive(!hasSprite);
            runeGlyphText.text = def.RuneGlyph;
        }

        if (nameText != null)
        {
            nameText.text = string.IsNullOrEmpty(def.DisplayName) ? string.Empty : def.DisplayName.ToUpperInvariant();
        }

        // Re-apply immediately so a repaint while already active doesn't flash the old
        // arena's accent for one frame before Update ticks.
        ApplyActivation(activation);
    }

    /// <summary>Toggles idle/active visuals; instant skips the spring and snaps straight there.</summary>
    public void SetActive(bool active, bool instant)
    {
        isActive = active;

        if (instant)
        {
            activation = active ? 1f : 0f;
            activationVelocity = 0f;
            ApplyActivation(activation);
        }
    }

    private void Update()
    {
        if (!validRefs)
        {
            return;
        }

        float target = isActive ? 1f : 0f;
        if (UiSpring.Settled(activation, target, activationVelocity))
        {
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
        activation = UiSpring.Step(activation, target, ref activationVelocity, SpringStiffness, SpringDamping, dt);
        ApplyActivation(activation);
    }

    private void ApplyActivation(float a)
    {
        float scale = Mathf.Lerp(1f, ActiveScale, a);
        if (scaleTarget != null)
        {
            scaleTarget.localScale = new Vector3(scale, scale, 1f);
        }

        if (group != null)
        {
            group.alpha = Mathf.Lerp(IdleAlpha, ActiveAlpha, a);
        }

        if (border != null)
        {
            Color activeColor = new Color(accentColor.r, accentColor.g, accentColor.b, 1f);
            border.color = Color.Lerp(borderIdleColor, activeColor, a);
        }

        if (rune != null)
        {
            Color activeColor = new Color(accentColor.r, accentColor.g, accentColor.b, runeIdleColor.a >= 1f ? 1f : Mathf.Max(runeIdleColor.a, 0.9f));
            rune.color = Color.Lerp(runeIdleColor, activeColor, a);
        }

        if (runeGlyphText != null)
        {
            runeGlyphText.color = Color.Lerp(Color.white, accentColor, a);
        }

        if (plate != null)
        {
            float boostedAlpha = Mathf.Clamp01(plateIdleColor.a * PlateActiveAlphaBoost);
            Color activeColor = new Color(plateIdleColor.r, plateIdleColor.g, plateIdleColor.b, boostedAlpha);
            plate.color = Color.Lerp(plateIdleColor, activeColor, a);
        }
    }
}
