#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Procedural Retro Bowl–style pixel sprites (field, players, ball, ring, crowd).
/// </summary>
public static class PixelArtFactory
{
    const string ArtRoot = "Assets/Art/Retro";

    public static void EnsureAllArt()
    {
        EnsureDir(ArtRoot);
        CreateFieldTexture();
        CreatePlayerSprite("PlayerOffense", new Color(0.95f, 0.95f, 0.97f), new Color(0.75f, 0.12f, 0.14f));
        CreatePlayerSprite("PlayerDefense", new Color(0.12f, 0.16f, 0.28f), new Color(0.85f, 0.85f, 0.9f));
        CreateBallSprite();
        CreateSelectionRing();
        CreateCrowdStrip();
        CreateSidelineStrip();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public static Sprite LoadSprite(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/{name}.png");
    }

    public static Texture2D LoadTexture(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtRoot}/{name}.png");
    }

    static void CreateFieldTexture()
    {
        const int w = 512;
        const int h = 256;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = "Field"
        };

        var turf = new Color(0.22f, 0.55f, 0.24f);
        var turfDark = new Color(0.18f, 0.48f, 0.20f);
        var endzone = new Color(0.08f, 0.12f, 0.28f);
        var line = Color.white;
        var numberCol = new Color(1f, 1f, 1f, 0.85f);

        // Field layout in texture: endzone | 100 yards | endzone
        float endFrac = 10f / 120f;
        int leftEnd = Mathf.RoundToInt(w * endFrac);
        int rightEnd = w - leftEnd;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Color c;
                if (x < leftEnd || x >= rightEnd)
                {
                    c = endzone;
                }
                else
                {
                    // Subtle stripe every few pixels
                    bool stripe = ((x / 8) % 2 == 0);
                    c = stripe ? turf : turfDark;
                }

                // Sideline borders
                if (y < 3 || y >= h - 3)
                    c = line;

