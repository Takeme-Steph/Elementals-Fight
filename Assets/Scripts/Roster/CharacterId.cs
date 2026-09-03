// Stable identifiers for every fighter in the roster.
//
// Mirror of the JS side: Prototypes/CharacterSelect/src/data/characters.js (id / Element / Playstyle).
// Keep member names in lower-case-matching sync with the `id` strings used there.

/// <summary>
/// Stable per-fighter identifier. These integer values are serialized into
/// ScriptableObject assets (CharacterDefinition.id) and into PlayerPrefs
/// ("selectedCharacter", "selectedOpponent" in PlayerSelection/LoadCharacter
/// store the roster *index*, but downstream systems increasingly key off this
/// enum instead). Because of that, members must NEVER be renumbered or
/// reordered - doing so silently reinterprets every already-saved value as a
/// different fighter. Only ever append new members with the next free number.
/// </summary>
public enum CharacterId
{
    None = 0,
    Yemoja = 1,
    Shango = 2,
    Oya = 3,
    Anansi = 4,
    EarthMage = 5,
    Ninja = 6,
    WarriorPrincess = 7,
}

/// <summary>
/// Elemental affinity. Drives the neon ring colour and the ambient backdrop.
/// </summary>
public enum Element
{
    Water,
    Thunder,
    Wind,
    Spirit,
    Earth,
    Sun,
}

/// <summary>
/// Broad archetype label shown as the playstyle chip.
/// </summary>
public enum Playstyle
{
    Bruiser,
    Rushdown,
    Zoner,
    Marksman,
    Guardian,
    Trickster,
}
