using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 3 pooled badge slots anchored top-right below the header. SetHazards activates
// however many the arena actually has (up to 3) and hides the rest, rather than
// instantiating/destroying anything at runtime - same pooling idea as
// CharacterSelect's PortraitCarousel, just fixed-size instead of roster-sized.

/// <summary>
/// Shows an ArenaDefinition's hazard badges (icon + label, tinted accent), each with
/// a small looping pulse.
/// </summary>
public class HazardBadgeStrip : MonoBehaviour
{
    private const float PulsePeriod = 1.6f;
    private const float PulseScaleHigh = 1.08f;
    private const float GlowAlphaLow = 0.25f;
    private const float GlowAlphaHigh = 0.55f;

    [System.Serializable]
    private class Badge
    {
        public GameObject root;
        [Tooltip("Glass RoundedRect chip background.")]
        public Image chip;
        public Image icon;
        [Tooltip("Soft glow behind the icon; alpha pulses.")]
        public Image glow;
        public TMP_Text label;
    }

    [SerializeField]
    private Badge[] badges = new Badge[0];

    [SerializeField]
    [Tooltip("Hazard icon sprites indexed by (int)ArenaHazard - element 0 (None) is unused.")]
    private Sprite[] hazardIcons = new Sprite[0];

    private bool validRefs = true;
    private float[] pulseStartTime;

    private void Awake()
    {
        if (badges == null || badges.Length == 0)
        {
            Debug.LogError("HazardBadgeStrip: badges is empty.");
            badges = new Badge[0];
            validRefs = false;
        }

        pulseStartTime = new float[badges.Length];

        for (int i = 0; i < badges.Length; i++)
        {
            if (badges[i] != null && badges[i].root != null)
            {
                badges[i].root.SetActive(false);
            }
        }
    }

    /// <summary>Activates and paints one badge per def.Hazards entry (up to the pooled slot count), hides the rest.</summary>
    public void SetHazards(ArenaDefinition def)
    {
        if (!validRefs)
        {
            return;
        }

        if (def == null)
        {
            Debug.LogError("HazardBadgeStrip.SetHazards: def is null.");
            return;
        }

        IReadOnlyList<ArenaHazard> hazards = def.Hazards;
        int count = hazards != null ? Mathf.Min(hazards.Count, badges.Length) : 0;

        for (int i = 0; i < badges.Length; i++)
        {
            Badge badge = badges[i];
            if (badge == null || badge.root == null)
            {
                continue;
            }

            bool active = i < count;
            badge.root.SetActive(active);

            if (!active)
            {
                continue;
            }

            ArenaHazard hazard = hazards[i];
            Sprite sprite = HazardSprite(hazard);

            if (badge.icon != null)
            {
                badge.icon.sprite = sprite;
                badge.icon.enabled = sprite != null;
                badge.icon.color = def.Accent;
            }

            if (badge.label != null)
            {
                badge.label.text = HazardLabel(hazard);
                badge.label.color = def.Accent;
            }

            if (badge.glow != null)
            {
                Color c = badge.glow.color;
                badge.glow.color = new Color(def.Accent.r, def.Accent.g, def.Accent.b, c.a);
            }

            pulseStartTime[i] = Time.unscaledTime;
        }
    }

    private Sprite HazardSprite(ArenaHazard hazard)
    {
        int index = (int)hazard;
        if (hazardIcons == null || index < 0 || index >= hazardIcons.Length)
        {
            return null;
        }

        return hazardIcons[index];
    }

    private static string HazardLabel(ArenaHazard hazard)
    {
        switch (hazard)
        {
            case ArenaHazard.Flame:
                return "FLAME";
            case ArenaHazard.Whirlwind:
                return "WHIRLWIND";
            case ArenaHazard.Lightning:
                return "LIGHTNING";
            case ArenaHazard.Sandstorm:
                return "SANDSTORM";
            case ArenaHazard.Tide:
                return "TIDE";
            case ArenaHazard.Frost:
                return "FROST";
            case ArenaHazard.Void:
                return "VOID";
            case ArenaHazard.None:
                return string.Empty;
            default:
                // New ArenaHazard members must be wired in above, or a badge silently
                // ships with a blank label.
                Debug.LogError($"HazardBadgeStrip: unhandled ArenaHazard '{hazard}'.");
                return hazard.ToString().ToUpperInvariant();
        }
    }

    private void Update()
    {
        if (!validRefs)
        {
            return;
        }

        float t = Time.unscaledTime;

        for (int i = 0; i < badges.Length; i++)
        {
            Badge badge = badges[i];
            if (badge == null || badge.root == null || !badge.root.activeSelf)
            {
                continue;
            }

            float elapsed = t - pulseStartTime[i];
            float phase = (Mathf.Sin(elapsed * (Mathf.PI * 2f / PulsePeriod)) + 1f) * 0.5f;
            float scale = Mathf.Lerp(1f, PulseScaleHigh, phase);
            badge.root.transform.localScale = new Vector3(scale, scale, 1f);

            if (badge.glow != null)
            {
                Color c = badge.glow.color;
                c.a = Mathf.Lerp(GlowAlphaLow, GlowAlphaHigh, phase);
                badge.glow.color = c;
            }
        }
    }
}
