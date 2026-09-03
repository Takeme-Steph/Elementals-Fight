using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// Small, dumb, reusable building blocks shared by every "1/2/3" editor script under
// Elementals Fight/Character Select. Kept free of any scene-specific knowledge (no
// character names, no hierarchy assumptions) so the scene builder stays the only
// place that has to reason about the actual CharacterSelect layout.
public static class CharacterSelectUiFactory
{
    private const string SpritesFolder = "Assets/UI/CharacterSelect/Sprites";

    // Cached so repeated MakeText calls (dozens per scene build) don't re-hit AssetDatabase.
    private static TMP_FontAsset cachedFont;

    // ---------------------------------------------------------------------
    // Folders / assets
    // ---------------------------------------------------------------------

    /// <summary>
    /// Walks "Assets/A/B/C" one segment at a time, creating any folder that doesn't
    /// exist yet. AssetDatabase.CreateFolder errors if the parent is missing, so a
    /// straight one-shot call would fail on a fresh checkout - this is what makes
    /// every generator idempotent and safe to run in a brand new project.
    /// </summary>
    public static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];

            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    /// <summary>Loads a generated sprite by bare name (no extension) from the shared sprites folder.</summary>
    public static Sprite LoadSprite(string name)
    {
        string path = $"{SpritesFolder}/{name}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (sprite == null)
        {
            // Almost always means step 1 hasn't been run yet - loud and specific beats a
            // silently blank Image three steps later.
            Debug.LogError($"CharacterSelectUiFactory: sprite '{name}' not found at {path}. Run 'Elementals Fight/Character Select/1 - Generate Sprites' first.");
        }

        return sprite;
    }

    /// <summary>Loads the raw Texture2D behind a generated sprite (particle materials need the texture, not the Sprite).</summary>
    public static Texture2D LoadSpriteTexture(string name)
    {
        string path = $"{SpritesFolder}/{name}.png";
        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        if (tex == null)
        {
            Debug.LogError($"CharacterSelectUiFactory: texture '{name}' not found at {path}. Run step 1 first.");
        }

        return tex;
    }

    /// <summary>
    /// Project's UI font. Falls back to TMP's own default so a fresh TMP Essentials
    /// import (no LiberationSans SDF path yet) still produces readable text.
    /// </summary>
    public static TMP_FontAsset LoadUiFont()
    {
        if (cachedFont != null)
        {
            return cachedFont;
        }

        cachedFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");

        if (cachedFont == null)
        {
            cachedFont = TMP_Settings.defaultFontAsset;
        }

        return cachedFont;
    }

    /// <summary>Parses a "#RRGGBB"/"#RRGGBBAA" string, optionally overriding alpha. Logs and returns magenta on a bad string so a typo is obvious, not invisible.</summary>
    public static Color HexColor(string hex, float alphaOverride = -1f)
    {
        if (!ColorUtility.TryParseHtmlString(hex, out Color color))
        {
            Debug.LogError($"CharacterSelectUiFactory: could not parse colour '{hex}'.");
            return Color.magenta;
        }

        if (alphaOverride >= 0f)
        {
            color.a = alphaOverride;
        }

        return color;
    }

    // ---------------------------------------------------------------------
    // Hierarchy builders
    // ---------------------------------------------------------------------

    /// <summary>Creates a bare RectTransform under `parent` (pass null for a scene root) with the given anchoring.</summary>
    public static RectTransform MakeRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);

        RectTransform rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;

        return rt;
    }

    /// <summary>Stretches an existing rect to fill its parent, then applies the given inset (offsetMin/offsetMax).</summary>
    public static void Stretch(RectTransform rt, Vector2 offsetMin, Vector2 offsetMax)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
    }

    /// <summary>Convenience: a full-bleed stretch rect with zero inset, ready for a decorative Image.</summary>
    public static RectTransform MakeStretchRect(Transform parent, string name)
    {
        RectTransform rt = MakeRect(parent, name, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        Stretch(rt, Vector2.zero, Vector2.zero);
        return rt;
    }

    public static Image MakeImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Sprite sprite, Color color, Image.Type type = Image.Type.Simple, bool raycastTarget = false)
    {
        RectTransform rt = MakeRect(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, size);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = type;
        img.raycastTarget = raycastTarget;
        return img;
    }

    /// <summary>Same as MakeImage but pre-stretched to fill the parent rect - the common case for decorative backgrounds/overlays.</summary>
    public static Image MakeImageStretch(Transform parent, string name, Sprite sprite, Color color, Image.Type type = Image.Type.Simple, bool raycastTarget = false)
    {
        RectTransform rt = MakeStretchRect(parent, name);
        Image img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = type;
        img.raycastTarget = raycastTarget;
        return img;
    }

    public static TextMeshProUGUI MakeText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, string text, float fontSize, Color color, TextAlignmentOptions alignment, bool bold = false, bool italic = false, float charSpacing = 0f, bool wrap = false)
    {
        RectTransform rt = MakeRect(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, size);
        TextMeshProUGUI tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = LoadUiFont();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.characterSpacing = charSpacing;
        tmp.raycastTarget = false;

        FontStyles style = FontStyles.Normal;
        if (bold)
        {
            style |= FontStyles.Bold;
        }
        if (italic)
        {
            style |= FontStyles.Italic;
        }
        tmp.fontStyle = style;

        // com.unity.ugui 2.5+ ships TMP with textWrappingMode replacing the old bool;
        // enableWordWrapping still exists but is deprecated and warns on every call.
        tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;

        return tmp;
    }

    public static Button MakeButton(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size, Sprite sprite, Color color)
    {
        // A 9-sliced sprite (RoundedRect carries a border) must be drawn Sliced or
        // its corners stretch; plain circles have no border and stay Simple.
        Image.Type type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        Image img = MakeImage(parent, name, anchorMin, anchorMax, pivot, anchoredPosition, size, sprite, color, type, true);
        Button button = img.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        return button;
    }

    // ---------------------------------------------------------------------
    // SerializedObject field setters
    //
    // Every runtime component in this scene exposes only private [SerializeField]
    // fields (repo convention: no public setters on MonoBehaviours), so this is the
    // only way to wire references from editor code. A typo in `field` makes
    // FindProperty return null and silently no-op the assignment - that failure mode
    // is exactly the kind of thing that looks fine in the editor and breaks at
    // runtime, so every setter below logs loudly instead of swallowing it.
    // ---------------------------------------------------------------------

    private static SerializedProperty FindOrLog(Object target, string field)
    {
        if (target == null)
        {
            Debug.LogError($"CharacterSelectUiFactory: cannot set field '{field}' - target object is null.");
            return null;
        }

        SerializedObject so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);

        if (prop == null)
        {
            Debug.LogError($"CharacterSelectUiFactory: field '{field}' not found on {target.GetType().Name}.");
        }

        return prop;
    }

    public static void SetSerialized(Object target, string field, Object value)
    {
        SerializedProperty prop = FindOrLog(target, field);
        if (prop == null) { return; }
        prop.objectReferenceValue = value;
        prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetSerialized(Object target, string field, float value)
    {
        SerializedProperty prop = FindOrLog(target, field);
        if (prop == null) { return; }
        prop.floatValue = value;
        prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetSerialized(Object target, string field, int value)
    {
        SerializedProperty prop = FindOrLog(target, field);
        if (prop == null) { return; }
        // Works for plain ints and for enum-backed properties alike (SerializedProperty
        // stores enums as their underlying int) - simpler and less error-prone than
        // juggling enumValueIndex against declaration order.
        prop.intValue = value;
        prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetSerialized(Object target, string field, string value)
    {
        SerializedProperty prop = FindOrLog(target, field);
        if (prop == null) { return; }
        prop.stringValue = value;
        prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetSerialized(Object target, string field, Color value)
    {
        SerializedProperty prop = FindOrLog(target, field);
        if (prop == null) { return; }
        prop.colorValue = value;
        prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetSerialized(Object target, string field, bool value)
    {
        SerializedProperty prop = FindOrLog(target, field);
        if (prop == null) { return; }
        prop.boolValue = value;
        prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void SetSerialized(Object target, string field, Vector2 value)
    {
        SerializedProperty prop = FindOrLog(target, field);
        if (prop == null) { return; }
        prop.vector2Value = value;
        prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Sets a serialized List&lt;T&gt;/T[] of object references (roster.characters, axisLabels, blobs, lines, ...).</summary>
    public static void SetSerializedArray(Object target, string field, Object[] values)
    {
        SerializedProperty prop = FindOrLog(target, field);
        if (prop == null) { return; }

        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
        prop.serializedObject.ApplyModifiedPropertiesWithoutUndo();
    }
}
