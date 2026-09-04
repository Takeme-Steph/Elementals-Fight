using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Top-level orchestrator for the arena-select screen. Owns every other widget and
// wires their events back into each other - nothing here draws or animates anything
// itself, that's WarpTransition/ArenaPanorama/ArenaParticles/ArenaHeader/
// HazardBadgeStrip/ArenaTabDock's job. Mirrors CharacterSelectController's shape.

/// <summary>
/// Drives the ArenaSelect scene: browsing arenas, warping between them, then loading
/// the fight scene with the choice persisted.
/// </summary>
public class ArenaSelectController : MonoBehaviour
{
    [SerializeField]
    private ArenaRoster roster;

    [SerializeField]
    private ArenaPanorama panorama;

    [SerializeField]
    private ArenaParticles particles;

    [SerializeField]
    private ArenaHeader header;

    [SerializeField]
    private HazardBadgeStrip hazardStrip;

    [SerializeField]
    private ArenaTabDock dock;

    [SerializeField]
    private Button backButton;

    [SerializeField]
    private ConfirmButton confirmButton;

    [SerializeField]
    private WarpTransition warp;

    public int CurrentIndex { get; private set; } = -1;

    private bool validRefs = true;

    private void Awake()
    {
        // Every one of these is load-bearing for the rest of the screen; failing loud
        // here beats a mystery NullReferenceException three calls deep in Select().
        if (roster == null)
        {
            Debug.LogError("ArenaSelectController: roster is not assigned.");
            validRefs = false;
        }

        if (panorama == null)
        {
            Debug.LogError("ArenaSelectController: panorama is not assigned.");
            validRefs = false;
        }

        if (particles == null)
        {
            Debug.LogError("ArenaSelectController: particles is not assigned.");
            validRefs = false;
        }

        if (header == null)
        {
            Debug.LogError("ArenaSelectController: header is not assigned.");
            validRefs = false;
        }

        if (hazardStrip == null)
        {
            Debug.LogError("ArenaSelectController: hazardStrip is not assigned.");
            validRefs = false;
        }

        if (dock == null)
        {
            Debug.LogError("ArenaSelectController: dock is not assigned.");
            validRefs = false;
        }

        if (backButton == null)
        {
            Debug.LogError("ArenaSelectController: backButton is not assigned.");
            validRefs = false;
        }

        if (confirmButton == null)
        {
            Debug.LogError("ArenaSelectController: confirmButton is not assigned.");
            validRefs = false;
        }

        if (warp == null)
        {
            Debug.LogError("ArenaSelectController: warp is not assigned.");
            validRefs = false;
        }
    }

    private void Start()
    {
        if (!validRefs)
        {
            return;
        }

        dock.Build(roster);

        int initial = Mathf.Clamp(PlayerPrefs.GetInt("selectedArena", 0), 0, Mathf.Max(0, roster.Count - 1));

        // Apply instantly BEFORE subscribing to IndexChanged, so the first paint never
        // runs the warp/spring animations meant for later taps.
        ApplySelection(initial, true);

        dock.IndexChanged += Select;
        confirmButton.Confirmed += OnConfirm;
        backButton.onClick.AddListener(OnBackTapped);
    }

    private void OnDisable()
    {
        if (dock != null)
        {
            dock.IndexChanged -= Select;
        }

        if (confirmButton != null)
        {
            confirmButton.Confirmed -= OnConfirm;
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveListener(OnBackTapped);
        }
    }

    /// <summary>Warps to roster[index]: called by the dock's IndexChanged event and by keyboard input.</summary>
    public void Select(int index)
    {
        if (!validRefs || roster == null)
        {
            return;
        }

        if (index == CurrentIndex)
        {
            return;
        }

        ArenaDefinition def = roster.Get(index);
        if (def == null)
        {
            return;
        }

        // The flash hides the swap: every other widget only actually repaints once the
        // flash calls us back at its brightest frame.
        warp.Play(def.Accent, () => ApplySelection(index, false));
    }

    private void ApplySelection(int index, bool instant)
    {
        if (roster == null)
        {
            return;
        }

        ArenaDefinition def = roster.Get(index);
        if (def == null)
        {
            return;
        }

        CurrentIndex = index;

        panorama.Show(def, instant);
        particles.Apply(def.ParticleStyle, def.Glow, def.Accent);
        header.SetArena(def, instant);
        hazardStrip.SetHazards(def);
        dock.SetActive(index, instant);
        // Lerped 25% toward white rather than the raw accent: ConfirmButton blends this
        // against its Gold base internally, and some accents (Olympus's cyan) land on
        // an unintentionally green-looking blend at full saturation.
        confirmButton.SetAccent(Color.Lerp(def.Accent, Color.white, 0.25f));
    }

    private void OnConfirm()
    {
        if (roster == null || CurrentIndex < 0)
        {
            return;
        }

        PlayerPrefs.SetInt("selectedArena", CurrentIndex);
        PlayerPrefs.Save();
        SceneManager.LoadScene("FightScene");
    }

    private void OnBackTapped()
    {
        SceneManager.LoadScene("CharacterSelect");
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
        if (roster == null)
        {
            return;
        }

        int next = Mathf.Clamp(CurrentIndex + delta, 0, roster.Count - 1);
        Select(next);
    }
}
