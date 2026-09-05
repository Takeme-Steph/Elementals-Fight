using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Top-level orchestrator for the character-select screen. Owns the two-phase flow
// (pick a player fighter, then pick an opponent) and wires every other widget's
// output back into every other widget's input - nothing here draws or animates
// anything itself beyond the two flow coroutines (flash + toast) and the shop bounce.

/// <summary>
/// Drives the CharacterSelect scene: browsing, locking a player fighter, then an
/// opponent, then loading the fight scene.
/// </summary>
public class CharacterSelectController : MonoBehaviour
{
    public enum Phase
    {
        Player,
        Opponent,
    }

    private const string OverallFormat = "0.0";
    private const float FlashDuration = 0.4f;
    private const float FlashStartAlpha = 0.6f;
    private const float ToastFadeTime = 0.15f;
    private const float ToastTotalTime = 1.2f;
    private const float ShopBounceStiffness = 300f;
    private const float ShopBounceDamping = 18f;
    private static readonly Color AxisLabelDim = new Color(1f, 1f, 1f, 0.7f);

    [SerializeField]
    private CharacterRoster roster;

    [SerializeField]
    private DeityStage stage;

    [SerializeField]
    private AmbientBackdrop backdrop;

    [SerializeField]
    private LorePanel lorePanel;

    [SerializeField]
    private RadarChartGraphic radar;

    [SerializeField]
    [Tooltip("Optional soft glow copy of the radar, drawn behind it. May be left null.")]
    private RadarChartGraphic radarGlow;

    [SerializeField]
    [Tooltip("Overall rating readout, e.g. \"6.2\"; counts up over 0.35s on each selection.")]
    private TMP_Text overallText;

    [SerializeField]
    [Tooltip("5 labels in PWR/SPD/RNG/DEF/MOB order; the max stat's label is tinted Primary.")]
    private TMP_Text[] axisLabels;

    [SerializeField]
    private PortraitCarousel carousel;

    [SerializeField]
    private ConfirmButton confirmButton;

    [SerializeField]
    private TMP_Text headerText;

    [SerializeField]
    [Tooltip("Hidden in Player phase; returns to Player phase when tapped in Opponent phase.")]
    private Button backButton;

    [SerializeField]
    [Tooltip("Inert placeholder - only plays a tap bounce, no other behaviour.")]
    private Button shopButton;

    [SerializeField]
    [Tooltip("Hidden until a player fighter is locked in.")]
    private GameObject vsChip;

    [SerializeField]
    private Image vsChipDisc;

    [SerializeField]
    private TMP_Text vsChipGlyph;

    [SerializeField]
    private TMP_Text vsChipName;

    [SerializeField]
    private CanvasGroup toastGroup;

    [SerializeField]
    private TMP_Text toastText;

    [SerializeField]
    [Tooltip("Full-screen flash on locking a fighter; alpha 0 at rest.")]
    private Image flashOverlay;

    [SerializeField]
    [Tooltip("Build index of the fight scene, loaded once both fighters are locked.")]
    private int fightSceneBuildIndex = 1;

    public int CurrentIndex { get; private set; } = -1;
    public Phase CurrentPhase { get; private set; } = Phase.Player;

    private bool validRefs = true;
    private int playerIndex = -1;
    private float lastOverallValue;
    private readonly float[] statScratch = new float[RadarChartGraphic.AxisCount];
    private readonly int[] statIntScratch = new int[RadarChartGraphic.AxisCount];

    private Coroutine overallRoutine;
    private Coroutine flashRoutine;
    private Coroutine toastRoutine;
    private Coroutine shopBounceRoutine;

    private void Awake()
    {
        // Every one of these is load-bearing for the rest of the screen; failing loud
        // here beats a mystery NullReferenceException three calls deep in Select().
        if (roster == null)
        {
            Debug.LogError("CharacterSelectController: roster is not assigned.");
            validRefs = false;
        }

        if (stage == null)
        {
            Debug.LogError("CharacterSelectController: stage is not assigned.");
            validRefs = false;
        }

        if (carousel == null)
        {
            Debug.LogError("CharacterSelectController: carousel is not assigned.");
            validRefs = false;
        }

        if (lorePanel == null)
        {
            Debug.LogError("CharacterSelectController: lorePanel is not assigned.");
            validRefs = false;
        }

        if (backdrop == null)
        {
            Debug.LogError("CharacterSelectController: backdrop is not assigned.");
            validRefs = false;
        }

        if (confirmButton == null)
        {
            Debug.LogError("CharacterSelectController: confirmButton is not assigned.");
            validRefs = false;
        }
    }

