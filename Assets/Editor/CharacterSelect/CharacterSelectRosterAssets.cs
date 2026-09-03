using UnityEditor;
using UnityEngine;

// Creates/updates the CharacterDefinition assets + the CharacterRoster asset that ties
// them together in the FIXED order LoadCharacter.charPrefabs expects. Idempotent: re-running
// this after an artist tweaks colours/stats in the seed table below updates the existing
// assets in place (same GUID, so PlayerPrefs indices and any prefab references keep working)
// rather than duplicating them.
public static class CharacterSelectRosterAssets
{
    private const string CharactersFolder = "Assets/Data/Roster/Characters";
    private const string RosterAssetPath = "Assets/Data/Roster/CharacterRoster.asset";

    private const string DisplayModelsFolder = "Assets/Prefabs/Characters/DisplayModels";
    private const string PlayerPrefabsFolder = "Assets/Prefabs/Characters/PlayerPrefabs";

    private struct Seed
    {
        public string fileName;
        public CharacterId id;
        public string displayName;
        public string pantheon;
        public string domain;
        public string title;
        public string lore;
        public Playstyle playstyle;
        public Element element;
        public string primaryHex;
        public string secondaryHex;
        public string glowHex;
        public string deepHex;
        public int power, speed, range, defense, mobility;
        public bool isPlaceholder;
        public string displayModelName;
        public string playerPrefabName;
    }

    // Order here IS the roster order (see CONTRACT.md: index 0 EarthMage, 1 WarriorPrincess,
    // 2 Ninja, 3 Yemoja - must match FightScene's LoadCharacter.charPrefabs). CharacterId's
    // EarthMage/Ninja/WarriorPrincess members are appended by the runtime agent per the
    // contract; this script assumes they already exist.
    private static readonly Seed[] Seeds =
    {
        new Seed
        {
            fileName = "EarthMage",
            id = CharacterId.EarthMage,
            displayName = "Earth Mage",
            pantheon = "Placeholder",
            domain = "Stone Caller",
            title = "Keeper of the Red Soil",
            lore = "A stand-in fighter from the original build. Replace this entry with a real deity: name, lore, colours and stats all live on this asset.",
            playstyle = Playstyle.Zoner,
            element = Element.Earth,
            primaryHex = "#7CDB6A",
            secondaryHex = "#F5D76E",
            glowHex = "#3FA34D",
            deepHex = "#0B2A12",
            power = 6, speed = 4, range = 9, defense = 6, mobility = 3,
            isPlaceholder = true,
            displayModelName = "EarthMageDisplay",
            playerPrefabName = "EarthMage",
        },
        new Seed
        {
            fileName = "WarriorPrincess",
            id = CharacterId.WarriorPrincess,
            displayName = "Warrior Princess",
            pantheon = "Placeholder",
            domain = "Sun Blade",
            title = "Daughter of the Noon Sun",
            lore = "A stand-in fighter from the original build. Replace this entry with a real deity: name, lore, colours and stats all live on this asset.",
            playstyle = Playstyle.Bruiser,
            element = Element.Sun,
            primaryHex = "#FFB03B",
            secondaryHex = "#FF4D3D",
            glowHex = "#FF7A1A",
            deepHex = "#3D1A05",
            power = 9, speed = 5, range = 4, defense = 7, mobility = 5,
            isPlaceholder = true,
            displayModelName = "WarriorPrincessDisplay",
            playerPrefabName = "WarriorPrincess",
        },
        new Seed
        {
            fileName = "Ninja",
            id = CharacterId.Ninja,
            displayName = "Ninja",
            pantheon = "Placeholder",
            domain = "Shadow Step",
            title = "Walker Between Realms",
            lore = "A stand-in fighter from the original build. Replace this entry with a real deity: name, lore, colours and stats all live on this asset.",
            playstyle = Playstyle.Rushdown,
            element = Element.Spirit,
            primaryHex = "#C084FC",
            secondaryHex = "#E2E8F0",
            glowHex = "#A855F7",
            deepHex = "#1E0A3A",
            power = 6, speed = 9, range = 3, defense = 4, mobility = 9,
            isPlaceholder = true,
            displayModelName = "NinjasDisplay",
            playerPrefabName = "Ninja",
        },
        new Seed
        {
            fileName = "Yemoja",
            id = CharacterId.Yemoja,
            displayName = "Yemoja",
            pantheon = "Yoruba",
            domain = "Ocean Mother",
            title = "Mother of the Waters",
            lore = "Born where the river meets the sea, Yemoja carries the weight of every tide. Her trident calls the deep to rise; her shield turns the storm itself aside.",
            playstyle = Playstyle.Guardian,
            element = Element.Water,
            primaryHex = "#38E8FF",
            secondaryHex = "#F5F0FF",
            glowHex = "#22B8D8",
            deepHex = "#052A4A",
            power = 7, speed = 5, range = 6, defense = 9, mobility = 4,
            isPlaceholder = false,
            displayModelName = "YemojaDisplay",
            playerPrefabName = "Yemoja",
        },
    };

    [MenuItem("Elementals Fight/Character Select/2 - Create Roster Assets")]
    public static void CreateRosterAssetsMenu()
    {
        CreateOrUpdate();
    }

