using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Generates every white/alpha sprite ArenaSelect needs beyond what CharacterSelect
// already provides (Ring/Circle/RoundedRect/SoftCircle/GradientV/... are reused as-is
// via ArenaSelectUiFactory.LoadSprite's fallback). Same pipeline and conventions as
// CharacterSelectArtGenerator: everything is procedural, drawn white RGB with the
// shape baked into alpha only, so a plain Image.color tint is all any caller needs to
// recolour one. Mirrors that file's math helpers rather than referencing its private
// ones directly (they're private to that class, and duplicating a handful of small
// smoothstep-based helpers is simpler than exposing them just for this).
public static class ArenaSelectArtGenerator
{
    private const string SpritesFolder = "Assets/UI/ArenaSelect/Sprites";

    // Antialiasing width in pixels for every hard edge below - same "smoothstep over
    // 1-1.5px" contract as CharacterSelectArtGenerator.
    private const float Aa = 1.25f;

    [MenuItem("Elementals Fight/Arena Select/1 - Generate Sprites")]
    public static void GenerateSpritesMenu()
    {
        GenerateAll();
    }

    public static void GenerateAll()
    {
        CharacterSelectUiFactory.EnsureFolder(SpritesFolder);

        WriteSprite("RuneBifrost", 128, 128, PixelRuneBifrost);
        WriteSprite("RuneDuat", 128, 128, PixelRuneDuat);
        WriteSprite("RuneOlympus", 128, 128, PixelRuneOlympus);

        WriteSprite("HazardFlame", 128, 128, PixelHazardFlame);
        WriteSprite("HazardWhirlwind", 128, 128, PixelHazardWhirlwind);
        WriteSprite("HazardLightning", 128, 128, PixelHazardLightning);
        WriteSprite("HazardSandstorm", 128, 128, PixelHazardSandstorm);
        WriteSprite("HazardTide", 128, 128, PixelHazardTide);
        WriteSprite("HazardFrost", 128, 128, PixelHazardFrost);
        WriteSprite("HazardVoid", 128, 128, PixelHazardVoid);

        WriteSprite("Band", 256, 128, PixelBand);
        WriteSprite("Vignette", 256, 256, PixelVignette);

        AssetDatabase.SaveAssets();
        Debug.Log("ArenaSelectArtGenerator: generated 12 sprites into " + SpritesFolder);
    }

    // ---------------------------------------------------------------------
    // Texture -> PNG -> Sprite import pipeline
    // ---------------------------------------------------------------------

    private static void WriteSprite(string name, int w, int h, Func<int, int, int, int, Color32> pixelFn)
    {
        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[w * h];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                pixels[y * w + x] = pixelFn(x, y, w, h);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply(false, false);

        byte[] png = tex.EncodeToPNG();
        string path = $"{SpritesFolder}/{name}.png";
        File.WriteAllBytes(path, png);
        UnityEngine.Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path);

