using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

// Builds the CharacterSelect scene hierarchy from scratch: backdrop, ambient particles,
// the deity stage and the full UI tree, then wires every runtime component's private
// serialized fields via CharacterSelectUiFactory.SetSerialized. Idempotent - deletes its
// own previously generated roots (and the legacy Canvas/Characters objects) before
// rebuilding, so re-running after a runtime script change is always safe.
//
// All colours/sizes that show up more than once live in the constants block below so a
// designer can retune the look without hunting through hierarchy code.
public static class CharacterSelectSceneBuilder
{
    // ---------------------------------------------------------------------
    // Constants
    // ---------------------------------------------------------------------

    private const string RosterAssetPath = "Assets/Data/Roster/CharacterRoster.asset";
    private const string SparkMaterialPath = "Assets/UI/CharacterSelect/Materials/Spark.mat";

    // Set to false if a project ever wants to keep the old selection script around.
    private const bool DeleteLegacySelectionScript = true;

    private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

    // (0.65 - 0.5) * 1920 reference width - where the deity model's feet/pedestal sit.
    // The deity's screen position is a viewport fraction, not a pixel offset, so the
    // backdrop pieces that sit on the model anchor to the same fractions (matching
    // DeityStage.viewportAnchor) and stay glued to it on every aspect ratio.
    private static readonly Vector2 DeityAnchor = new Vector2(0.65f, 0.5f);
    private static readonly Vector2 DeityFeetAnchor = new Vector2(0.65f, 0.16f);

    private static readonly Color Gold = CharacterSelectUiFactory.HexColor("#F5D76E");
    private static readonly Color Glass = CharacterSelectUiFactory.HexColor("#0A0F1A", 0.55f);
    private static readonly Color BaseColor = CharacterSelectUiFactory.HexColor("#030712");

    // Not given an exact hex by the contract (only Gold/Glass/Base are); picked to read
    // clearly as "amber" against the gold/primary palette already in use for the
    // PLACEHOLDER pill. Retune here if art wants a different shade.
    private const string AmberHex = "#F5A623";
    private static readonly Color Amber = CharacterSelectUiFactory.HexColor(AmberHex);

