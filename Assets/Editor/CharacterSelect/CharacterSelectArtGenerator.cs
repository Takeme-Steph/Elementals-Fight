using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Generates every white/alpha sprite the CharacterSelect UI tints at runtime. Everything
// is procedural (no source art asset) so the whole screen can be rebuilt from nothing on
// a clean checkout. All sprites are drawn white RGB with the shape baked into alpha only,
// so a plain Image.color tint is all any caller ever needs to do to recolour one.
public static class CharacterSelectArtGenerator
{
    private const string SpritesFolder = "Assets/UI/CharacterSelect/Sprites";

    // Antialiasing width in pixels for every hard edge below - contract asks for
    // "smoothstep over 1-1.5px"; splitting the difference reads clean at both the
    // sprite's native size and scaled up on a 6" phone.
    private const float Aa = 1.25f;

    [MenuItem("Elementals Fight/Character Select/1 - Generate Sprites")]
    public static void GenerateSpritesMenu()
    {
        GenerateAll();
    }

    public static void GenerateAll()
    {
        CharacterSelectUiFactory.EnsureFolder(SpritesFolder);

        WriteSprite("SoftCircle", 256, 256, PixelSoftCircle, Vector4.zero, false);
        WriteSprite("Circle", 128, 128, PixelCircle, Vector4.zero, false);
        WriteSprite("Ring", 128, 128, PixelRing, Vector4.zero, false);
        WriteSprite("DashedRing", 256, 256, PixelDashedRing, Vector4.zero, false);
        WriteSprite("RoundedRect", 64, 64, PixelRoundedRect, new Vector4(24, 24, 24, 24), false);
        WriteSprite("GradientV", 4, 128, PixelGradientV, Vector4.zero, false);
        WriteSprite("GradientH", 128, 4, PixelGradientH, Vector4.zero, false);
        WriteSprite("Pillar", 32, 256, PixelPillar, Vector4.zero, false);
        WriteSprite("Ellipse", 256, 96, PixelEllipse, Vector4.zero, false);
        WriteSprite("EllipseRing", 256, 96, PixelEllipseRing, Vector4.zero, false);
        WriteSprite("EllipseDashed", 256, 96, PixelEllipseDashed, Vector4.zero, false);
        WriteSprite("Star", 64, 64, PixelStar, Vector4.zero, true);
        WriteSprite("Trim", 4, 256, PixelTrim, Vector4.zero, false);

        AssetDatabase.SaveAssets();
        Debug.Log("CharacterSelectArtGenerator: generated 13 sprites into " + SpritesFolder);
    }

    // ---------------------------------------------------------------------
    // Texture -> PNG -> Sprite import pipeline
    // ---------------------------------------------------------------------