    private void Start()
    {
        if (!validRefs)
        {
            return;
        }

        stage.Build(roster);
        carousel.Build(roster);

        int initial = Mathf.Clamp(PlayerPrefs.GetInt("selectedCharacter", 0), 0, Mathf.Max(0, roster.Count - 1));

        // Snap and apply instantly BEFORE subscribing to IndexChanged, so the first
        // paint never runs the (0.35s+) selection animations meant for later swipes.
        carousel.SnapTo(initial, true);
        ApplySelection(initial, true);

        carousel.IndexChanged += Select;
        confirmButton.Confirmed += OnConfirm;

        if (backButton != null)
        {
            backButton.onClick.AddListener(OnBackTapped);
        }

        if (shopButton != null)
        {
            shopButton.onClick.AddListener(OnShopTapped);
        }

        playerIndex = -1;
        CurrentPhase = Phase.Player;
        ApplyPhaseVisuals();
    }

    /// <summary>Previews roster[index]: called by the carousel's IndexChanged event and by keyboard input.</summary>
    public void Select(int index)
    {
        ApplySelection(index, false);
    }

    private void ApplySelection(int index, bool instant)
    {
        if (roster == null)
        {
            return;
        }

        CharacterDefinition def = roster.Get(index);
        if (def == null)
        {
            return;
        }

        CurrentIndex = index;

        stage.Show(index, instant);
        backdrop.ApplyPalette(def, instant);
        lorePanel.SetCharacter(def, instant);

        statScratch[0] = def.Power / (float)CharacterDefinition.StatMax;
        statScratch[1] = def.Speed / (float)CharacterDefinition.StatMax;
        statScratch[2] = def.Range / (float)CharacterDefinition.StatMax;
        statScratch[3] = def.Defense / (float)CharacterDefinition.StatMax;
        statScratch[4] = def.Mobility / (float)CharacterDefinition.StatMax;

        radar.SetValues(statScratch, instant);
        radar.SetAccent(def.Primary);

        if (radarGlow != null)
        {
            radarGlow.SetValues(statScratch, instant);
            radarGlow.SetAccent(def.Primary);
        }

        UpdateAxisLabels(def);

        if (overallRoutine != null)
        {
            StopCoroutine(overallRoutine);
            overallRoutine = null;
        }

        if (instant)
        {
            lastOverallValue = def.OverallRating;
            if (overallText != null)
            {
                overallText.text = lastOverallValue.ToString(OverallFormat);
            }
        }
        else
        {
            overallRoutine = StartCoroutine(CountUpOverall(def.OverallRating));
        }

        confirmButton.SetAccent(def.Primary);
    }

    private void UpdateAxisLabels(CharacterDefinition def)
    {
        if (axisLabels == null || axisLabels.Length == 0)
        {
            return;
        }

        // Reused instead of a `new int[] { ... }` literal so selecting a character
        // never allocates here.
        statIntScratch[0] = def.Power;
        statIntScratch[1] = def.Speed;
        statIntScratch[2] = def.Range;
        statIntScratch[3] = def.Defense;
        statIntScratch[4] = def.Mobility;

        int maxIndex = 0;
        int maxValue = statIntScratch[0];

        // Strict '>' keeps the FIRST axis on a tie, matching the contract.
        for (int i = 1; i < statIntScratch.Length; i++)
        {
            if (statIntScratch[i] > maxValue)
            {
                maxValue = statIntScratch[i];
                maxIndex = i;
            }
        }

        for (int i = 0; i < axisLabels.Length; i++)
        {
            if (axisLabels[i] == null)
            {
                continue;
            }

            axisLabels[i].color = i == maxIndex ? def.Primary : AxisLabelDim;
        }
    }

    private IEnumerator CountUpOverall(float targetValue)
    {
        float start = lastOverallValue;
        float t = 0f;
        const float duration = 0.35f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);
            float eased = 1f - Mathf.Pow(1f - p, 3f);
            float value = Mathf.Lerp(start, targetValue, eased);

            if (overallText != null)
            {
                overallText.text = value.ToString(OverallFormat);
            }

