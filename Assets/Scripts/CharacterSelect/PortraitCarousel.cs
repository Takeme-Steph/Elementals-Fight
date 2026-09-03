using UnityEngine;
using UnityEngine.EventSystems;

// Horizontally-scrolling character picker. Content sits at x = -index*step (the
// viewport is centred, so index 0 needs no offset); dragging moves it directly, a
// snap-on-release picks the nearest index by projecting the release velocity forward,
// and a spring carries the rest of the way there whenever the user isn't touching it.

/// <summary>
/// Drag/snap carousel of PortraitIcon entries, one per roster character.
/// </summary>
public class PortraitCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField]
    [Tooltip("Defines the drag/raycast area; needs an Image with alpha 0 and raycastTarget true.")]
    private RectTransform viewport;

    [SerializeField]
    [Tooltip("Moved on x to scroll; icons are its children.")]
    private RectTransform content;

    [SerializeField]
    [Tooltip("Inactive template cloned once per roster character.")]
    private PortraitIcon iconTemplate;

    [SerializeField]
    [Tooltip("Distance between icon centres, in canvas units (1920x1080 reference).")]
    private float step = 150f;

    [SerializeField]
    [Tooltip("How much a drag past either end is scaled down (0 = a hard wall, 1 = no resistance).")]
    private float rubberBand = 0.25f;

    [SerializeField]
    [Tooltip("Seconds of release velocity added to the drag position before rounding to the nearest index.")]
    private float velocityProjection = 0.2f;

    [SerializeField]
    private float stiffness = 380f;

    [SerializeField]
    private float damping = 32f;

    public event System.Action<int> IndexChanged;

    public int CurrentIndex { get; private set; } = -1;

    private PortraitIcon[] icons;
    private Canvas parentCanvas;

    private float currentX;
    private float targetX;
    private float velocityX;
    private bool settled = true;
    private bool dragging;
    private float dragVelocity;

    /// <summary>Clones iconTemplate once per roster character and lays them out at i*step.</summary>
    public void Build(CharacterRoster roster)
    {
        if (roster == null || viewport == null || content == null || iconTemplate == null)
        {
            Debug.LogError("PortraitCarousel.Build: roster/viewport/content/iconTemplate must all be assigned.");
            return;
        }

        parentCanvas = viewport.GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            Debug.LogError("PortraitCarousel.Build: no parent Canvas found above viewport - drag distances need its scaleFactor.");
        }

        int count = roster.Count;
        icons = new PortraitIcon[count];
        iconTemplate.gameObject.SetActive(false);

        for (int i = 0; i < count; i++)
        {
            CharacterDefinition def = roster.Get(i);
            PortraitIcon icon = Instantiate(iconTemplate, content);
            icon.gameObject.SetActive(true);

            RectTransform rect = icon.transform as RectTransform;
            if (rect != null)
            {
                Vector3 pos = rect.localPosition;
                pos.x = i * step;
                rect.localPosition = pos;
            }

            icon.Bind(def, i, OnIconTapped);
            icons[i] = icon;
        }

        CurrentIndex = -1;
        currentX = 0f;
        targetX = 0f;
        velocityX = 0f;
        settled = true;
    }

    /// <summary>Springs (or jumps, if instant) content to index and fires IndexChanged if it changed.</summary>
    public void SnapTo(int index, bool instant)
    {
        if (icons == null || icons.Length == 0)
        {
            Debug.LogError("PortraitCarousel.SnapTo: carousel has not been Built yet.");
            return;
        }

        index = Mathf.Clamp(index, 0, icons.Length - 1);
        targetX = -index * step;

        bool changed = index != CurrentIndex;
        int previous = CurrentIndex;

        if (instant)
        {
            currentX = targetX;
            velocityX = 0f;
            settled = true;
            ApplyContentPosition();
        }
        else
        {
            settled = false;
        }

        if (changed)
        {
            CurrentIndex = index;

            if (previous >= 0 && previous < icons.Length)
            {
                icons[previous].SetSelected(false, instant);
            }

            icons[index].SetSelected(true, instant);
            IndexChanged?.Invoke(index);
        }
    }

    /// <summary>Returns the icon at index, or null if the carousel has not been built or index is out of range.</summary>
    public PortraitIcon GetIcon(int index)
    {
        if (icons == null || index < 0 || index >= icons.Length)
        {
            return null;
        }

        return icons[index];
    }

    private void OnIconTapped(int index)
    {
        SnapTo(index, false);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (icons == null || icons.Length == 0)
        {
            return;
        }

        dragging = true;
        velocityX = 0f;
        dragVelocity = 0f;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragging || icons == null || icons.Length == 0)
        {
            return;
        }

        float scaleFactor = parentCanvas != null && parentCanvas.scaleFactor > 0f ? parentCanvas.scaleFactor : 1f;
        Vector2 deltaCanvas = eventData.delta / scaleFactor;

        float newX = currentX + deltaCanvas.x;
        float minX = -(icons.Length - 1) * step;
        const float maxX = 0f;

        if (newX > maxX)
        {
            newX = maxX + (newX - maxX) * rubberBand;
        }
        else if (newX < minX)
        {
            newX = minX + (newX - minX) * rubberBand;
        }

        currentX = newX;
        ApplyContentPosition();

        float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        float instantVelocity = deltaCanvas.x / dt;
        // Smoothed rather than raw per-frame velocity - a single jittery sample would
        // otherwise send the release projection to the wrong index.
        dragVelocity = Mathf.Lerp(dragVelocity, instantVelocity, 0.5f);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!dragging || icons == null || icons.Length == 0)
        {
            dragging = false;
            return;
        }

        dragging = false;

        float projected = currentX + dragVelocity * velocityProjection;
        int index = Mathf.Clamp(Mathf.RoundToInt(-projected / step), 0, icons.Length - 1);
        SnapTo(index, false);
    }

    private void Update()
    {
        if (dragging || settled || icons == null || icons.Length == 0)
        {
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
        currentX = UiSpring.Step(currentX, targetX, ref velocityX, stiffness, damping, dt);

        if (UiSpring.Settled(currentX, targetX, velocityX))
        {
            currentX = targetX;
            velocityX = 0f;
            settled = true;
        }

        ApplyContentPosition();
    }

    private void ApplyContentPosition()
    {
        if (content == null)
        {
            return;
        }

        Vector3 pos = content.localPosition;
        pos.x = currentX;
        content.localPosition = pos;
    }
}
