using UnityEngine;

// Instantiates one (inactive) display model per roster entry under modelsRoot and
// toggles between them as the carousel selection changes, with a small pop-in spring
// and a continuous idle bob so the stage never looks static.

/// <summary>
/// Owns the 3D "pedestal" that shows the currently selected fighter's display model.
/// </summary>
public class DeityStage : MonoBehaviour
{
    // Pop-in spring constants (not exposed - the proto uses this exact feel for the
    // stage model reveal, see CharacterSelect.jsx stiffness:260 damping:20).
    private const float PopStiffness = 260f;
    private const float PopDamping = 20f;
    private const float StartScaleMul = 0.82f;
    private const float StartYOffset = -0.3f;

    [SerializeField]
    [Tooltip("Camera the stage is framed for; used to convert viewportAnchor into a world position.")]
    private Camera stageCamera;

    [SerializeField]
    [Tooltip("Parent transform that instantiated display models are placed under.")]
    private Transform modelsRoot;

    [SerializeField]
    [Tooltip("Viewport-space point (0-1) where the model's feet should land.")]
    private Vector2 viewportAnchor = new Vector2(0.65f, 0.16f);

    [SerializeField]
    [Tooltip("How far the idle bob moves the model up/down, in world units.")]
    private float idleBobAmplitude = 0.04f;

    [SerializeField]
    [Tooltip("Seconds for one full idle bob cycle.")]
    private float idleBobPeriod = 3.5f;

    private GameObject[] models;
    private Vector3[] baseScales;
    private int currentIndex = -1;

    private float scaleMul;
    private float scaleVel;
    private float yOffset;
    private float yVel;
    private bool popSettled = true;

    private int lastScreenWidth;
    private int lastScreenHeight;

    /// <summary>Instantiates (inactive) one display model per roster entry, in roster order.</summary>
    public void Build(CharacterRoster roster)
    {
        if (roster == null)
        {
            Debug.LogError("DeityStage.Build: roster is null.");
            return;
        }

        if (modelsRoot == null)
        {
            Debug.LogError("DeityStage.Build: modelsRoot is not assigned.");
            return;
        }

        int count = roster.Count;
        models = new GameObject[count];
        baseScales = new Vector3[count];

        for (int i = 0; i < count; i++)
        {
            CharacterDefinition def = roster.Get(i);
            GameObject instance;

            if (def != null && def.DisplayPrefab != null)
            {
                instance = Instantiate(def.DisplayPrefab, modelsRoot);
                // Display prefabs face +Z away from the stage camera at -Z; the 180
                // turns them to face the camera instead.
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

                // Display prefabs ship with an Animator but no controller, which leaves
                // the model frozen in bind pose (a T-pose on the Mixamo rigs). Borrow the
                // fight controller: its entry state is Idle and nothing here ever sets a
                // parameter, so the model simply idles. Root motion stays off so the idle
                // clip cannot walk the model off its pedestal.
                if (def.DisplayAnimator != null && instance.TryGetComponent(out Animator animator) && animator.runtimeAnimatorController == null)
                {
                    animator.runtimeAnimatorController = def.DisplayAnimator;
                    animator.applyRootMotion = false;
                }
                // PrefabUtility isn't available at runtime, so the instantiated copy's
                // own localScale (whatever the prefab authored) is what we keep.
                baseScales[i] = instance.transform.localScale;
            }
            else
            {
                // No art yet for this fighter - a tinted capsule keeps the roster
                // browsable instead of showing nothing.
                instance = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                instance.transform.SetParent(modelsRoot, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
                instance.transform.localScale = new Vector3(1f, 1.7f, 1f);
                baseScales[i] = instance.transform.localScale;

                if (instance.TryGetComponent(out Collider col))
                {
                    Destroy(col);
                }

                Color tint = def != null ? def.Primary : Color.white;
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", tint); // URP Lit exposes _BaseColor; Material.color maps to _Color which URP Lit does not have

                if (instance.TryGetComponent(out Renderer rend))
                {
                    rend.sharedMaterial = mat;
                }
            }

            instance.name = def != null ? $"Display_{def.DisplayName}" : $"Display_{i}";
            instance.SetActive(false);
            models[i] = instance;
        }
    }

    /// <summary>Activates the model at index, deactivating whatever was showing before.</summary>
    public void Show(int index, bool instant)
    {
        if (models == null || index < 0 || index >= models.Length)
        {
            Debug.LogError($"DeityStage.Show: index {index} is out of range.");
            return;
        }

        if (currentIndex >= 0 && currentIndex < models.Length && currentIndex != index && models[currentIndex] != null)
        {
            models[currentIndex].SetActive(false);
        }

        currentIndex = index;
        GameObject model = models[index];
        model.SetActive(true);

        if (instant)
        {
            scaleMul = 1f;
            yOffset = 0f;
            popSettled = true;
        }
        else
        {
            scaleMul = StartScaleMul;
            yOffset = StartYOffset;
            popSettled = false;
        }

        scaleVel = 0f;
        yVel = 0f;
        model.transform.localScale = baseScales[index] * scaleMul;
        model.transform.localPosition = new Vector3(0f, yOffset, 0f);
    }

    /// <summary>Moves modelsRoot so its local origin sits at viewportAnchor on the z = 0 plane.</summary>
    public void PositionRoot()
    {
        if (stageCamera == null || modelsRoot == null)
        {
            Debug.LogError("DeityStage.PositionRoot: stageCamera or modelsRoot is not assigned.");
            return;
        }

        float z = Mathf.Abs(stageCamera.transform.position.z);
        Vector3 p = stageCamera.ViewportToWorldPoint(new Vector3(viewportAnchor.x, viewportAnchor.y, z));
        modelsRoot.position = new Vector3(p.x, p.y, 0f);
    }

    private void Start()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;
        PositionRoot();
    }

    private void Update()
    {
        // Cheap int compare stands in for OnRectTransformDimensionsChange, which this
        // MonoBehaviour (not a UI element) never receives.
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
            PositionRoot();
        }

        if (currentIndex < 0 || models == null)
        {
            return;
        }

        GameObject model = models[currentIndex];
        if (model == null)
        {
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

        if (!popSettled)
        {
            scaleMul = UiSpring.Step(scaleMul, 1f, ref scaleVel, PopStiffness, PopDamping, dt);
            yOffset = UiSpring.Step(yOffset, 0f, ref yVel, PopStiffness, PopDamping, dt);
            model.transform.localScale = baseScales[currentIndex] * scaleMul;

            if (UiSpring.Settled(scaleMul, 1f, scaleVel) && UiSpring.Settled(yOffset, 0f, yVel))
            {
                popSettled = true;
                scaleMul = 1f;
                yOffset = 0f;
                model.transform.localScale = baseScales[currentIndex];
            }
        }

        // Idle bob layers on top once the pop-in has settled so it doesn't fight the spring.
        float bob = popSettled ? Mathf.Sin(Time.unscaledTime * (Mathf.PI * 2f / Mathf.Max(idleBobPeriod, 0.01f))) * idleBobAmplitude : 0f;
        model.transform.localPosition = new Vector3(0f, yOffset + bob, 0f);
    }
}
