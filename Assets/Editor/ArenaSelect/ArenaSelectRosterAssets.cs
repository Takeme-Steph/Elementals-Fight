using UnityEditor;
using UnityEngine;

// Creates/updates the ArenaDefinition assets + the ArenaRoster asset that ties them
// together in seed-table order. Idempotent: re-running this after retuning a colour or
// hazard list below updates the existing assets in place (same GUID, so PlayerPrefs
// "selectedArena" indices keep resolving to the same arena) rather than duplicating
// them. Mirrors CharacterSelectRosterAssets.cs.
public static class ArenaSelectRosterAssets
{
    private const string ArenasFolder = "Assets/Data/Arenas/Arenas";
    private const string RosterAssetPath = "Assets/Data/Arenas/ArenaRoster.asset";

    private struct Seed
    {
        public string fileName;
        public ArenaId id;
        public string displayName;
        public string pantheon;
        public string subtitle;
        public string runeSpriteName;
        public string runeGlyph;
        public string skyTopHex;
        public string skyBottomHex;
        public string horizonHex;
        public string accentHex;
        public string glowHex;
        public string deepHex;
        public ArenaParticleStyle particleStyle;
        public ArenaHazard[] hazards;
        public bool isPlaceholder;
    }

    // Order here IS the roster order (and the PlayerPrefs "selectedArena" index it
    // resolves to) - append-only, same convention as CharacterSelectRosterAssets.
    private static readonly Seed[] Seeds =
    {
        new Seed
        {
            fileName = "BifrostPalace",
            id = ArenaId.BifrostPalace,
            displayName = "Bifrost Palace",
            pantheon = "Norse Realm",
            subtitle = "The eternal stormy thunder hall",
            runeSpriteName = "RuneBifrost",
            runeGlyph = "B",
            skyTopHex = "#0B1E5B",
            skyBottomHex = "#F5C542",
            horizonHex = "#FFE08A",
            accentHex = "#F5C542",
            glowHex = "#7FB4FF",
            deepHex = "#060B24",
            particleStyle = ArenaParticleStyle.Stardust,
            hazards = new[] { ArenaHazard.Lightning, ArenaHazard.Frost },
            isPlaceholder = true,
        },
        new Seed
        {
            fileName = "DuatTemple",
            id = ArenaId.DuatTemple,
            displayName = "Duat Temple",
            pantheon = "Egyptian Realm",
            subtitle = "The sun chariot's final resting place",
            runeSpriteName = "RuneDuat",
            runeGlyph = "D",
            skyTopHex = "#7A1B0C",
            skyBottomHex = "#FF8C1A",
            horizonHex = "#FFD27A",
            accentHex = "#FFB347",
            glowHex = "#FF5A1F",
            deepHex = "#2A0805",
            particleStyle = ArenaParticleStyle.SandWisps,
            hazards = new[] { ArenaHazard.Flame, ArenaHazard.Sandstorm },
            isPlaceholder = true,
        },
        new Seed
        {
            fileName = "OlympusHeights",
            id = ArenaId.OlympusHeights,
            displayName = "Olympus Heights",
            pantheon = "Greek Realm",
            subtitle = "The sky throne of infinite storms",
            runeSpriteName = "RuneOlympus",
            runeGlyph = "O",
            skyTopHex = "#0E6E8C",
            skyBottomHex = "#EAF6FF",
            horizonHex = "#FFFFFF",
            accentHex = "#19D3F5",
            glowHex = "#9FF2FF",
            deepHex = "#062C3A",
            particleStyle = ArenaParticleStyle.CloudMist,
            hazards = new[] { ArenaHazard.Lightning, ArenaHazard.Whirlwind },
            isPlaceholder = true,
        },
    };

    [MenuItem("Elementals Fight/Arena Select/2 - Create Arena Assets")]
    public static void CreateArenaAssetsMenu()
    {
        CreateOrUpdate();
    }

    public static void CreateOrUpdate()
    {
        CharacterSelectUiFactory.EnsureFolder(ArenasFolder);

        var defs = new ArenaDefinition[Seeds.Length];
        int created = 0, updated = 0;

        for (int i = 0; i < Seeds.Length; i++)
        {
            defs[i] = CreateOrUpdateArena(Seeds[i], ref created, ref updated);
        }

        ArenaRoster roster = AssetDatabase.LoadAssetAtPath<ArenaRoster>(RosterAssetPath);
        bool rosterIsNew = roster == null;

        if (rosterIsNew)
        {
            roster = ScriptableObject.CreateInstance<ArenaRoster>();
            AssetDatabase.CreateAsset(roster, RosterAssetPath);
        }

        CharacterSelectUiFactory.SetSerializedArray(roster, "arenas", defs);
        EditorUtility.SetDirty(roster);

        AssetDatabase.SaveAssets();
        Debug.Log($"ArenaSelectRosterAssets: {created} created, {updated} updated, roster {(rosterIsNew ? "created" : "updated")} at {RosterAssetPath}.");
    }

