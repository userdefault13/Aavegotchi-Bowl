using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using RetroBowl.Gameplay;
using RetroBowl.UI.Career;

namespace RetroBowl.UI
{
    /// <summary>
    /// Shared Tecmo play-select card chrome (title · green field diagram · pad hint)
    /// used by <see cref="PlayCallingUI"/> and the career Playbook screen.
    /// </summary>
    public static class PlayDiagramUi
    {
        public static readonly Color CardBg = CareerUiKit.TrainBtnFill;
        public static readonly Color FieldGreen = new Color(0.08f, 0.48f, 0.18f, 1f);
        public static readonly Color RoutePink = new Color(1f, 0.35f, 0.7f, 1f);
        public static readonly Color AccentBlue = new Color(0.45f, 0.85f, 1f, 1f);
        public static readonly Color AccentPink = new Color(1f, 0.45f, 0.75f, 1f);
        public static readonly Color SelectCyan = new Color(1f, 0.92f, 0.15f, 1f);
        public static readonly Color TitleBar = new Color(0.1f, 0.18f, 0.48f, 1f);
        public static readonly Color CardOutline = Color.white;
        public static readonly Color OffDim = new Color(0.12f, 0.14f, 0.22f, 0.85f);

        public static readonly string[] DirGlyphs = { "← A", "↑ W", "→ D", "↓ S" };

        /// <summary>
        /// NES pad combo icons — 2×4 grid of 96×80 cells.
        /// Rows 0-1 light the left face button (J / pass), rows 2-3 the right one (K / run).
        /// Within each button block the reading order is left, right, up, down.
        /// The two sheets are the source GIF's frames: white outlines idle, blue outlines highlighted.
        /// </summary>
        const string PadSheetResource = "UI/playcall-pad";
        const string PadSheetHighlightResource = "UI/playcall-pad-active";
        const int PadCellWidth = 96;
        const int PadCellHeight = 80;
        /// <summary>Artwork sits in the middle 48 rows of each cell — trim the padding so icons draw bigger.</summary>
        const int PadArtInsetY = 16;
        const int PadArtHeight = PadCellHeight - PadArtInsetY * 2;
        /// <summary>Card column order (← ↑ → ↓) remapped to the sheet's left/right/up/down order.</summary>
        static readonly int[] ColumnToSheetSlot = { 0, 2, 1, 3 };

        static Sprite[] padSprites;
        static Sprite[] padSpritesHighlight;
        static bool padSheetLoadFailed;
        static bool padHighlightSheetLoadFailed;

        public struct SlotCard
        {
            public GameObject Root;
            public Image Frame;
            public TextMeshProUGUI Title;
            public TextMeshProUGUI Pad;
            public Image PadIcon;
            public Transform Field;
            public Button MainButton;
            public Button ToggleButton;
            public Image ToggleFrame;
            public TextMeshProUGUI ToggleLabel;
        }

        /// <summary>Combo icon for a play slot, or null when the sheet is missing.</summary>
        public static Sprite GetPadSprite(bool isRun, int col, bool highlighted = false)
        {
            var sheet = highlighted ? EnsurePadHighlightSheet() : EnsurePadSheet();
            if (sheet == null) return null;

            int slot = ColumnToSheetSlot[Mathf.Clamp(col, 0, ColumnToSheetSlot.Length - 1)];
            int index = (isRun ? 4 : 0) + slot;
            return index >= 0 && index < sheet.Length ? sheet[index] : null;
        }

        static Sprite[] EnsurePadSheet()
        {
            if (padSprites != null || padSheetLoadFailed) return padSprites;

            padSprites = LoadPadSheet(PadSheetResource);
            padSheetLoadFailed = padSprites == null;
            return padSprites;
        }

        static Sprite[] EnsurePadHighlightSheet()
        {
            if (padSpritesHighlight != null || padHighlightSheetLoadFailed) return padSpritesHighlight;

            padSpritesHighlight = LoadPadSheet(PadSheetHighlightResource);
            padHighlightSheetLoadFailed = padSpritesHighlight == null;
            // Fall back to the idle art so a missing highlight sheet never blanks the icon.
            return padSpritesHighlight ?? EnsurePadSheet();
        }

