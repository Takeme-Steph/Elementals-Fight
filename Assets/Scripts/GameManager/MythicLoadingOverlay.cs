using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// A self-building, fullscreen transition between arena selection and FightScene.
/// It intentionally lives in code rather than a second scene: a loading scene would
/// add another asynchronous hop, while this canvas can remain visible until the real
/// fight scene is activated. The visual is palette-driven from ArenaDefinition so a
/// new mythology gets the same gateway treatment without a bespoke UI prefab.
/// </summary>
public sealed class MythicLoadingOverlay : MonoBehaviour
{
    private const float MinimumDisplaySeconds = 2.15f;
    private const float LorePeriod = 4.4f;
    private const int StardustCount = 34;
    // Unity documents 0.9 as the pre-activation ceiling, but some platform/player
    // combinations report a value a few ULPs below it. Waiting for an exact 0.9f can
    // therefore strand a perfectly-ready scene behind the loading overlay.
    private const float ReadyProgress = 0.89f;
    private const float ActivationSafetySeconds = 30f;

    // Keep the decoder ASCII-only. The project defaults dynamically-created TMP text
    // to Liberation Sans SDF, whose glyph atlas does not include the decorative rune
    // code points used by the initial prototype.
    private static readonly string[] Runes = { "//", "<>", "[]", "{}", "++", "==", "##", "^^", "**", "||", ">>", "~~" };
    private static readonly string[] LoreLines =
    {
        "Every culture tells tales of the world's forge—where thunder, drums, and distant stars answer one another.",
        "Sacred rivers, cloud paths, and world trees all mark the threshold between mortal ground and myth.",
        "Legendary warriors cross the same boundary in every tradition: the place where courage becomes story.",
        "Different pantheons name the horizon differently; every one imagines a road between worlds."
    };

    // CharacterSelect owns the CharacterRoster today, while ArenaSelect deliberately
    // owns only ArenaRoster. Cache the already-confirmed definitions at that boundary
    // instead of duplicating roster wiring just for this transition.
    private static FighterStyle cachedPlayer;
    private static FighterStyle cachedOpponent;
    private static bool hasCachedMatchup;

    private readonly List<RectTransform> stars = new();
    private readonly List<float> starSpeeds = new();

    private ArenaDefinition arena;
    private Color accent;
    private Color glow;
    private Image background;
    private Image horizon;
    private RectTransform panorama;
    private Image flash;
    private TMP_Text progressText;
    private TMP_Text loreText;
    private TMP_Text runeLine;
    private RectTransform beamFill;
    private RectTransform beamTip;
    private CanvasGroup rootGroup;
    private Sprite circleSprite;
    private float displayedProgress;
    private float nextRuneShuffle;
    private float nextLoreSwap;
    private int loreIndex;

    /// <summary>Creates the gateway and begins loading the supplied scene immediately.</summary>
    public static void Begin(string sceneName, ArenaDefinition selectedArena, int playerIndex, int opponentIndex)
    {
        GameObject host = new GameObject("MythicLoadingOverlay");
        DontDestroyOnLoad(host);
        MythicLoadingOverlay overlay = host.AddComponent<MythicLoadingOverlay>();
        overlay.arena = selectedArena;
        overlay.StartCoroutine(overlay.LoadRoutine(sceneName, playerIndex, opponentIndex));
    }

    /// <summary>Called when the second fighter is confirmed, before moving to ArenaSelect.</summary>
    public static void CacheMatchup(CharacterDefinition player, CharacterDefinition opponent)
    {
        if (player == null || opponent == null)
        {
            hasCachedMatchup = false;
            return;
        }

        cachedPlayer = FighterStyle.FromDefinition(player);
        cachedOpponent = FighterStyle.FromDefinition(opponent);
        hasCachedMatchup = true;
    }