    // Reused anchor points.
    private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 TopRight = new Vector2(1f, 1f);
    private static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);
    private static readonly Vector2 BottomRight = new Vector2(1f, 0f);
    private static readonly Vector2 LeftMiddle = new Vector2(0f, 0.5f);
    private static readonly Vector2 RightMiddle = new Vector2(1f, 0.5f);

    private const float RadarRadius = 88f;
    private const float CarouselStep = 150f;
    private const float StarTextureSize = 64f;
    // Decorative alphas. The runtime palette only ever changes hue, never alpha, so
    // these are the final on-screen strengths.
    private const float BlobAlpha = 0.45f;
    private const float HorizonAlpha = 0.6f;
    private const float PillarAlpha = 0.7f;
    private const float StarSpacing = 160f; // desired on-screen gap between star dots, in reference-canvas px

    // ---------------------------------------------------------------------
    // Menu items
    // ---------------------------------------------------------------------

    [MenuItem("Elementals Fight/Character Select/3 - Build Scene")]
    public static void BuildSceneMenu()
    {
        BuildScene();
    }

    [MenuItem("Elementals Fight/Character Select/Run All (1-3)")]
    public static void RunAll()
    {
        CharacterSelectArtGenerator.GenerateAll();
        AssetDatabase.Refresh();

        CharacterSelectRosterAssets.CreateOrUpdate();
        AssetDatabase.Refresh();

        BuildScene();
    }

    // ---------------------------------------------------------------------
    // Top-level build
    // ---------------------------------------------------------------------

    public static void BuildScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "CharacterSelect")
        {
            Debug.LogError($"CharacterSelectSceneBuilder: active scene is '{scene.name}', expected 'CharacterSelect'. Open CharacterSelect.unity and run this again.");
            return;
        }

        DeleteLegacyAndGeneratedRoots(scene);

        Camera cam = SetupCamera();
        if (cam == null)
        {
            return;
        }

        ParticleSystem particles = BuildParticles();
        AmbientBackdrop backdrop = BuildBackdrop(cam, particles);
        DeityStage stage = BuildStage(cam);
        BuildUi(backdrop, stage);

        DeleteLegacyScriptIfConfigured();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log("CharacterSelectSceneBuilder: scene built - roots: Backdrop, Particles, Stage, UI.");
    }

    private static void DeleteLegacyAndGeneratedRoots(Scene scene)
    {
        string[] namesToDelete = { "Backdrop", "Stage", "UI", "Particles", "Canvas", "Characters" };
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
            Debug.LogError("CharacterSelectSceneBuilder: no 'Main Camera' found in the open scene - cannot continue.");
            return null;
        }

        cam.orthographic = false;
        cam.fieldOfView = 30f;
        cam.transform.position = new Vector3(0f, 1.6f, -9f);
        cam.transform.rotation = Quaternion.identity;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BaseColor;

        return cam;
    }

    private static GameObject CreateRoot(string name)
    {
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    private static void DeleteLegacyScriptIfConfigured()
    {
        if (!DeleteLegacySelectionScript)
        {
            return;
        }

        const string path = "Assets/Scripts/PlayerSelection.cs";
        if (File.Exists(path))
        {
            bool ok = AssetDatabase.DeleteAsset(path);
            Debug.Log(ok
                ? $"CharacterSelectSceneBuilder: deleted legacy script {path}."
                : $"CharacterSelectSceneBuilder: failed to delete {path}.");
        }
        else
        {
            Debug.Log($"CharacterSelectSceneBuilder: legacy script {path} not present, nothing to delete.");
        }
    }

    // ---------------------------------------------------------------------
    // Particles
    // ---------------------------------------------------------------------

    private static ParticleSystem BuildParticles()
    {
        GameObject go = CreateRoot("Particles");
        go.transform.position = new Vector3(0f, -1f, 2f);
        go.transform.rotation = Quaternion.identity;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.maxParticles = 40;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startColor = Color.white;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 5f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(18f, 1f, 0.1f);
        // Box shape emits along its local +Z by default; rotate so that becomes world "up".
        shape.rotation = new Vector3(-90f, 0f, 0f);

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.5f), new GradientAlphaKey(0f, 1f) });
        colorOverLifetime.color = gradient;

        if (go.TryGetComponent(out ParticleSystemRenderer renderer))
        {
            renderer.sharedMaterial = BuildSparkMaterial();
        }

        return ps;
    }

    private static Material BuildSparkMaterial()
    {
        CharacterSelectUiFactory.EnsureFolder("Assets/UI/CharacterSelect/Materials");

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(SparkMaterialPath);
        bool isNew = mat == null;

        if (isNew)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                Debug.LogError("CharacterSelectSceneBuilder: shader 'Universal Render Pipeline/Particles/Unlit' not found - is URP installed?");
                return null;
            }
            mat = new Material(shader);
        }

        // Manual URP transparent-unlit setup (no ShaderGraph asset backs this material) -
        // the inspector re-validates these keywords the next time it's opened, so this
        // is "good enough": if it renders slightly wrong, the particles are still just
        // soft dots and nothing breaks.
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", 5f);
        mat.SetFloat("_DstBlend", 10f);
        mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = 3000;
        mat.SetTexture("_BaseMap", CharacterSelectUiFactory.LoadSpriteTexture("SoftCircle"));
        mat.SetColor("_BaseColor", Color.white);

        if (isNew)
        {
            AssetDatabase.CreateAsset(mat, SparkMaterialPath);
        }
        else
        {
            EditorUtility.SetDirty(mat);
        }

        return mat;
    }

    // ---------------------------------------------------------------------
    // Backdrop
    // ---------------------------------------------------------------------

    private static AmbientBackdrop BuildBackdrop(Camera cam, ParticleSystem particles)
    {
        GameObject go = CreateRoot("Backdrop");
        Canvas canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 30f;
        canvas.sortingOrder = -10;

        CanvasScaler scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        scaler.matchWidthOrHeight = 0.5f;
        // Deliberately no GraphicRaycaster - purely decorative, must never eat a click
        // meant for the UI canvas rendered above it.

        Transform root = go.transform;
        Sprite softCircle = CharacterSelectUiFactory.LoadSprite("SoftCircle");

        // Runtime-tinted elements (AmbientBackdrop.ApplyPalette overwrites these the
        // instant the scene starts) get a plain white/base placeholder here; only the
        // pedestal ring is genuinely static gold.
        Image baseGradient = CharacterSelectUiFactory.MakeImageStretch(root, "BaseGradient", CharacterSelectUiFactory.LoadSprite("GradientV"), BaseColor);

        Image stars = CharacterSelectUiFactory.MakeImageStretch(root, "Stars", CharacterSelectUiFactory.LoadSprite("Star"), new Color(1f, 1f, 1f, 0.16f), Image.Type.Tiled);
        stars.pixelsPerUnitMultiplier = StarTextureSize / StarSpacing;

        GameObject blobsGo = CharacterSelectUiFactory.MakeRect(root, "Blobs", Center, Center, Center, Vector2.zero, ReferenceResolution).gameObject;
        blobsGo.AddComponent<Canvas>(); // nested canvas: blob drift rebuilds only this subtree, not the whole backdrop
        Transform blobsRoot = blobsGo.transform;

        Image blob0 = CharacterSelectUiFactory.MakeImage(blobsRoot, "Blob0", Center, Center, Center, new Vector2(-500f, 200f), new Vector2(900f, 900f), softCircle, new Color(1f, 1f, 1f, BlobAlpha));
        Image blob1 = CharacterSelectUiFactory.MakeImage(blobsRoot, "Blob1", Center, Center, Center, new Vector2(450f, -150f), new Vector2(720f, 720f), softCircle, new Color(1f, 1f, 1f, BlobAlpha));
        Image blob2 = CharacterSelectUiFactory.MakeImage(blobsRoot, "Blob2", Center, Center, Center, new Vector2(200f, 300f), new Vector2(620f, 620f), softCircle, new Color(1f, 1f, 1f, BlobAlpha));

        // Anchored to the bottom-right corner and pushed down by 120: most of the ellipse
        // bleeds off the bottom edge on purpose, leaving just its upper arc as a soft
        // horizon glow rather than a visible hard-edged shape.
        Image horizon = CharacterSelectUiFactory.MakeImage(root, "Horizon", BottomRight, BottomRight, BottomRight, new Vector2(0f, -120f), new Vector2(1700f, 520f), CharacterSelectUiFactory.LoadSprite("Ellipse"), new Color(1f, 1f, 1f, HorizonAlpha));

        Image pillar = CharacterSelectUiFactory.MakeImage(root, "Pillar", DeityAnchor, DeityAnchor, Center, new Vector2(0f, 60f), new Vector2(40f, 760f), CharacterSelectUiFactory.LoadSprite("Pillar"), new Color(1f, 1f, 1f, PillarAlpha));

        Image halo = CharacterSelectUiFactory.MakeImage(root, "Halo", DeityAnchor, DeityAnchor, Center, new Vector2(0f, 40f), new Vector2(640f, 640f), softCircle, Color.white);

        GameObject pedestalGo = CharacterSelectUiFactory.MakeRect(root, "Pedestal", DeityFeetAnchor, DeityFeetAnchor, Center, new Vector2(0f, 10f), Vector2.zero).gameObject;
        Transform pedestal = pedestalGo.transform;

        // Small filled glow first (bottom), then the dashed ring, then the crisp gold
        // ring on top - biggest/crispest shape wins the draw order.
        CharacterSelectUiFactory.MakeImage(pedestal, "PedestalGlow", Center, Center, Center, Vector2.zero, new Vector2(300f, 70f), CharacterSelectUiFactory.LoadSprite("Ellipse"), new Color(1f, 1f, 1f, 0.35f));
        Image pedestalDashed = CharacterSelectUiFactory.MakeImage(pedestal, "PedestalDashed", Center, Center, Center, Vector2.zero, new Vector2(470f, 120f), CharacterSelectUiFactory.LoadSprite("EllipseDashed"), new Color(1f, 1f, 1f, 0.9f));
        Image pedestalRing = CharacterSelectUiFactory.MakeImage(pedestal, "PedestalRing", Center, Center, Center, Vector2.zero, new Vector2(560f, 140f), CharacterSelectUiFactory.LoadSprite("EllipseRing"), Gold);

        AmbientBackdrop backdrop = go.AddComponent<AmbientBackdrop>();
        CharacterSelectUiFactory.SetSerialized(backdrop, "baseGradient", baseGradient);
        CharacterSelectUiFactory.SetSerializedArray(backdrop, "blobs", new Object[] { blob0, blob1, blob2 });
        CharacterSelectUiFactory.SetSerialized(backdrop, "horizon", horizon);
        CharacterSelectUiFactory.SetSerialized(backdrop, "halo", halo);
        CharacterSelectUiFactory.SetSerialized(backdrop, "pillar", pillar);
        CharacterSelectUiFactory.SetSerialized(backdrop, "pedestalRing", pedestalRing);
        CharacterSelectUiFactory.SetSerialized(backdrop, "pedestalDashed", pedestalDashed);
        CharacterSelectUiFactory.SetSerialized(backdrop, "particles", particles);

        return backdrop;
    }

    // ---------------------------------------------------------------------
    // Stage
    // ---------------------------------------------------------------------

    private static DeityStage BuildStage(Camera cam)
    {
        GameObject stageGo = CreateRoot("Stage");
        DeityStage stage = stageGo.AddComponent<DeityStage>();

        GameObject modelsGo = new GameObject("Models");
        Undo.RegisterCreatedObjectUndo(modelsGo, "Create Models");
        modelsGo.transform.SetParent(stageGo.transform, false);

        CharacterSelectUiFactory.SetSerialized(stage, "stageCamera", cam);
        CharacterSelectUiFactory.SetSerialized(stage, "modelsRoot", modelsGo.transform);

        // Positions modelsRoot so its origin lands at viewportAnchor on the z=0 plane -
        // needs stageCamera already wired, which is why this runs after the field set above.
        stage.PositionRoot();

        return stage;
    }

    // ---------------------------------------------------------------------
    // UI root + controller wiring
    // ---------------------------------------------------------------------

    private static void BuildUi(AmbientBackdrop backdrop, DeityStage stage)
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

        BuildTopBar(root, out Button backButton, out TMP_Text headerText, out GameObject vsChip, out Image vsChipDisc, out TMP_Text vsChipGlyph, out TMP_Text vsChipName, out Button shopButton);
        LorePanel lorePanel = BuildLorePanel(root);
        BuildRadarPanel(root, out RadarChartGraphic radar, out RadarChartGraphic radarGlow, out TMP_Text overallText, out TMP_Text[] axisLabels);
        PortraitCarousel carousel = BuildCarousel(root);
        ConfirmButton confirmButton = BuildConfirmButton(root);
        BuildFlashAndToast(root, out Image flashOverlay, out CanvasGroup toastGroup, out TMP_Text toastText);

        CharacterRoster roster = AssetDatabase.LoadAssetAtPath<CharacterRoster>(RosterAssetPath);
        if (roster == null)
        {
            Debug.LogError($"CharacterSelectSceneBuilder: roster asset not found at {RosterAssetPath} - run 'Elementals Fight/Character Select/2 - Create Roster Assets' first.");
        }

        CharacterSelectController controller = uiGo.AddComponent<CharacterSelectController>();
        CharacterSelectUiFactory.SetSerialized(controller, "roster", roster);
        CharacterSelectUiFactory.SetSerialized(controller, "stage", stage);
        CharacterSelectUiFactory.SetSerialized(controller, "backdrop", backdrop);
        CharacterSelectUiFactory.SetSerialized(controller, "lorePanel", lorePanel);
        CharacterSelectUiFactory.SetSerialized(controller, "radar", radar);
        CharacterSelectUiFactory.SetSerialized(controller, "radarGlow", radarGlow);
        CharacterSelectUiFactory.SetSerialized(controller, "overallText", overallText);
        CharacterSelectUiFactory.SetSerializedArray(controller, "axisLabels", axisLabels);
        CharacterSelectUiFactory.SetSerialized(controller, "carousel", carousel);
        CharacterSelectUiFactory.SetSerialized(controller, "confirmButton", confirmButton);
        CharacterSelectUiFactory.SetSerialized(controller, "headerText", headerText);
        CharacterSelectUiFactory.SetSerialized(controller, "backButton", backButton);
        CharacterSelectUiFactory.SetSerialized(controller, "shopButton", shopButton);
        CharacterSelectUiFactory.SetSerialized(controller, "vsChip", vsChip);
        CharacterSelectUiFactory.SetSerialized(controller, "vsChipDisc", vsChipDisc);
        CharacterSelectUiFactory.SetSerialized(controller, "vsChipGlyph", vsChipGlyph);
        CharacterSelectUiFactory.SetSerialized(controller, "vsChipName", vsChipName);
        CharacterSelectUiFactory.SetSerialized(controller, "toastGroup", toastGroup);
        CharacterSelectUiFactory.SetSerialized(controller, "toastText", toastText);
        CharacterSelectUiFactory.SetSerialized(controller, "flashOverlay", flashOverlay);
    }

    // ---------------------------------------------------------------------
    // TopBar: back / header / vsChip / shop
    // ---------------------------------------------------------------------

    private static void BuildTopBar(Transform uiRoot, out Button backButton, out TMP_Text headerText, out GameObject vsChip, out Image vsChipDisc, out TMP_Text vsChipGlyph, out TMP_Text vsChipName, out Button shopButton)
    {
        RectTransform topBarRect = CharacterSelectUiFactory.MakeRect(uiRoot, "TopBar", new Vector2(0f, 1f), new Vector2(1f, 1f), TopCenter, Vector2.zero, new Vector2(0f, 140f));
        Transform topBar = topBarRect.transform;

        Sprite circle = CharacterSelectUiFactory.LoadSprite("Circle");

        backButton = CharacterSelectUiFactory.MakeButton(topBar, "BackButton", TopLeft, TopLeft, TopLeft, new Vector2(48f, -48f), new Vector2(72f, 72f), circle, new Color(0f, 0f, 0f, 0.45f));
        CharacterSelectUiFactory.MakeImageStretch(backButton.transform, "Ring", CharacterSelectUiFactory.LoadSprite("Ring"), new Color(1f, 1f, 1f, 0.15f));
        CharacterSelectUiFactory.MakeText(backButton.transform, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "<", 40f, Color.white, TextAlignmentOptions.Center);

        headerText = CharacterSelectUiFactory.MakeText(topBar, "Header", TopLeft, TopLeft, TopLeft, new Vector2(150f, -48f), new Vector2(600f, 56f), "CHOOSE YOUR DEITY", 22f, Gold, TextAlignmentOptions.MidlineLeft, true, false, 12f);

        RectTransform vsChipRect = CharacterSelectUiFactory.MakeRect(topBar, "VsChip", TopCenter, TopCenter, TopCenter, new Vector2(0f, -48f), new Vector2(320f, 64f));
        vsChip = vsChipRect.gameObject;
        CharacterSelectUiFactory.MakeImageStretch(vsChipRect, "Background", CharacterSelectUiFactory.LoadSprite("RoundedRect"), Glass, Image.Type.Sliced);
        vsChipDisc = CharacterSelectUiFactory.MakeImage(vsChipRect, "Disc", LeftMiddle, LeftMiddle, LeftMiddle, new Vector2(38f, 0f), new Vector2(44f, 44f), circle, Color.white);
        vsChipGlyph = CharacterSelectUiFactory.MakeText(vsChipDisc.transform, "Glyph", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "Y", 20f, Color.white, TextAlignmentOptions.Center, true);
        vsChipName = CharacterSelectUiFactory.MakeText(vsChipRect, "Name", LeftMiddle, LeftMiddle, LeftMiddle, new Vector2(96f, 0f), new Vector2(210f, 40f), "NAME", 18f, Color.white, TextAlignmentOptions.MidlineLeft, true);
        vsChip.SetActive(false);

        shopButton = CharacterSelectUiFactory.MakeButton(topBar, "ShopButton", TopRight, TopRight, TopRight, new Vector2(-48f, -48f), new Vector2(120f, 60f), CharacterSelectUiFactory.LoadSprite("RoundedRect"), new Color(0f, 0f, 0f, 0.45f));
        CharacterSelectUiFactory.MakeText(shopButton.transform, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "SHOP", 20f, Color.white, TextAlignmentOptions.Center, true);
    }

    // ---------------------------------------------------------------------
    // LorePanel
    // ---------------------------------------------------------------------

    private static LorePanel BuildLorePanel(Transform uiRoot)
    {
        RectTransform panelRect = CharacterSelectUiFactory.MakeRect(uiRoot, "LorePanel", LeftMiddle, LeftMiddle, LeftMiddle, new Vector2(40f, 30f), new Vector2(600f, 430f));
        Transform panel = panelRect.transform;

        Image trim = CharacterSelectUiFactory.MakeImageStretch(panel, "Trim", CharacterSelectUiFactory.LoadSprite("Trim"), Color.white);
        CharacterSelectUiFactory.MakeImageStretch(panel, "Glass", CharacterSelectUiFactory.LoadSprite("RoundedRect"), Glass, Image.Type.Sliced);

        RectTransform contentRect = CharacterSelectUiFactory.MakeStretchRect(panel, "Content");
        VerticalLayoutGroup vlg = contentRect.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(36, 36, 36, 36);
        vlg.spacing = 10f;
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        Transform content = contentRect.transform;

        // --- Row 1: eyebrow + placeholder pill ---
        RectTransform row1Rect = CharacterSelectUiFactory.MakeRect(content, "Row1", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
        row1Rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;
        HorizontalLayoutGroup row1Hlg = row1Rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        row1Hlg.spacing = 12f;
        row1Hlg.childAlignment = TextAnchor.MiddleLeft;
        row1Hlg.childControlWidth = true;
        row1Hlg.childControlHeight = true;
        row1Hlg.childForceExpandWidth = false;
        row1Hlg.childForceExpandHeight = false;
        CanvasGroup row1Group = row1Rect.gameObject.AddComponent<CanvasGroup>();

        TMP_Text eyebrow = CharacterSelectUiFactory.MakeText(row1Rect, "Eyebrow", Vector2.zero, Vector2.one, Center, Vector2.zero, new Vector2(300f, 28f), "PANTHEON · DOMAIN", 20f, Gold, TextAlignmentOptions.MidlineLeft, true, false, 8f);

        RectTransform placeholderRect = CharacterSelectUiFactory.MakeRect(row1Rect, "PlaceholderTag", Vector2.zero, Vector2.one, Center, Vector2.zero, new Vector2(150f, 28f));
        GameObject placeholderTag = placeholderRect.gameObject;
        LayoutElement placeholderLe = placeholderTag.AddComponent<LayoutElement>();
        placeholderLe.preferredWidth = 150f;
        placeholderLe.preferredHeight = 28f;
        CharacterSelectUiFactory.MakeImageStretch(placeholderRect, "Background", CharacterSelectUiFactory.LoadSprite("RoundedRect"), CharacterSelectUiFactory.HexColor(AmberHex, 0.18f), Image.Type.Sliced);
        CharacterSelectUiFactory.MakeText(placeholderRect, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "PLACEHOLDER", 14f, Amber, TextAlignmentOptions.Center, true);

        // --- Name ---
        RectTransform nameRect = CharacterSelectUiFactory.MakeRect(content, "Name", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
        nameRect.gameObject.AddComponent<LayoutElement>().preferredHeight = 78f;
        CanvasGroup nameGroup = nameRect.gameObject.AddComponent<CanvasGroup>();
        TMP_Text nameText = CharacterSelectUiFactory.MakeText(nameRect, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "NAME", 64f, Color.white, TextAlignmentOptions.MidlineLeft, true);
        nameText.fontStyle |= FontStyles.UpperCase;
        // Runtime sets a white->Primary VertexGradient; the gradient never renders unless
        // this flag is on, so it has to be baked in here rather than left for the caller.
        nameText.enableVertexGradient = true;

        // --- Title ---
        RectTransform titleRect = CharacterSelectUiFactory.MakeRect(content, "Title", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
        titleRect.gameObject.AddComponent<LayoutElement>().preferredHeight = 36f;
        CanvasGroup titleGroup = titleRect.gameObject.AddComponent<CanvasGroup>();
        TMP_Text titleText = CharacterSelectUiFactory.MakeText(titleRect, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "Title", 28f, Color.white, TextAlignmentOptions.MidlineLeft, false, true);

        // --- Lore ---
        RectTransform loreRect = CharacterSelectUiFactory.MakeRect(content, "Lore", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
        loreRect.gameObject.AddComponent<LayoutElement>().preferredHeight = 120f;
        CanvasGroup loreGroup = loreRect.gameObject.AddComponent<CanvasGroup>();
        TMP_Text loreText = CharacterSelectUiFactory.MakeText(loreRect, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "Lore text goes here.", 22f, new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.TopLeft, false, false, 0f, true);
        loreText.overflowMode = TextOverflowModes.Ellipsis;

        // --- Row 2: chips ---
        RectTransform row2Rect = CharacterSelectUiFactory.MakeRect(content, "Row2", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
        row2Rect.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
        HorizontalLayoutGroup row2Hlg = row2Rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        row2Hlg.spacing = 12f;
        row2Hlg.childAlignment = TextAnchor.MiddleLeft;
        row2Hlg.childControlWidth = true;
        row2Hlg.childControlHeight = true;
        row2Hlg.childForceExpandWidth = false;
        row2Hlg.childForceExpandHeight = false;
        CanvasGroup row2Group = row2Rect.gameObject.AddComponent<CanvasGroup>();

        RectTransform playstyleRect = CharacterSelectUiFactory.MakeRect(row2Rect, "PlaystyleChip", Vector2.zero, Vector2.one, Center, Vector2.zero, new Vector2(270f, 44f));
        LayoutElement playstyleLe = playstyleRect.gameObject.AddComponent<LayoutElement>();
        playstyleLe.preferredWidth = 270f;
        playstyleLe.preferredHeight = 40f;
        // Outline sibling (unwired, decorative) sits full-bleed behind a 2px-inset fill -
        // together they read as a bordered pill without needing a dedicated ring sprite.
        CharacterSelectUiFactory.MakeImageStretch(playstyleRect, "Outline", CharacterSelectUiFactory.LoadSprite("RoundedRect"), Color.white, Image.Type.Sliced);
        RectTransform playstyleFillRect = CharacterSelectUiFactory.MakeRect(playstyleRect, "Fill", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero);
        CharacterSelectUiFactory.Stretch(playstyleFillRect, new Vector2(2f, 2f), new Vector2(-2f, -2f));
        Image playstyleChip = playstyleFillRect.gameObject.AddComponent<Image>();
        playstyleChip.sprite = CharacterSelectUiFactory.LoadSprite("RoundedRect");
        playstyleChip.type = Image.Type.Sliced;
        playstyleChip.color = new Color(1f, 1f, 1f, 0.15f);
        playstyleChip.raycastTarget = false;
        TMP_Text playstyleText = CharacterSelectUiFactory.MakeText(playstyleRect, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "PLAYSTYLE", 18f, Color.white, TextAlignmentOptions.Center, true);

        RectTransform elementRect = CharacterSelectUiFactory.MakeRect(row2Rect, "ElementChip", Vector2.zero, Vector2.one, Center, Vector2.zero, new Vector2(130f, 44f));
        LayoutElement elementLe = elementRect.gameObject.AddComponent<LayoutElement>();
        elementLe.preferredWidth = 130f;
        elementLe.preferredHeight = 40f;
        CharacterSelectUiFactory.MakeImageStretch(elementRect, "Background", CharacterSelectUiFactory.LoadSprite("RoundedRect"), new Color(1f, 1f, 1f, 0.12f), Image.Type.Sliced);
        TMP_Text elementText = CharacterSelectUiFactory.MakeText(elementRect, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "ELEMENT", 18f, Color.white, TextAlignmentOptions.Center, true);

        // Resolve the layout now so the hierarchy looks right in the Scene view
        // immediately, without needing to enter Play mode first.
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

        LorePanel lorePanel = panelRect.gameObject.AddComponent<LorePanel>();
        CharacterSelectUiFactory.SetSerialized(lorePanel, "eyebrow", eyebrow);
        CharacterSelectUiFactory.SetSerialized(lorePanel, "placeholderTag", placeholderTag);
        CharacterSelectUiFactory.SetSerialized(lorePanel, "nameText", nameText);
        CharacterSelectUiFactory.SetSerialized(lorePanel, "titleText", titleText);
        CharacterSelectUiFactory.SetSerialized(lorePanel, "loreText", loreText);
        CharacterSelectUiFactory.SetSerialized(lorePanel, "playstyleText", playstyleText);
        CharacterSelectUiFactory.SetSerialized(lorePanel, "playstyleChip", playstyleChip);
        CharacterSelectUiFactory.SetSerialized(lorePanel, "elementText", elementText);
        CharacterSelectUiFactory.SetSerialized(lorePanel, "trim", trim);
        // Stagger order per contract: eyebrow row, name, title, lore, chips row.
        CharacterSelectUiFactory.SetSerializedArray(lorePanel, "lines", new Object[] { row1Group, nameGroup, titleGroup, loreGroup, row2Group });

        return lorePanel;
    }

    // ---------------------------------------------------------------------
    // RadarPanel
    // ---------------------------------------------------------------------

    private static void BuildRadarPanel(Transform uiRoot, out RadarChartGraphic radar, out RadarChartGraphic radarGlow, out TMP_Text overallText, out TMP_Text[] axisLabels)
    {
        RectTransform panelRect = CharacterSelectUiFactory.MakeRect(uiRoot, "RadarPanel", RightMiddle, RightMiddle, RightMiddle, new Vector2(-200f, 10f), new Vector2(400f, 400f));
        Transform panel = panelRect.transform;

        CharacterSelectUiFactory.MakeText(panel, "AttributesLabel", Center, Center, Center, new Vector2(0f, 180f), new Vector2(360f, 24f), "ATTRIBUTES", 16f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center, false, false, 10f);

        Vector2 radarCenter = new Vector2(0f, 30f);

        // Glow added first so the crisper Radar draws on top of it (later sibling = on top).
        radarGlow = MakeRadar(panel, "RadarGlow", radarCenter, RadarRadius, 10f, 0.08f, false, 0f);
        radarGlow.color = new Color(1f, 1f, 1f, 0.35f);

        radar = MakeRadar(panel, "Radar", radarCenter, RadarRadius, 3f, 0.22f, true, 5f);
        radar.color = Color.white;

        RectTransform axisLabelsRect = CharacterSelectUiFactory.MakeRect(panel, "AxisLabels", Center, Center, Center, radarCenter, new Vector2(240f, 240f));
        axisLabels = new TMP_Text[RadarChartGraphic.AxisCount];
        for (int i = 0; i < RadarChartGraphic.AxisCount; i++)
        {
            // Axis 0 straight up, clockwise - matches RadarChartGraphic.AxisDirection exactly.
            float angle = i * (Mathf.PI * 2f / RadarChartGraphic.AxisCount);
            Vector2 dir = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            Vector2 pos = dir * (RadarRadius + 26f);

            axisLabels[i] = CharacterSelectUiFactory.MakeText(axisLabelsRect, $"Axis{i}_{RadarChartGraphic.AxisLabels[i]}", Center, Center, Center, pos, new Vector2(60f, 24f), RadarChartGraphic.AxisLabels[i], 18f, new Color(1f, 1f, 1f, 0.7f), TextAlignmentOptions.Center, true);
        }

        overallText = CharacterSelectUiFactory.MakeText(panel, "OverallText", Center, Center, Center, new Vector2(0f, -110f), new Vector2(200f, 64f), "0.0", 56f, Color.white, TextAlignmentOptions.Center, true);
        CharacterSelectUiFactory.MakeText(panel, "OverallCaption", Center, Center, Center, new Vector2(0f, -155f), new Vector2(200f, 20f), "OVR", 14f, new Color(1f, 1f, 1f, 0.6f), TextAlignmentOptions.Center);
    }

    private static RadarChartGraphic MakeRadar(Transform parent, string name, Vector2 anchoredPos, float radius, float strokeWidth, float fillAlpha, bool drawGuides, float dotRadius)
    {
        RectTransform rt = CharacterSelectUiFactory.MakeRect(parent, name, Center, Center, Center, anchoredPos, new Vector2(240f, 240f));
        // AddComponent does not honour RequireComponent declared on a base class
        // (Graphic), and a Graphic with no CanvasRenderer renders nothing.
        if (!rt.gameObject.TryGetComponent<CanvasRenderer>(out _))
        {
            rt.gameObject.AddComponent<CanvasRenderer>();
        }
        RadarChartGraphic radar = rt.gameObject.AddComponent<RadarChartGraphic>();
        CharacterSelectUiFactory.SetSerialized(radar, "radius", radius);
        CharacterSelectUiFactory.SetSerialized(radar, "strokeWidth", strokeWidth);
        CharacterSelectUiFactory.SetSerialized(radar, "fillAlpha", fillAlpha);
        CharacterSelectUiFactory.SetSerialized(radar, "drawGuides", drawGuides);
        CharacterSelectUiFactory.SetSerialized(radar, "dotRadius", dotRadius);
        return radar;
    }

    // ---------------------------------------------------------------------
    // Carousel
    // ---------------------------------------------------------------------

    private static PortraitCarousel BuildCarousel(Transform uiRoot)
    {
        RectTransform carouselRect = CharacterSelectUiFactory.MakeRect(uiRoot, "Carousel", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, Vector2.zero);
        carouselRect.offsetMin = new Vector2(0f, 30f);
        carouselRect.offsetMax = new Vector2(-300f, 230f);
        Transform carousel = carouselRect.transform;

        // No mask: icons pop-scale beyond their own bounds and must not be clipped.
        RectTransform viewportRect = CharacterSelectUiFactory.MakeStretchRect(carousel, "Viewport");
        Image viewportImage = viewportRect.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0f);
        viewportImage.raycastTarget = true;

        RectTransform contentRect = CharacterSelectUiFactory.MakeRect(viewportRect, "Content", Center, Center, Center, Vector2.zero, new Vector2(10f, 10f));

        RectTransform templateRect = CharacterSelectUiFactory.MakeRect(contentRect, "IconTemplate", Center, Center, Center, Vector2.zero, new Vector2(10f, 10f));
        GameObject templateGo = templateRect.gameObject;

        RectTransform scaleTargetRect = CharacterSelectUiFactory.MakeRect(templateRect, "ScaleTarget", Center, Center, Center, Vector2.zero, new Vector2(120f, 120f));
        scaleTargetRect.gameObject.AddComponent<CanvasGroup>();

        Sprite dashedRingSprite = CharacterSelectUiFactory.LoadSprite("DashedRing");
        Image dashedRing = CharacterSelectUiFactory.MakeImage(scaleTargetRect, "DashedRing", Center, Center, Center, Vector2.zero, new Vector2(168f, 168f), dashedRingSprite, Color.white);
        dashedRing.gameObject.SetActive(false);

        Image ring = CharacterSelectUiFactory.MakeImage(scaleTargetRect, "Ring", Center, Center, Center, Vector2.zero, new Vector2(132f, 132f), CharacterSelectUiFactory.LoadSprite("Ring"), new Color(1f, 1f, 1f, 0.2f));
        Image disc = CharacterSelectUiFactory.MakeImage(scaleTargetRect, "Disc", Center, Center, Center, Vector2.zero, new Vector2(120f, 120f), CharacterSelectUiFactory.LoadSprite("Circle"), Color.white);

        Image iconImage = CharacterSelectUiFactory.MakeImage(scaleTargetRect, "IconImage", Center, Center, Center, Vector2.zero, new Vector2(108f, 108f), null, Color.white);
        iconImage.preserveAspect = true;
        iconImage.enabled = false; // PortraitIcon.Bind() re-enables it when def.Icon is set

        TMP_Text glyph = CharacterSelectUiFactory.MakeText(scaleTargetRect, "Glyph", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "?", 48f, Color.white, TextAlignmentOptions.Center, true);

        PortraitIcon iconTemplate = templateGo.AddComponent<PortraitIcon>();
        CharacterSelectUiFactory.SetSerialized(iconTemplate, "scaleTarget", scaleTargetRect);
        CharacterSelectUiFactory.SetSerialized(iconTemplate, "disc", disc);
        CharacterSelectUiFactory.SetSerialized(iconTemplate, "ring", ring);
        CharacterSelectUiFactory.SetSerialized(iconTemplate, "dashedRing", dashedRing);
        CharacterSelectUiFactory.SetSerialized(iconTemplate, "iconImage", iconImage);
        CharacterSelectUiFactory.SetSerialized(iconTemplate, "glyph", glyph);
        // `button` is deliberately left unset (null) per contract.

        templateGo.SetActive(false);

        PortraitCarousel carouselComp = carouselRect.gameObject.AddComponent<PortraitCarousel>();
        CharacterSelectUiFactory.SetSerialized(carouselComp, "viewport", viewportRect);
        CharacterSelectUiFactory.SetSerialized(carouselComp, "content", contentRect);
        CharacterSelectUiFactory.SetSerialized(carouselComp, "iconTemplate", iconTemplate);
        CharacterSelectUiFactory.SetSerialized(carouselComp, "step", CarouselStep);

        return carouselComp;
    }

    // ---------------------------------------------------------------------
    // ConfirmButton
    // ---------------------------------------------------------------------

    private static ConfirmButton BuildConfirmButton(Transform uiRoot)
    {
        RectTransform rootRect = CharacterSelectUiFactory.MakeRect(uiRoot, "ConfirmButton", BottomRight, BottomRight, BottomRight, new Vector2(-60f, 60f), new Vector2(320f, 96f));
        Transform root = rootRect.transform;

        Image glow = CharacterSelectUiFactory.MakeImage(root, "Glow", Center, Center, Center, Vector2.zero, new Vector2(420f, 220f), CharacterSelectUiFactory.LoadSprite("SoftCircle"), Color.white);

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

        TMP_Text label = CharacterSelectUiFactory.MakeText(root, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "CONFIRM", 30f, CharacterSelectUiFactory.HexColor("#0B0B12"), TextAlignmentOptions.Center, true, false, 8f);

        ConfirmButton confirmButton = rootRect.gameObject.AddComponent<ConfirmButton>();
        CharacterSelectUiFactory.SetSerialized(confirmButton, "button", button);
        // The whole control (glow + background + label) squashes/pulses together as one unit.
        CharacterSelectUiFactory.SetSerialized(confirmButton, "scaleTarget", rootRect);
        CharacterSelectUiFactory.SetSerialized(confirmButton, "background", background);
        CharacterSelectUiFactory.SetSerialized(confirmButton, "glow", glow);
        CharacterSelectUiFactory.SetSerialized(confirmButton, "label", label);

        return confirmButton;
    }

    // ---------------------------------------------------------------------
    // FlashOverlay + Toast
    // ---------------------------------------------------------------------

    private static void BuildFlashAndToast(Transform uiRoot, out Image flashOverlay, out CanvasGroup toastGroup, out TMP_Text toastText)
    {
        RectTransform flashRect = CharacterSelectUiFactory.MakeStretchRect(uiRoot, "FlashOverlay");
        flashOverlay = flashRect.gameObject.AddComponent<Image>();
        flashOverlay.color = new Color(1f, 1f, 1f, 0f);
        flashOverlay.raycastTarget = false;

        RectTransform toastRect = CharacterSelectUiFactory.MakeRect(uiRoot, "Toast", TopCenter, TopCenter, TopCenter, new Vector2(0f, -130f), new Vector2(520f, 64f));
        CharacterSelectUiFactory.MakeImageStretch(toastRect, "Background", CharacterSelectUiFactory.LoadSprite("RoundedRect"), Glass, Image.Type.Sliced);
        toastText = CharacterSelectUiFactory.MakeText(toastRect, "Label", Vector2.zero, Vector2.one, Center, Vector2.zero, Vector2.zero, "LOCKED IN", 22f, Color.white, TextAlignmentOptions.Center);

        toastGroup = toastRect.gameObject.AddComponent<CanvasGroup>();
        toastGroup.alpha = 0f;
    }
}
