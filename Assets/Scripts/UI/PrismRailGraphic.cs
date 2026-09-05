using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Low-cost UGUI mesh used by the loading rail. It draws a six-sided prism and can
/// progressively reveal an animated horizontal gradient without shaders or textures.
/// Keeping the silhouette in geometry means it remains crisp from 16:9 through 21:9.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class PrismRailGraphic : MaskableGraphic
{
    private const int GradientSlices = 28;

    private float bevel = 16f;
    private float progress = 1f;
    private float phase;
    private bool useGradient;
    private bool outlineOnly;
    private float outlineThickness = 2f;
    private Color gradientStart = Color.white;
    private Color gradientEnd = Color.white;
    private readonly List<Vector2> polygon = new(8);
    private readonly List<Vector2> scratch = new(8);

    public void Configure(float bevelPixels, float initialProgress)
    {
        bevel = Mathf.Max(0f, bevelPixels);
        progress = Mathf.Clamp01(initialProgress);
        SetVerticesDirty();
    }

    public void ConfigureGradient(Color start, Color end)
    {
        gradientStart = start;
        gradientEnd = end;
        useGradient = true;
        SetVerticesDirty();
    }

    public void ConfigureOutline(float thickness)
    {
        outlineOnly = true;
        outlineThickness = Mathf.Max(0.5f, thickness);
        SetVerticesDirty();
    }

    public void SetProgress(float value, float animationPhase)
    {
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(progress, value) && Mathf.Abs(phase - animationPhase) < 0.02f)
        {
            return;
        }

        progress = value;
        phase = animationPhase;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        if (progress <= 0f)
        {
            return;
        }

        Rect rect = GetPixelAdjustedRect();
        float safeBevel = Mathf.Min(bevel, Mathf.Min(rect.width * 0.2f, rect.height * 0.5f));
        float revealX = Mathf.Lerp(rect.xMin, rect.xMax, progress);

        if (outlineOnly)
        {
            AddOutline(vertexHelper, rect, safeBevel);
            return;
        }

        if (!useGradient)
        {
            BuildPrism(rect, safeBevel, polygon);
            ClipAtMaximumX(polygon, revealX, scratch);
            AddPolygon(vertexHelper, scratch, rect);
            return;
        }

        // Several narrow convex slices provide enough vertices for a rich gradient
        // while remaining tiny compared with the particle and TMP geometry on screen.
        for (int slice = 0; slice < GradientSlices; slice++)
        {
            float x0 = Mathf.Lerp(rect.xMin, revealX, slice / (float)GradientSlices);
            float x1 = Mathf.Lerp(rect.xMin, revealX, (slice + 1f) / GradientSlices);
            BuildPrism(rect, safeBevel, polygon);
            ClipAtMinimumX(polygon, x0, scratch);
            ClipAtMaximumX(scratch, x1, polygon);
            AddPolygon(vertexHelper, polygon, rect);
        }
    }

    private void AddOutline(VertexHelper vertexHelper, Rect rect, float safeBevel)
    {
        float thickness = Mathf.Min(outlineThickness, rect.height * 0.22f);
        Rect innerRect = new Rect(
            rect.xMin + thickness,
            rect.yMin + thickness,
            Mathf.Max(0f, rect.width - thickness * 2f),
            Mathf.Max(0f, rect.height - thickness * 2f));
        float innerBevel = Mathf.Max(0f, safeBevel - thickness);

        BuildPrism(rect, safeBevel, polygon);
        BuildPrism(innerRect, innerBevel, scratch);
        for (int i = 0; i < polygon.Count; i++)
        {
            int next = (i + 1) % polygon.Count;
            int first = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, polygon[i], rect);
            AddVertex(vertexHelper, polygon[next], rect);
            AddVertex(vertexHelper, scratch[next], rect);
            AddVertex(vertexHelper, scratch[i], rect);
            vertexHelper.AddTriangle(first, first + 1, first + 2);
            vertexHelper.AddTriangle(first, first + 2, first + 3);
        }
    }

    private void AddVertex(VertexHelper vertexHelper, Vector2 point, Rect rect)
    {
        Color32 vertexColor = EvaluateColor(Mathf.InverseLerp(rect.xMin, rect.xMax, point.x));
        vertexHelper.AddVert(point, vertexColor, Vector2.zero);
    }

    private void AddPolygon(VertexHelper vertexHelper, List<Vector2> points, Rect rect)
    {
        if (points.Count < 3)
        {
            return;
        }

        int first = vertexHelper.currentVertCount;
        for (int i = 0; i < points.Count; i++)
        {
            Vector2 point = points[i];
            Color32 vertexColor = EvaluateColor(Mathf.InverseLerp(rect.xMin, rect.xMax, point.x));
            vertexHelper.AddVert(point, vertexColor, Vector2.zero);
        }

        for (int i = 1; i < points.Count - 1; i++)
        {
            vertexHelper.AddTriangle(first, first + i, first + i + 1);
        }
    }

    private Color EvaluateColor(float normalizedX)
    {
        if (!useGradient)
        {
            return color;
        }

        float flowingBand = 0.5f + 0.5f * Mathf.Sin(normalizedX * 15f - phase * 5f);
        float ramp = Mathf.SmoothStep(0f, 1f, normalizedX);
        Color result = Color.Lerp(gradientStart, gradientEnd, Mathf.Clamp01(ramp * 0.82f + flowingBand * 0.18f));
        float tipDistance = Mathf.Abs(normalizedX - progress);
        float tipGlow = 1f - Mathf.Clamp01(tipDistance / 0.075f);
        return Color.Lerp(result, new Color(1f, 1f, 1f, result.a), tipGlow * tipGlow * 0.72f) * color;
    }

    private static void BuildPrism(Rect rect, float bevelPixels, List<Vector2> output)
    {
        output.Clear();
        float centreY = rect.center.y;
        output.Add(new Vector2(rect.xMin + bevelPixels, rect.yMin));
        output.Add(new Vector2(rect.xMax - bevelPixels, rect.yMin));
        output.Add(new Vector2(rect.xMax, centreY));
        output.Add(new Vector2(rect.xMax - bevelPixels, rect.yMax));
        output.Add(new Vector2(rect.xMin + bevelPixels, rect.yMax));
        output.Add(new Vector2(rect.xMin, centreY));
    }

    private static void ClipAtMaximumX(List<Vector2> input, float maximumX, List<Vector2> output)
    {
        ClipVertical(input, maximumX, true, output);
    }

    private static void ClipAtMinimumX(List<Vector2> input, float minimumX, List<Vector2> output)
    {
        ClipVertical(input, minimumX, false, output);
    }

    private static void ClipVertical(List<Vector2> input, float boundaryX, bool keepLess, List<Vector2> output)
    {
        // Sutherland-Hodgman clipping keeps the charged leading edge vertical while
        // retaining the prism taper at either end of the usable rail.
        output.Clear();
        if (input.Count == 0)
        {
            return;
        }

        Vector2 previous = input[input.Count - 1];
        bool previousInside = keepLess ? previous.x <= boundaryX : previous.x >= boundaryX;
        for (int i = 0; i < input.Count; i++)
        {
            Vector2 current = input[i];
            bool currentInside = keepLess ? current.x <= boundaryX : current.x >= boundaryX;
            if (currentInside != previousInside)
            {
                float denominator = current.x - previous.x;
                float t = Mathf.Abs(denominator) > 0.0001f ? (boundaryX - previous.x) / denominator : 0f;
                output.Add(Vector2.LerpUnclamped(previous, current, t));
            }
            if (currentInside)
            {
                output.Add(current);
            }
            previous = current;
            previousInside = currentInside;
        }
    }
}