    private IEnumerator LoadRoutine(string sceneName, int playerIndex, int opponentIndex)
    {
        Build(playerIndex, opponentIndex);
        yield return null; // Lets the Canvas render the warp's first frame before loading work begins.

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        if (operation == null)
        {
            Debug.LogError($"MythicLoadingOverlay: could not start asynchronous load for '{sceneName}'.");
            Destroy(gameObject);
            yield break;
        }

        operation.allowSceneActivation = false;
        float shownFor = 0f;
        while (operation.progress < ReadyProgress || shownFor < MinimumDisplaySeconds)
        {
            shownFor += Time.unscaledDeltaTime;
            float target = operation.progress < ReadyProgress
                ? Mathf.Clamp01(operation.progress / ReadyProgress) * 0.92f
                : Mathf.Min(0.96f, shownFor / MinimumDisplaySeconds * 0.96f);
            displayedProgress = Mathf.MoveTowards(displayedProgress, target, Time.unscaledDeltaTime * 0.68f);

            if (shownFor >= ActivationSafetySeconds && operation.progress < ReadyProgress)
            {
                // Do not keep an input-blocking overlay alive forever if a platform
                // reports a non-standard async progress value. Activation is still
                // safe: Unity will complete it when its outstanding work is ready.
                Debug.LogWarning($"MythicLoadingOverlay: '{sceneName}' reported only {operation.progress:0.000} progress after {ActivationSafetySeconds:0} seconds; requesting activation.");
                break;
            }
            yield return null;
        }

        while (displayedProgress < 1f)
        {
            displayedProgress = Mathf.MoveTowards(displayedProgress, 1f, Time.unscaledDeltaTime * 1.8f);
            yield return null;
        }

        operation.allowSceneActivation = true;
        while (!operation.isDone)
        {
            yield return null;
        }

        // The canvas is DontDestroyOnLoad so it can cover the scene activation frame.
        // It must be removed immediately afterwards; otherwise FightScene is running
        // successfully behind this fullscreen, input-blocking Canvas forever.
        Destroy(gameObject);
    }

    private void Build(int playerIndex, int opponentIndex)
    {
        accent = arena != null ? arena.Accent : new Color(0.18f, 0.83f, 0.9f);
        glow = arena != null ? arena.Glow : new Color(0.38f, 0.95f, 1f);
        circleSprite = CreateCircleSprite();

        Canvas canvas = CreateUi<Canvas>("GatewayCanvas", transform);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        rootGroup = canvas.gameObject.AddComponent<CanvasGroup>();

        Color deep = arena != null ? arena.Deep : new Color(0.015f, 0.07f, 0.11f);
        background = CreateImage("DeepPanorama", canvas.transform, deep);
        Stretch(background.rectTransform);

        panorama = CreateRect("PanoramaDrift", canvas.transform);
        Stretch(panorama);
        Image sky = CreateImage("SkyWash", panorama, arena != null ? arena.SkyTop : accent);
        Stretch(sky.rectTransform);
        sky.color = WithAlpha(sky.color, 0.40f);
        if (arena != null && arena.PanoramaSprite != null)
        {
            // Arena key art is optional during the current placeholder-arena phase.
            // When supplied later it sits beneath the atmosphere and UI, preserving
            // this screen's cinematic stage-preview composition without new layout.
            Image keyArt = CreateImage("ArenaKeyArt", panorama, Color.white);
            keyArt.sprite = arena.PanoramaSprite;
            keyArt.preserveAspect = false;
            keyArt.color = WithAlpha(Color.white, 0.62f);
            Stretch(keyArt.rectTransform);
        }
        horizon = CreateImage("HorizonGlow", panorama, arena != null ? arena.Horizon : glow);
        SetAnchors(horizon.rectTransform, new Vector2(0f, 0.29f), new Vector2(1f, 0.67f));
        horizon.color = WithAlpha(horizon.color, 0.14f);
        CreateStagePillars(panorama, deep);
        CreateSilhouetteBand(panorama, 0.16f, 0.27f, WithAlpha(deep, 0.82f));
        CreateSilhouetteBand(panorama, 0.06f, 0.18f, WithAlpha(deep, 0.95f));

        for (int i = 0; i < StardustCount; i++)
        {
            Image star = CreateImage("Stardust", panorama, Color.Lerp(glow, Color.white, 0.45f));
            RectTransform rect = star.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(Random.value, Random.value);
            rect.sizeDelta = Vector2.one * Random.Range(3f, 10f);
            star.color = WithAlpha(star.color, Random.Range(0.22f, 0.78f));
            stars.Add(rect);
            starSpeeds.Add(Random.Range(6f, 23f));
        }

        CreateTopAnchors(canvas.transform);
        CreateMedals(canvas.transform, playerIndex, opponentIndex);
        CreateLowerDecoder(canvas.transform);
        CreateWarpFlash(canvas.transform);
        ShuffleRunes();
        loreText.text = FormatLore(LoreLines[0]);
        nextLoreSwap = Time.unscaledTime + LorePeriod;
        StartCoroutine(WarpIn());
    }