        static Sprite[] LoadPadSheet(string resource)
        {
            // Prefer real sub-sprites when the sheet has been sliced in the Sprite Editor —
            // those survive scene saves, unlike the Sprite.Create fallback below.
            var sliced = TryLoadSlicedPadSprites(resource);
            if (sliced != null) return sliced;

            var tex = Resources.Load<Texture2D>(resource);
            if (tex == null) return null;

            tex.filterMode = FilterMode.Point;

            var sprites = new Sprite[8];
            for (int i = 0; i < sprites.Length; i++)
            {
                int row = i / 2;
                int col = i % 2;
                // Texture space is bottom-up; the sheet reads top-down.
                var rect = new Rect(
                    col * PadCellWidth,
                    tex.height - (row + 1) * PadCellHeight + PadArtInsetY,
                    PadCellWidth,
                    PadArtHeight);
                sprites[i] = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), 100f);
            }

            return sprites;
        }

        /// <summary>Picks up "&lt;sheet&gt;_0 … _7" sub-sprites, ordered left→right, top→bottom.</summary>
        static Sprite[] TryLoadSlicedPadSprites(string resource)
        {
            var loaded = Resources.LoadAll<Sprite>(resource);
            if (loaded == null || loaded.Length < 8) return null;

            var ordered = new Sprite[8];
            foreach (var sprite in loaded)
            {
                if (sprite == null) continue;
                int underscore = sprite.name.LastIndexOf('_');
                if (underscore < 0) continue;
                if (!int.TryParse(sprite.name.Substring(underscore + 1), out int index)) continue;
                if (index < 0 || index >= ordered.Length) continue;
                ordered[index] = sprite;
            }

            foreach (var sprite in ordered)
            {
                if (sprite == null) return null;
            }

            return ordered;
        }

        /// <summary>Career playbook slot card — click cycles play, toggle activates for match.</summary>
        public static SlotCard CreateSlotCard(
            Transform parent,
            string name,
            Vector2 anchorMin,
            Vector2 anchorMax,
            bool isRun,
            int col,
            UnityAction onCycle,
            UnityAction onToggle)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var frame = go.GetComponent<Image>();
            frame.color = CardBg;
            var frameOutline = go.AddComponent<Outline>();
            frameOutline.effectColor = CardOutline;
            frameOutline.effectDistance = new Vector2(2f, -2f);

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Image));
            titleGo.transform.SetParent(go.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.04f, 0.82f);
            titleRt.anchorMax = new Vector2(0.96f, 0.96f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            titleGo.GetComponent<Image>().color = TitleBar;

            var title = MakeLabel(titleGo.transform, "TitleText",
                Vector2.zero, Vector2.one, 15f, TextAlignmentOptions.Center);
            Stretch(title.rectTransform);
            title.text = isRun ? "RUN" : "PASS";
            title.color = Color.white;
            title.fontStyle = FontStyles.Bold;

            var fieldGo = new GameObject("Field", typeof(RectTransform), typeof(Image));
            fieldGo.transform.SetParent(go.transform, false);
            var fieldRt = fieldGo.GetComponent<RectTransform>();
            fieldRt.anchorMin = new Vector2(0.06f, 0.26f);
            fieldRt.anchorMax = new Vector2(0.94f, 0.8f);
            fieldRt.offsetMin = Vector2.zero;
            fieldRt.offsetMax = Vector2.zero;
            fieldGo.GetComponent<Image>().color = FieldGreen;
            fieldGo.GetComponent<Image>().raycastTarget = false;

            var padSprite = GetPadSprite(isRun, col);
            TextMeshProUGUI pad = null;
            Image padIcon = null;
            if (padSprite != null)
            {
                padIcon = MakePadIcon(go.transform, padSprite,
                    new Vector2(0.04f, 0.02f), new Vector2(0.68f, 0.24f));
                PadHighlightSwapper.Attach(go, padIcon, padSprite, GetPadSprite(isRun, col, highlighted: true));
            }
            else
            {
                string face = isRun ? "K" : "J";
                string padHint = DirGlyphs[Mathf.Clamp(col, 0, DirGlyphs.Length - 1)];
                pad = MakeLabel(go.transform, "Pad",
                    new Vector2(0.04f, 0.02f), new Vector2(0.68f, 0.24f), 15f, TextAlignmentOptions.Center);
                pad.text = $"{face} + {padHint}";
                pad.color = isRun ? SelectCyan : AccentPink;
            }

            var mainBtn = go.GetComponent<Button>();
            mainBtn.targetGraphic = frame;
            mainBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            mainBtn.transition = Selectable.Transition.None;
            if (onCycle != null)
                mainBtn.onClick.AddListener(onCycle);

            // ON/OFF chip — bottom-right (same outlined navy as career buttons).
            var togGo = new GameObject("Toggle", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Outline));
            togGo.transform.SetParent(go.transform, false);
            var togRt = togGo.GetComponent<RectTransform>();
            togRt.anchorMin = new Vector2(0.7f, 0.03f);
            togRt.anchorMax = new Vector2(0.96f, 0.23f);
            togRt.offsetMin = Vector2.zero;
            togRt.offsetMax = Vector2.zero;
            var togImg = togGo.GetComponent<Image>();
            togImg.color = CareerUiKit.RbBlueDark;
            var togOutline = togGo.GetComponent<Outline>();
            togOutline.effectColor = Color.white;
            togOutline.effectDistance = new Vector2(2f, -2f);
            var togLab = MakeLabel(togGo.transform, "Lab", Vector2.zero, Vector2.one, 13f,
                TextAlignmentOptions.Center);
            Stretch(togLab.rectTransform);
            togLab.text = "ON";
            togLab.fontStyle = FontStyles.Bold;
            var togBtn = togGo.GetComponent<Button>();
            togBtn.targetGraphic = togImg;
            togBtn.navigation = new Navigation { mode = Navigation.Mode.None };
            togBtn.transition = Selectable.Transition.None;
            if (onToggle != null)
                togBtn.onClick.AddListener(onToggle);

            return new SlotCard
            {
                Root = go,
                Frame = frame,
                Title = title,
                Pad = pad,
                PadIcon = padIcon,
                Field = fieldGo.transform,
                MainButton = mainBtn,
                ToggleButton = togBtn,
                ToggleFrame = togImg,
                ToggleLabel = togLab
            };
        }

        public static void SetSlotVisual(SlotCard card, OffensivePlay play, bool active, bool isRun,
            bool flipped = false)
        {
            if (card.Title == null) return;

            if (!active || play == null)
            {
                card.Title.text = "(OFF)";
                if (card.Frame != null) card.Frame.color = OffDim;
                if (card.ToggleLabel != null) card.ToggleLabel.text = "OFF";
                if (card.ToggleFrame != null) card.ToggleFrame.color = new Color(0.25f, 0.2f, 0.2f, 1f);
                if (card.PadIcon != null) card.PadIcon.color = new Color(1f, 1f, 1f, 0.35f);
                ClearChildren(card.Field);
                return;
            }

            // Flipped slots keep their name but read in yellow, since the mirrored routes
            // are the only other cue that the concept was inverted.
            card.Title.text = ShortTitle(play);
            card.Title.color = flipped ? SelectCyan : Color.white;
            if (card.Frame != null) card.Frame.color = CardBg;
            if (card.PadIcon != null) card.PadIcon.color = Color.white;
            if (card.ToggleLabel != null) card.ToggleLabel.text = "ON";
            if (card.ToggleFrame != null)
                card.ToggleFrame.color = CareerUiKit.RbBlueDark;

            ClearChildren(card.Field);
            if (card.Field != null)
            {
                var bg = card.Field.GetComponent<Image>();
                if (bg != null) bg.color = FieldGreen;
                // The play handed in is already mirrored when flipped — routes carry the whole change.
                DrawPlayDiagram(card.Field, play);
            }
        }

        public static string ShortTitle(OffensivePlay play)
        {
            if (play == null) return "";
            string n = play.DisplayName ?? play.Id ?? "";
            if (n.Length > 10)
                n = n.Substring(0, 10);
            return n.ToUpperInvariant();
        }

        public static void DrawPlayDiagram(Transform field, OffensivePlay play)
        {
            if (field == null || play == null) return;

            Vector2 Los(float across) => new Vector2(0.3f, across);
            Vector2 qb = new Vector2(0.2f, 0.5f);
            Vector2 rb = new Vector2(0.12f, 0.5f);
            Vector2 wrTop = new Vector2(0.28f, 0.88f);
            Vector2 wrBot = new Vector2(0.28f, 0.12f);
            Vector2 te = new Vector2(0.3f, 0.32f);
            Vector2[] ol =
            {
                Los(0.62f), Los(0.55f), Los(0.5f), Los(0.45f), Los(0.38f)
            };

            foreach (var p in ol)
                Dot(field, p, 5f, Color.white);
            Dot(field, qb, 6f, Color.white);
            Dot(field, rb, 6f, Color.white);
            Dot(field, wrTop, 5f, Color.white);
            Dot(field, wrBot, 5f, Color.white);
            Dot(field, te, 5f, Color.white);

            const float sx = 0.055f;
            const float sy = 0.07f;

            void Route(Vector2 start, Vector2[] offsets, bool emphasize)
            {
                if (offsets == null || offsets.Length == 0) return;
                var prev = start;
                for (int i = 0; i < offsets.Length; i++)
                {
                    var next = new Vector2(
                        Mathf.Clamp01(start.x + offsets[i].x * sx),
                        Mathf.Clamp01(start.y + offsets[i].y * sy));
                    Segment(field, prev, next, emphasize ? 3.5f : 2.2f, RoutePink);
                    prev = next;
                }
            }

            if (play.Type == OffensivePlayType.Run)
            {
                Route(rb, play.Rb, emphasize: true);
                Route(wrTop, play.WrTop, false);
                Route(wrBot, play.WrBot, false);
                Route(te, play.Te, false);
            }
            else
            {
                Route(wrTop, play.WrTop, true);
                Route(wrBot, play.WrBot, true);
                Route(te, play.Te, true);
                Route(rb, play.Rb, false);
            }
        }

        static void Dot(Transform parent, Vector2 n, float size, Color color)
        {
            var go = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = n;
            rt.anchorMax = n;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            go.GetComponent<Image>().color = color;
            go.GetComponent<Image>().raycastTarget = false;
        }

        static void Segment(Transform parent, Vector2 a, Vector2 b, float thickness, Color color)
        {
            var go = new GameObject("Seg", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            Vector2 mid = (a + b) * 0.5f;
            Vector2 d = b - a;
            rt.anchorMin = mid;
            rt.anchorMax = mid;
            rt.pivot = new Vector2(0.5f, 0.5f);

            const float parentW = 200f;
            const float parentH = 120f;
            float pxLen = Mathf.Sqrt((d.x * parentW) * (d.x * parentW) + (d.y * parentH) * (d.y * parentH));
            rt.sizeDelta = new Vector2(Mathf.Max(4f, pxLen), thickness);
            float angle = Mathf.Atan2(d.y * parentH, d.x * parentW) * Mathf.Rad2Deg;
            rt.localEulerAngles = new Vector3(0f, 0f, angle);

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
        }

        public static Image MakePadIcon(Transform parent, Sprite sprite, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject("Pad", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI MakeLabel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, float size, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = size;
            tmp.alignment = align;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            GameFonts.Apply(tmp);
            return tmp;
        }

        static void Stretch(RectTransform rt)
        {
            if (rt == null) return;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void ClearChildren(Transform t)
        {
            if (t == null) return;
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var child = t.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Object.Destroy(child);
                else
                    Object.DestroyImmediate(child);
            }
        }
    }
}
