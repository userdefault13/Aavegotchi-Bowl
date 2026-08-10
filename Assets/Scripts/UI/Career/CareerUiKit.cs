using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
using RetroBowl.UI;

namespace RetroBowl.UI.Career
{
    /// <summary>Shared runtime UI helpers for career screens (placeholder chrome).</summary>
    public static class CareerUiKit
    {
        public static readonly Color RbBlue = new Color(0.19f, 0.38f, 1f, 1f);
        public static readonly Color RbBlueDark = new Color(0.12f, 0.22f, 0.65f, 1f);
        public static readonly Color RbYellow = new Color(0.88f, 0.86f, 0.12f, 1f);
        public static readonly Color RbPanel = new Color(0.16f, 0.32f, 0.92f, 1f);
        /// <summary>Training / tutorial modal fill — blue, see-through so the field shows.</summary>
        public static readonly Color TrainModalFill = new Color(0.12f, 0.22f, 0.55f, 0.32f);
        public static readonly Color TrainBtnFill = new Color(0.14f, 0.24f, 0.58f, 1f);
        public static readonly Color RbStarRed = new Color(0.92f, 0.18f, 0.16f, 1f);
        public static readonly Color RbStarWhite = Color.white;
        public static readonly Color RbStarBlue = new Color(0.08f, 0.12f, 0.55f, 1f);
        public static readonly Color RbInputFill = new Color(0.96f, 0.96f, 0.98f, 1f);
        public static readonly Color RbPlate = new Color(0.14f, 0.28f, 0.88f, 1f);
        public static readonly Color RbNavInactive = new Color(0.22f, 0.24f, 0.28f, 0.95f);
        public static readonly Color RbTipBar = new Color(0.08f, 0.1f, 0.18f, 0.95f);

        /// <summary>
        /// Classic Retro Bowl header: colored stars flanking a bold title.
        /// </summary>
        public static TextMeshProUGUI StarTitle(Transform parent, string name, Vector2 anchor, string title,
            float font = 40f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1100f, 56f);

            // Left stars: red · white · blue (outside → in)
            PlaceStar(go.transform, "L0", new Vector2(0.08f, 0.5f), RbStarRed);
            PlaceStar(go.transform, "L1", new Vector2(0.14f, 0.5f), RbStarWhite);
            PlaceStar(go.transform, "L2", new Vector2(0.20f, 0.5f), RbStarBlue);

            var titleLabel = Label(go.transform, "TitleText", new Vector2(0.5f, 0.5f), new Vector2(520f, 52f), font);
            titleLabel.text = title;
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.outlineWidth = 0.22f;

            // Right stars: blue · white · red (inside → out)
            PlaceStar(go.transform, "R0", new Vector2(0.80f, 0.5f), RbStarBlue);
            PlaceStar(go.transform, "R1", new Vector2(0.86f, 0.5f), RbStarWhite);
            PlaceStar(go.transform, "R2", new Vector2(0.92f, 0.5f), RbStarRed);

            return titleLabel;
        }

        static void PlaceStar(Transform parent, string name, Vector2 anchor, Color color)
        {
            var star = Label(parent, name, anchor, new Vector2(40f, 40f), 30f);
            star.text = "*";
            star.color = color;
            star.outlineWidth = 0.05f;
            star.outlineColor = new Color(0f, 0f, 0f, 0.35f);
        }

        /// <summary>
        /// Bordered panel with a title label sitting on the top edge (Retro Bowl section chrome).
        /// </summary>
        public static GameObject SectionPanel(Transform parent, string name, Vector2 anchor, Vector2 size,
            string title, out TextMeshProUGUI titleLabel)
        {
            var box = BorderedBox(parent, name, anchor, size, RbPanel);
            var outline = box.GetComponent<Outline>();
            if (outline != null)
                outline.effectDistance = new Vector2(3f, -3f);

            // Title sits on the top border so it reads as part of the frame.
            titleLabel = Label(box.transform, "SectionTitle", new Vector2(0.5f, 1f), new Vector2(size.x * 0.7f, 28f), 20f);
            titleLabel.rectTransform.anchoredPosition = new Vector2(0f, 2f);
            titleLabel.text = title;
            titleLabel.fontStyle = FontStyles.Bold;
            titleLabel.outlineWidth = 0.2f;

            // Underlay so the title punches through the border cleanly.
            var under = new GameObject("TitleUnder");
            under.transform.SetParent(box.transform, false);
            under.transform.SetAsFirstSibling();
            var urt = under.AddComponent<RectTransform>();
            urt.anchorMin = new Vector2(0.5f, 1f);
            urt.anchorMax = new Vector2(0.5f, 1f);
            urt.pivot = new Vector2(0.5f, 0.5f);
            urt.anchoredPosition = new Vector2(0f, 2f);
            urt.sizeDelta = new Vector2(Mathf.Min(size.x * 0.55f, title.Length * 14f + 40f), 22f);
            under.AddComponent<Image>().color = RbPanel;

            titleLabel.transform.SetAsLastSibling();
            return box;
        }