    private void CreateTopAnchors(Transform parent)
    {
        TMP_Text sync = CreateText("SyncTag", parent, "[ SYNCHRONIZING BOUNDARIES ]", 22, FontStyles.Bold, Color.white);
        SetAnchors(sync.rectTransform, new Vector2(0.035f, 0.91f), new Vector2(0.4f, 0.97f));
        sync.alignment = TextAlignmentOptions.Left;
        TMP_Text stage = CreateText("StageTag", parent, $"[ STAGE: {(arena != null ? arena.DisplayName : "MYTHIC GATEWAY").ToUpperInvariant()} ]", 22, FontStyles.Bold, Color.white);
        SetAnchors(stage.rectTransform, new Vector2(0.60f, 0.91f), new Vector2(0.965f, 0.97f));
        stage.alignment = TextAlignmentOptions.Right;
    }

    private void CreateMedals(Transform parent, int playerIndex, int opponentIndex)
    {
        FighterStyle p1 = hasCachedMatchup ? cachedPlayer : GetFallbackFighterStyle(playerIndex);
        FighterStyle p2 = hasCachedMatchup ? cachedOpponent : GetFallbackFighterStyle(opponentIndex);
        CreateMedal(parent, new Vector2(0.385f, 0.55f), p1, false);
        CreateMedal(parent, new Vector2(0.615f, 0.55f), p2, true);

        TMP_Text versus = CreateText("Versus", parent, "( vs )", 32, FontStyles.Bold, Color.white);
        SetAnchors(versus.rectTransform, new Vector2(0.465f, 0.50f), new Vector2(0.535f, 0.60f));
        versus.alignment = TextAlignmentOptions.Center;
        versus.characterSpacing = 5f;
    }

    private void CreateMedal(Transform parent, Vector2 anchor, FighterStyle fighter, bool right)
    {
        Image halo = CreateImage("ElementHalo", parent, WithAlpha(fighter.glow, 0.22f));
        halo.sprite = circleSprite;
        halo.rectTransform.anchorMin = halo.rectTransform.anchorMax = anchor;
        halo.rectTransform.sizeDelta = new Vector2(250f, 250f);
        Image frame = CreateImage("ProfileFrame", halo.transform, new Color(0.015f, 0.04f, 0.07f, 0.90f));
        frame.sprite = circleSprite;
        Stretch(frame.rectTransform, new Vector2(23f, 23f));
        Outline outline = frame.gameObject.AddComponent<Outline>();
        outline.effectColor = fighter.primary;
        outline.effectDistance = new Vector2(5f, -5f);
        TMP_Text initial = CreateText("Monogram", frame.transform, fighter.initial, 82, FontStyles.Bold, fighter.glow);
        Stretch(initial.rectTransform);
        initial.alignment = TextAlignmentOptions.Center;
        TMP_Text label = CreateText("FighterName", parent, $"{(right ? "P2" : "P1")}: {fighter.name.ToUpperInvariant()}", 25, FontStyles.Bold, Color.white);
        float xMin = right ? 0.54f : 0.26f;
        float xMax = right ? 0.80f : 0.46f;
        SetAnchors(label.rectTransform, new Vector2(xMin, 0.34f), new Vector2(xMax, 0.39f));
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.Lerp(new Color(1f, 0.82f, 0.42f), fighter.glow, 0.22f);
    }