    public static void CreateOrUpdate()
    {
        CharacterSelectUiFactory.EnsureFolder(CharactersFolder);
        CharacterSelectUiFactory.EnsureFolder("Assets/Prefabs");
        CharacterSelectUiFactory.EnsureFolder(DisplayModelsFolder);
        CharacterSelectUiFactory.EnsureFolder(PlayerPrefabsFolder);

        var defs = new CharacterDefinition[Seeds.Length];
        int created = 0, updated = 0;

        for (int i = 0; i < Seeds.Length; i++)
        {
            defs[i] = CreateOrUpdateCharacter(Seeds[i], ref created, ref updated);
        }

        CharacterRoster roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>(RosterAssetPath);
        bool rosterIsNew = roster == null;

        if (rosterIsNew)
        {
            roster = ScriptableObject.CreateInstance<CharacterRoster>();
            AssetDatabase.CreateAsset(roster, RosterAssetPath);
        }

        CharacterSelectUiFactory.SetSerializedArray(roster, "characters", defs);
        EditorUtility.SetDirty(roster);

        AssetDatabase.SaveAssets();
        Debug.Log($"CharacterSelectRosterAssets: {created} created, {updated} updated, roster {(rosterIsNew ? "created" : "updated")} at {RosterAssetPath}.");
    }

    private static CharacterDefinition CreateOrUpdateCharacter(Seed seed, ref int created, ref int updated)
    {
        string assetPath = $"{CharactersFolder}/{seed.fileName}.asset";
        CharacterDefinition def = AssetDatabase.LoadAssetAtPath<CharacterDefinition>(assetPath);

        if (def == null)
        {
            def = ScriptableObject.CreateInstance<CharacterDefinition>();
            AssetDatabase.CreateAsset(def, assetPath);
            created++;
        }
        else
        {
            updated++;
        }

        CharacterSelectUiFactory.SetSerialized(def, "id", (int)seed.id);
        CharacterSelectUiFactory.SetSerialized(def, "displayName", seed.displayName);
        CharacterSelectUiFactory.SetSerialized(def, "pantheon", seed.pantheon);
        CharacterSelectUiFactory.SetSerialized(def, "domain", seed.domain);
        CharacterSelectUiFactory.SetSerialized(def, "title", seed.title);
        CharacterSelectUiFactory.SetSerialized(def, "lore", seed.lore);
        CharacterSelectUiFactory.SetSerialized(def, "playstyle", (int)seed.playstyle);
        CharacterSelectUiFactory.SetSerialized(def, "element", (int)seed.element);

        CharacterSelectUiFactory.SetSerialized(def, "primary", CharacterSelectUiFactory.HexColor(seed.primaryHex));
        CharacterSelectUiFactory.SetSerialized(def, "secondary", CharacterSelectUiFactory.HexColor(seed.secondaryHex));
        CharacterSelectUiFactory.SetSerialized(def, "glow", CharacterSelectUiFactory.HexColor(seed.glowHex));
        CharacterSelectUiFactory.SetSerialized(def, "deep", CharacterSelectUiFactory.HexColor(seed.deepHex));

        CharacterSelectUiFactory.SetSerialized(def, "power", seed.power);
        CharacterSelectUiFactory.SetSerialized(def, "speed", seed.speed);
        CharacterSelectUiFactory.SetSerialized(def, "range", seed.range);
        CharacterSelectUiFactory.SetSerialized(def, "defense", seed.defense);
        CharacterSelectUiFactory.SetSerialized(def, "mobility", seed.mobility);

        CharacterSelectUiFactory.SetSerialized(def, "isPlaceholder", seed.isPlaceholder);

        GameObject displayPrefab = LoadPrefabOrWarn($"{DisplayModelsFolder}/{seed.displayModelName}.prefab");
        GameObject playablePrefab = LoadPrefabOrWarn($"{PlayerPrefabsFolder}/{seed.playerPrefabName}.prefab");
        CharacterSelectUiFactory.SetSerialized(def, "displayPrefab", displayPrefab);
        CharacterSelectUiFactory.SetSerialized(def, "playablePrefab", playablePrefab);

        // Default the select-screen idle to the fight controller so display models
        // animate; only fill it when empty so a hand-picked controller survives re-runs.
        SerializedObject defSo = new SerializedObject(def);
        SerializedProperty displayAnimProp = defSo.FindProperty("displayAnimator");
        if (displayAnimProp != null && displayAnimProp.objectReferenceValue == null && playablePrefab != null && playablePrefab.TryGetComponent(out Animator playableAnimator))
        {
            CharacterSelectUiFactory.SetSerialized(def, "displayAnimator", playableAnimator.runtimeAnimatorController);
        }

        EditorUtility.SetDirty(def);
        return def;
    }

    private static GameObject LoadPrefabOrWarn(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            // Not fatal: placeholders are explicitly allowed to ship without prefabs
            // (CharacterDefinition.OnValidate only errors for non-placeholder entries),
            // and the prefab-building agent may simply not have run yet.
            Debug.LogWarning($"CharacterSelectRosterAssets: prefab not found at {path} (ok if it hasn't been built yet).");
        }
        return prefab;
    }
}