    private static void WriteSprite(string name, int w, int h, Func<int, int, int, int, Color32> pixelFn, Vector4 border, bool repeat)
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
        ti.maxTextureSize = 512;
        ti.spriteBorder = border;
        ti.spritePixelsPerUnit = 100;
        ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        ti.SaveAndReimport();
    }

    // ---------------------------------------------------------------------
    // Shared math
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

    /// <summary>
    /// 0/1 mask (antialiased) for "am I inside the drawn portion of a dash" given my
    /// angular position (0..1 around the circle) and the arc's approximate radius (used
    /// only to convert the pixel-space AA width into an angular fraction).
    /// </summary>
    private static float DashMask(float angle01, int dashCount, float dashFraction, float aa, float radius)
    {
        float segLen = 1f / dashCount;
        float local = angle01 - Mathf.Floor(angle01 / segLen) * segLen;
        float t = local / segLen;

        float segArcLength = 2f * Mathf.PI * Mathf.Max(radius, 1f) * segLen;
        float aaFrac = Mathf.Clamp(aa / Mathf.Max(segArcLength, 0.0001f), 0.001f, 0.45f);

        float rise = Smoothstep(0f, aaFrac, t);
        float fall = 1f - Smoothstep(dashFraction - aaFrac, dashFraction, t);
        return Mathf.Clamp01(Mathf.Min(rise, fall));
    }

    // ---------------------------------------------------------------------
    // Per-sprite pixel functions - each returns the colour of pixel (x, y) for a
    // texture of size (w, h). y = 0 is the bottom row (Unity's texture convention),
    // which is also what keeps "up" consistently +y with RadarChartGraphic.AxisDirection
    // and the dash angle math below.
    // ---------------------------------------------------------------------

    private static Color32 PixelSoftCircle(int x, int y, int w, int h)
    {
        float cx = w * 0.5f, cy = h * 0.5f;
        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        float radius = w * 0.5f;
        // Full-radius radial falloff (not just an edge band) - this is the soft glow
        // used behind blobs/halo/glow, so it should be bright at the centre and fade
        // smoothly all the way to transparent at the rim.
        float a = 1f - Smoothstep(0f, radius, dist);
        return White(a);
    }

    private static Color32 PixelCircle(int x, int y, int w, int h)
    {
        float cx = w * 0.5f, cy = h * 0.5f;
        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        float r = w * 0.5f - 2f;
        return White(DiscMask(dist, r, Aa));
    }

    private static Color32 PixelRing(int x, int y, int w, int h)
    {
        float cx = w * 0.5f, cy = h * 0.5f;
        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        float outer = w * 0.5f - 2f;
        const float stroke = 6f;
        float inner = outer - stroke;
        return White(BandMask(dist, inner, outer, Aa));
    }

    private static Color32 PixelDashedRing(int x, int y, int w, int h)
    {
        float cx = w * 0.5f, cy = h * 0.5f;
        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        float outer = w * 0.5f - 4f;
        const float stroke = 5f;
        float inner = outer - stroke;

        float band = BandMask(dist, inner, outer, Aa);
        if (band <= 0f)
        {
            return White(0f);
        }

        float angle = Mathf.Atan2(dx, dy); // 0 = up, clockwise - matches RadarChartGraphic.AxisDirection
        if (angle < 0f) { angle += Mathf.PI * 2f; }
        float angle01 = angle / (Mathf.PI * 2f);

        const int dashCount = 24;
        const float gapRatio = 0.45f;
        float dash = DashMask(angle01, dashCount, 1f - gapRatio, Aa, (inner + outer) * 0.5f);

        return White(band * dash);
    }

    private static Color32 PixelRoundedRect(int x, int y, int w, int h)
    {
        float hx = w * 0.5f, hy = h * 0.5f;
        const float radius = 20f;
        float px = x + 0.5f - hx, py = y + 0.5f - hy;

        float qx = Mathf.Max(Mathf.Abs(px) - (hx - radius), 0f);
        float qy = Mathf.Max(Mathf.Abs(py) - (hy - radius), 0f);
        float dist = Mathf.Sqrt(qx * qx + qy * qy) - radius;

        float a = 1f - Smoothstep(-Aa, Aa, dist);
        return White(a);
    }

    private static Color32 PixelGradientV(int x, int y, int w, int h)
    {
        // Bottom (y=0) alpha 1 -> top (y=h-1) alpha 0.
        float t = h <= 1 ? 0f : y / (float)(h - 1);
        return White(1f - t);
    }

    private static Color32 PixelGradientH(int x, int y, int w, int h)
    {
        // Left (x=0) alpha 1 -> right (x=w-1) alpha 0.35.
        float t = w <= 1 ? 0f : x / (float)(w - 1);
        return White(Mathf.Lerp(1f, 0.35f, t));
    }

    private static Color32 PixelPillar(int x, int y, int w, int h)
    {
        float cx = w * 0.5f;
        float halfWidth = w * 0.5f;
        float tx = Mathf.Abs(x + 0.5f - cx) / halfWidth;
        float xFalloff = Mathf.Clamp01(1f - Smoothstep(0f, 1f, tx));

        // Bottom (y=0) full strength, fading to nothing by the top - a light bar that
        // dissipates as it rises rather than a uniform column.
        float tv = h <= 1 ? 0f : y / (float)(h - 1);
        float vFalloff = 1f - tv;

        return White(xFalloff * vFalloff);
    }

    private static Color32 PixelEllipse(int x, int y, int w, int h)
    {
        float cx = w * 0.5f, cy = h * 0.5f;
        float rx = w * 0.5f, ry = h * 0.5f;
        float nx = (x + 0.5f - cx) / rx;
        float ny = (y + 0.5f - cy) / ry;
        float nd = Mathf.Sqrt(nx * nx + ny * ny);
        // Soft glow, same spirit as SoftCircle but anisotropic.
        float a = 1f - Smoothstep(0f, 1f, nd);
        return White(a);
    }

    private static Color32 PixelEllipseRing(int x, int y, int w, int h)
    {
        float cx = w * 0.5f, cy = h * 0.5f;
        float rx = w * 0.5f - 2f, ry = h * 0.5f - 2f;
        float avgR = (rx + ry) * 0.5f;
        float nx = (x + 0.5f - cx) / rx;
        float ny = (y + 0.5f - cy) / ry;
        float nd = Mathf.Sqrt(nx * nx + ny * ny);

        const float strokePx = 3f;
        float strokeNorm = strokePx / avgR;
        float aaNorm = Aa / avgR;
        float a = BandMask(nd, 1f - strokeNorm, 1f, aaNorm);
        return White(a);
    }

    private static Color32 PixelEllipseDashed(int x, int y, int w, int h)
    {
        float cx = w * 0.5f, cy = h * 0.5f;
        float rx = w * 0.5f - 2f, ry = h * 0.5f - 2f;
        float avgR = (rx + ry) * 0.5f;
        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
        float nx = dx / rx, ny = dy / ry;
        float nd = Mathf.Sqrt(nx * nx + ny * ny);

        const float strokePx = 4f;
        float strokeNorm = strokePx / avgR;
        float aaNorm = Aa / avgR;
        float band = BandMask(nd, 1f - strokeNorm, 1f, aaNorm);
        if (band <= 0f)
        {
            return White(0f);
        }

        float angle = Mathf.Atan2(nx, ny);
        if (angle < 0f) { angle += Mathf.PI * 2f; }
        float angle01 = angle / (Mathf.PI * 2f);

        const int dashCount = 20;
        const float gapRatio = 0.45f;
        float dash = DashMask(angle01, dashCount, 1f - gapRatio, Aa, avgR);

        return White(band * dash);
    }

    private static Color32 PixelStar(int x, int y, int w, int h)
    {
        // The tile repeats across the whole screen, so it holds two small dots at
        // asymmetric positions rather than one centred dot: a single centred dot
        // reads as a rigid grid the moment it tiles. Dot radii are in pixels, so
        // the dots stay tiny however large the tile is scaled on screen.
        float a = StarDot(x, y, w * 0.30f, h * 0.68f, 1.7f) + 0.6f * StarDot(x, y, w * 0.76f, h * 0.22f, 1.1f);
        return White(Mathf.Clamp01(a));
    }

    private static float StarDot(int x, int y, float cx, float cy, float radius)
    {
        float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
        float dist = Mathf.Sqrt(dx * dx + dy * dy);
        return 1f - Smoothstep(radius * 0.4f, radius + 0.8f, dist);
    }

    private static Color32 PixelTrim(int x, int y, int w, int h)
    {
        // Bottom (y=0) alpha 0 -> top (y=h-1) alpha 0.8.
        float t = h <= 1 ? 0f : y / (float)(h - 1);
        return White(0.8f * t);
    }
}