    private void CreateLowerDecoder(Transform parent)
    {
        Image ticker = CreateImage("LoreTicker", parent, new Color(0.01f, 0.025f, 0.05f, 0.72f));
        SetAnchors(ticker.rectTransform, new Vector2(0.16f, 0.175f), new Vector2(0.84f, 0.245f));
        loreText = CreateText("LoreText", ticker.transform, string.Empty, 20, FontStyles.Normal, new Color(0.89f, 0.95f, 1f));
        Stretch(loreText.rectTransform, new Vector2(26f, 10f));
        loreText.alignment = TextAlignmentOptions.Center;
        loreText.textWrappingMode = TextWrappingModes.NoWrap;

        runeLine = CreateText("RuneDecoder", parent, string.Empty, 29, FontStyles.Bold, WithAlpha(glow, 0.72f));
        SetAnchors(runeLine.rectTransform, new Vector2(0.13f, 0.074f), new Vector2(0.81f, 0.11f));
        runeLine.alignment = TextAlignmentOptions.Center;
        runeLine.characterSpacing = 15f;

        Image beamBack = CreateImage("LoadingBeamBack", parent, new Color(0.52f, 0.78f, 0.9f, 0.18f));
        SetAnchors(beamBack.rectTransform, new Vector2(0.10f, 0.055f), new Vector2(0.90f, 0.072f));
        beamFill = CreateRect("LoadingBeam", beamBack.transform);
        beamFill.anchorMin = Vector2.zero;
        beamFill.anchorMax = new Vector2(0f, 1f);
        beamFill.offsetMin = beamFill.offsetMax = Vector2.zero;
        Image fillImage = beamFill.gameObject.AddComponent<Image>();
        fillImage.color = accent;
        beamTip = CreateRect("EmissionTip", beamBack.transform);
        beamTip.anchorMin = beamTip.anchorMax = new Vector2(0f, 0.5f);
        beamTip.sizeDelta = new Vector2(22f, 30f);
        beamTip.gameObject.AddComponent<Image>().color = Color.white;
        TMP_Text decoderStatus = CreateText("DecoderStatus", parent, "[ DECODING RUNES ]", 17, FontStyles.Bold, new Color(0.84f, 0.93f, 1f));
        SetAnchors(decoderStatus.rectTransform, new Vector2(0.10f, 0.018f), new Vector2(0.43f, 0.048f));
        decoderStatus.alignment = TextAlignmentOptions.Left;
        progressText = CreateText("Progress", parent, "0%", 22, FontStyles.Bold, new Color(0.84f, 0.93f, 1f));
        SetAnchors(progressText.rectTransform, new Vector2(0.78f, 0.018f), new Vector2(0.90f, 0.048f));
        progressText.alignment = TextAlignmentOptions.Right;
    }

    private void CreateWarpFlash(Transform parent)
    {
        flash = CreateImage("RealmWarp", parent, accent);
        Stretch(flash.rectTransform);
        flash.color = WithAlpha(Color.Lerp(accent, Color.white, 0.45f), 0f);
    }

    private IEnumerator WarpIn()
    {
        float time = 0f;
        while (time < 0.42f)
        {
            time += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(time / 0.42f);
            flash.color = WithAlpha(flash.color, (1f - p) * (1f - p) * 0.86f);
            yield return null;
        }
        flash.color = WithAlpha(flash.color, 0f);
    }

    private void Update()
    {
        if (panorama == null)
        {
            return;
        }

        float t = Time.unscaledTime;
        panorama.anchoredPosition = new Vector2(Mathf.Sin(t * 0.16f) * 22f, Mathf.Cos(t * 0.11f) * 8f);
        panorama.localScale = Vector3.one * (1.04f + Mathf.Sin(t * 0.13f) * 0.018f);
        for (int i = 0; i < stars.Count; i++)
        {
            RectTransform star = stars[i];
            Vector2 position = star.anchoredPosition;
            position.x -= starSpeeds[i] * Time.unscaledDeltaTime;
            if (position.x < -32f)
            {
                position.x = 32f;
            }
            star.anchoredPosition = position;
        }

        if (beamFill != null)
        {
            beamFill.anchorMax = new Vector2(displayedProgress, 1f);
            beamTip.anchorMin = beamTip.anchorMax = new Vector2(displayedProgress, 0.5f);
            beamTip.localScale = Vector3.one * (1f + Mathf.PingPong(t * (4f + displayedProgress * 16f), 0.55f));
            progressText.text = $"{Mathf.RoundToInt(displayedProgress * 100f)}%";
        }

        if (Time.unscaledTime >= nextRuneShuffle)
        {
            ShuffleRunes();
            nextRuneShuffle = Time.unscaledTime + Mathf.Lerp(0.22f, 0.055f, displayedProgress);
        }
        if (Time.unscaledTime >= nextLoreSwap)
        {
            loreIndex = (loreIndex + 1) % LoreLines.Length;
            loreText.text = FormatLore(LoreLines[loreIndex]);
            nextLoreSwap = Time.unscaledTime + LorePeriod;
        }
    }

    private void ShuffleRunes()
    {
        if (runeLine == null) return;
        string decoded = string.Empty;
        for (int i = 0; i < 13; i++) decoded += Runes[Random.Range(0, Runes.Length)] + "  ";
        runeLine.text = decoded;
    }

