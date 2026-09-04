using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Builds the ArenaSelect scene from scratch (creating Assets/Scenes/ArenaSelect.unity
// if it doesn't exist yet, opening it if it does): camera, Backdrop (panorama),
// Particles, UI (header/hazard strip/dock/back/confirm/warp), EventSystem, then wires
// every runtime component's private serialized fields via CharacterSelectUiFactory.
// Idempotent - deletes its own previously generated roots before rebuilding, so
// re-running after a runtime script change is always safe. Mirrors
// CharacterSelectSceneBuilder.cs's structure and conventions.
//
// All colours/sizes that show up more than once live in the constants block below so
// a designer can retune the look without hunting through hierarchy code.
public static class ArenaSelectSceneBuilder
{
    // ---------------------------------------------------------------------
    // Constants
    // ---------------------------------------------------------------------

    private const string ScenePath = "Assets/Scenes/ArenaSelect.unity";
    private const string RosterAssetPath = "Assets/Data/Arenas/ArenaRoster.asset";
    private const string SparkMaterialPath = "Assets/UI/CharacterSelect/Materials/Spark.mat";

    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    private static readonly Color Gold = CharacterSelectUiFactory.HexColor("#F5D76E");
    private static readonly Color Glass = CharacterSelectUiFactory.HexColor("#0A0F1A", 0.55f);
    private static readonly Color BaseColor = CharacterSelectUiFactory.HexColor("#030712");

    // Reused anchor points.
    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    private static readonly Vector2 TopRight = new Vector2(1f, 1f);
    private static readonly Vector2 BottomCenter = new Vector2(0.5f, 0f);
    private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);
    private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
    private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);

    // Header (reference 1920x1080, measured from true screen top per the design spec).
    private const float HeaderTitleY = -80f;
    private const float HeaderPantheonY = -40f;
    private const float HeaderSubtitleY = -156f;

    // Readability bands: full-width, flush to the top/bottom edges, tinted deep - keep
    // header text and the dock/buttons legible over a pale sky (e.g. Olympus's near-
    // white top, where cyan header text was otherwise unreadable).
    private const float BandTopHeight = 260f;
    private const float BandBottomHeight = 320f;
    private const float BandTopAlpha = 0.55f;
    private const float BandBottomAlpha = 0.65f;
    private const float BandWidth = 2500f; // matches bandFar/bandNear's overscan width

    // Hazard strip: top-right, 48px safe-area margin, 3 stacked chips.
    private const float HazardMargin = 48f;
    private const float HazardChipHeight = 54f;
    private const float HazardChipGap = 12f;

    // Dock: centred at y 120 from the bottom, 300x110 tabs, 24px gaps.
    private const float DockY = 120f;
    private const float TabWidth = 300f;
    private const float TabHeight = 110f;
    private const float TabGap = 24f;

    // Back: bottom-left, 88x88, at (72, 96).
    private static readonly Vector2 BackButtonPos = new Vector2(72f, 96f);
    private const float BackButtonSize = 88f;

    // Confirm: bottom-right, 460x120, at (-72, 96).
    private static readonly Vector2 ConfirmButtonPos = new Vector2(-72f, 96f);
    private static readonly Vector2 ConfirmButtonSize = new Vector2(460f, 120f);

    // ---------------------------------------------------------------------
    // Menu items
    // ---------------------------------------------------------------------

    [MenuItem("Elementals Fight/Arena Select/3 - Build Scene")]
    public static void BuildSceneMenu()
    {
        BuildScene();
    }

    [MenuItem("Elementals Fight/Arena Select/Run All (1-3)")]
    public static void RunAll()
    {
        ArenaSelectArtGenerator.GenerateAll();
        AssetDatabase.Refresh();

        ArenaSelectRosterAssets.CreateOrUpdate();
        AssetDatabase.Refresh();

        BuildScene();
    }

    // ---------------------------------------------------------------------
    // Top-level build
    // ---------------------------------------------------------------------

    public static void BuildScene()
    {
        Scene scene = OpenOrCreateScene();
        if (!scene.IsValid())
        {
            Debug.LogError($"ArenaSelectSceneBuilder: could not open or create the scene at {ScenePath}.");
            return;
        }

        DeleteGeneratedRoots(scene);
        RemoveDefaultDirectionalLight(scene);

        Camera cam = SetupCamera();
        if (cam == null)
        {
            return;
        }

        EnsureEventSystem();

        ArenaRoster roster = AssetDatabase.LoadAssetAtPath<ArenaRoster>(RosterAssetPath);
        if (roster == null)
        {
            Debug.LogError($"ArenaSelectSceneBuilder: roster asset not found at {RosterAssetPath} - run 'Elementals Fight/Arena Select/2 - Create Arena Assets' first.");
        }

        ArenaPanorama panorama = BuildBackdrop(cam);
        ArenaParticles particles = BuildParticlesAndLightning(panorama.transform);
        BuildUi(roster, panorama, particles);

        EnsureBuildSettingsEntry();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);

        Debug.Log("ArenaSelectSceneBuilder: scene built - roots: Backdrop, Particles, UI.");
    }

    private static Scene OpenOrCreateScene()
    {
        if (File.Exists(ScenePath))
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        CharacterSelectUiFactory.EnsureFolder("Assets/Scenes");
        return EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
    }

    private static void DeleteGeneratedRoots(Scene scene)
    {
        string[] namesToDelete = { "Backdrop", "Particles", "UI" };
        GameObject[] roots = scene.GetRootGameObjects();

        foreach (string name in namesToDelete)
        {
            foreach (GameObject go in roots)
            {
                if (go != null && go.name == name)
                {
                    Undo.DestroyObjectImmediate(go);
                }
            }
        }
    }

    // NewSceneSetup.DefaultGameObjects (used when this scene doesn't exist yet) seeds a
    // Directional Light; nothing on this screen is lit (it's all Screen Space UI), so it
    // just sits there unused. Strip it every rebuild rather than only on first creation,
    // in case an earlier build (or a designer poking at the scene) left one behind.
    private static void RemoveDefaultDirectionalLight(Scene scene)
    {
        GameObject[] roots = scene.GetRootGameObjects();
        foreach (GameObject go in roots)
        {
            if (go != null && go.name == "Directional Light" && go.TryGetComponent<Light>(out _))
            {
                Undo.DestroyObjectImmediate(go);
            }
        }
    }

    private static Camera SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camGo = GameObject.Find("Main Camera");
            if (camGo != null)
            {
                camGo.TryGetComponent(out cam);
            }
        }

        if (cam == null)
        {
            // NewSceneSetup.DefaultGameObjects normally provides this; only reachable
            // if the opened scene had its camera removed by hand.
            GameObject go = new GameObject("Main Camera");
            Undo.RegisterCreatedObjectUndo(go, "Create Main Camera");
            go.tag = "MainCamera";
            cam = go.AddComponent<Camera>();
        }

        // Nothing 3D on this screen - orthographic keeps the camera setup trivial, the
        // whole screen is UI drawn on Screen Space canvases.
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.transform.rotation = Quaternion.identity;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BaseColor;

        return cam;
    }

    private static void EnsureEventSystem()
    {
        EventSystem es = Object.FindAnyObjectByType<EventSystem>();
        GameObject go;

        if (es == null)
        {
            go = new GameObject("EventSystem");
            Undo.RegisterCreatedObjectUndo(go, "Create EventSystem");
            go.AddComponent<EventSystem>();
        }
        else
        {
            go = es.gameObject;
        }

        // Unity auto-attaches StandaloneInputModule to any EventSystem it creates,
        // regardless of the project's Active Input Handling setting. This project runs
        // Input System Package (New) only, where UnityEngine.Input (which
        // StandaloneInputModule polls through) throws instead of returning stale
        // values - the same failure CharacterSelect.unity shipped with until it was
        // fixed by hand (see TASKS.md). Building this scene fresh, make sure it never
        // ships with that stale module in the first place.
        if (go.TryGetComponent(out StandaloneInputModule standalone))
        {
            Object.DestroyImmediate(standalone);
        }

        if (!go.TryGetComponent<InputSystemUIInputModule>(out _))
        {
            go.AddComponent<InputSystemUIInputModule>();
        }
    }

    private static GameObject CreateRoot(string name)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    // ---------------------------------------------------------------------
    // Backdrop / ArenaPanorama
    // ---------------------------------------------------------------------

    private struct LayerRefs
    {
        public RectTransform root;
        public CanvasGroup group;
        public Image sky;
        public Image skyBottomFill;
        public Image horizonGlow;
        public Image bandFar;
        public Image bandNear;
        public Image bandTop;
        public Image bandBottom;
        public Image vignette;
    }

    private static ArenaPanorama BuildBackdrop(Camera cam)
    {
        GameObject root = CreateRoot("Backdrop");
        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 30f;
        canvas.sortingOrder = -10;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        // Deliberately no GraphicRaycaster - purely decorative, must never eat a click
        // meant for the UI canvas rendered above it.

        ArenaPanorama panorama = root.AddComponent<ArenaPanorama>();

        LayerRefs layerA = BuildPanoramaLayer(root.transform, "SetA");
        LayerRefs layerB = BuildPanoramaLayer(root.transform, "SetB");

        WireLayer(panorama, "layerA", layerA);
        WireLayer(panorama, "layerB", layerB);

        return panorama;
    }

    private static LayerRefs BuildPanoramaLayer(Transform parent, string name)
    {
        // Full-screen stretch rect with its own nested Canvas: ArenaPanorama scales/pans
        // this root's Transform every frame for the idle drift, and a nested Canvas
        // keeps that per-frame rebuild scoped to this one layer instead of the whole
        // Backdrop canvas - same trick as CharacterSelect's AmbientBackdrop "Blobs".
        RectTransform root = CharacterSelectUiFactory.MakeStretchRect(parent, name);
        GameObject go = root.gameObject;
        go.AddComponent<Canvas>();
        CanvasGroup group = go.AddComponent<CanvasGroup>();

        Image skyBottomFill = CharacterSelectUiFactory.MakeImageStretch(root, "SkyBottomFill", null, Color.white);
        Image sky = CharacterSelectUiFactory.MakeImageStretch(root, "Sky", CharacterSelectUiFactory.LoadSprite("GradientV"), Color.white);
        // GradientV is opaque at the sprite's bottom edge and transparent at its top (see
        // CharacterSelectArtGenerator.PixelGradientV). Unflipped, that puts skyTop's tint
        // at the BOTTOM of the screen and lets skyBottomFill show through at the top -
        // backwards from "skyTop reads clearly in the top third, skyBottom at the
        // bottom". Flipping vertically moves the opaque (skyTop) end to the top.
        sky.rectTransform.localScale = new Vector3(1f, -1f, 1f);

        Image horizonGlow = CharacterSelectUiFactory.MakeImage(root, "HorizonGlow", BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, -200f), new Vector2(1600f, 700f), CharacterSelectUiFactory.LoadSprite("SoftCircle"), new Color(1f, 1f, 1f, 0.7f));

        Image bandFar = CharacterSelectUiFactory.MakeImage(root, "BandFar", BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, -60f), new Vector2(2500f, 420f), ArenaSelectUiFactory.LoadSprite("Band"), new Color(1f, 1f, 1f, 0.85f));
        Image bandNear = CharacterSelectUiFactory.MakeImage(root, "BandNear", BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, -120f), new Vector2(2500f, 300f), ArenaSelectUiFactory.LoadSprite("Band"), new Color(1f, 1f, 1f, 0.95f));

        // Readability bands, full-width, flush to the top/bottom edges - see the
        // BandTopHeight/BandBottomHeight comment above for why these exist.
        Image bandTop = CharacterSelectUiFactory.MakeImage(root, "BandTop", TopCenter, TopCenter, TopCenter, Vector2.zero, new Vector2(BandWidth, BandTopHeight), CharacterSelectUiFactory.LoadSprite("GradientV"), new Color(1f, 1f, 1f, BandTopAlpha));
        // Same flip as Sky above: opaque end moves to the screen's top edge, fading to
        // transparent going down into the frame.
        bandTop.rectTransform.localScale = new Vector3(1f, -1f, 1f);

        Image bandBottom = CharacterSelectUiFactory.MakeImage(root, "BandBottom", BottomCenter, BottomCenter, BottomCenter, Vector2.zero, new Vector2(BandWidth, BandBottomHeight), CharacterSelectUiFactory.LoadSprite("GradientV"), new Color(1f, 1f, 1f, BandBottomAlpha));
        // Unflipped: GradientV's naturally-opaque bottom edge already lands on the
        // screen's bottom edge here, fading to transparent going up - exactly what this
        // band needs, no flip required.

        Image vignette = CharacterSelectUiFactory.MakeImageStretch(root, "Vignette", ArenaSelectUiFactory.LoadSprite("Vignette"), new Color(1f, 1f, 1f, 0.6f));

        return new LayerRefs
        {
            root = root,
            group = group,
            sky = sky,
            skyBottomFill = skyBottomFill,
            horizonGlow = horizonGlow,
            bandFar = bandFar,
            bandNear = bandNear,
            bandTop = bandTop,
            bandBottom = bandBottom,
            vignette = vignette,
        };
    }

    private static void WireLayer(Object target, string fieldPrefix, LayerRefs layer)
    {
        CharacterSelectUiFactory.SetSerialized(target, $"{fieldPrefix}.root", layer.root);
        CharacterSelectUiFactory.SetSerialized(target, $"{fieldPrefix}.group", layer.group);
        CharacterSelectUiFactory.SetSerialized(target, $"{fieldPrefix}.sky", layer.sky);
        CharacterSelectUiFactory.SetSerialized(target, $"{fieldPrefix}.skyBottomFill", layer.skyBottomFill);
        CharacterSelectUiFactory.SetSerialized(target, $"{fieldPrefix}.horizonGlow", layer.horizonGlow);
        CharacterSelectUiFactory.SetSerialized(target, $"{fieldPrefix}.bandFar", layer.bandFar);
        CharacterSelectUiFactory.SetSerialized(target, $"{fieldPrefix}.bandNear", layer.bandNear);
        CharacterSelectUiFactory.SetSerialized(target, $"{fieldPrefix}.bandTop", layer.bandTop);
        CharacterSelectUiFactory.SetSerialized(target, $"{fieldPrefix}.bandBottom", layer.bandBottom);
        CharacterSelectUiFactory.SetSerialized(target, $"{fieldPrefix}.vignette", layer.vignette);
    }

    // ---------------------------------------------------------------------
    // Particles + LightningFlash
    // ---------------------------------------------------------------------

    // LightningFlash is a full-screen UI Image, so it needs a Canvas to render under -
    // the design spec doesn't pin down which one, and Backdrop (already a full-screen
    // Screen Space - Camera canvas) is the natural home for a "sky flash" effect,
    // keeping the Particles root itself a plain ParticleSystem holder exactly like
    // CharacterSelect's.
    private static ArenaParticles BuildParticlesAndLightning(Transform backdropRoot)
    {
        GameObject psGo = CreateRoot("Particles");
        // Identity transform, explicitly - a non-zero rotation here would carry straight
        // into ParticleSystemShapeType.BoxShell's world-space shell (Stardust), turning
        // its ring-the-edges spawn into whatever plane the transform's rotated onto.
        psGo.transform.position = Vector3.zero;
        psGo.transform.rotation = Quaternion.identity;

        ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
        ConfigureBaseParticleSystem(ps);

        if (psGo.TryGetComponent(out ParticleSystemRenderer renderer))
        {
            renderer.sharedMaterial = LoadSparkMaterial();
        }

        Image lightningFlash = CharacterSelectUiFactory.MakeImageStretch(backdropRoot, "LightningFlash", null, new Color(1f, 1f, 1f, 0f));

        ArenaParticles particles = psGo.AddComponent<ArenaParticles>();
        CharacterSelectUiFactory.SetSerialized(particles, "particles", ps);
        CharacterSelectUiFactory.SetSerialized(particles, "lightningFlash", lightningFlash);

        return particles;
    }

    // Placeholder module state so the system doesn't look broken in the Scene view
    // before Play; ArenaParticles.Apply reconfigures main/emission/shape/velocity/
    // rotation/noise/colorOverLifetime per arena the moment the controller starts.
    private static void ConfigureBaseParticleSystem(ParticleSystem ps)
    {
        ParticleSystem.MainModule main = ps.main;
        main.maxParticles = 60;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = Color.white;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 5f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(18f, 10f, 0.1f);
    }

    private static Material LoadSparkMaterial()
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
        if (mat == null)
        {
            // Reused as-is per the design spec, never recreated here - CharacterSelect's
            // own scene build is what creates this material.
            Debug.LogError($"ArenaSelectSceneBuilder: Spark material not found at {SparkMaterialPath} - run CharacterSelect's scene build first (it creates this material).");
        }

        return mat;
    }

    // ---------------------------------------------------------------------
    // UI root + controller wiring
    // ---------------------------------------------------------------------

    private static void BuildUi(ArenaRoster roster, ArenaPanorama panorama, ArenaParticles particles)
    {
        GameObject uiGo = CreateRoot("UI");
        Canvas canvas = uiGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = uiGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;

        uiGo.AddComponent<GraphicRaycaster>();

        Transform root = uiGo.transform;

        ArenaHeader header = BuildHeader(root);
        HazardBadgeStrip hazardStrip = BuildHazardStrip(root);
        ArenaTabDock dock = BuildDock(root);
        Button backButton = BuildBackButton(root);
        ConfirmButton confirmButton = BuildConfirmButton(root);
        // Warp overlay last so it's the topmost sibling - it has to cover everything
        // else built above, including the confirm/back buttons, while it flashes.
        WarpTransition warp = BuildWarpTransition(root);

        ArenaSelectController controller = uiGo.AddComponent<ArenaSelectController>();
        CharacterSelectUiFactory.SetSerialized(controller, "roster", roster);
        CharacterSelectUiFactory.SetSerialized(controller, "panorama", panorama);
        CharacterSelectUiFactory.SetSerialized(controller, "particles", particles);
        CharacterSelectUiFactory.SetSerialized(controller, "header", header);
        CharacterSelectUiFactory.SetSerialized(controller, "hazardStrip", hazardStrip);
        CharacterSelectUiFactory.SetSerialized(controller, "dock", dock);
        CharacterSelectUiFactory.SetSerialized(controller, "backButton", backButton);
        CharacterSelectUiFactory.SetSerialized(controller, "confirmButton", confirmButton);
        CharacterSelectUiFactory.SetSerialized(controller, "warp", warp);
    }

    // ---------------------------------------------------------------------
    // Header
    // ---------------------------------------------------------------------

    private static ArenaHeader BuildHeader(Transform uiRoot)
    {
        RectTransform anchor = CharacterSelectUiFactory.MakeRect(uiRoot, "Header", TopCenter, TopCenter, TopCenter, Vector2.zero, Vector2.zero);
        Transform panel = anchor.transform;

        RectTransform pantheonRect = CharacterSelectUiFactory.MakeRect(panel, "Pantheon", TopCenter, TopCenter, TopCenter, new Vector2(0f, HeaderPantheonY), new Vector2(900f, 34f));
        CanvasGroup pantheonGroup = pantheonRect.gameObject.AddComponent<CanvasGroup>();
        TMP_Text pantheonText = CharacterSelectUiFactory.MakeText(pantheonRect, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "PANTHEON", 26f, Gold, TextAlignmentOptions.Center, true, false, 10f);

        // Rect height trimmed to 64 (from 72) to match the 56pt label it holds - the
        // extra padding was letting the subtitle's -156 offset still read as touching it.
        RectTransform titleRect = CharacterSelectUiFactory.MakeRect(panel, "Title", TopCenter, TopCenter, TopCenter, new Vector2(0f, HeaderTitleY), new Vector2(1100f, 64f));
        CanvasGroup titleGroup = titleRect.gameObject.AddComponent<CanvasGroup>();
        TMP_Text titleText = CharacterSelectUiFactory.MakeText(titleRect, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "ARENA NAME", 56f, Color.white, TextAlignmentOptions.Center, true, false, 6f);

        RectTransform subtitleRect = CharacterSelectUiFactory.MakeRect(panel, "Subtitle", TopCenter, TopCenter, TopCenter, new Vector2(0f, HeaderSubtitleY), new Vector2(900f, 34f));
        CanvasGroup subtitleGroup = subtitleRect.gameObject.AddComponent<CanvasGroup>();
        TMP_Text subtitleText = CharacterSelectUiFactory.MakeText(subtitleRect, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "Flavour line.", 26f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center, false, true);

        ArenaHeader header = anchor.gameObject.AddComponent<ArenaHeader>();
        CharacterSelectUiFactory.SetSerialized(header, "pantheonText", pantheonText);
        CharacterSelectUiFactory.SetSerialized(header, "titleText", titleText);
        CharacterSelectUiFactory.SetSerialized(header, "subtitleText", subtitleText);
        CharacterSelectUiFactory.SetSerializedArray(header, "lines", new Object[] { pantheonGroup, titleGroup, subtitleGroup });

        return header;
    }

    // ---------------------------------------------------------------------
    // HazardBadgeStrip
    // ---------------------------------------------------------------------

    private struct HazardBadgeSlot
    {
        public GameObject root;
        public Image chip;
        public Image icon;
        public Image glow;
        public TMP_Text label;
    }

    private static HazardBadgeStrip BuildHazardStrip(Transform uiRoot)
    {
        RectTransform stripRect = CharacterSelectUiFactory.MakeRect(uiRoot, "HazardStrip", TopRight, TopRight, TopRight, new Vector2(-HazardMargin, -HazardMargin), new Vector2(280f, 3f * HazardChipHeight + 2f * HazardChipGap));
        Transform stripTransform = stripRect.transform;

        Sprite chipSprite = CharacterSelectUiFactory.LoadSprite("RoundedRect");
        Sprite glowSprite = CharacterSelectUiFactory.LoadSprite("SoftCircle");

        var slots = new HazardBadgeSlot[3];

        for (int i = 0; i < 3; i++)
        {
            float y = -i * (HazardChipHeight + HazardChipGap);
            RectTransform badgeRect = CharacterSelectUiFactory.MakeRect(stripTransform, $"Badge{i}", TopRight, TopRight, TopRight, new Vector2(0f, y), new Vector2(260f, HazardChipHeight));
            GameObject badgeGo = badgeRect.gameObject;

            Image chip = CharacterSelectUiFactory.MakeImageStretch(badgeRect, "Chip", chipSprite, Glass, Image.Type.Sliced);
            Image glow = CharacterSelectUiFactory.MakeImage(badgeRect, "Glow", LeftMiddle, LeftMiddle, LeftMiddle, new Vector2(32f, 0f), new Vector2(64f, 64f), glowSprite, new Color(1f, 1f, 1f, 0.35f));

            Image icon = CharacterSelectUiFactory.MakeImage(badgeRect, "Icon", LeftMiddle, LeftMiddle, LeftMiddle, new Vector2(16f, 0f), new Vector2(32f, 32f), null, Color.white);
            icon.enabled = false;

            TMP_Text label = CharacterSelectUiFactory.MakeText(badgeRect, "Label", LeftMiddle, LeftMiddle, LeftMiddle, new Vector2(60f, 0f), new Vector2(180f, 40f), "HAZARD", 18f, Color.white, TextAlignmentOptions.MidlineLeft, true, false, 3f);

            badgeGo.SetActive(false);

            slots[i] = new HazardBadgeSlot { root = badgeGo, chip = chip, icon = icon, glow = glow, label = label };
        }

        HazardBadgeStrip hazardStrip = stripRect.gameObject.AddComponent<HazardBadgeStrip>();

        SetArraySize(hazardStrip, "badges", slots.Length);
        for (int i = 0; i < slots.Length; i++)
        {
            string p = $"badges.Array.data[{i}]";
            CharacterSelectUiFactory.SetSerialized(hazardStrip, $"{p}.root", slots[i].root);
            CharacterSelectUiFactory.SetSerialized(hazardStrip, $"{p}.chip", slots[i].chip);
            CharacterSelectUiFactory.SetSerialized(hazardStrip, $"{p}.icon", slots[i].icon);
            CharacterSelectUiFactory.SetSerialized(hazardStrip, $"{p}.glow", slots[i].glow);
            CharacterSelectUiFactory.SetSerialized(hazardStrip, $"{p}.label", slots[i].label);
        }

        // Indexed by (int)ArenaHazard - element 0 (None) is intentionally unused.
        Sprite[] hazardIcons =
        {
            null,
            ArenaSelectUiFactory.LoadSprite("HazardFlame"),
            ArenaSelectUiFactory.LoadSprite("HazardWhirlwind"),
            ArenaSelectUiFactory.LoadSprite("HazardLightning"),
            ArenaSelectUiFactory.LoadSprite("HazardSandstorm"),
            ArenaSelectUiFactory.LoadSprite("HazardTide"),
            ArenaSelectUiFactory.LoadSprite("HazardFrost"),
            ArenaSelectUiFactory.LoadSprite("HazardVoid"),
        };
        CharacterSelectUiFactory.SetSerializedArray(hazardStrip, "hazardIcons", hazardIcons);

        return hazardStrip;
    }

    /// <summary>
    /// CharacterSelectUiFactory only exposes SetSerializedArray for flat Object[]
    /// fields; HazardBadgeStrip.badges is an array of a nested [Serializable] class, so
    /// its elements are set individually below via "badges.Array.data[i].field" paths -
    /// this just sizes the array first so those indices exist.
    /// </summary>
    private static void SetArraySize(Object target, string field, int size)
    {
        var so = new SerializedObject(target);
        SerializedProperty prop = so.FindProperty(field);

        if (prop == null)
        {
            Debug.LogError($"ArenaSelectSceneBuilder: field '{field}' not found on {target.GetType().Name}.");
            return;
        }

        prop.arraySize = size;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ---------------------------------------------------------------------
    // ArenaTabDock + ArenaTab template
    // ---------------------------------------------------------------------

    private static ArenaTabDock BuildDock(Transform uiRoot)
    {
        RectTransform dockRect = CharacterSelectUiFactory.MakeRect(uiRoot, "ArenaTabDock", BottomCenter, BottomCenter, BottomCenter, new Vector2(0f, DockY), Vector2.zero);
        Transform dockTransform = dockRect.transform;

        RectTransform contentRect = CharacterSelectUiFactory.MakeRect(dockTransform, "Content", Center, Center, Center, Vector2.zero, new Vector2(0f, TabHeight));
        HorizontalLayoutGroup hlg = contentRect.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = TabGap;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        ContentSizeFitter fitter = contentRect.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

        ArenaTab template = BuildTabTemplate(contentRect);
        template.gameObject.SetActive(false);

        ArenaTabDock dock = dockRect.gameObject.AddComponent<ArenaTabDock>();
        CharacterSelectUiFactory.SetSerialized(dock, "content", contentRect);
        CharacterSelectUiFactory.SetSerialized(dock, "template", template);

        return dock;
    }

    private static ArenaTab BuildTabTemplate(Transform parent)
    {
        Button button = CharacterSelectUiFactory.MakeButton(parent, "ArenaTabTemplate", Center, Center, Center, Vector2.zero, new Vector2(TabWidth, TabHeight), CharacterSelectUiFactory.LoadSprite("RoundedRect"), Glass);
        RectTransform tabRect = button.transform as RectTransform;
        GameObject tabGo = button.gameObject;
        Image plate = button.GetComponent<Image>();
        CanvasGroup group = tabGo.AddComponent<CanvasGroup>();

        // A second Sliced RoundedRect rather than the circular Ring sprite - Ring
        // stretched to a 300x110 rect would distort into an ellipse instead of tracing
        // the plate's own rounded-rect edge.
        Image border = CharacterSelectUiFactory.MakeImageStretch(tabRect, "Border", CharacterSelectUiFactory.LoadSprite("RoundedRect"), new Color(1f, 1f, 1f, 0.25f), Image.Type.Sliced);

        RectTransform runeRect = CharacterSelectUiFactory.MakeRect(tabRect, "Rune", Center, Center, Center, new Vector2(0f, 20f), new Vector2(48f, 48f));
        Image rune = runeRect.gameObject.AddComponent<Image>();
        rune.color = new Color(1f, 1f, 1f, 0.7f);
        rune.raycastTarget = false;
        rune.preserveAspect = true;

        TMP_Text runeGlyphText = CharacterSelectUiFactory.MakeText(tabRect, "RuneGlyph", Center, Center, Center, new Vector2(0f, 20f), new Vector2(48f, 48f), "?", 28f, Color.white, TextAlignmentOptions.Center, true);
        runeGlyphText.gameObject.SetActive(false);

        TMP_Text nameText = CharacterSelectUiFactory.MakeText(tabRect, "Name", Center, Center, Center, new Vector2(0f, -28f), new Vector2(270f, 30f), "ARENA", 18f, Color.white, TextAlignmentOptions.Center, true, false, 4f);

        ArenaTab tab = tabGo.AddComponent<ArenaTab>();
        CharacterSelectUiFactory.SetSerialized(tab, "scaleTarget", tabRect);
        CharacterSelectUiFactory.SetSerialized(tab, "group", group);
        CharacterSelectUiFactory.SetSerialized(tab, "plate", plate);
        CharacterSelectUiFactory.SetSerialized(tab, "border", border);
        CharacterSelectUiFactory.SetSerialized(tab, "rune", rune);
        CharacterSelectUiFactory.SetSerialized(tab, "runeGlyphText", runeGlyphText);
        CharacterSelectUiFactory.SetSerialized(tab, "nameText", nameText);
        CharacterSelectUiFactory.SetSerialized(tab, "button", button);

        return tab;
    }

    // ---------------------------------------------------------------------
    // Back button
    // ---------------------------------------------------------------------

    private static Button BuildBackButton(Transform uiRoot)
    {
        Sprite circle = CharacterSelectUiFactory.LoadSprite("Circle");
        Button backButton = CharacterSelectUiFactory.MakeButton(uiRoot, "BackButton", BottomLeft, BottomLeft, BottomLeft, BackButtonPos, new Vector2(BackButtonSize, BackButtonSize), circle, new Color(0f, 0f, 0f, 0.45f));
        CharacterSelectUiFactory.MakeImageStretch(backButton.transform, "Ring", CharacterSelectUiFactory.LoadSprite("Ring"), new Color(1f, 1f, 1f, 0.2f));
        CharacterSelectUiFactory.MakeText(backButton.transform, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "<", 40f, Color.white, TextAlignmentOptions.Center);
        return backButton;
    }

    // ---------------------------------------------------------------------
    // ConfirmButton - built the same way CharacterSelectSceneBuilder.BuildConfirmButton does.
    // ---------------------------------------------------------------------

    private static ConfirmButton BuildConfirmButton(Transform uiRoot)
    {
        RectTransform rootRect = CharacterSelectUiFactory.MakeRect(uiRoot, "ConfirmButton", BottomRight, BottomRight, BottomRight, ConfirmButtonPos, ConfirmButtonSize);
        Transform root = rootRect.transform;

        Image glow = CharacterSelectUiFactory.MakeImage(root, "Glow", Center, Center, Center, Vector2.zero, new Vector2(ConfirmButtonSize.x + 100f, ConfirmButtonSize.y + 100f), CharacterSelectUiFactory.LoadSprite("SoftCircle"), Color.white);

        RectTransform backgroundRect = CharacterSelectUiFactory.MakeStretchRect(root, "Background");
        Image background = backgroundRect.gameObject.AddComponent<Image>();
        background.sprite = CharacterSelectUiFactory.LoadSprite("RoundedRect");
        background.type = Image.Type.Sliced;
        background.color = Gold;
        background.raycastTarget = true;

        Button button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Color.white;
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.selectedColor = Color.white;
        button.colors = colors;

        // Smaller size/spacing than CharacterSelect's "CONFIRM"/"FIGHT" label - this
        // one has to fit "CONFIRM BATTLEGROUND" in the same-shaped box.
        TMP_Text label = CharacterSelectUiFactory.MakeText(root, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "CONFIRM BATTLEGROUND", 26f, CharacterSelectUiFactory.HexColor("#0B0B12"), TextAlignmentOptions.Center, true, false, 3f);

        ConfirmButton confirmButton = rootRect.gameObject.AddComponent<ConfirmButton>();
        CharacterSelectUiFactory.SetSerialized(confirmButton, "button", button);
        CharacterSelectUiFactory.SetSerialized(confirmButton, "scaleTarget", rootRect);
        CharacterSelectUiFactory.SetSerialized(confirmButton, "background", background);
        CharacterSelectUiFactory.SetSerialized(confirmButton, "glow", glow);
        CharacterSelectUiFactory.SetSerialized(confirmButton, "label", label);

        return confirmButton;
    }

    // ---------------------------------------------------------------------
    // WarpTransition - topmost overlay.
    // ---------------------------------------------------------------------

    private static WarpTransition BuildWarpTransition(Transform uiRoot)
    {
        RectTransform flashRect = CharacterSelectUiFactory.MakeStretchRect(uiRoot, "WarpTransition");
        Image flash = flashRect.gameObject.AddComponent<Image>();
        flash.color = new Color(1f, 1f, 1f, 0f);
        flash.raycastTarget = false;

        RectTransform ringRect = CharacterSelectUiFactory.MakeRect(flashRect, "Ring", Center, Center, Center, Vector2.zero, new Vector2(700f, 700f));
        Image ring = ringRect.gameObject.AddComponent<Image>();
        ring.sprite = CharacterSelectUiFactory.LoadSprite("Ring");
        ring.color = new Color(1f, 1f, 1f, 0f);
        ring.raycastTarget = false;

        WarpTransition warp = flashRect.gameObject.AddComponent<WarpTransition>();
        CharacterSelectUiFactory.SetSerialized(warp, "flash", flash);
        CharacterSelectUiFactory.SetSerialized(warp, "ringScaleTarget", ringRect);
        CharacterSelectUiFactory.SetSerialized(warp, "ring", ring);

        return warp;
    }

    // ---------------------------------------------------------------------
    // Build settings
    // ---------------------------------------------------------------------

    private static void EnsureBuildSettingsEntry()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path == ScenePath)
            {
                // Already present - leave build order exactly as it is rather than
                // forcing index 1 back into place on every re-run.
                return;
            }
        }

        var list = new List<EditorBuildSettingsScene>(scenes);
        int insertIndex = Mathf.Min(1, list.Count);
        list.Insert(insertIndex, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();

        Debug.Log($"ArenaSelectSceneBuilder: inserted {ScenePath} into EditorBuildSettings.scenes at index {insertIndex}.");
    }
}