        TextureImporter ti = (TextureImporter)AssetImporter.GetAtPath(path);
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.sRGBTexture = true;
        ti.filterMode = FilterMode.Bilinear;
        ti.textureCompression = TextureImporterCompression.Compressed;
        ti.maxTextureSize = 256;
        ti.spriteBorder = Vector4.zero;
        ti.spritePixelsPerUnit = 100;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.SaveAndReimport();
    }

    // ---------------------------------------------------------------------
    // Shared math (mirrors CharacterSelectArtGenerator's private helpers)
    // ---------------------------------------------------------------------

    private static float Smoothstep(float edge0, float edge1, float x)
    {
        if (Mathf.Approximately(edge0, edge1))
        {
            return x < edge0 ? 0f : 1f;
        }

        float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private static Color32 White(float alpha01)
    {
        byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha01) * 255f);
        return new Color32(255, 255, 255, a);
    }

    /// <summary>1 fully inside radius R, 0 fully outside, smoothed across `aa` centred on R.</summary>
    private static float DiscMask(float dist, float r, float aa)
    {
        return 1f - Smoothstep(r - aa, r + aa, dist);
    }

    /// <summary>1 inside the [inner, outer] band, 0 elsewhere, smoothed edges - an antialiased ring.</summary>
    private static float BandMask(float dist, float inner, float outer, float aa)
    {
        float outerEdge = 1f - Smoothstep(outer - aa, outer + aa, dist);
        float innerEdge = Smoothstep(inner - aa, inner + aa, dist);
        return outerEdge * innerEdge;
    }

    /// <summary>Axis-aligned box mask (SDF-style): 1 inside [x0,x1]x[y0,y1], smoothed at the edges.</summary>
    private static float RectMask(float px, float py, float x0, float y0, float x1, float y1, float aa)
    {
        float dx = Mathf.Max(x0 - px, px - x1);
        float dy = Mathf.Max(y0 - py, py - y1);
        float d = Mathf.Max(dx, dy);
        return 1f - Smoothstep(-aa, aa, d);
    }

    /// <summary>Shortest distance from point (px,py) to the segment a-b.</summary>
    private static float SegmentDist(float px, float py, float ax, float ay, float bx, float by)
    {
        float abx = bx - ax, aby = by - ay;
        float apx = px - ax, apy = py - ay;
        float ab2 = abx * abx + aby * aby;
        float t = ab2 > 0.0001f ? Mathf.Clamp01((apx * abx + apy * aby) / ab2) : 0f;
        float cx = ax + abx * t, cy = ay + aby * t;
        float dx = px - cx, dy = py - cy;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>1 within `thickness` of the segment a-b, smoothed edge - a drawn stroke.</summary>
    private static float StrokeMask(float px, float py, float ax, float ay, float bx, float by, float thickness, float aa)
    {
        float d = SegmentDist(px, py, ax, ay, bx, by);
        return 1f - Smoothstep(thickness - aa, thickness + aa, d);
    }

    /// <summary>1 within `thickness` of a sine wave y = centerV + amplitude*sin(u*2pi*freq), in 0..1 UV space.</summary>
    private static float WaveLineMask(float u, float v, float centerV, float amplitude, float freq, float thickness, float aaNorm)
    {
        float wave = centerV + amplitude * Mathf.Sin(u * Mathf.PI * 2f * freq);
        float d = Mathf.Abs(v - wave);
        return 1f - Smoothstep(thickness - aaNorm, thickness + aaNorm, d);
    }

    // ---------------------------------------------------------------------
    // Runes - simple line-art, tinted accent on the active tab at runtime.
    // ---------------------------------------------------------------------

    // Three concentric arcs bowing up from a point below the canvas - a rainbow-bridge
    // silhouette read at a glance, not a literal render of Bifrost.
    private static Color32 PixelRuneBifrost(int x, int y, int w, int h)
    {
        float cx = w * 0.5f, cy = -h * 0.15f;
        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);

        float thickness = h * 0.065f;
        float a = 0f;
        a = Mathf.Max(a, BandMask(dist, h * 0.95f - thickness, h * 0.95f, Aa));
        a = Mathf.Max(a, BandMask(dist, h * 0.78f - thickness, h * 0.78f, Aa));
        a = Mathf.Max(a, BandMask(dist, h * 0.61f - thickness, h * 0.61f, Aa));

        return White(Mathf.Clamp01(a));
    }

    // Ankh: a ring loop over a vertical bar with a horizontal crossbar.
    private static Color32 PixelRuneDuat(int x, int y, int w, int h)
    {
        float cx = w * 0.5f;
        float px = x + 0.5f, py = y + 0.5f;

        float loopCy = h * 0.72f;
        float loopR = w * 0.20f;
        float loopThickness = w * 0.085f;
        float loopDist = Mathf.Sqrt((px - cx) * (px - cx) + (py - loopCy) * (py - loopCy));
        float a = BandMask(loopDist, loopR - loopThickness, loopR, Aa);

        float barHalf = w * 0.06f;
        a = Mathf.Max(a, RectMask(px, py, cx - barHalf, 0f, cx + barHalf, h * 0.60f, Aa));

        float crossHalf = w * 0.30f;
        a = Mathf.Max(a, RectMask(px, py, cx - crossHalf, h * 0.40f, cx + crossHalf, h * 0.50f, Aa));

        return White(Mathf.Clamp01(a));
    }

    // Lightning-bolt zigzag: two joined segments through the canvas.
    private static Color32 PixelRuneOlympus(int x, int y, int w, int h)
    {
        float px = x + 0.5f, py = y + 0.5f;
        float thickness = w * 0.075f;

        float a = StrokeMask(px, py, w * 0.62f, h * 1f, w * 0.30f, h * 0.55f, thickness, Aa);
        a = Mathf.Max(a, StrokeMask(px, py, w * 0.30f, h * 0.55f, w * 0.55f, h * 0.55f, thickness, Aa));
        a = Mathf.Max(a, StrokeMask(px, py, w * 0.55f, h * 0.55f, w * 0.20f, h * 0f, thickness, Aa));

        return White(Mathf.Clamp01(a));
    }

    // ---------------------------------------------------------------------
    // Hazard badge icons - simple pictograms, tinted accent at runtime.
    // ---------------------------------------------------------------------

    private static Color32 PixelHazardFlame(int x, int y, int w, int h)
    {
        float u = (x + 0.5f) / w;
        float v = (y + 0.5f) / h;
        float aaN = Aa / w;

        // Width narrows toward the top with a gentle wobble - a simple flame
        // silhouette, not a literal render.
        float widthAtV = Mathf.Lerp(0.32f, 0.02f, Mathf.Pow(v, 0.6f)) * (1f + 0.18f * Mathf.Sin(v * Mathf.PI * 3f));
        float dx = Mathf.Abs(u - 0.5f);
        float edge = widthAtV - dx;
        float a = Smoothstep(-aaN, aaN, edge) * Smoothstep(0f, 0.05f, v);

        return White(Mathf.Clamp01(a));
    }

    private static Color32 PixelHazardWhirlwind(int x, int y, int w, int h)
    {
        float cx = w * 0.5f, cy = h * 0.5f;
        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        float maxR = w * 0.46f;

        if (dist > maxR + Aa)
        {
            return White(0f);
        }

        float angle = Mathf.Atan2(dx, dy);
        if (angle < 0f) { angle += Mathf.PI * 2f; }

        const float spiralTurns = 1.6f;
        float t = angle / (Mathf.PI * 2f) + dist / Mathf.Max(maxR, 0.0001f) * spiralTurns;
        float frac = t - Mathf.Floor(t);
        float band = Mathf.Abs(frac - 0.5f) * 2f; // triangle wave, 0 at band centre, 1 at edges

        float aaN = Aa / maxR;
        float a = 1f - Smoothstep(0.28f - aaN, 0.28f + aaN, band);
        a *= 1f - Smoothstep(maxR - Aa, maxR + Aa, dist);

        return White(Mathf.Clamp01(a));
    }

    private static Color32 PixelHazardLightning(int x, int y, int w, int h)
    {
        float px = x + 0.5f, py = y + 0.5f;
        float thickness = w * 0.09f;

        float a = StrokeMask(px, py, w * 0.60f, h * 0.95f, w * 0.32f, h * 0.55f, thickness, Aa);
        a = Mathf.Max(a, StrokeMask(px, py, w * 0.32f, h * 0.55f, w * 0.58f, h * 0.55f, thickness, Aa));
        a = Mathf.Max(a, StrokeMask(px, py, w * 0.58f, h * 0.55f, w * 0.22f, h * 0.05f, thickness, Aa));

        return White(Mathf.Clamp01(a));
    }

    private static Color32 PixelHazardSandstorm(int x, int y, int w, int h)
    {
        float u = (x + 0.5f) / w;
        float v = (y + 0.5f) / h;
        float aaN = Aa / w;
        const float thickness = 0.045f;

        float a = 0f;
        a = Mathf.Max(a, WaveLineMask(u, v, 0.28f, 0.045f, 1.4f, thickness, aaN));
        a = Mathf.Max(a, WaveLineMask(u, v, 0.52f, 0.055f, 1.1f, thickness, aaN));
        a = Mathf.Max(a, WaveLineMask(u, v, 0.76f, 0.04f, 1.7f, thickness, aaN));

        return White(Mathf.Clamp01(a));
    }

    private static Color32 PixelHazardTide(int x, int y, int w, int h)
    {
        float u = (x + 0.5f) / w;
        float v = (y + 0.5f) / h;
        float aaN = Aa / w;
        const float thickness = 0.05f;

        float a = 0f;
        a = Mathf.Max(a, WaveLineMask(u, v, 0.40f, 0.09f, 2.1f, thickness, aaN));
        a = Mathf.Max(a, WaveLineMask(u, v, 0.64f, 0.09f, 2.1f, thickness, aaN));

        return White(Mathf.Clamp01(a));
    }

    private static Color32 PixelHazardFrost(int x, int y, int w, int h)
    {
        float cx = w * 0.5f, cy = h * 0.5f;
        float px = x + 0.5f - cx, py = y + 0.5f - cy;
        float r = Mathf.Sqrt(px * px + py * py);
        float maxR = w * 0.42f;

        if (r > maxR + Aa)
        {
            return White(0f);
        }

        // Three diameters through the centre (0/60/120 degrees) read as a 6-arm
        // snowflake/asterisk; a line through the centre is symmetric so only the
        // upper half-plane angle (0..pi) needs checking per arm.
        float angle = Mathf.Atan2(py, px);
        if (angle < 0f) { angle += Mathf.PI; }

        float best = float.MaxValue;
        for (int i = 0; i < 3; i++)
        {
            float armAngle = i * (Mathf.PI / 3f);
            float diff = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, armAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float distFromLine = r * Mathf.Sin(diff);
            best = Mathf.Min(best, Mathf.Abs(distFromLine));
        }

        float thickness = w * 0.032f;
        float a = 1f - Smoothstep(thickness - Aa, thickness + Aa, best);
        a *= 1f - Smoothstep(maxR - Aa, maxR + Aa, r);

        return White(Mathf.Clamp01(a));
    }

    private static Color32 PixelHazardVoid(int x, int y, int w, int h)
    {
        float cx = w * 0.5f, cy = h * 0.5f;
        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        float outer = w * 0.42f;

        float a = BandMask(dist, outer - w * 0.045f, outer, Aa);
        a = Mathf.Max(a, BandMask(dist, outer * 0.55f - w * 0.035f, outer * 0.55f, Aa));
        a = Mathf.Max(a, DiscMask(dist, outer * 0.12f, Aa));

        return White(Mathf.Clamp01(a));
    }

    // ---------------------------------------------------------------------
    // Panorama pieces
    // ---------------------------------------------------------------------

    // Solid from the bottom with a soft, gently undulating top edge - a distant
    // skyline silhouette, used twice per ArenaPanorama layer at different scroll
    // speeds/tints (bandFar/bandNear).
    private static Color32 PixelBand(int x, int y, int w, int h)
    {
        float u = (x + 0.5f) / w;
        float topEdge = h * 0.55f + Mathf.Sin(u * Mathf.PI * 2f * 2.5f) * h * 0.08f + Mathf.Sin(u * Mathf.PI * 2f * 5.3f) * h * 0.03f;
        float aa = Aa * 2f;
        float a = 1f - Smoothstep(topEdge - aa, topEdge + aa, y + 0.5f);

        return White(a);
    }

    // Inverse of SoftCircle: transparent centre, ramping to opaque at the rim - tinted
    // Deep and layered on top of the panorama as a vignette.
    private static Color32 PixelVignette(int x, int y, int w, int h)
    {
        float cx = w * 0.5f, cy = h * 0.5f;
        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        float radius = w * 0.5f;
        float a = Smoothstep(0f, radius, dist);

        return White(a);
    }
}
