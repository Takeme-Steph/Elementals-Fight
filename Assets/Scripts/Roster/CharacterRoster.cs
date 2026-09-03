using System.Collections.Generic;
using UnityEngine;

// Mirror of the JS side: Prototypes/CharacterSelect/src/data/characters.js (the CHARACTERS array
// and its getCharacter(id) lookup helper).

/// <summary>
/// The ordered, canonical list of playable fighters.
///
/// List order IS the roster order: it is what PlayerSelection.characters[] /
/// .opponents[] and LoadCharacter.charPrefabs[] are indexed by, and it is the
/// index PlayerPrefs ("selectedCharacter", "selectedOpponent") stores for a
/// saved selection. Reordering or removing entries after release changes what
/// an already-saved index resolves to on a player's device - treat this list
/// as append-only. New fighters go at the end.
///
/// PlayerSelection and LoadCharacter currently keep their own hand-aligned
/// GameObject[] arrays that must be kept in sync with this list by convention
/// only. This asset is meant to become the single place that owns that
/// ordering (each array replaced by roster.Characters[i].displayPrefab /
/// .playablePrefab) - that migration is a separate task and is intentionally
/// not done here.
/// </summary>
[CreateAssetMenu(fileName = "CharacterRoster", menuName = "Elementals Fight/Character Roster")]
public class CharacterRoster : ScriptableObject
{
    [SerializeField]
    private List<CharacterDefinition> characters = new();

    private Dictionary<CharacterId, CharacterDefinition> lookup;

    public IReadOnlyList<CharacterDefinition> Characters => characters;

    public int Count => characters.Count;

    public CharacterDefinition Get(int index)
    {
        if (index < 0 || index >= characters.Count)
        {
            // A bad index here usually means a saved PlayerPrefs selection has gone
            // stale against a roster that shrank - fail loud instead of throwing deep
            // inside PlayerSelection/LoadCharacter.
            Debug.LogError($"CharacterRoster '{name}': index {index} is out of range (Count = {characters.Count}).");
            return null;
        }

        return characters[index];
    }

    public CharacterDefinition Get(CharacterId id)
    {
        TryGet(id, out CharacterDefinition def);
        return def;
    }

    public int IndexOf(CharacterId id)
    {
        for (int i = 0; i < characters.Count; i++)
        {
            if (characters[i] != null && characters[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    public bool TryGet(CharacterId id, out CharacterDefinition def)
    {
        BuildLookupIfNeeded();
        return lookup.TryGetValue(id, out def);
    }

    private void BuildLookupIfNeeded()
    {
        if (lookup != null)
        {
            return;
        }

        lookup = new Dictionary<CharacterId, CharacterDefinition>(characters.Count);
        foreach (CharacterDefinition character in characters)
        {
            if (character == null || character.Id == CharacterId.None)
            {
                continue;
            }

            // A duplicate id here would make TryGet/Get(CharacterId) silently return
            // whichever entry happened to be inserted last - OnValidate already flags
            // this, so here we just keep the first one to stay deterministic.
            if (!lookup.ContainsKey(character.Id))
            {
                lookup.Add(character.Id, character);
            }
        }
    }

    private void OnValidate()
    {
        // The lookup is built lazily from `characters`; any edit made in the inspector
        // must invalidate it or TryGet/Get(CharacterId) would keep serving stale data.
        lookup = null;

        var seenIds = new HashSet<CharacterId>();

        for (int i = 0; i < characters.Count; i++)
        {
            CharacterDefinition character = characters[i];

            if (character == null)
            {
                Debug.LogError($"CharacterRoster '{name}': entry at index {i} is empty (null).");
                continue;
            }

            if (character.Id == CharacterId.None)
            {
                Debug.LogError($"CharacterRoster '{name}': entry at index {i} ('{character.name}') has id CharacterId.None.");
            }
            else if (!seenIds.Add(character.Id))
            {
                Debug.LogError($"CharacterRoster '{name}': entry at index {i} ('{character.name}') duplicates id '{character.Id}' already used earlier in the list.");
            }
        }
    }
}
