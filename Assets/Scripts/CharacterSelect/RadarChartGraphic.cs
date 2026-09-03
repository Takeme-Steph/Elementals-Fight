using UnityEngine;
using UnityEngine.UI;

// Hand-built radar (spider) chart: MaskableGraphic subclass that draws its own mesh
// (guide rings, spokes, a filled data polygon, its outline and vertex dots) instead of
// composing child Images, so it can be redrawn cheaply every frame while the values
// spring toward a new target.

/// <summary>
/// A 5-axis radar chart drawn as a single UI mesh. Values spring toward their target
/// and the mesh is only rebuilt while that spring is still moving.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class RadarChartGraphic : MaskableGraphic
{
    public const int AxisCount = 5;
    public static readonly string[] AxisLabels = { "PWR", "SPD", "RNG", "DEF", "MOB" };

    [SerializeField]
    [Tooltip("Outer radius of the chart, in local (canvas) units.")]
    private float radius = 70f;

    [SerializeField]
    [Tooltip("Number of concentric guide rings drawn between the centre and the outer radius.")]
    private int guideRings = 3;

    [SerializeField]
    private bool drawGuides = true;

    [SerializeField]
    private Color guideColor = new Color(1f, 1f, 1f, 0.15f);

    [SerializeField]
    [Tooltip("Thickness, in local units, of the data outline and guide lines.")]
    private float strokeWidth = 2f;

    [SerializeField]
    [Tooltip("Alpha of the filled data polygon, relative to this.color's own alpha.")]
    private float fillAlpha = 0.25f;

    [SerializeField]
    private float dotRadius = 4f;

    [SerializeField]
    private float stiffness = 120f;

    [SerializeField]
    private float damping = 16f;

    private float[] current;
    private float[] target;
    private float[] velocity;
    private Vector2[] pointsScratch;
    private bool settled = true;

    protected override void Awake()
    {
        base.Awake();

        // Off by default: a radar chart is decorative, not interactive, and leaving
        // raycastTarget on would let it eat pointer input meant for widgets behind it.
        raycastTarget = false;

        current = new float[AxisCount];
        target = new float[AxisCount];
        velocity = new float[AxisCount];
        pointsScratch = new Vector2[AxisCount];
    }

    /// <summary>Copies normalized (0..1) values into the spring target; instant skips the spring.</summary>
    public void SetValues(float[] normalized, bool instant)
    {
        if (normalized == null || normalized.Length != AxisCount)
        {
            Debug.LogError("RadarChartGraphic.SetValues: expected an array of length AxisCount.");
            return;
        }

        for (int i = 0; i < AxisCount; i++)
        {
            target[i] = Mathf.Clamp01(normalized[i]);
        }

        if (instant)
        {
            for (int i = 0; i < AxisCount; i++)
            {
                current[i] = target[i];
                velocity[i] = 0f;
            }

            settled = true;
            SetVerticesDirty();
        }
        else
        {
            // The Update loop early-outs once settled == true; clear it so this new
            // target actually gets animated toward. NOTE: this must stay a plain bool,
            // not MonoBehaviour.enabled - Graphic.OnDisable() calls
            // canvasRenderer.Clear(), so disabling the component would blank the chart
            // the moment it settles instead of just skipping its Update().
            settled = false;
        }
    }

    /// <summary>Sets the stroke/fill/dot colour (this.color).</summary>
    public void SetAccent(Color c)
    {
        color = c;
    }

    /// <summary>Unit direction for axis i: axis 0 points straight up, indices proceed clockwise.</summary>
    public Vector2 AxisDirection(int i)
    {
        float angle = i * (Mathf.PI * 2f / AxisCount);
        return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
    }

    /// <summary>Local position for axis i's label, padding units outside the outer ring.</summary>
    public Vector2 AxisLabelPosition(int i, float padding)
    {
        return AxisDirection(i) * (radius + padding);
    }

    private void Update()
    {
        if (settled)
        {
            return;
        }

        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
        bool allSettled = true;

        for (int i = 0; i < AxisCount; i++)
        {
            current[i] = UiSpring.Step(current[i], target[i], ref velocity[i], stiffness, damping, dt);
            if (!UiSpring.Settled(current[i], target[i], velocity[i]))
            {
                allSettled = false;
            }
        }

        if (allSettled)
        {
            for (int i = 0; i < AxisCount; i++)
            {
                current[i] = target[i];
                velocity[i] = 0f;
            }

            // Nothing left to animate - skip the mesh rebuild on future frames until
            // SetValues() clears this again.
            settled = true;
        }

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (current == null)
        {
            // Can be hit once by the UI system before Awake runs in edit mode.
            return;
        }

        if (drawGuides)
        {
            for (int i = 0; i < AxisCount; i++)
            {
                AddLine(vh, Vector2.zero, AxisDirection(i) * radius, 1f, guideColor);
            }

            for (int ring = 1; ring <= guideRings; ring++)
            {
                float r = radius * ring / Mathf.Max(1f, guideRings);
                for (int i = 0; i < AxisCount; i++)
                {
                    Vector2 a = AxisDirection(i) * r;
                    Vector2 b = AxisDirection((i + 1) % AxisCount) * r;
                    AddLine(vh, a, b, 1f, guideColor);
                }
            }
        }

        for (int i = 0; i < AxisCount; i++)
        {
            pointsScratch[i] = AxisDirection(i) * (radius * current[i]);
        }

        Color32 fillColor = color;
        fillColor.a = (byte)Mathf.RoundToInt(color.a * fillAlpha * 255f);
        int centerIdx = vh.currentVertCount;
        AddVertex(vh, Vector2.zero, fillColor);
        for (int i = 0; i < AxisCount; i++)
        {
            AddVertex(vh, pointsScratch[i], fillColor);
        }
        for (int i = 0; i < AxisCount; i++)
        {
            vh.AddTriangle(centerIdx, centerIdx + 1 + i, centerIdx + 1 + ((i + 1) % AxisCount));
        }

        Color32 strokeColor = color;
        for (int i = 0; i < AxisCount; i++)
        {
            AddLine(vh, pointsScratch[i], pointsScratch[(i + 1) % AxisCount], strokeWidth, strokeColor);
        }

        for (int i = 0; i < AxisCount; i++)
        {
            AddDot(vh, pointsScratch[i], dotRadius, strokeColor);
        }
    }

    /// <summary>Adds a strokeWidth-thick quad from a to b.</summary>
    private void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color32 color)
    {
        Vector2 dir = b - a;
        if (dir.sqrMagnitude < 1e-8f)
        {
            return;
        }

        dir.Normalize();
        Vector2 normal = new Vector2(-dir.y, dir.x) * (width * 0.5f);

        int idx = vh.currentVertCount;
        AddVertex(vh, a - normal, color);
        AddVertex(vh, a + normal, color);
        AddVertex(vh, b + normal, color);
        AddVertex(vh, b - normal, color);
        vh.AddTriangle(idx, idx + 1, idx + 2);
        vh.AddTriangle(idx, idx + 2, idx + 3);
    }

    /// <summary>Adds a small filled octagon centred on centre, used for the axis vertex markers.</summary>
    private void AddDot(VertexHelper vh, Vector2 centre, float r, Color32 color)
    {
        const int sides = 8;
        int start = vh.currentVertCount;
        AddVertex(vh, centre, color);
        for (int i = 0; i <= sides; i++)
        {
            float angle = i / (float)sides * Mathf.PI * 2f;
            Vector2 p = centre + new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * r;
            AddVertex(vh, p, color);
        }
        for (int i = 0; i < sides; i++)
        {
            vh.AddTriangle(start, start + 1 + i, start + 2 + i);
        }
    }

    private static void AddVertex(VertexHelper vh, Vector2 pos, Color32 color)
    {
        UIVertex v = UIVertex.simpleVert;
        v.position = pos;
        v.color = color;
        vh.AddVert(v);
    }
}