            yield return null;
        }

        if (overallText != null)
        {
            overallText.text = targetValue.ToString(OverallFormat);
        }

        lastOverallValue = targetValue;
        overallRoutine = null;
    }

    private void OnConfirm()
    {
        if (roster == null)
        {
            return;
        }

        if (CurrentPhase == Phase.Player)
        {
            playerIndex = CurrentIndex;
            CharacterDefinition lockedDef = roster.Get(playerIndex);

            PlayFlash();
            PlayToast(lockedDef != null ? $"{lockedDef.DisplayName.ToUpperInvariant()} LOCKED IN" : "LOCKED IN");
            SetVsChip(lockedDef);

            CurrentPhase = Phase.Opponent;
            ApplyPhaseVisuals();

            // Default the opponent preview to someone other than a mirror match.
            int defaultOpponent = roster.Count > 1 ? (playerIndex + 1) % roster.Count : playerIndex;
            carousel.SnapTo(defaultOpponent, false);
        }
        else
        {
            // ArenaSelect has no CharacterRoster dependency by design. Preserve the
            // confirmed definitions for its loading gateway before this scene unloads,
            // so the next screen can present any future fighter without index-based UI
            // styling or another duplicate roster reference.
            MythicLoadingOverlay.CacheMatchup(roster.Get(playerIndex), roster.Get(CurrentIndex));
            PlayerPrefs.SetInt("selectedCharacter", playerIndex);
            PlayerPrefs.SetInt("selectedOpponent", CurrentIndex);
            PlayerPrefs.Save();
            SceneManager.LoadScene(fightSceneBuildIndex, LoadSceneMode.Single);
        }
    }

    private void OnBackTapped()
    {
        if (CurrentPhase != Phase.Opponent)
        {
            return;
        }

        CurrentPhase = Phase.Player;
        ApplyPhaseVisuals();
        carousel.SnapTo(playerIndex, false);
    }

    private void OnShopTapped()
    {
        if (shopButton == null)
        {
            return;
        }

        if (shopBounceRoutine != null)
        {
            StopCoroutine(shopBounceRoutine);
        }

        shopBounceRoutine = StartCoroutine(ShopBounceRoutine());
    }

    private IEnumerator ShopBounceRoutine()
    {
        RectTransform rect = shopButton.transform as RectTransform;
        if (rect == null)
        {
            yield break;
        }

        float scale = 0.9f;
        float velocity = 0f;
        rect.localScale = new Vector3(scale, scale, 1f);

        while (!UiSpring.Settled(scale, 1f, velocity))
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
            scale = UiSpring.Step(scale, 1f, ref velocity, ShopBounceStiffness, ShopBounceDamping, dt);
            rect.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        rect.localScale = Vector3.one;
        shopBounceRoutine = null;
    }

    private void ApplyPhaseVisuals()
    {
        bool opponentPhase = CurrentPhase == Phase.Opponent;

        if (headerText != null)
        {
            headerText.text = opponentPhase ? "CHOOSE YOUR OPPONENT" : "CHOOSE YOUR DEITY";
        }

        confirmButton.SetLabel(opponentPhase ? "FIGHT" : "CONFIRM");

        if (backButton != null)
        {
            backButton.gameObject.SetActive(opponentPhase);
        }

        if (vsChip != null)
        {
            vsChip.SetActive(opponentPhase);
        }
    }

    private void SetVsChip(CharacterDefinition def)
    {
        if (def == null)
        {
            return;
        }

        if (vsChipDisc != null)
        {
            vsChipDisc.color = def.Primary;
        }

        if (vsChipGlyph != null)
        {
            vsChipGlyph.text = string.IsNullOrEmpty(def.DisplayName) ? "?" : def.DisplayName.Substring(0, 1).ToUpperInvariant();
        }

        if (vsChipName != null)
        {
            vsChipName.text = def.DisplayName;
        }
    }

    private void PlayFlash()
    {
        if (flashOverlay == null)
        {
            return;
        }

        if (flashRoutine != null)
        {
            StopCoroutine(flashRoutine);
        }

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        Color c = flashOverlay.color;
        c.a = FlashStartAlpha;
        flashOverlay.color = c;

        float t = 0f;
        while (t < FlashDuration)
        {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(FlashStartAlpha, 0f, Mathf.Clamp01(t / FlashDuration));
            flashOverlay.color = c;
            yield return null;
        }

        c.a = 0f;
        flashOverlay.color = c;
        flashRoutine = null;
    }

    private void PlayToast(string text)
    {
        if (toastGroup == null)
        {
            return;
        }

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
        }

        toastRoutine = StartCoroutine(ToastRoutine(text));
    }

    private IEnumerator ToastRoutine(string text)
    {
        if (toastText != null)
        {
            toastText.text = text;
        }

        float holdTime = Mathf.Max(0f, ToastTotalTime - ToastFadeTime * 2f);

        float t = 0f;
        while (t < ToastFadeTime)
        {
            t += Time.unscaledDeltaTime;
            toastGroup.alpha = Mathf.Clamp01(t / ToastFadeTime);
            yield return null;
        }
        toastGroup.alpha = 1f;

        t = 0f;
        while (t < holdTime)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        t = 0f;
        while (t < ToastFadeTime)
        {
            t += Time.unscaledDeltaTime;
            toastGroup.alpha = 1f - Mathf.Clamp01(t / ToastFadeTime);
            yield return null;
        }

        toastGroup.alpha = 0f;
        toastRoutine = null;
    }

    private void Update()
    {
        if (!validRefs)
        {
            return;
        }

        // New Input System only - UnityEngine.Input throws in this project - so every
        // read here goes through Keyboard.current and is guarded against it being null
        // (no keyboard attached, e.g. on some mobile test devices).
        Keyboard kb = Keyboard.current;
        if (kb == null)
        {
            return;
        }

        if (kb.leftArrowKey.wasPressedThisFrame)
        {
            StepSelection(-1);
        }
        else if (kb.rightArrowKey.wasPressedThisFrame)
        {
            StepSelection(1);
        }

        if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
        {
            confirmButton.Press();
        }
    }

    private void StepSelection(int delta)
    {
        if (roster == null || carousel == null)
        {
            return;
        }

        int next = Mathf.Clamp(CurrentIndex + delta, 0, roster.Count - 1);
        carousel.SnapTo(next, false);
    }
}
