/// <summary>
/// Carries the two confirmed roster definitions across the CharacterSelect ->
/// ArenaSelect -> FightScene flow. PlayerPrefs intentionally stores only the stable
/// indexes for future launches; this cache preserves the actual data objects for the
/// current match, so FightScene does not need to fall back to its old prefab array
/// while its scene-level CharacterRoster migration is still awaiting an Editor edit.
/// </summary>
public static class MatchSelection
{
    private static CharacterDefinition player;
    private static CharacterDefinition opponent;

    public static void Set(CharacterDefinition selectedPlayer, CharacterDefinition selectedOpponent)
    {
        player = selectedPlayer;
        opponent = selectedOpponent;
    }

    public static bool TryGetPlayer(out CharacterDefinition definition)
    {
        definition = player;
        return definition != null;
    }

    public static bool TryGetOpponent(out CharacterDefinition definition)
    {
        definition = opponent;
        return definition != null;
    }
}
