using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.UI;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Tecmo-style kickoff returner pick before the opponent kick flies.
    /// Cycle with J/K (prev/next), confirm with Enter/click, or auto-pick on timeout.
    /// </summary>
    public class KickReturnerSelect : MonoBehaviour
    {
        public static KickReturnerSelect Instance { get; private set; }
        public static bool IsOpen => Instance != null && Instance.open;

        static readonly string[] Candidates = { "RB", "WR_Top", "WR_Bot", "TE", "Quarterback" };

        bool open;
        int index;
        float closesAt;
        Action<string> onDone;

        GameObject uiRoot;
        TextMeshProUGUI titleLabel;
        TextMeshProUGUI nameLabel;
        TextMeshProUGUI hintLabel;

        public static void Begin(Action<string> onSelected)
        {
            EnsureExists();
            Instance.BeginInternal(onSelected);
        }

        static void EnsureExists()
        {
            if (Instance != null) return;
            var host = new GameObject("KickReturnerSelect");
            DontDestroyOnLoad(host);
            host.AddComponent<KickReturnerSelect>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void BeginInternal(Action<string> onSelected)
        {
            onDone = onSelected;
            open = true;
            index = 0;
            // Prefer designated / RB if still valid.
            string prefer = FormationRoster.DesignatedKickReturnerName;
            if (string.IsNullOrEmpty(prefer)) prefer = "RB";
            for (int i = 0; i < Candidates.Length; i++)
            {
                if (Candidates[i] == prefer)
                {
                    index = i;
                    break;
                }
            }

            closesAt = Time.unscaledTime + 8f;
            EnsureUi();
            if (uiRoot != null) uiRoot.SetActive(true);
            Refresh();
        }

        void Update()
        {
            if (!open) return;

            if (TecmoInput.CycleNextDown() || Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.E))
            {
                index = (index + 1) % Candidates.Length;
                Refresh();
            }
            else if (TecmoInput.CyclePrevDown()
                     || Input.GetKeyDown(KeyCode.LeftArrow)
                     || Input.GetKeyDown(KeyCode.Q))
            {
                index = (index - 1 + Candidates.Length) % Candidates.Length;
                Refresh();
            }

            bool confirm = TecmoInput.ConfirmDown();
            if (confirm || Time.unscaledTime >= closesAt)
                Confirm();
        }

        void Confirm()
        {
            if (!open) return;
            open = false;
            string name = Candidates[Mathf.Clamp(index, 0, Candidates.Length - 1)];
            if (uiRoot != null) uiRoot.SetActive(false);

            var cb = onDone;
            onDone = null;
            cb?.Invoke(name);
        }

        void Refresh()
        {
            string name = Candidates[Mathf.Clamp(index, 0, Candidates.Length - 1)];
            if (nameLabel != null)
                nameLabel.text = FormatName(name);
            if (hintLabel != null)
            {
                float left = Mathf.Max(0f, closesAt - Time.unscaledTime);
                hintLabel.text = $"J PREV  ·  K NEXT  ·  ENTER CONFIRM  ·  AUTO {left:0}s";
            }
        }

        static string FormatName(string unit)
        {
            return unit switch
            {
                "WR_Top" => "WR TOP",
                "WR_Bot" => "WR BOT",
                "Quarterback" => "QB",
                _ => unit
            };
        }

        void EnsureUi()
        {
            if (uiRoot != null) return;

            var canvasGo = new GameObject("ReturnerSelectCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            uiRoot = new GameObject("ReturnerSelectUi");
            uiRoot.transform.SetParent(canvasGo.transform, false);
            var rootRt = uiRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var dim = new GameObject("Dim");
            dim.transform.SetParent(uiRoot.transform, false);
            var dimRt = dim.AddComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.45f);
            dimImg.raycastTarget = false;

            titleLabel = MakeLabel(uiRoot.transform, "Title", new Vector2(0.5f, 0.72f), 40f);
            titleLabel.text = "SELECT RETURNER";
            nameLabel = MakeLabel(uiRoot.transform, "Name", new Vector2(0.5f, 0.52f), 56f);
            hintLabel = MakeLabel(uiRoot.transform, "Hint", new Vector2(0.5f, 0.28f), 24f);
        }

        static TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 anchor, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 80f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            GameFonts.Apply(tmp);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = new Color(0f, 0f, 0f, 0.9f);
            tmp.raycastTarget = false;
            return tmp;
        }
    }
}
