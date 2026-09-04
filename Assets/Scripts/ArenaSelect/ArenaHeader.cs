using System.Collections;
using TMPro;
using UnityEngine;

// Top-centre title block: pantheon (small, above), title (large), subtitle (small,
// below). Same fade-out-then-staggered-fade-in approach as CharacterSelect's LorePanel,
// trimmed to 3 fixed-position lines - nothing here wraps or reflows, so unlike LorePanel
// there's no need to re-measure a layout group's rest position on every rewrite.

/// <summary>
/// Displays an ArenaDefinition's title text and animates the swap between arenas.
/// </summary>
public class ArenaHeader : MonoBehaviour
{
    private const float FadeOutDuration = 0.1f;
    private const float FadeOutY = -6f;
    private const float FadeInY = 12f;
    private const float LineDuration = 0.25f;
    private const float StaggerDelay = 0.06f;

    [SerializeField]
    private TMP_Text pantheonText;

    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text subtitleText;

    [SerializeField]
    [Tooltip("Stagger order: pantheon, title, subtitle.")]
    private CanvasGroup[] lines;

    private RectTransform[] lineRects;
    private Vector2[] lineRestPos;
    private Coroutine activeRoutine;
    private bool validRefs = true;

    // Reused by SetArenaRoutine instead of allocating a float[]/bool[] on every arena
    // switch (this coroutine runs on every tab tap, not just once).
    private float[] staggerElapsedScratch;
    private bool[] staggerDoneScratch;

    private void Awake()
    {
        if (lines == null || lines.Length == 0)
        {
            Debug.LogError("ArenaHeader: lines is empty.");
            lines = new CanvasGroup[0];
            validRefs = false;
        }

        lineRects = new RectTransform[lines.Length];
        lineRestPos = new Vector2[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] == null)
            {
                continue;
            }

            lineRects[i] = lines[i].transform as RectTransform;
            lineRestPos[i] = lineRects[i] != null ? lineRects[i].anchoredPosition : Vector2.zero;
        }

        staggerElapsedScratch = new float[lines.Length];
        staggerDoneScratch = new bool[lines.Length];
    }

    /// <summary>Writes def's title text into the header, instantly or via the fade/stagger sequence.</summary>
    public void SetArena(ArenaDefinition def, bool instant)
    {
        if (!validRefs)
        {
            return;
        }

        if (def == null)
        {
            Debug.LogError("ArenaHeader.SetArena: def is null.");
            return;
        }

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        if (instant)
        {
            WriteContent(def);
            for (int i = 0; i < lines.Length; i++)
            {
                ApplyLineState(i, 1f, lineRestPos[i]);
            }
        }
        else
        {
            activeRoutine = StartCoroutine(SetArenaRoutine(def));
        }
    }

    private IEnumerator SetArenaRoutine(ArenaDefinition def)
    {
        float t = 0f;
        while (t < FadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / FadeOutDuration);
            for (int i = 0; i < lines.Length; i++)
            {
                ApplyLineState(i, a, lineRestPos[i] + new Vector2(0f, FadeOutY * (1f - a)));
            }
            yield return null;
        }

        for (int i = 0; i < lines.Length; i++)
        {
            ApplyLineState(i, 0f, lineRestPos[i] + new Vector2(0f, FadeOutY));
        }

        WriteContent(def);

        for (int i = 0; i < lines.Length; i++)
        {
            staggerElapsedScratch[i] = 0f;
            staggerDoneScratch[i] = false;
        }
        int remaining = lines.Length;

        while (remaining > 0)
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < lines.Length; i++)
            {
                if (staggerDoneScratch[i])
                {
                    continue;
                }

                float delay = i * StaggerDelay;
                staggerElapsedScratch[i] += dt;
                float local = staggerElapsedScratch[i] - delay;

                if (local < 0f)
                {
                    continue;
                }

                float p = LineDuration > 0f ? Mathf.Clamp01(local / LineDuration) : 1f;
                float eased = 1f - Mathf.Pow(1f - p, 3f); // ease-out cubic
                float y = Mathf.Lerp(lineRestPos[i].y + FadeInY, lineRestPos[i].y, eased);
                ApplyLineState(i, eased, new Vector2(lineRestPos[i].x, y));

                if (p >= 1f)
                {
                    staggerDoneScratch[i] = true;
                    remaining--;
                }
            }
            yield return null;
        }

        activeRoutine = null;
    }

    private void ApplyLineState(int i, float alpha, Vector2 pos)
    {
        if (lines[i] != null)
        {
            lines[i].alpha = alpha;
        }

        if (lineRects[i] != null)
        {
            lineRects[i].anchoredPosition = pos;
        }
    }

    private void WriteContent(ArenaDefinition def)
    {
        if (pantheonText != null)
        {
            pantheonText.text = string.IsNullOrEmpty(def.Pantheon) ? string.Empty : def.Pantheon.ToUpperInvariant();
            pantheonText.color = def.Accent;
        }

        if (titleText != null)
        {
            titleText.text = string.IsNullOrEmpty(def.DisplayName) ? string.Empty : def.DisplayName.ToUpperInvariant();
        }

        if (subtitleText != null)
        {
            subtitleText.text = def.Subtitle;
        }
    }
}