                tex.SetPixel(x, y, c);
            }
        }

        // Yard lines every 5 yards across the 100-yard body
        int body = rightEnd - leftEnd;
        for (int yard = 0; yard <= 100; yard += 5)
        {
            int x = leftEnd + Mathf.RoundToInt(body * (yard / 100f));
            bool major = yard % 10 == 0;
            int thickness = major ? 2 : 1;
            for (int t = 0; t < thickness; t++)
            {
                int xx = Mathf.Clamp(x + t, 0, w - 1);
                for (int y = 4; y < h - 4; y++)
                    tex.SetPixel(xx, y, line);
            }

            // Hash marks
            if (yard % 1 == 0 && yard % 5 == 0)
            {
                for (int y = h / 3; y < h / 3 + 6; y++)
                    tex.SetPixel(x, y, line);
                for (int y = 2 * h / 3; y < 2 * h / 3 + 6; y++)
                    tex.SetPixel(x, y, line);
            }
        }

        // Yard numbers (10, 20, 30, 40, 50) — mirrored style like Retro Bowl
        int[] nums = { 10, 20, 30, 40, 50, 40, 30, 20, 10 };
        int[] yards = { 10, 20, 30, 40, 50, 60, 70, 80, 90 };
        for (int i = 0; i < nums.Length; i++)
        {
            int x = leftEnd + Mathf.RoundToInt(body * (yards[i] / 100f));
            DrawNumber(tex, nums[i], x - 10, h / 2 - 8, numberCol);
        }

        tex.Apply();
        SavePng(tex, "Field");
    }

    static void CreatePlayerSprite(string name, Color jersey, Color accent)
    {
        const int w = 16;
        const int h = 24;
        var tex = NewPointTex(w, h, name);
        Clear(tex);

        var skin = new Color(1f, 0.85f, 0.65f);
        var outline = new Color(0.05f, 0.05f, 0.08f);
        var pants = Color.Lerp(jersey, Color.black, 0.15f);
        var helmet = Color.Lerp(jersey, Color.white, 0.1f);

        // Legs
        FillRect(tex, 5, 1, 2, 6, pants);
        FillRect(tex, 9, 1, 2, 6, pants);
        // Body
        FillRect(tex, 4, 7, 8, 8, jersey);
        FillRect(tex, 4, 12, 8, 2, accent); // stripe
        // Arms
        FillRect(tex, 2, 8, 2, 5, jersey);
        FillRect(tex, 12, 8, 2, 5, jersey);
        // Helmet
        FillRect(tex, 5, 15, 6, 6, helmet);
        FillRect(tex, 6, 16, 4, 2, accent); // facemask hint
        // Outline accents
        Set(tex, 5, 20, outline);
        Set(tex, 10, 20, outline);

        tex.Apply();
        SaveSpritePng(tex, name, new Vector2(0.5f, 0f));
    }

    static void CreateBallSprite()
    {
        // Wide brown oval + laces — must not be a tall white "U" (reads as goalpost on tee).
        const int w = 24;
        const int h = 16;
        var tex = NewPointTex(w, h, "Football");
        Clear(tex);
        var brown = new Color(0.55f, 0.27f, 0.11f);
        var dark = new Color(0.35f, 0.16f, 0.05f);
        var lace = new Color(0.96f, 0.96f, 0.96f);
        float cx = (w - 1) * 0.5f;
        float cy = (h - 1) * 0.5f;
        float rx = 10.2f;
        float ry = 6.2f;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx = (x - cx) / rx;
            float ny = (y - cy) / ry;
            float d = nx * nx + ny * ny;
            if (d > 1f) continue;
            tex.SetPixel(x, y, d > 0.78f ? dark : brown);
        }
        for (int x = 7; x <= 16; x++)
        {
            tex.SetPixel(x, 7, dark);
            tex.SetPixel(x, 8, dark);
        }
        foreach (var y in new[] { 5, 6, 9, 10 })
            for (int x = 10; x <= 13; x++)
                tex.SetPixel(x, y, lace);
        tex.SetPixel(11, 7, lace);
        tex.SetPixel(12, 7, lace);
        tex.SetPixel(11, 8, lace);
        tex.SetPixel(12, 8, lace);
        tex.Apply();
        SaveSpritePng(tex, "Football", new Vector2(0.5f, 0.5f));
    }

    static void CreateSelectionRing()
    {
        const int s = 32;
        var tex = NewPointTex(s, s, "SelectionRing");
        Clear(tex);
        var col = new Color(1f, 1f, 1f, 0.85f);
        float cx = (s - 1) / 2f;
        float cy = (s - 1) / 2f;
        for (int y = 0; y < s; y++)
        for (int x = 0; x < s; x++)
        {
            float dx = (x - cx) / (s * 0.42f);
            float dy = (y - cy) / (s * 0.28f); // elliptical
            float d = dx * dx + dy * dy;
            if (d > 0.72f && d < 1.05f)
                tex.SetPixel(x, y, col);
        }
        tex.Apply();
        SaveSpritePng(tex, "SelectionRing", new Vector2(0.5f, 0.5f));
    }

    static void CreateCrowdStrip()
    {
        const int w = 256;
        const int h = 32;
        var tex = NewPointTex(w, h, "Crowd");
        var rng = new System.Random(42);
        Color[] palette =
        {
            new Color(0.8f, 0.2f, 0.2f),
            new Color(0.2f, 0.3f, 0.8f),
            new Color(0.9f, 0.85f, 0.2f),
            new Color(0.2f, 0.7f, 0.3f),
            new Color(0.7f, 0.4f, 0.8f),
            new Color(0.95f, 0.95f, 0.95f),
            new Color(0.1f, 0.1f, 0.12f)
        };

        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            // Bleacher rows
            if (y > 20)
            {
                tex.SetPixel(x, y, new Color(0.25f, 0.25f, 0.28f));
            }
            else
            {
                // Pixel heads
                if (rng.NextDouble() > 0.35 && y % 3 != 0)
                    tex.SetPixel(x, y, palette[rng.Next(palette.Length)]);
                else
                    tex.SetPixel(x, y, new Color(0.15f, 0.15f, 0.18f, 0f));
            }
        }
        tex.Apply();
        SaveSpritePng(tex, "Crowd", new Vector2(0.5f, 0f));
    }

    static void CreateSidelineStrip()
    {
        const int w = 256;
        const int h = 16;
        var tex = NewPointTex(w, h, "Sideline");
        Clear(tex);
        var grass = new Color(0.2f, 0.5f, 0.22f);
        var dash = new Color(0.95f, 0.85f, 0.2f);
        var orange = new Color(1f, 0.45f, 0.1f);
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            tex.SetPixel(x, y, grass);
            if (y == 8 && (x % 8) < 4)
                tex.SetPixel(x, y, dash);
            if (x % 32 == 0 && y < 6)
                tex.SetPixel(x, y, orange);
        }
        tex.Apply();
        SaveSpritePng(tex, "Sideline", new Vector2(0.5f, 0.5f));
    }

    // --- helpers ---

    static Texture2D NewPointTex(int w, int h, string name)
    {
        return new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            name = name
        };
    }

    static void Clear(Texture2D tex)
    {
        var clear = new Color(0, 0, 0, 0);
        for (int y = 0; y < tex.height; y++)
        for (int x = 0; x < tex.width; x++)
            tex.SetPixel(x, y, clear);
    }

    static void FillRect(Texture2D tex, int x, int y, int w, int h, Color c)
    {
        for (int yy = y; yy < y + h; yy++)
        for (int xx = x; xx < x + w; xx++)
            Set(tex, xx, yy, c);
    }

    static void Set(Texture2D tex, int x, int y, Color c)
    {
        if (x < 0 || y < 0 || x >= tex.width || y >= tex.height) return;
        tex.SetPixel(x, y, c);
    }

    static void DrawNumber(Texture2D tex, int number, int x, int y, Color c)
    {
        string s = number.ToString();
        int cursor = x;
        foreach (char ch in s)
        {
            DrawDigit(tex, ch - '0', cursor, y, c);
            cursor += 7;
        }
    }

    static void DrawDigit(Texture2D tex, int digit, int x, int y, Color c)
    {
        // 5x7 bitmap digits
        int[][] glyphs =
        {
            new[] { 0b01110, 0b10001, 0b10001, 0b10001, 0b10001, 0b10001, 0b01110 }, // 0
            new[] { 0b00100, 0b01100, 0b00100, 0b00100, 0b00100, 0b00100, 0b01110 }, // 1
            new[] { 0b01110, 0b10001, 0b00001, 0b00110, 0b01000, 0b10000, 0b11111 }, // 2
            new[] { 0b01110, 0b10001, 0b00001, 0b00110, 0b00001, 0b10001, 0b01110 }, // 3
            new[] { 0b00010, 0b00110, 0b01010, 0b10010, 0b11111, 0b00010, 0b00010 }, // 4
            new[] { 0b11111, 0b10000, 0b11110, 0b00001, 0b00001, 0b10001, 0b01110 }, // 5
            new[] { 0b00110, 0b01000, 0b10000, 0b11110, 0b10001, 0b10001, 0b01110 }, // 6
            new[] { 0b11111, 0b00001, 0b00010, 0b00100, 0b01000, 0b01000, 0b01000 }, // 7
            new[] { 0b01110, 0b10001, 0b10001, 0b01110, 0b10001, 0b10001, 0b01110 }, // 8
            new[] { 0b01110, 0b10001, 0b10001, 0b01111, 0b00001, 0b00010, 0b01100 }, // 9
        };
        if (digit < 0 || digit > 9) return;
        var g = glyphs[digit];
        for (int row = 0; row < 7; row++)
        for (int col = 0; col < 5; col++)
            if (((g[row] >> (4 - col)) & 1) == 1)
                Set(tex, x + col, y + (6 - row), c);
    }

    static void SavePng(Texture2D tex, string name)
    {
        EnsureDir(ArtRoot);
        var path = $"{ArtRoot}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Default;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.SaveAndReimport();
    }

    static void SaveSpritePng(Texture2D tex, string name, Vector2 pivot)
    {
        EnsureDir(ArtRoot);
        var path = $"{ArtRoot}/{name}.png";
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.spritePixelsPerUnit = 16f;
        importer.npotScale = TextureImporterNPOTScale.None;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    static void EnsureDir(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }
}
#endif
