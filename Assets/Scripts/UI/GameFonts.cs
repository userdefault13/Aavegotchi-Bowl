using TMPro;
using UnityEngine;

namespace RetroBowl.UI
{
    /// <summary>
    /// Project UI font — Broken Console (pixel / console look).
    /// Loads from TMP Resources; falls back to <see cref="TMP_Settings.defaultFontAsset"/>.
    /// </summary>
    public static class GameFonts
    {
        const string BrokenConsoleResource =
            "Fonts & Materials/Broken Console Regular SDF";

        static TMP_FontAsset cached;

        public static TMP_FontAsset Primary
        {
            get
            {
                if (cached != null) return cached;
                cached = Resources.Load<TMP_FontAsset>(BrokenConsoleResource);
                if (cached == null)
                    cached = TMP_Settings.defaultFontAsset;
                return cached;
            }
        }

        public static void Apply(TMP_Text tmp)
        {
            if (tmp == null) return;
            var font = Primary;
            if (font == null) return;
            tmp.font = font;
            if (font.material != null)
                tmp.fontSharedMaterial = font.material;
            // Broken Console has no ellipsis glyph — Truncate avoids the TMP fallback warning spam.
            if (tmp.overflowMode == TextOverflowModes.Ellipsis)
                tmp.overflowMode = TextOverflowModes.Truncate;
        }

        public static void ApplyAllUnder(Transform root, bool includeInactive = true)
        {
            if (root == null) return;
            var labels = root.GetComponentsInChildren<TMP_Text>(includeInactive);
            for (int i = 0; i < labels.Length; i++)
                Apply(labels[i]);
        }
    }
}