    private static ArenaDefinition CreateOrUpdateArena(Seed seed, ref int created, ref int updated)
    {
        string assetPath = $"{ArenasFolder}/{seed.fileName}.asset";
        ArenaDefinition def = AssetDatabase.LoadAssetAtPath<ArenaDefinition>(assetPath);

        if (def == null)
        {
            def = ScriptableObject.CreateInstance<ArenaDefinition>();
            AssetDatabase.CreateAsset(def, assetPath);
            created++;
        }
        else
        {
            updated++;
        }

        CharacterSelectUiFactory.SetSerialized(def, "id", (int)seed.id);

        // Set isPlaceholder before any other field: CharacterSelectUiFactory.SetSerialized
        // applies each field through its own SerializedObject.ApplyModifiedProperties call,
        // which re-runs ArenaDefinition.OnValidate after every single field write below. Until
        // this is set, OnValidate sees isPlaceholder still at its default (false) and
        // environmentPrefab still unset, and spuriously logs "environmentPrefab is missing and
        // isPlaceholder is false" on every intermediate write for a placeholder arena.
        CharacterSelectUiFactory.SetSerialized(def, "isPlaceholder", seed.isPlaceholder);

        CharacterSelectUiFactory.SetSerialized(def, "displayName", seed.displayName);
        CharacterSelectUiFactory.SetSerialized(def, "pantheon", seed.pantheon);
        CharacterSelectUiFactory.SetSerialized(def, "subtitle", seed.subtitle);
        CharacterSelectUiFactory.SetSerialized(def, "runeSprite", ArenaSelectUiFactory.LoadSprite(seed.runeSpriteName));
        CharacterSelectUiFactory.SetSerialized(def, "runeGlyph", seed.runeGlyph);

        CharacterSelectUiFactory.SetSerialized(def, "skyTop", CharacterSelectUiFactory.HexColor(seed.skyTopHex));
        CharacterSelectUiFactory.SetSerialized(def, "skyBottom", CharacterSelectUiFactory.HexColor(seed.skyBottomHex));
        CharacterSelectUiFactory.SetSerialized(def, "horizon", CharacterSelectUiFactory.HexColor(seed.horizonHex));
        CharacterSelectUiFactory.SetSerialized(def, "accent", CharacterSelectUiFactory.HexColor(seed.accentHex));
        CharacterSelectUiFactory.SetSerialized(def, "glow", CharacterSelectUiFactory.HexColor(seed.glowHex));
        CharacterSelectUiFactory.SetSerialized(def, "deep", CharacterSelectUiFactory.HexColor(seed.deepHex));

        CharacterSelectUiFactory.SetSerialized(def, "particleStyle", (int)seed.particleStyle);

        var hazardValues = new int[seed.hazards.Length];
        for (int i = 0; i < seed.hazards.Length; i++)
        {
            hazardValues[i] = (int)seed.hazards[i];
        }
        SetSerializedIntArray(def, "hazards", hazardValues);

        // panoramaSprite / environmentPrefab intentionally left unset - the placeholder
        // seed arenas draw entirely from the palette colours per the design spec, and a
        // non-placeholder arena without an environmentPrefab is caught loudly by
        // ArenaDefinition.OnValidate. (isPlaceholder itself is set first, above.)


        EditorUtility.SetDirty(def);
        return def;
    }

    /// <summary>
    /// CharacterSelectUiFactory only exposes SetSerializedArray for Object-reference
    /// arrays (roster.characters, axisLabels, ...); ArenaDefinition.hazards is a plain
    /// ArenaHazard[] (int-backed enum), so it needs its own int-array setter here rather
    /// than adding an enum-specific overload to the shared factory for one caller.
    /// </summary>
    private static void SetSerializedIntArray(Object target, string field, int[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);

        if (prop == null)
        {
            Debug.LogError($"ArenaSelectRosterAssets: field '{field}' not found on {target.GetType().Name}.");
            return;
        }

        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).intValue = values[i];
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
