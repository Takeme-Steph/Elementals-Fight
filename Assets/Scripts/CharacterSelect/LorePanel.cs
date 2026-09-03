using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Left-side identity panel: eyebrow / name / title / lore / chip row. Switching
// characters fades the current content out, rewrites every field, then staggers each
// row back in so the panel never looks like it "cuts" between fighters.

/// <summary>
/// Displays a CharacterDefinition's identity text and animates the swap between
/// fighters.
/// </summary>
public class LorePanel : MonoBehaviour
{
    private const float FadeOutDuration = 0.12f;
    private const float FadeOutY = -6f;
    private const float FadeInY = 12f;

    [SerializeField]
    [Tooltip("\"YORUBA · OCEAN MOTHER\" style pantheon/domain line.")]
    private TMP_Text eyebrow;

    [SerializeField]
    [Tooltip("Amber pill shown only while the selected fighter is a placeholder.")]
    private GameObject placeholderTag;

    [SerializeField]
    private TMP_Text nameText;

    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text loreText;

    [SerializeField]
    private TMP_Text playstyleText;

    [SerializeField]
    private Image playstyleChip;

    [SerializeField]
    private TMP_Text elementText;

    [SerializeField]
    [Tooltip("Vertical gradient trim strip along the panel edge, tinted Primary.")]
    private Image trim;

    [SerializeField]
    [Tooltip("Stagger order: eyebrow row, name, title, lore, chips row.")]
    private CanvasGroup[] lines;

    [SerializeField]
    private float staggerDelay = 0.05f;

    [SerializeField]
    private float lineDuration = 0.2f;

    private RectTransform[] lineRects;
    private Vector2[] lineRestPos;
    private Coroutine activeRoutine;

    // Reused by SetCharacterRoutine instead of allocating a float[]/bool[] on every
    // character switch (this coroutine runs on every carousel swipe, not just once).
    private float[] staggerElapsedScratch;
    private bool[] staggerDoneScratch;

    private void Awake()
    {
        if (lines == null || lines.Length == 0)
        {
            Debug.LogError("LorePanel: lines is empty.");
            lines = new CanvasGroup[0];
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
        }

        staggerElapsedScratch = new float[lines.Length];
        staggerDoneScratch = new bool[lines.Length];
    }

    /// <summary>Writes def's identity text into the panel, instantly or via the fade/stagger sequence.</summary>
    public void SetCharacter(CharacterDefinition def, bool instant)
    {
        if (def == null)
        {
            Debug.LogError("LorePanel.SetCharacter: def is null.");
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
            CaptureRestPositions();
            for (int i = 0; i < lines.Length; i++)
            {
                ApplyLineState(i, 1f, lineRestPos[i]);
            }
        }
        else
        {
            activeRoutine = StartCoroutine(SetCharacterRoutine(def));
        }
    }

    private IEnumerator SetCharacterRoutine(CharacterDefinition def)
    {
        // Fade everything out together first so the old text never sits half-replaced.
        CaptureRestPositions();
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
        CaptureRestPositions();

        // Stagger each row back in independently so later rows visibly trail the earlier ones.
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

                float delay = i * staggerDelay;
                staggerElapsedScratch[i] += dt;
                float local = staggerElapsedScratch[i] - delay;

                if (local < 0f)
                {
                    continue;
                }

                float p = lineDuration > 0f ? Mathf.Clamp01(local / lineDuration) : 1f;
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

    // The rows are children of a VerticalLayoutGroup, so their rest positions are
    // owned by the layout and change whenever the text does (a longer lore wraps to
    // more lines and pushes the chip row down). Capturing them once in Awake gave
    // (0,0) - the pre-layout value - and the stagger then slid every row into the
    // panel's top-left corner. Force the layout to settle and read back the real
    // positions every time the content is rewritten.
    private void CaptureRestPositions()
    {
        // ForceRebuildLayoutImmediate only drives children of a rect that itself
        // carries a layout controller; passing this panel's root (no controller)
        // silently did nothing and every rewrite stacked another animation offset
        // onto the rows. The VerticalLayoutGroup lives on the rows' parent.
        RectTransform layoutRoot = null;
        for (int i = 0; i < lineRects.Length && layoutRoot == null; i++)
        {
            if (lineRects[i] != null)
            {
                layoutRoot = lineRects[i].parent as RectTransform;
            }
        }

        if (layoutRoot == null)
        {
            layoutRoot = transform as RectTransform;
        }

        if (layoutRoot != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(layoutRoot);
        }

        for (int i = 0; i < lineRects.Length; i++)
        {
            lineRestPos[i] = lineRects[i] != null ? lineRects[i].anchoredPosition : Vector2.zero;
        }
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

    private void WriteContent(CharacterDefinition def)
    {
        if (eyebrow != null)
        {
            eyebrow.text = $"{def.Pantheon.ToUpperInvariant()} · {def.Domain.ToUpperInvariant()}";
        }

        if (placeholderTag != null)
        {
            placeholderTag.SetActive(def.IsPlaceholder);
        }

        if (nameText != null)
        {
            nameText.text = def.DisplayName.ToUpperInvariant();
            nameText.enableVertexGradient = true;
            nameText.colorGradient = new VertexGradient(Color.white, Color.white, def.Primary, def.Primary);
        }

        if (titleText != null)
        {
            titleText.text = def.Title;
            titleText.color = def.Secondary;
        }

        if (loreText != null)
        {
            loreText.text = def.Lore;
        }

        if (playstyleText != null)
        {
            playstyleText.text = SpacedPlaystyle(def.Playstyle);
            playstyleText.color = def.Primary;
        }

        if (playstyleChip != null)
        {
            Color c = def.Primary;
            c.a = 0.15f;
            playstyleChip.color = c;
        }

        if (elementText != null)
        {
            elementText.text = def.Element.ToString().ToUpperInvariant();
        }

        if (trim != null)
        {
            float alpha = trim.color.a;
            Color c = def.Primary;
            c.a = alpha;
            trim.color = c;
        }
    }

    private static string SpacedPlaystyle(Playstyle style)
    {
        switch (style)
        {
            case Playstyle.Bruiser:
                return "BRUISER / HEAVY";
            case Playstyle.Rushdown:
                return "ASSASSIN / RUSHDOWN";
            case Playstyle.Zoner:
                return "RANGED / ZONING";
            case Playstyle.Marksman:
                return "MARKSMAN / AGILE";
            case Playstyle.Guardian:
                return "GUARDIAN / COUNTER";
            case Playstyle.Trickster:
                return "TRICKSTER / MIX-UP";
            default:
                Debug.LogError($"LorePanel: unhandled Playstyle '{style}'.");
                return style.ToString().ToUpperInvariant();
        }
    }
}
