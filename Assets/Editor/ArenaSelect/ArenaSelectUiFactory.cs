using UnityEditor;
using UnityEngine;

// Thin sprite-loading wrapper for the ArenaSelect editor tools. Looks in this screen's
// own sprite folder first (runes, hazard icons, Band, Vignette), then falls back to the
// shared CharacterSelect sprites (Ring, Circle, RoundedRect, SoftCircle, GradientV,
// ...) so this screen never duplicates art CharacterSelect already generates. Every
// other editor helper (MakeRect/MakeImage/MakeText/MakeButton/SetSerialized/...) is
// reused directly from CharacterSelectUiFactory - this class only adds the lookup path.
public static class ArenaSelectUiFactory
{
    private const string SpritesFolder = "Assets/UI/ArenaSelect/Sprites";

    /// <summary>Loads a generated ArenaSelect sprite by bare name (no extension); falls back to the shared CharacterSelect sprite of the same name.</summary>
    public static Sprite LoadSprite(string name)
    {
        string path = $"{SpritesFolder}/{name}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite != null)
        {
            return sprite;
        }

        // CharacterSelectUiFactory.LoadSprite already logs its own error if the name
        // isn't found there either, so a genuinely missing sprite still surfaces loudly
        // exactly once instead of this method swallowing the miss silently.
        return CharacterSelectUiFactory.LoadSprite(name);
    }
}
