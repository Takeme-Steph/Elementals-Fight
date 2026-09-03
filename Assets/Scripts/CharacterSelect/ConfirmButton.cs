using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The single call-to-action button: a permanent slow scale/glow pulse so it always
// reads as "tap me", plus a quick squash-and-release on tap before the Confirmed event
// actually fires (so the fighter transition never starts before the button animation
// has visibly registered the tap).

/// <summary>
/// CONFIRM / FIGHT button with an idle pulse and a press squash animation.
/// </summary>
public class ConfirmButton : MonoBehaviour
{
    private const float SquashTarget = 0.92f;
    private const float SquashDownDuration = 0.08f;
    private const float SquashSpringStiffness = 400f;
    private const float SquashSpringDamping = 24f;

    [SerializeField]
    private Button button;

    [SerializeField]
    private RectTransform scaleTarget;

    [SerializeField]
    [Tooltip("Gold->white gradient sprite; tinted toward the palette so it reads gold->primary.")]
    private Image background;

    [SerializeField]
    [Tooltip("Soft glow behind the button, tinted Primary; alpha pulses independently of hue.")]
    private Image glow;

    [SerializeField]
    private TMP_Text label;

    [SerializeField]
    private float pulsePeriod = 1.8f;

    [SerializeField]
    private float pulseScale = 1.04f;

    public event System.Action Confirmed;

    private Color glowBaseColor = Color.white;
    private float squashScale = 1f;
    private float squashVelocity;
    private Coroutine pressRoutine;

    private void Awake()
    {
        if (button == null || scaleTarget == null)
        {
            Debug.LogError("ConfirmButton: button and scaleTarget must be assigned.");
        }

        if (glow != null)
        {
            glowBaseColor = glow.color;
        }

        if (button != null)
        {
            button.onClick.AddListener(Press);
        }
    }

    /// <summary>Tints the background/glow toward the fighter's primary colour.</summary>
    public void SetAccent(Color primary)
    {
        if (background != null)
        {
            // Keep the button reading as gold with a hint of the fighter's colour; a plain
            // white->primary tint over the gold sprite went pale on cool palettes.
            background.color = Color.Lerp(new Color(0.96f, 0.84f, 0.43f), primary, 0.35f);
        }

        if (glow != null)
        {
            glowBaseColor = new Color(primary.r, primary.g, primary.b, glow.color.a);
        }

        if (label != null)
        {
            label.color = new Color32(0x0B, 0x0B, 0x12, 255);
        }
    }

    public void SetLabel(string text)
    {
        if (label != null)
        {
            label.text = text;
        }
    }

    /// <summary>Plays the squash-and-release tap animation, then fires Confirmed.</summary>
    public void Press()
    {
        if (pressRoutine != null)
        {
            // A double-tap restarts cleanly rather than stacking two releases.
            StopCoroutine(pressRoutine);
        }

        pressRoutine = StartCoroutine(PressRoutine());
    }

    private IEnumerator PressRoutine()
    {
        float start = squashScale;
        float t = 0f;

        while (t < SquashDownDuration)
        {
            t += Time.unscaledDeltaTime;
            squashScale = Mathf.Lerp(start, SquashTarget, Mathf.Clamp01(t / SquashDownDuration));
            yield return null;
        }
        squashScale = SquashTarget;

        squashVelocity = 0f;
        while (!UiSpring.Settled(squashScale, 1f, squashVelocity))
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
            squashScale = UiSpring.Step(squashScale, 1f, ref squashVelocity, SquashSpringStiffness, SquashSpringDamping, dt);
            yield return null;
        }
        squashScale = 1f;

        pressRoutine = null;
        Confirmed?.Invoke();
    }

    private void Update()
    {
        float t = Time.unscaledTime;
        float phase = (Mathf.Sin(t * (Mathf.PI * 2f / Mathf.Max(pulsePeriod, 0.01f))) + 1f) * 0.5f;
        float idleMul = Mathf.Lerp(1f, pulseScale, phase);

        if (scaleTarget != null)
        {
            float final = idleMul * squashScale;
            scaleTarget.localScale = new Vector3(final, final, 1f);
        }

        if (glow != null)
        {
            Color c = glowBaseColor;
            c.a = Mathf.Lerp(0.35f, 0.6f, phase);
            glow.color = c;
        }
    }
}
