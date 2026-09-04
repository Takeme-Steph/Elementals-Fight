using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// The "flying to a new realm" flash: a full-screen tint punches up fast then fades
// slow, with a ring shockwave scaling out underneath it across the whole play. atPeak
// fires exactly once, at the flash's brightest frame, which is when the controller
// swaps every other widget over to the new arena - the flash hides the swap so nothing
// reads as a hard cut.

/// <summary>
/// Full-screen dimensional-warp flash played on every arena switch.
/// </summary>
public class WarpTransition : MonoBehaviour
{
    private const float FlashUpDuration = 0.06f;
    private const float FlashPeakAlpha = 0.85f;
    private const float FlashDownDuration = 0.32f;
    private const float TotalDuration = FlashUpDuration + FlashDownDuration;

    private const float RingStartScale = 0.2f;
    private const float RingEndScale = 2.4f;

    [SerializeField]
    [Tooltip("Full-screen Image; painted white lerped 30% toward the arena's accent.")]
    private Image flash;

    [SerializeField]
    private RectTransform ringScaleTarget;

    [SerializeField]
    private Image ring;

    private bool validRefs = true;
    private Coroutine routine;

    private void Awake()
    {
        if (flash == null || ring == null || ringScaleTarget == null)
        {
            Debug.LogError("WarpTransition: flash, ring and ringScaleTarget must be assigned.");
            validRefs = false;
        }

        SetFlashAlpha(Color.white, 0f);
    }

    /// <summary>Plays the flash/ring; atPeak fires once at the brightest frame. Restarts cleanly if already playing.</summary>
    public void Play(Color accent, Action atPeak)
    {
        if (!validRefs)
        {
            // Still have to notify the caller so the arena actually swaps even if the
            // transition itself can't play.
            atPeak?.Invoke();
            return;
        }

        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        routine = StartCoroutine(PlayRoutine(accent, atPeak));
    }

    private IEnumerator PlayRoutine(Color accent, Action atPeak)
    {
        Color flashColor = Color.Lerp(Color.white, accent, 0.3f);
        Color ringColor = accent;
        ringColor.a = 1f;

        SetFlashAlpha(flashColor, 0f);
        UpdateRing(ringColor, 0f);

        float t = 0f;
        bool peakFired = false;

        while (t < TotalDuration)
        {
            t += Time.unscaledDeltaTime;
            float ringP = Mathf.Clamp01(t / TotalDuration);
            UpdateRing(ringColor, ringP);

            if (!peakFired && t >= FlashUpDuration)
            {
                // Snap to the exact peak alpha before invoking so atPeak's caller (which
                // repaints every other widget) always sees the flash fully opaque, never
                // a value the interpolation slightly overshot on a big frame dt.
                SetFlashAlpha(flashColor, FlashPeakAlpha);
                peakFired = true;
                atPeak?.Invoke();
            }
            else if (!peakFired)
            {
                SetFlashAlpha(flashColor, Mathf.Lerp(0f, FlashPeakAlpha, Mathf.Clamp01(t / FlashUpDuration)));
            }
            else
            {
                float downP = Mathf.Clamp01((t - FlashUpDuration) / FlashDownDuration);
                SetFlashAlpha(flashColor, Mathf.Lerp(FlashPeakAlpha, 0f, downP));
            }

            yield return null;
        }

        SetFlashAlpha(flashColor, 0f);
        UpdateRing(ringColor, 1f);

        if (!peakFired)
        {
            // Only reachable if a single huge frame dt skipped straight past the whole
            // duration - still must fire, callers rely on atPeak to swap the arena.
            atPeak?.Invoke();
        }

        routine = null;
    }

    private void SetFlashAlpha(Color baseColor, float alpha)
    {
        if (flash == null)
        {
            return;
        }

        Color c = baseColor;
        c.a = alpha;
        flash.color = c;
    }

    private void UpdateRing(Color ringColor, float p)
    {
        float scale = Mathf.Lerp(RingStartScale, RingEndScale, p);
        if (ringScaleTarget != null)
        {
            ringScaleTarget.localScale = new Vector3(scale, scale, 1f);
        }

        if (ring != null)
        {
            Color c = ringColor;
            c.a = Mathf.Lerp(1f, 0f, p);
            ring.color = c;
        }
    }
}
