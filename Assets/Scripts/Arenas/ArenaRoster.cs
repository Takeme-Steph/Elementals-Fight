using System.Collections.Generic;
using UnityEngine;

// Mirror of Assets/Scripts/Roster/CharacterRoster.cs - same shape, one to one.

/// <summary>
/// The ordered, canonical list of selectable arenas.
///
/// List order IS the roster order: it is what ArenaTabDock.Build iterates and the
/// index PlayerPrefs ("selectedArena") stores for a saved selection. Reordering or
/// removing entries after release changes what an already-saved index resolves to on
/// a player's device - treat this list as append-only. New arenas go at the end.
/// </summary>
[CreateAssetMenu(fileName = "ArenaRoster", menuName = "Elementals Fight/Arena Roster")]
public class ArenaRoster : ScriptableObject
{
    [SerializeField]
    private List<ArenaDefinition> arenas = new();

    private Dictionary<ArenaId, ArenaDefinition> lookup;

    public IReadOnlyList<ArenaDefinition> Arenas => arenas;

    public int Count => arenas.Count;

    public ArenaDefinition Get(int index)
    {
        if (index < 0 || index >= arenas.Count)
        {
            // A bad index here usually means a saved PlayerPrefs selection has gone
            // stale against a roster that shrank - fail loud instead of throwing deep
            // inside ArenaSelectController.
            Debug.LogError($"ArenaRoster '{name}': index {index} is out of range (Count = {arenas.Count}).");
            return null;
        }

        return arenas[index];
    }

    public ArenaDefinition Get(ArenaId id)
    {
        TryGet(id, out ArenaDefinition def);
        return def;
    }

    public int IndexOf(ArenaId id)
    {
        for (int i = 0; i < arenas.Count; i++)
        {
            if (arenas[i] != null && arenas[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }

    public bool TryGet(ArenaId id, out ArenaDefinition def)
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

        lookup = new Dictionary<ArenaId, ArenaDefinition>(arenas.Count);
        foreach (ArenaDefinition arena in arenas)
        {
            if (arena == null || arena.Id == ArenaId.None)
            {
                continue;
            }

            // A duplicate id here would make TryGet/Get(ArenaId) silently return
            // whichever entry happened to be inserted last - OnValidate already flags
            // this, so here we just keep the first one to stay deterministic.
            if (!lookup.ContainsKey(arena.Id))
            {
                lookup.Add(arena.Id, arena);
            }
        }
    }

    private void OnValidate()
    {
        // The lookup is built lazily from `arenas`; any edit made in the inspector
        // must invalidate it or TryGet/Get(ArenaId) would keep serving stale data.
        lookup = null;

        var seenIds = new HashSet<ArenaId>();

        for (int i = 0; i < arenas.Count; i++)
        {
            ArenaDefinition arena = arenas[i];

            if (arena == null)
            {
                Debug.LogError($"ArenaRoster '{name}': entry at index {i} is empty (null).");
                continue;
            }

            if (arena.Id == ArenaId.None)
            {
                Debug.LogError($"ArenaRoster '{name}': entry at index {i} ('{arena.name}') has id ArenaId.None.");
            }
            else if (!seenIds.Add(arena.Id))
            {
                Debug.LogError($"ArenaRoster '{name}': entry at index {i} ('{arena.name}') duplicates id '{arena.Id}' already used earlier in the list.");
            }
        }
    }
}