    private static void CreateSilhouetteBand(Transform parent, float minY, float maxY, Color color)
    {
        Image band = CreateImage("RiverbankSilhouette", parent, color);
        SetAnchors(band.rectTransform, new Vector2(0f, minY), new Vector2(1f, maxY));
    }

    private static void CreateStagePillars(Transform parent, Color deep)
    {
        // Four distant vertical forms echo the gateway columns in the visual target
        // without tying the neutral screen to one culture or requiring final key art.
        float[] xPositions = { 0.12f, 0.23f, 0.77f, 0.88f };
        for (int i = 0; i < xPositions.Length; i++)
        {
            Image pillar = CreateImage("DistantPillar", parent, WithAlpha(deep, 0.60f));
            float height = i % 2 == 0 ? 0.42f : 0.31f;
            SetAnchors(pillar.rectTransform, new Vector2(xPositions[i] - 0.018f, 0.19f), new Vector2(xPositions[i] + 0.018f, 0.19f + height));
            Outline rim = pillar.gameObject.AddComponent<Outline>();
            rim.effectColor = WithAlpha(Color.Lerp(deep, Color.white, 0.18f), 0.35f);
            rim.effectDistance = new Vector2(2f, 0f);
        }
    }

    private static string FormatLore(string lore) => $"[ LORE: \"{lore}\" ]";

    private static FighterStyle GetFallbackFighterStyle(int index)
    {
        // Fallback for opening ArenaSelect outside the normal CharacterSelect flow.
        // The normal path uses the cached CharacterDefinition data above.
        switch (index)
        {
            case 0: return new FighterStyle("Earth Mage", "E", new Color(0.40f, 0.92f, 0.56f), new Color(0.72f, 1f, 0.80f));
            case 1: return new FighterStyle("Ninja", "N", new Color(0.70f, 0.36f, 1f), new Color(0.89f, 0.71f, 1f));
            case 2: return new FighterStyle("Warrior Princess", "W", new Color(1f, 0.43f, 0.24f), new Color(1f, 0.78f, 0.42f));
            case 3: return new FighterStyle("Yemoja", "Y", new Color(0.18f, 0.72f, 1f), new Color(0.80f, 0.94f, 1f));
            default: return new FighterStyle("Challenger", "?", new Color(0.95f, 0.70f, 0.2f), new Color(1f, 0.90f, 0.54f));
        }
    }

    private readonly struct FighterStyle
    {
        public readonly string name;
        public readonly string initial;
        public readonly Color primary;
        public readonly Color glow;
        public FighterStyle(string name, string initial, Color primary, Color glow) { this.name = name; this.initial = initial; this.primary = primary; this.glow = glow; }

        public static FighterStyle FromDefinition(CharacterDefinition definition)
        {
            string displayName = string.IsNullOrWhiteSpace(definition.DisplayName) ? "Challenger" : definition.DisplayName;
            string initial = displayName.Substring(0, 1).ToUpperInvariant();
            return new FighterStyle(displayName, initial, definition.Primary, definition.Glow);
        }
    }

    private static Sprite CreateCircleSprite()
    {
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "RuntimeGatewayCircle";
        texture.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        float radius = size * 0.5f - 1f;
        Vector2 centre = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float edge = radius - Vector2.Distance(new Vector2(x, y), centre);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(edge + 1f));
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
    }

    private void OnDestroy()
    {
        if (circleSprite != null)
        {
            Destroy(circleSprite.texture);
            Destroy(circleSprite);
        }
    }

    private static T CreateUi<T>(string name, Transform parent) where T : Component
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.AddComponent<T>();
    }
    private static RectTransform CreateRect(string name, Transform parent)
    {
        // RectTransform is created automatically with a UI GameObject; AddComponent on
        // it would be invalid because Unity permits only one Transform per object.
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }
    private static Image CreateImage(string name, Transform parent, Color color) { Image image = CreateUi<Image>(name, parent); image.color = color; image.raycastTarget = true; return image; }
    private static TMP_Text CreateText(string name, Transform parent, string text, float size, FontStyles style, Color color) { TextMeshProUGUI label = CreateUi<TextMeshProUGUI>(name, parent); label.text = text; label.fontSize = size; label.fontStyle = style; label.color = color; label.raycastTarget = false; return label; }
    private static void Stretch(RectTransform rect, Vector2 inset = default) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = inset; rect.offsetMax = -inset; }
    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    private static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
}
