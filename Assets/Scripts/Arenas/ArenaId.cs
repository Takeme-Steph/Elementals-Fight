// Stable identifiers for every arena in the game.
//
// Mirror of Assets/Scripts/Roster/CharacterId.cs - see that file's header for why
// append-only numbering matters here too.

/// <summary>
/// Stable per-arena identifier. Serialized into ArenaDefinition.id. PlayerPrefs
/// "selectedArena" stores the roster *index*, not this enum (same convention as
/// "selectedCharacter"), but ArenaRoster.Get(ArenaId)/IndexOf(ArenaId) look up
/// definitions by this id, so members must NEVER be renumbered or reordered -
/// only ever append new members with the next free number.
/// </summary>
public enum ArenaId
{
    None = 0,
    BifrostPalace = 1,
    DuatTemple = 2,
    OlympusHeights = 3,
}

/// <summary>
/// Environmental hazard badges an arena can show in the HazardBadgeStrip. An
/// ArenaDefinition lists up to 3 of these; None is never added to that list, it
/// only exists as the "nothing assigned" sentinel for an unused badge slot.
/// </summary>
public enum ArenaHazard
{
    None,
    Flame,
    Whirlwind,
    Lightning,
    Sandstorm,
    Tide,
    Frost,
    Void,
}

/// <summary>
/// VFX profile the screen's single ArenaParticles system is reconfigured to when an
/// arena becomes active. See ArenaParticles.Apply for what each style actually does.
/// </summary>
public enum ArenaParticleStyle
{
    Stardust,
    SandWisps,
    CloudMist,
}