        /// <summary>White checkbox; fill yellow when checked, with a black X mark.</summary>
        public static (Image box, TextMeshProUGUI mark, Button button) Checkbox(
            Transform parent, string name, Vector2 anchor, Vector2 size)
        {
            var go = BorderedBox(parent, name, anchor, size, Color.white);
            var outline = go.GetComponent<Outline>();
            if (outline != null)
                outline.effectDistance = new Vector2(2f, -2f);

            var mark = Label(go.transform, "Mark", new Vector2(0.5f, 0.5f), size, size.y * 0.7f);
            mark.text = "";
            mark.color = Color.black;
            mark.fontStyle = FontStyles.Bold;
            mark.outlineWidth = 0f;

            var img = go.GetComponent<Image>();
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            return (img, mark, btn);
        }

        static Texture2D _ghostPattern;

        static Texture2D GhostPatternTex
        {
            get
            {
                if (_ghostPattern != null) return _ghostPattern;
                _ghostPattern = Resources.Load<Texture2D>("UI/ghost-pattern");
                if (_ghostPattern != null)
                {
                    _ghostPattern.wrapMode = TextureWrapMode.Repeat;
                    _ghostPattern.filterMode = FilterMode.Point;
                }
                return _ghostPattern;
            }
        }

        /// <summary>
        /// Aarcade-style ghost watermark (from public/bg.svg) tiled over career blue screens.
        /// </summary>
        public static void AddBlueFieldDecor(Transform panel)
        {
            if (panel == null) return;
            if (panel.Find("FieldDecor") != null) return;

            var decor = new GameObject("FieldDecor");
            decor.transform.SetParent(panel, false);
            decor.transform.SetAsFirstSibling();
            var rt = decor.AddComponent<RectTransform>();
            Stretch(rt);

            // Soft wash so panels still read over the pattern.
            var wash = new GameObject("Wash");
            wash.transform.SetParent(decor.transform, false);
            Stretch(wash.AddComponent<RectTransform>());
            wash.AddComponent<Image>().color = new Color(0.08f, 0.16f, 0.55f, 0.18f);

            var tex = GhostPatternTex;
            if (tex != null)
            {
                var patGo = new GameObject("GhostPattern");
                patGo.transform.SetParent(decor.transform, false);
                Stretch(patGo.AddComponent<RectTransform>());
                var raw = patGo.AddComponent<RawImage>();
                raw.texture = tex;
                raw.raycastTarget = false;
                // Match Aarcade mask-size ≈ 307×326 on a 1920×1080 career canvas.
                const float tileW = 307f;
                const float tileH = 326f;
                raw.uvRect = new Rect(0f, 0f, 1920f / tileW, 1080f / tileH);
                // Site default opacity ~0.10–0.12 over dark/blue.
                raw.color = new Color(1f, 1f, 1f, 0.12f);
            }
            else
            {
                // Fallback hairlines if the texture failed to load.
                for (int i = 0; i < 5; i++)
                {
                    float y = 0.15f + i * 0.17f;
                    var line = new GameObject("Line" + i);
                    line.transform.SetParent(decor.transform, false);
                    var lrt = line.AddComponent<RectTransform>();
                    lrt.anchorMin = new Vector2(0.06f, y);
                    lrt.anchorMax = new Vector2(0.94f, y);
                    lrt.pivot = new Vector2(0.5f, 0.5f);
                    lrt.sizeDelta = new Vector2(0f, 2f);
                    line.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f + (i % 2) * 0.03f);
                }
            }
        }

        /// <summary>Team name plate used on the new-career favorite-team row.</summary>
        public static TextMeshProUGUI TeamPlate(Transform parent, string name, Vector2 anchor, Vector2 size)
        {
            var plate = BorderedBox(parent, name, anchor, size, RbPlate);
            var outline = plate.GetComponent<Outline>();
            if (outline != null)
                outline.effectDistance = new Vector2(2.5f, -2.5f);
            var shadow = plate.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(3f, -3f);

            var label = Label(plate.transform, "Txt", new Vector2(0.5f, 0.5f), size, 28f);
            Stretch(label.rectTransform);
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.18f;
            return label;
        }

        public static readonly Color RbOffenseCard = new Color(0.35f, 0.62f, 0.95f, 1f);
        public static readonly Color RbDefenseCard = new Color(0.82f, 0.32f, 0.22f, 1f);
        public static readonly Color RbStaffHc = new Color(0.28f, 0.62f, 0.38f, 1f);
        public static readonly Color RbStaffOwner = new Color(0.55f, 0.38f, 0.88f, 1f);
        public static readonly Color RbNameBar = new Color(0.12f, 0.14f, 0.2f, 0.92f);
        public static readonly Color RbCondition = new Color(0.25f, 0.82f, 0.32f, 1f);

        static Sprite _gltrSprite;

        /// <summary>GLTR token sprite (coaching credits currency).</summary>
        public static Sprite GltrSprite
        {
            get
            {
                if (_gltrSprite != null) return _gltrSprite;
                _gltrSprite = Resources.Load<Sprite>("UI/gltr-token");
                if (_gltrSprite == null)
                {
                    var tex = Resources.Load<Texture2D>("UI/gltr-token");
                    if (tex != null)
                        _gltrSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f), 100f);
                }
                return _gltrSprite;
            }
        }

        /// <summary>Credits counter pill: amount + GLTR icon. Clickable to open GAX buy modal.</summary>
        public static (TextMeshProUGUI amount, Button button) CreditsPill(Transform parent, string name,
            Vector2 anchor, Vector2 size = default)
        {
            if (size == default) size = new Vector2(140f, 42f);
            var box = BorderedBox(parent, name, anchor, size, RbBlueDark);
            var outline = box.GetComponent<Outline>();
            if (outline != null) outline.effectDistance = new Vector2(2f, -2f);

            var amount = Label(box.transform, "Amt", new Vector2(0.38f, 0.5f),
                new Vector2(size.x * 0.55f, size.y * 0.75f), 22f);
            amount.fontStyle = FontStyles.Bold;
            amount.text = "0";
            amount.raycastTarget = false;

            GltrIcon(box.transform, "Icon", new Vector2(0.78f, 0.5f), Mathf.Min(32f, size.y * 0.7f))
                .raycastTarget = false;

            var btn = box.AddComponent<Button>();
            btn.targetGraphic = box.GetComponent<Image>();
            return (amount, btn);
        }

        /// <summary>Small GLTR token image for cost buttons / inline currency.</summary>
        public static Image GltrIcon(Transform parent, string name, Vector2 anchor, float size = 28f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = GltrSprite;
            img.preserveAspect = true;
            img.color = Color.white;
            if (img.sprite == null)
                img.color = new Color(1f, 0.8f, 0.15f, 1f); // fallback disc if sprite missing
            return img;
        }

        /// <summary>Cost button labeled with a number + GLTR icon (e.g. free-agent sign / upgrades).</summary>
        public static Button GltrCostButton(Transform parent, string name, Vector2 anchor, Vector2 size,
            int cost, UnityAction onClick, Color? fill = null)
        {
            var btn = OutlinedButton(parent, name, anchor, size, cost.ToString(), onClick, fill ?? RbBlueDark);
            var label = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.rectTransform.anchorMin = new Vector2(0.08f, 0.1f);
                label.rectTransform.anchorMax = new Vector2(0.58f, 0.9f);
                label.rectTransform.offsetMin = Vector2.zero;
                label.rectTransform.offsetMax = Vector2.zero;
                label.alignment = TextAlignmentOptions.MidlineRight;
            }
            GltrIcon(btn.transform, "Gltr", new Vector2(0.78f, 0.5f), Mathf.Min(28f, size.y * 0.65f));
            return btn;
        }

        /// <summary>
        /// Retro Bowl roster card: pos, morale, portrait, name, stars, condition bar.
        /// Children use stretch anchors so GridLayoutGroup cells size correctly.
        /// </summary>
        public static GameObject RosterPlayerCard(
            Transform parent,
            string name,
            string position,
            string lastName,
            float stars,
            float condition01,
            bool offenseSide,
            float morale01,
            out Button button)
        {
            Color face = offenseSide ? RbOffenseCard : RbDefenseCard;
            Color faceDeep = offenseSide
                ? new Color(0.18f, 0.38f, 0.72f)
                : new Color(0.55f, 0.18f, 0.14f);

            var card = new GameObject(name);
            card.transform.SetParent(parent, false);
            var crt = card.AddComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            var cardImg = card.AddComponent<Image>();
            cardImg.color = face;
            var cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = Color.white;
            cardOutline.effectDistance = new Vector2(2.5f, -2.5f);

            var le = card.AddComponent<LayoutElement>();
            le.minWidth = 180f;
            le.minHeight = 220f;
            le.preferredWidth = 188f;
            le.preferredHeight = 236f;

            // Position
            var posLab = Label(card.transform, "Pos", new Vector2(0.16f, 0.93f), new Vector2(64f, 26f), 18f);
            posLab.text = position;
            posLab.fontStyle = FontStyles.Bold;

            // Morale face
            var mood = Label(card.transform, "Mood", new Vector2(0.86f, 0.93f), new Vector2(40f, 28f), 22f);
            mood.text = morale01 >= 0.65f ? "☺" : morale01 >= 0.35f ? "😐" : "☹";
            mood.color = morale01 >= 0.65f ? RbYellow : Color.white;

            // Portrait frame
            var portrait = new GameObject("Portrait");
            portrait.transform.SetParent(card.transform, false);
            var prt = portrait.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.12f, 0.38f);
            prt.anchorMax = new Vector2(0.88f, 0.86f);
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            portrait.AddComponent<Image>().color = faceDeep;
            var pOutline = portrait.AddComponent<Outline>();
            pOutline.effectColor = Color.white;
            pOutline.effectDistance = new Vector2(1.5f, -1.5f);

            // Simple gotchi head stub
            var head = new GameObject("Head");
            head.transform.SetParent(portrait.transform, false);
            var hrt = head.AddComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0.22f, 0.28f);
            hrt.anchorMax = new Vector2(0.78f, 0.88f);
            hrt.offsetMin = Vector2.zero;
            hrt.offsetMax = Vector2.zero;
            head.AddComponent<Image>().color = Color.HSVToRGB((position.GetHashCode() & 0xFF) / 255f, 0.35f, 0.85f);
            Label(portrait.transform, "Eyes", new Vector2(0.5f, 0.58f), new Vector2(90f, 28f), 20f).text = "●  ●";
            var mouth = Label(portrait.transform, "Mouth", new Vector2(0.5f, 0.32f), new Vector2(50f, 20f), 16f);
            mouth.text = "‿";

            // Name plate
            var nameBar = new GameObject("NameBar");
            nameBar.transform.SetParent(card.transform, false);
            var nrt = nameBar.AddComponent<RectTransform>();
            nrt.anchorMin = new Vector2(0.06f, 0.22f);
            nrt.anchorMax = new Vector2(0.94f, 0.36f);
            nrt.offsetMin = Vector2.zero;
            nrt.offsetMax = Vector2.zero;
            nameBar.AddComponent<Image>().color = RbNameBar;
            var nameLab = Label(nameBar.transform, "Name", new Vector2(0.5f, 0.5f), new Vector2(160f, 28f), 17f);
            Stretch(nameLab.rectTransform);
            nameLab.text = lastName.ToUpperInvariant();
            nameLab.fontStyle = FontStyles.Bold;
            nameLab.overflowMode = TextOverflowModes.Ellipsis;

            // Stars — gold filled, dim empty (no mid-dot glyphs)
            var starLab = Label(card.transform, "Stars", new Vector2(0.5f, 0.14f), new Vector2(170f, 24f), 16f);
            starLab.text = RosterStarString(stars);
            starLab.richText = true;

            // Condition bar
            var condBg = new GameObject("Cond");
            condBg.transform.SetParent(card.transform, false);
            var cbrt = condBg.AddComponent<RectTransform>();
            cbrt.anchorMin = new Vector2(0.08f, 0.04f);
            cbrt.anchorMax = new Vector2(0.92f, 0.1f);
            cbrt.offsetMin = Vector2.zero;
            cbrt.offsetMax = Vector2.zero;
            condBg.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.08f, 1f);
            var condOutline = condBg.AddComponent<Outline>();
            condOutline.effectColor = Color.white;
            condOutline.effectDistance = new Vector2(1f, -1f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(condBg.transform, false);
            var frt = fillGo.AddComponent<RectTransform>();
            float c = Mathf.Clamp01(condition01);
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(Mathf.Max(0.04f, c), 1f);
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            Color condColor = c < 0.35f
                ? new Color(0.85f, 0.2f, 0.15f)
                : c < 0.65f
                    ? new Color(0.9f, 0.75f, 0.15f)
                    : RbCondition;
            fillGo.AddComponent<Image>().color = condColor;

            button = card.AddComponent<Button>();
            button.targetGraphic = cardImg;
            return card;
        }

        public static readonly Color RbSpecialCard = new Color(0.78f, 0.48f, 0.22f, 1f);

        /// <summary>
        /// Free-agent card: roster face + salary + coaching-credit sign cost under the card.
        /// </summary>
        public static GameObject FreeAgentCard(
            Transform parent,
            string name,
            string position,
            string lastName,
            float stars,
            int salaryM,
            int signCostCc,
            bool offenseSide,
            bool specialTeams,
            float morale01,
            UnityAction onSign,
            out Button cardButton)
        {
            Color face = specialTeams ? RbSpecialCard : (offenseSide ? RbOffenseCard : RbDefenseCard);
            Color faceDeep = specialTeams
                ? new Color(0.55f, 0.32f, 0.12f)
                : offenseSide
                    ? new Color(0.18f, 0.38f, 0.72f)
                    : new Color(0.55f, 0.18f, 0.14f);

            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rrt = root.AddComponent<RectTransform>();
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero;
            rrt.offsetMax = Vector2.zero;
            var le = root.AddComponent<LayoutElement>();
            le.minWidth = 180f;
            le.minHeight = 268f;
            le.preferredWidth = 188f;
            le.preferredHeight = 278f;

            // Card body (upper portion)
            var card = new GameObject("Card");
            card.transform.SetParent(root.transform, false);
            var crt = card.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 0.18f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            var cardImg = card.AddComponent<Image>();
            cardImg.color = face;
            var cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = Color.white;
            cardOutline.effectDistance = new Vector2(2.5f, -2.5f);

            var posLab = Label(card.transform, "Pos", new Vector2(0.16f, 0.92f), new Vector2(64f, 24f), 16f);
            posLab.text = position;
            posLab.fontStyle = FontStyles.Bold;

            var mood = Label(card.transform, "Mood", new Vector2(0.86f, 0.92f), new Vector2(40f, 26f), 20f);
            mood.text = morale01 >= 0.65f ? "☺" : morale01 >= 0.35f ? "😐" : "☹";
            mood.color = morale01 >= 0.65f ? RbYellow : Color.white;

            var portrait = new GameObject("Portrait");
            portrait.transform.SetParent(card.transform, false);
            var prt = portrait.AddComponent<RectTransform>();
            prt.anchorMin = new Vector2(0.12f, 0.42f);
            prt.anchorMax = new Vector2(0.88f, 0.86f);
            prt.offsetMin = Vector2.zero;
            prt.offsetMax = Vector2.zero;
            portrait.AddComponent<Image>().color = faceDeep;
            portrait.AddComponent<Outline>().effectColor = Color.white;
            portrait.GetComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);
            Label(portrait.transform, "Eyes", new Vector2(0.5f, 0.58f), new Vector2(90f, 28f), 18f).text = "●  ●";

            var nameBar = new GameObject("NameBar");
            nameBar.transform.SetParent(card.transform, false);
            var nrt = nameBar.AddComponent<RectTransform>();
            nrt.anchorMin = new Vector2(0.06f, 0.28f);
            nrt.anchorMax = new Vector2(0.94f, 0.4f);
            nrt.offsetMin = Vector2.zero;
            nrt.offsetMax = Vector2.zero;
            nameBar.AddComponent<Image>().color = RbNameBar;
            var nameLab = Label(nameBar.transform, "Name", new Vector2(0.5f, 0.5f), new Vector2(160f, 24f), 16f);
            Stretch(nameLab.rectTransform);
            nameLab.text = lastName.ToUpperInvariant();
            nameLab.fontStyle = FontStyles.Bold;
            nameLab.overflowMode = TextOverflowModes.Ellipsis;

            var starLab = Label(card.transform, "Stars", new Vector2(0.5f, 0.18f), new Vector2(170f, 22f), 15f);
            starLab.text = RosterStarString(stars);
            starLab.richText = true;

            var sal = Label(card.transform, "Salary", new Vector2(0.5f, 0.06f), new Vector2(160f, 22f), 18f);
            sal.text = $"${salaryM}M";
            sal.fontStyle = FontStyles.Bold;

            cardButton = card.AddComponent<Button>();
            cardButton.targetGraphic = cardImg;

            // Sign cost pill under card
            GltrCostButton(root.transform, "Sign", new Vector2(0.5f, 0.08f),
                new Vector2(110f, 36f), signCostCc, onSign, RbBlueDark);

            return root;
        }

        /// <summary>Empty roster slot — dashed outline matching player card cell size.</summary>
        public static GameObject RosterEmptySlot(Transform parent, string name = "EmptySlot")
        {
            var card = new GameObject(name);
            card.transform.SetParent(parent, false);
            var crt = card.AddComponent<RectTransform>();
            crt.anchorMin = Vector2.zero;
            crt.anchorMax = Vector2.one;
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            var cardImg = card.AddComponent<Image>();
            cardImg.color = new Color(0.1f, 0.18f, 0.48f, 0.55f);
            var cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = new Color(1f, 1f, 1f, 0.45f);
            cardOutline.effectDistance = new Vector2(2f, -2f);

            var le = card.AddComponent<LayoutElement>();
            le.minWidth = 180f;
            le.minHeight = 220f;
            le.preferredWidth = 188f;
            le.preferredHeight = 236f;

            // Inner dashed frame
            var inner = new GameObject("Inner");
            inner.transform.SetParent(card.transform, false);
            var irt = inner.AddComponent<RectTransform>();
            irt.anchorMin = new Vector2(0.08f, 0.08f);
            irt.anchorMax = new Vector2(0.92f, 0.92f);
            irt.offsetMin = Vector2.zero;
            irt.offsetMax = Vector2.zero;
            var innerImg = inner.AddComponent<Image>();
            innerImg.color = new Color(0.08f, 0.14f, 0.4f, 0.35f);
            var innerOutline = inner.AddComponent<Outline>();
            innerOutline.effectColor = new Color(1f, 1f, 1f, 0.28f);
            innerOutline.effectDistance = new Vector2(1.5f, -1.5f);

            var plus = Label(card.transform, "Plus", new Vector2(0.5f, 0.55f), new Vector2(80f, 60f), 42f);
            plus.text = "+";
            plus.color = new Color(1f, 1f, 1f, 0.35f);
            plus.fontStyle = FontStyles.Bold;

            var open = Label(card.transform, "Open", new Vector2(0.5f, 0.28f), new Vector2(150f, 28f), 16f);
            open.text = "OPEN";
            open.color = new Color(1f, 1f, 1f, 0.4f);
            open.fontStyle = FontStyles.Bold;

            return card;
        }

        /// <summary>Gold * for filled / dim * for empty — Broken Console has no ★ glyphs.</summary>
        public static string RosterStarString(float stars, int max = 5)
        {
            float clamped = Mathf.Clamp(stars, 0f, max);
            int full = Mathf.FloorToInt(clamped);
            bool half = clamped - full >= 0.4f && full < max;
            var s = new System.Text.StringBuilder();
            for (int i = 0; i < full; i++)
                s.Append("<color=#E0DC1F>*</color>");
            if (half)
                s.Append("<color=#E0DC1F>+</color>");
            int rest = max - full - (half ? 1 : 0);
            for (int i = 0; i < rest; i++)
                s.Append("<color=#2A3355>*</color>");
            return s.ToString();
        }

        /// <summary>
        /// Front-office facility row: titled section box + progress bar (+ optional CC upgrade button).
        /// </summary>
        public static (TextMeshProUGUI title, Image fill, Button upgrade) FacilityRow(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 size,
            string title,
            bool withUpgrade,
            UnityAction onUpgrade,
            int costCc = 2)
        {
            var box = SectionPanel(parent, name, anchor, size, title, out var titleLab);
            var fill = ProgressBar(box.transform, "Bar", new Vector2(withUpgrade ? 0.42f : 0.5f, 0.38f),
                new Vector2(withUpgrade ? size.x * 0.72f : size.x * 0.88f, 22f), 0.2f, RbYellow);

            Button upgrade = null;
            if (withUpgrade)
            {
                upgrade = GltrCostButton(box.transform, "Up", new Vector2(0.88f, 0.38f),
                    new Vector2(100f, 40f), costCc, onUpgrade, RbBlueDark);
            }

            return (titleLab, fill, upgrade);
        }

        /// <summary>Mini staff card for Front Office (Owner / HC / OC / DC).</summary>
        public static (Image portrait, TextMeshProUGUI nameLab, TextMeshProUGUI posLab, Button button) CoordinatorCard(
            Transform parent,
            string name,
            Vector2 anchor,
            Vector2 size,
            bool offense,
            string posTag,
            Color? faceOverride = null)
        {
            Color face = faceOverride ?? (offense ? RbOffenseCard : RbDefenseCard);
            Color deep = new Color(face.r * 0.55f, face.g * 0.55f, face.b * 0.55f, 1f);

            var card = BorderedBox(parent, name, anchor, size, face);
            var outline = card.GetComponent<Outline>();
            if (outline != null) outline.effectDistance = new Vector2(2.5f, -2.5f);

            var posLab = Label(card.transform, "Pos", new Vector2(0.18f, 0.9f), new Vector2(54f, 22f), 14f);
            posLab.text = posTag;
            posLab.fontStyle = FontStyles.Bold;
            var mood = Label(card.transform, "Mood", new Vector2(0.82f, 0.9f), new Vector2(36f, 22f), 16f);
            mood.text = "☺";
            mood.color = RbYellow;

            var portrait = BorderedBox(card.transform, "Port", new Vector2(0.5f, 0.52f),
                new Vector2(size.x * 0.7f, size.y * 0.48f), deep).GetComponent<Image>();
            Label(portrait.transform, "Eyes", new Vector2(0.5f, 0.55f), new Vector2(70f, 24f), 16f).text = "●  ●";

            var nameBar = BorderedBox(card.transform, "NameBar", new Vector2(0.5f, 0.18f),
                new Vector2(size.x * 0.88f, 26f), RbNameBar);
            var nameLab = Label(nameBar.transform, "Name", new Vector2(0.5f, 0.5f), new Vector2(size.x * 0.8f, 22f), 14f);
            nameLab.fontStyle = FontStyles.Bold;
            nameLab.overflowMode = TextOverflowModes.Ellipsis;

            var bolt = Label(card.transform, "Bolt", new Vector2(0.5f, 0.05f), new Vector2(40f, 16f), 12f);
            bolt.text = "!";
            bolt.color = RbYellow;

            var button = card.AddComponent<Button>();
            button.targetGraphic = card.GetComponent<Image>();
            return (portrait, nameLab, posLab, button);
        }

        /// <summary>Small footer stat box (MORALE / OFFENSE / DEFENSE).</summary>
        public static TextMeshProUGUI FooterStatBox(Transform parent, string name, Vector2 anchor, Vector2 size,
            string title)
        {
            var box = BorderedBox(parent, name, anchor, size, RbPanel);
            var outline = box.GetComponent<Outline>();
            if (outline != null)
                outline.effectDistance = new Vector2(2f, -2f);
            Label(box.transform, "T", new Vector2(0.5f, 0.78f), new Vector2(size.x - 8f, 20f), 14f).text = title;
            var body = Label(box.transform, "Body", new Vector2(0.5f, 0.35f), new Vector2(size.x - 10f, 36f), 20f);
            body.fontStyle = FontStyles.Bold;
            return body;
        }

        public static Canvas EnsureCanvas(string name = "CareerCanvas")
        {
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                var c = existing.GetComponent<Canvas>();
                if (c != null) return c;
            }

            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static GameObject Panel(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            Stretch(rt);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.06f, 0.08f, 0.12f, 0.96f);
            return go;
        }

        public static GameObject SolidPanel(Transform parent, string name, Color color)
        {
            var go = Panel(parent, name);
            go.GetComponent<Image>().color = color;
            return go;
        }

        /// <summary>Blue transparent panel + white border (Training Facility / tutorial chrome).</summary>
        public static void StyleTrainModal(GameObject panel, Color? fill = null)
        {
            if (panel == null) return;
            var img = panel.GetComponent<Image>();
            if (img != null) img.color = fill ?? TrainModalFill;
            var outline = panel.GetComponent<Outline>();
            if (outline == null) outline = panel.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, -3f);
        }

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static TextMeshProUGUI Label(Transform parent, string name, Vector2 anchor, Vector2 size, float font,
            TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment = align;
            tmp.fontSize = font;
            tmp.fontStyle = FontStyles.Normal;
            tmp.color = Color.white;
            tmp.outlineWidth = 0.15f;
            tmp.outlineColor = Color.black;
            tmp.raycastTarget = false;
            GameFonts.Apply(tmp);
            return tmp;
        }

        public static Button Button(Transform parent, string name, Vector2 anchor, Vector2 size, string text, UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.16f, 0.2f, 0.32f, 0.95f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null)
                btn.onClick.AddListener(onClick);

            var label = Label(go.transform, "Label", new Vector2(0.5f, 0.5f), size, 22f);
            Stretch(label.rectTransform);
            label.text = text;
            label.raycastTarget = false;
            return btn;
        }

        public static Button OutlinedButton(Transform parent, string name, Vector2 anchor, Vector2 size, string text,
            UnityAction onClick, Color? fill = null)
        {
            var btn = Button(parent, name, anchor, size, text, onClick);
            btn.GetComponent<Image>().color = fill ?? RbBlueDark;
            var outline = btn.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(2f, -2f);
            var shadow = btn.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.55f);
            shadow.effectDistance = new Vector2(3f, -3f);
            return btn;
        }

        public static GameObject BorderedBox(Transform parent, string name, Vector2 anchor, Vector2 size, Color fill)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = fill;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(2f, -2f);
            return go;
        }

        public static TMP_InputField InputField(Transform parent, string name, Vector2 anchor, Vector2 size, string placeholder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.92f, 0.92f, 0.95f, 1f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            Stretch(trt);
            trt.offsetMin = new Vector2(8f, 4f);
            trt.offsetMax = new Vector2(-8f, -4f);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 22f;
            tmp.color = Color.black;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            GameFonts.Apply(tmp);

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(go.transform, false);
            var prt = phGo.AddComponent<RectTransform>();
            Stretch(prt);
            prt.offsetMin = new Vector2(8f, 4f);
            prt.offsetMax = new Vector2(-8f, -4f);
            var ph = phGo.AddComponent<TextMeshProUGUI>();
            ph.fontSize = 20f;
            ph.fontStyle = FontStyles.Italic;
            ph.color = new Color(0.4f, 0.4f, 0.45f);
            ph.text = placeholder;
            ph.alignment = TextAlignmentOptions.MidlineLeft;
            GameFonts.Apply(ph);

            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = trt;
            field.textComponent = tmp;
            field.placeholder = ph;
            field.fontAsset = GameFonts.Primary;
            return field;
        }

        public static Image ProgressBar(Transform parent, string name, Vector2 anchor, Vector2 size, float fill01, Color fillColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = size;
            var bg = go.AddComponent<Image>();
            bg.color = Color.black;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(go.transform, false);
            var frt = fill.AddComponent<RectTransform>();
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(Mathf.Clamp01(fill01), 1f);
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            var fimg = fill.AddComponent<Image>();
            fimg.color = fillColor;
            return fimg;
        }

        public static void SetProgress(Image fillImg, float fill01)
        {
            if (fillImg == null) return;
            var frt = fillImg.rectTransform;
            frt.anchorMin = new Vector2(0f, 0f);
            frt.anchorMax = new Vector2(Mathf.Clamp01(fill01), 1f);
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
        }

        public static GameObject Modal(Transform parent, string name, string body, string okLabel, UnityAction onOk,
            string altLabel = null, UnityAction onAlt = null)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var rt = root.AddComponent<RectTransform>();
            Stretch(rt);
            var dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.35f);

            var box = BorderedBox(root.transform, "Box", new Vector2(0.5f, 0.52f), new Vector2(780f, 320f), RbPanel);
            var label = Label(box.transform, "Body", new Vector2(0.5f, 0.58f), new Vector2(700f, 220f), 24f);
            label.text = body;
            label.textWrappingMode = TextWrappingModes.Normal;

            OutlinedButton(root.transform, "OK", new Vector2(0.78f, 0.18f), new Vector2(160f, 48f), okLabel ?? "OK", onOk);
            if (!string.IsNullOrEmpty(altLabel) && onAlt != null)
                OutlinedButton(root.transform, "Alt", new Vector2(0.55f, 0.18f), new Vector2(220f, 48f), altLabel, onAlt);

            return root;
        }

        public static string StarString(float stars, int max = 5)
        {
            int full = Mathf.Clamp(Mathf.FloorToInt(stars), 0, max);
            bool half = stars - full >= 0.4f && full < max;
            var s = new System.Text.StringBuilder();
            for (int i = 0; i < full; i++) s.Append("*");
            if (half) s.Append("+");
            int rest = max - full - (half ? 1 : 0);
            for (int i = 0; i < rest; i++) s.Append(".");
            return s.ToString();
        }

        public static float StarsFromOvr(int ovr)
        {
            return Mathf.Clamp(ovr / 20f, 0.5f, 5f);
        }

        public static ScrollRect ScrollList(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = new Vector2(40f, 40f);
            rt.offsetMax = new Vector2(-40f, -120f);
            var scroll = go.AddComponent<ScrollRect>();
            var img = go.AddComponent<Image>();
            img.color = new Color(0.1f, 0.12f, 0.16f, 0.6f);

            var content = new GameObject("Content");
            content.transform.SetParent(go.transform, false);
            var crt = content.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.sizeDelta = new Vector2(0f, 0f);
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 6f;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.content = crt;
            scroll.viewport = rt;
            scroll.horizontal = false;
            scroll.vertical = true;
            return scroll;
        }
    }
}
