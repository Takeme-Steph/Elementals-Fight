using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One carousel portrait. Deliberately has NO Button driving its tap: a Button under a
// drag area would swallow the drag before PortraitCarousel ever sees it. Instead this
// implements IPointerClickHandler directly (the EventSystem only fires a click when no
// drag happened) and forwards Begin/Drag/EndDrag up to the carousel itself via
// ExecuteEvents, so a drag that starts on an icon still scrolls the whole carousel.

/// <summary>
/// A single selectable portrait inside PortraitCarousel.
/// </summary>
public class PortraitIcon : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    // Non-spring colour transitions (ring/disc tint) ease at a fixed rate rather than
    // through UiSpring - a physical overshoot on a colour reads as a glitch, not life.
    private const float ColorLerpSpeed = 10f;
    private const float DashedRingDegPerSec = -30f;
    private static readonly Color UnselectedRingColor = new Color(1f, 1f, 1f, 0.2f);

    [SerializeField]
    [Tooltip("Root RectTransform of the whole icon; also carries the CanvasGroup used for the selection alpha.")]
    private RectTransform scaleTarget;

    [SerializeField]
    private Image disc;

    [SerializeField]
    private Image ring;

    [SerializeField]
    [Tooltip("Rotates while selected; hidden otherwise.")]
    private Image dashedRing;

    [SerializeField]
    [Tooltip("def.Icon, when the fighter has one.")]
    private Image iconImage;

    [SerializeField]
    [Tooltip("First letter of DisplayName, shown when the fighter has no Icon.")]
    private TMP_Text glyph;

    [SerializeField]
    [Tooltip("Kept for builder wiring only - selection is driven by IPointerClickHandler, not Button.onClick.")]
    private Button button;

    [SerializeField]
    private float selectedScale = 1.25f;

    [SerializeField]
    private float unselectedScale = 0.85f;

    [SerializeField]
    private float stiffness = 300f;

    [SerializeField]
    private float damping = 18f;

    public int Index { get; private set; }
    public CharacterDefinition Definition { get; private set; }

    private System.Action<int> onTap;
    private PortraitCarousel carousel;
    private CanvasGroup canvasGroup;

    private float scaleCurrent;
    private float scaleTargetValue;
    private float scaleVelocity;

    private float alphaCurrent;
    private float alphaTargetValue;
    private float alphaVelocity;

    private Color ringColorCurrent;
    private Color ringColorTarget;
    private Color discColorCurrent;
    private Color discColorTarget;

    private bool isSelected;
    private bool settled = true;

    private void Awake()
    {
        if (scaleTarget == null || disc == null || ring == null)
        {
            Debug.LogError("PortraitIcon: scaleTarget/disc/ring are required and must be assigned.");
        }

        if (scaleTarget != null && !scaleTarget.TryGetComponent(out canvasGroup))
        {
            Debug.LogError("PortraitIcon: scaleTarget has no CanvasGroup.");
        }

        // The carousel is an ancestor in the built hierarchy (Carousel -> Viewport ->
        // Content -> icon); this is how an icon forwards drags without holding a
        // serialized back-reference the contract doesn't provide.
        carousel = GetComponentInParent<PortraitCarousel>();

        if (dashedRing != null)
        {
            dashedRing.gameObject.SetActive(false);
        }
    }

    /// <summary>Configures the icon for one roster entry; visuals start in the unselected state.</summary>
    public void Bind(CharacterDefinition def, int index, System.Action<int> onTap)
    {
        if (def == null)
        {
            Debug.LogError("PortraitIcon.Bind: def is null.");
            return;
        }

        Definition = def;
        Index = index;
        this.onTap = onTap;

        bool hasIcon = def.Icon != null;
        if (iconImage != null)
        {
            iconImage.enabled = hasIcon;
            if (hasIcon)
            {
                iconImage.sprite = def.Icon;
            }
        }

        if (glyph != null)
        {
            glyph.gameObject.SetActive(!hasIcon);
            if (!hasIcon)
            {
                glyph.text = string.IsNullOrEmpty(def.DisplayName) ? "?" : def.DisplayName.Substring(0, 1).ToUpperInvariant();
            }
        }

        isSelected = false;
        scaleCurrent = unselectedScale;
        scaleTargetValue = unselectedScale;
        scaleVelocity = 0f;

        alphaCurrent = 0.55f;
        alphaTargetValue = 0.55f;
        alphaVelocity = 0f;

        ringColorCurrent = UnselectedRingColor;
        ringColorTarget = UnselectedRingColor;
        discColorCurrent = Color.Lerp(def.Primary, def.Deep, 0.45f);
        discColorTarget = discColorCurrent;

        settled = true;
        ApplyVisualState();
    }

    /// <summary>Springs scale/alpha/colours toward the selected or unselected state.</summary>
    public void SetSelected(bool selected, bool instant)
    {
        if (Definition == null)
        {
            Debug.LogError("PortraitIcon.SetSelected: icon has not been Bind()-ed yet.");
            return;
        }

        isSelected = selected;
        scaleTargetValue = selected ? selectedScale : unselectedScale;
        alphaTargetValue = selected ? 1f : 0.55f;
        ringColorTarget = selected ? Definition.Primary : UnselectedRingColor;
        discColorTarget = selected ? Definition.Primary : Color.Lerp(Definition.Primary, Definition.Deep, 0.45f);

        if (selected)
        {
            // Pops the selected icon above its neighbours so its larger scale never
            // clips behind them.
            transform.SetAsLastSibling();
        }

        if (dashedRing != null)
        {
            dashedRing.gameObject.SetActive(selected);
        }

        if (instant)
        {
            scaleCurrent = scaleTargetValue;
            scaleVelocity = 0f;
            alphaCurrent = alphaTargetValue;
            alphaVelocity = 0f;
            ringColorCurrent = ringColorTarget;
            discColorCurrent = discColorTarget;
            settled = true;
            ApplyVisualState();
        }
        else
        {
            settled = false;
        }
    }

    private void ApplyVisualState()
    {
        if (scaleTarget != null)
        {
            scaleTarget.localScale = new Vector3(scaleCurrent, scaleCurrent, 1f);
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = alphaCurrent;
        }

        if (ring != null)
        {
            ring.color = ringColorCurrent;
        }

        if (disc != null)
        {
            disc.color = discColorCurrent;
        }
    }

    private void Update()
    {
        // Nothing left to animate and the dashed ring (which spins continuously while
        // selected) is hidden - skip the frame entirely.
        if (settled && !isSelected)
        {
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

        if (!settled)
        {
            scaleCurrent = UiSpring.Step(scaleCurrent, scaleTargetValue, ref scaleVelocity, stiffness, damping, dt);
            alphaCurrent = UiSpring.Step(alphaCurrent, alphaTargetValue, ref alphaVelocity, stiffness, damping, dt);

            float colorT = 1f - Mathf.Exp(-ColorLerpSpeed * dt);
            ringColorCurrent = Color.Lerp(ringColorCurrent, ringColorTarget, colorT);
            discColorCurrent = Color.Lerp(discColorCurrent, discColorTarget, colorT);

            bool scaleDone = UiSpring.Settled(scaleCurrent, scaleTargetValue, scaleVelocity);
            bool alphaDone = UiSpring.Settled(alphaCurrent, alphaTargetValue, alphaVelocity);
            bool colorDone = ((Vector4)ringColorCurrent - (Vector4)ringColorTarget).sqrMagnitude < 0.0001f
                && ((Vector4)discColorCurrent - (Vector4)discColorTarget).sqrMagnitude < 0.0001f;

            if (scaleDone && alphaDone && colorDone)
            {
                scaleCurrent = scaleTargetValue;
                alphaCurrent = alphaTargetValue;
                ringColorCurrent = ringColorTarget;
                discColorCurrent = discColorTarget;
                settled = true;
            }

            ApplyVisualState();
        }

        if (isSelected && dashedRing != null)
        {
            RectTransform rt = dashedRing.rectTransform;
            Vector3 euler = rt.localEulerAngles;
            euler.z += DashedRingDegPerSec * dt;
            rt.localEulerAngles = euler;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // The EventSystem withholds this call entirely if the pointer dragged past the
        // threshold, so a drag that started here never also fires a tap.
        onTap?.Invoke(Index);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        ForwardToCarousel(eventData, ExecuteEvents.beginDragHandler);
    }

    public void OnDrag(PointerEventData eventData)
    {
        ForwardToCarousel(eventData, ExecuteEvents.dragHandler);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        ForwardToCarousel(eventData, ExecuteEvents.endDragHandler);
    }

    private void ForwardToCarousel<T>(PointerEventData eventData, ExecuteEvents.EventFunction<T> handler) where T : IEventSystemHandler
    {
        if (carousel == null)
        {
            Debug.LogError("PortraitIcon: no PortraitCarousel found in parents to forward drag events to.");
            return;
        }

        ExecuteEvents.ExecuteHierarchy(carousel.gameObject, eventData, handler);
    }
}
