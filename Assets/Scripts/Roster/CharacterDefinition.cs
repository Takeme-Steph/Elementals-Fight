using UnityEngine;

// Mirror of the JS side: Prototypes/CharacterSelect/src/data/characters.js (the Character objects
// inside CHARACTERS). Keep field-for-field parity so the game and the React
// UI prototype agree on what a fighter is.

/// <summary>
/// One entry of the stat radar, in the order the radar chart draws them
/// (clockwise from the top). Matches STAT_AXES in characters.js.
/// </summary>
public enum StatAxis
{
    Power,
    Speed,
    Range,
    Defense,
    Mobility,
}

/// <summary>
/// Data-only description of a single fighter: identity, lore, colours, stats
/// and the prefabs/sprite used to represent it. One asset per fighter; a
/// CharacterRoster holds the ordered list that the game actually reads from.
/// </summary>
[CreateAssetMenu(fileName = "NewCharacter", menuName = "Elementals Fight/Character Definition")]
public class CharacterDefinition : ScriptableObject
{
    /// <summary>Stats are 0-10 across the board; keep this in lock-step with STAT_MAX in characters.js.</summary>
    public const int StatMax = 10;

    [Header("Identity")]
    [SerializeField]
    private CharacterId id;

    [SerializeField]
    private string displayName;

    [SerializeField]
    private string pantheon;

    [SerializeField]
    private string domain;

    [SerializeField]
    private string title;

    [SerializeField]
    [TextArea(3, 5)]
    private string lore;

    [SerializeField]
    private Playstyle playstyle;

    [SerializeField]
    private Element element;

    [Header("Colours")]
    [SerializeField]
    private Color primary;

    [SerializeField]
    private Color secondary;

    [SerializeField]
    private Color glow;

    [SerializeField]
    private Color deep;

    [Header("Stats (0-10)")]
    [SerializeField]
    [Range(0, StatMax)]
    private int power;

    [SerializeField]
    [Range(0, StatMax)]
    private int speed;

    [SerializeField]
    [Range(0, StatMax)]
    private int range;

    [SerializeField]
    [Range(0, StatMax)]
    private int defense;

    [SerializeField]
    [Range(0, StatMax)]
    private int mobility;

    [Header("Presentation")]
    [SerializeField]
    [Tooltip("2D carousel portrait shown in character select.")]
    private Sprite icon;

    [SerializeField]
    [Tooltip("Display model toggled on/off in CharacterSelect (PlayerSelection.characters / .opponents).")]
    private GameObject displayPrefab;

    [SerializeField]
    [Tooltip("Playable fighter prefab spawned into the fight scene (LoadCharacter.charPrefabs).")]
    private GameObject playablePrefab;

    [SerializeField]
    [Tooltip("Optional. Animator controller played on the display model in CharacterSelect so the fighter idles instead of standing in bind pose. The roster tool defaults it to the playable prefab's controller, whose entry state is Idle.")]
    private RuntimeAnimatorController displayAnimator;

    [SerializeField]
    [Tooltip("True while this entry is a stand-in for a not-yet-final fighter.")]
    private bool isPlaceholder;

    public CharacterId Id => id;
    public string DisplayName => displayName;
    public string Pantheon => pantheon;
    public string Domain => domain;
    public string Title => title;
    public string Lore => lore;
    public Playstyle Playstyle => playstyle;
    public Element Element => element;

    public Color Primary => primary;
    public Color Secondary => secondary;
    public Color Glow => glow;
    public Color Deep => deep;

    public int Power => power;
    public int Speed => speed;
    public int Range => range;
    public int Defense => defense;
    public int Mobility => mobility;

    public Sprite Icon => icon;
    public GameObject DisplayPrefab => displayPrefab;
    public GameObject PlayablePrefab => playablePrefab;
    public RuntimeAnimatorController DisplayAnimator => displayAnimator;
    public bool IsPlaceholder => isPlaceholder;

    /// <summary>Average of the five stats, matching how the UI summarises the radar chart.</summary>
    public float OverallRating => (power + speed + range + defense + mobility) / 5f;

    public int GetStat(StatAxis axis)
    {
        switch (axis)
        {
            case StatAxis.Power:
                return power;
            case StatAxis.Speed:
                return speed;
            case StatAxis.Range:
                return range;
            case StatAxis.Defense:
                return defense;
            case StatAxis.Mobility:
                return mobility;
            default:
                // New StatAxis members must be wired in above, or callers silently get 0.
                Debug.LogError($"CharacterDefinition '{name}': unhandled StatAxis '{axis}'.");
                return 0;
        }
    }

    private void OnValidate()
    {
        // None means "no fighter assigned" everywhere else in the codebase (PlayerPrefs
        // defaults, empty roster slots) - an asset shipped with id == None would collide
        // with that sentinel and silently resolve to "nobody" at runtime.
        if (id == CharacterId.None)
        {
            Debug.LogError($"CharacterDefinition '{name}': id is not set (still CharacterId.None).");
        }

        // Placeholders are allowed to ship without art while the fighter is still being
        // built; a non-placeholder without prefabs would crash PlayerSelection/LoadCharacter
        // the moment it gets selected, so catch it here instead of at runtime.
        if (!isPlaceholder)
        {
            if (displayPrefab == null)
            {
                Debug.LogError($"CharacterDefinition '{name}': displayPrefab is missing and isPlaceholder is false.");
            }

            if (playablePrefab == null)
            {
                Debug.LogError($"CharacterDefinition '{name}': playablePrefab is missing and isPlaceholder is false.");
            }
        }
    }
}
