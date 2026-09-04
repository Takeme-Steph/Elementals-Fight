using System.Collections.Generic;
using UnityEngine;

// Mirror of Assets/Scripts/Roster/CharacterDefinition.cs - same shape, one asset per
// arena, data-only. An ArenaRoster holds the ordered list the game actually reads from.

/// <summary>
/// Data-only description of a single arena: identity, palette, VFX profile, hazard
/// badges and the optional art/prefab hooks the select screen and FightScene read.
/// </summary>
[CreateAssetMenu(fileName = "NewArena", menuName = "Elementals Fight/Arena Definition")]
public class ArenaDefinition : ScriptableObject
{
    /// <summary>HazardBadgeStrip only has 3 badge slots; OnValidate warns past this.</summary>
    public const int MaxHazards = 3;

    [Header("Identity")]
    [SerializeField]
    private ArenaId id;

    [SerializeField]
    private string displayName;

    [SerializeField]
    [Tooltip("e.g. \"Norse Realm\" - shown small above the title in ArenaHeader.")]
    private string pantheon;

    [SerializeField]
    [Tooltip("Flavour line shown below the title in ArenaHeader.")]
    private string subtitle;

    [SerializeField]
    [Tooltip("Optional. Tinted-at-runtime rune icon shown on the arena's tab. Falls back to runeGlyph when null.")]
    private Sprite runeSprite;

    [SerializeField]
    [Tooltip("Fallback glyph shown on the tab when runeSprite is not assigned.")]
    private string runeGlyph;

    [Header("Palette")]
    [SerializeField]
    private Color skyTop;

    [SerializeField]
    private Color skyBottom;

    [SerializeField]
    private Color horizon;

    [SerializeField]
    private Color accent;

    [SerializeField]
    private Color glow;

    [SerializeField]
    private Color deep;

    [Header("VFX")]
    [SerializeField]
    private ArenaParticleStyle particleStyle;

    [SerializeField]
    [Tooltip("Up to 3 hazard badges shown in HazardBadgeStrip, in display order.")]
    private ArenaHazard[] hazards = new ArenaHazard[0];

    [Header("Scene hooks")]
    [SerializeField]
    [Tooltip("Optional. Static panorama art; when null the panorama is drawn entirely from the palette colours.")]
    private Sprite panoramaSprite;

    [SerializeField]
    [Tooltip("Optional for a placeholder arena. Instantiated into FightScene when this arena is selected.")]
    private GameObject environmentPrefab;

    [SerializeField]
    [Tooltip("True while this entry is a stand-in for a not-yet-final arena.")]
    private bool isPlaceholder;

    public ArenaId Id => id;
    public string DisplayName => displayName;
    public string Pantheon => pantheon;
    public string Subtitle => subtitle;
    public Sprite RuneSprite => runeSprite;
    public string RuneGlyph => runeGlyph;

    public Color SkyTop => skyTop;
    public Color SkyBottom => skyBottom;
    public Color Horizon => horizon;
    public Color Accent => accent;
    public Color Glow => glow;
    public Color Deep => deep;

    public ArenaParticleStyle ParticleStyle => particleStyle;
    public IReadOnlyList<ArenaHazard> Hazards => hazards;

    public Sprite PanoramaSprite => panoramaSprite;
    public GameObject EnvironmentPrefab => environmentPrefab;
    public bool IsPlaceholder => isPlaceholder;

    private void OnValidate()
    {
        // None means "no arena assigned" everywhere else in the codebase (PlayerPrefs
        // defaults, empty roster slots) - an asset shipped with id == None would collide
        // with that sentinel and silently resolve to "nowhere" at runtime.
        if (id == ArenaId.None)
        {
            Debug.LogError($"ArenaDefinition '{name}': id is not set (still ArenaId.None).");
        }

        // Placeholders are allowed to ship without an environment while the arena is
        // still being built; a non-placeholder without one would leave FightScene with
        // nothing to instantiate the moment it gets selected, so catch it here instead
        // of at runtime.
        if (!isPlaceholder && environmentPrefab == null)
        {
            Debug.LogError($"ArenaDefinition '{name}': environmentPrefab is missing and isPlaceholder is false.");
        }

        if (hazards != null && hazards.Length > MaxHazards)
        {
            Debug.LogError($"ArenaDefinition '{name}': {hazards.Length} hazards assigned, HazardBadgeStrip only has {MaxHazards} slots.");
        }
    }
}
