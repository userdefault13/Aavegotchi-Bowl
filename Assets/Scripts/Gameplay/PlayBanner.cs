using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.Core;
using RetroBowl.UI;

namespace RetroBowl.Gameplay
{
    /// <summary>Big center callout (incomplete, interception, etc.).</summary>
    public class PlayBanner : MonoBehaviour
    {
        public static PlayBanner Instance { get; private set; }

        TextMeshProUGUI label;
        TextMeshProUGUI hintLabel;
        float hideAt;
        bool awaitingNextPlayClick;
        bool awaitingKickoffIntroClick;
        Action onKickoffIntroContinue;
        GameObject kickoffIntroRoot;
        float punchUntil;
        Vector3 labelBaseScale = Vector3.one;

        void Awake()
        {
            Instance = this;
            EnsureUi();
            Hide();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Timed flash (e.g. mid-play "SCRAMBLE" / "TIPPED") — auto-hides.</summary>
        public static void Show(string message, float seconds = 2.2f, BannerTone tone = BannerTone.Neutral)
        {
            EnsureInstance();
            Instance.ShowTimed(message, seconds, tone);
        }

        /// <summary>End-of-play result — stays until click / Space / Enter, then next play.</summary>
        public static void ShowPlayOver(string message, BannerTone tone = BannerTone.Neutral)
        {
            EnsureInstance();
            Instance.ShowPlayOverInternal(message, tone);
        }

        /// <summary>
        /// Opening kickoff stacked bubbles: GET READY! + Receive (football icon stub).
        /// Click / Space / Enter continues via <paramref name="onContinue"/>.
        /// </summary>
        public static void ShowKickoffIntro(Action onContinue)
        {
            EnsureInstance();
            Instance.ShowKickoffIntroInternal(onContinue);
        }

        /// <summary>Hide immediately (e.g. quarter expired after the play).</summary>
        public static void ForceHide()
        {
            if (Instance == null) return;
            Instance.Hide();
        }

        static void EnsureInstance()
        {
            if (Instance != null) return;
            var host = new GameObject("PlayBanner");
            Instance = host.AddComponent<PlayBanner>();
        }

        void EnsureUi()
        {
            if (label != null) return;

            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("BannerCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            var go = new GameObject("BannerText");
            go.transform.SetParent(canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.55f);
            rt.anchorMax = new Vector2(0.5f, 0.55f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1100f, 280f);

            label = go.AddComponent<TextMeshProUGUI>();
            GameFonts.Apply(label);
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 48f;
            label.enableAutoSizing = true;
            label.fontSizeMin = 28f;
            label.fontSizeMax = 60f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.outlineWidth = 0.25f;
            label.outlineColor = new Color(0f, 0f, 0f, 0.9f);
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;

            var hintGo = new GameObject("BannerHint");
            hintGo.transform.SetParent(canvas.transform, false);
            var hintRt = hintGo.AddComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.5f, 0.30f);
            hintRt.anchorMax = new Vector2(0.5f, 0.30f);
            hintRt.pivot = new Vector2(0.5f, 0.5f);
            hintRt.sizeDelta = new Vector2(900f, 48f);

            hintLabel = hintGo.AddComponent<TextMeshProUGUI>();
            GameFonts.Apply(hintLabel);
            hintLabel.alignment = TextAlignmentOptions.Center;
            hintLabel.fontSize = 28f;
            hintLabel.fontStyle = FontStyles.Bold;
            hintLabel.color = new Color(1f, 1f, 1f, 0.85f);
            hintLabel.outlineWidth = 0.2f;
            hintLabel.outlineColor = new Color(0f, 0f, 0f, 0.85f);
            hintLabel.raycastTarget = false;
            hintLabel.text = "CLICK FOR NEXT PLAY";
            hintGo.SetActive(false);
        }

        void ShowTimed(string message, float seconds, BannerTone tone)
        {
            EnsureUi();
            ClearKickoffIntro();
            awaitingNextPlayClick = false;
            if (hintLabel != null)
                hintLabel.gameObject.SetActive(false);
            label.gameObject.SetActive(true);
            label.text = message;
            label.color = ColorForTone(tone);
            hideAt = Time.time + seconds;
            BeginPunch();
            ReactToMessage(message, tone);
        }

        void ShowPlayOverInternal(string message, BannerTone tone)
        {
            EnsureUi();
            ClearKickoffIntro();
            awaitingNextPlayClick = true;
            label.gameObject.SetActive(true);
            label.text = message;
            label.color = ColorForTone(tone);
            if (hintLabel != null)
            {
                hintLabel.text = "CLICK FOR NEXT PLAY";
                hintLabel.gameObject.SetActive(true);
            }
            hideAt = float.PositiveInfinity;
            BeginPunch();
            ReactToMessage(message, tone);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.waitingForNextPlay = true;

                // Clock already hit 0 during this play — end quarter now that the play is over.
                if (GameManager.Instance.NotifyPlayCompleted())
                {
                    awaitingNextPlayClick = false;
                    Hide();
                }
            }
        }

        void ShowKickoffIntroInternal(Action onContinue)
        {
            EnsureUi();
            EnsureKickoffIntroUi();

            awaitingNextPlayClick = false;
            awaitingKickoffIntroClick = true;
            onKickoffIntroContinue = onContinue;
            hideAt = float.PositiveInfinity;

            if (label != null)
                label.gameObject.SetActive(false);
            if (hintLabel != null)
                hintLabel.gameObject.SetActive(false);
            if (kickoffIntroRoot != null)
                kickoffIntroRoot.SetActive(true);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.waitingForNextPlay = true;
                // Freeze live play — do not clear pre-snap (kickoff walls sit past tee LOS).
                GameManager.Instance.isPreSnap = true;
            }

            MatchPresentation.GetReady();
        }

        void EnsureKickoffIntroUi()
        {
            if (kickoffIntroRoot != null) return;

            var canvas = label != null
                ? label.canvas
                : UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("BannerCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            kickoffIntroRoot = new GameObject("KickoffIntro");
            kickoffIntroRoot.transform.SetParent(canvas.transform, false);
            var rootRt = kickoffIntroRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // Stacked Retro Bowl bubbles — GET READY! above Receive + football stub.
            MakeIntroBubble(kickoffIntroRoot.transform, "GetReadyBubble",
                new Vector2(0.5f, 0.58f), new Vector2(360f, 72f), "GET READY!");
            MakeIntroBubble(kickoffIntroRoot.transform, "ReceiveBubble",
                new Vector2(0.5f, 0.46f), new Vector2(300f, 96f), "Receive", withFootballStub: true);

            kickoffIntroRoot.SetActive(false);
        }

        static void MakeIntroBubble(Transform parent, string name, Vector2 anchor, Vector2 size,
            string text, bool withFootballStub = false)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.06f, 0.07f, 0.1f, 0.94f);
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            float labelHeight = withFootballStub ? size.y * 0.48f : size.y * 0.85f;
            float labelY = withFootballStub ? 0.68f : 0.5f;

            var textGo = new GameObject("Txt");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.5f, labelY);
            trt.anchorMax = new Vector2(0.5f, labelY);
            trt.pivot = new Vector2(0.5f, 0.5f);
            trt.sizeDelta = new Vector2(size.x - 24f, labelHeight);
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            GameFonts.Apply(tmp);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = withFootballStub ? 34f : 40f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.outlineWidth = 0.18f;
            tmp.outlineColor = Color.black;
            tmp.raycastTarget = false;
            tmp.text = text;

            if (withFootballStub)
                MakeFootballStub(go.transform);
        }

        /// <summary>Simple pixel-style football + motion lines (no asset rip required).</summary>
        static void MakeFootballStub(Transform parent)
        {
            var stub = new GameObject("FootballStub");
            stub.transform.SetParent(parent, false);
            var srt = stub.AddComponent<RectTransform>();
            srt.anchorMin = new Vector2(0.5f, 0.22f);
            srt.anchorMax = new Vector2(0.5f, 0.22f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.sizeDelta = new Vector2(120f, 28f);

            // Motion lines (left of ball).
            for (int i = 0; i < 3; i++)
            {
                var line = new GameObject($"Motion{i}");
                line.transform.SetParent(stub.transform, false);
                var lrt = line.AddComponent<RectTransform>();
                lrt.anchorMin = new Vector2(0.12f, 0.25f + i * 0.22f);
                lrt.anchorMax = new Vector2(0.12f, 0.25f + i * 0.22f);
                lrt.pivot = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(18f - i * 2f, 3f);
                var limg = line.AddComponent<Image>();
                limg.color = Color.white;
                limg.raycastTarget = false;
            }

            var ball = new GameObject("Ball");
            ball.transform.SetParent(stub.transform, false);
            var brt = ball.AddComponent<RectTransform>();
            brt.anchorMin = new Vector2(0.62f, 0.5f);
            brt.anchorMax = new Vector2(0.62f, 0.5f);
            brt.pivot = new Vector2(0.5f, 0.5f);
            brt.sizeDelta = new Vector2(36f, 20f);
            var bimg = ball.AddComponent<Image>();
            bimg.color = new Color(0.95f, 0.95f, 0.95f, 1f);
            bimg.raycastTarget = false;

            // Lace mark.
            var lace = new GameObject("Lace");
            lace.transform.SetParent(ball.transform, false);
            var laceRt = lace.AddComponent<RectTransform>();
            laceRt.anchorMin = new Vector2(0.5f, 0.5f);
            laceRt.anchorMax = new Vector2(0.5f, 0.5f);
            laceRt.pivot = new Vector2(0.5f, 0.5f);
            laceRt.sizeDelta = new Vector2(14f, 3f);
            var laceImg = lace.AddComponent<Image>();
            laceImg.color = new Color(0.15f, 0.15f, 0.18f, 1f);
            laceImg.raycastTarget = false;
        }

        void ClearKickoffIntro()
        {
            awaitingKickoffIntroClick = false;
            onKickoffIntroContinue = null;
            if (kickoffIntroRoot != null)
                kickoffIntroRoot.SetActive(false);
        }

        void BeginPunch()
        {
            if (label == null) return;
            labelBaseScale = Vector3.one;
            label.rectTransform.localScale = labelBaseScale * 1.18f;
            punchUntil = Time.unscaledTime + 0.22f;
        }

        void TickPunch()
        {
            if (label == null || !label.gameObject.activeSelf) return;
            if (Time.unscaledTime >= punchUntil)
            {
                label.rectTransform.localScale = labelBaseScale;
                return;
            }

            float t = 1f - Mathf.Clamp01((punchUntil - Time.unscaledTime) / 0.22f);
            float s = Mathf.Lerp(1.18f, 1f, t * t);
            label.rectTransform.localScale = labelBaseScale * s;
        }

        static void ReactToMessage(string message, BannerTone tone)
        {
            if (string.IsNullOrEmpty(message)) return;
            string m = message.ToUpperInvariant();

            if (m.Contains("TOUCHDOWN") || m.Contains("PICK SIX")
                || m.Contains("FIELD GOAL\nGOOD") || m.Contains("EXTRA POINT\nGOOD")
                || m.Contains("2-POINT GOOD"))
            {
                MatchPresentation.Score();
                return;
            }

            if (m.Contains("INTERCEPTION") || tone == BannerTone.Turnover)
            {
                MatchPresentation.Interception();
                return;
            }

            if (m.Contains("TIPPED") || tone == BannerTone.Tip)
            {
                MatchPresentation.Tip();
                return;
            }

            if (m.Contains("INCOMPLETE") || m.Contains("DROPPED") || m.Contains("NO GOOD") || m.Contains("SHORT"))
            {
                MatchPresentation.Incomplete();
                return;
            }

            if (m.Contains("FIRST DOWN"))
                MatchPresentation.FirstDown();
        }

        void Update()
        {
            TickPunch();

            if (awaitingKickoffIntroClick)
            {
                if (TecmoInput.ConfirmDown())
                {
                    if (GameManager.Instance != null && GameManager.Instance.SuppressPlayClick)
                        return;
                    ContinueKickoffIntro();
                }
                return;
            }

            if (awaitingNextPlayClick)
            {
                // Soft pulse on the hint.
                if (hintLabel != null && hintLabel.gameObject.activeSelf)
                {
                    float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 2.4f));
                    var c = hintLabel.color;
                    c.a = a;
                    hintLabel.color = c;
                }

                if (TecmoInput.ConfirmDown())
                {
                    if (GameManager.Instance != null && GameManager.Instance.SuppressPlayClick)
                        return;
                    ContinueToNextPlay();
                }
                return;
            }

            if (label != null && label.gameObject.activeSelf && Time.time >= hideAt)
                Hide();
        }

        void ContinueKickoffIntro()
        {
            var cb = onKickoffIntroContinue;
            ClearKickoffIntro();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.waitingForNextPlay = false;
                // Same click must not also fire the kick.
                GameManager.Instance.SuppressNextPlayClicks(12);
            }
            cb?.Invoke();
        }

        void ContinueToNextPlay()
        {
            awaitingNextPlayClick = false;
            Hide();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.waitingForNextPlay = false;
                // Same Space/Enter/A must not instantly confirm the play-call modal.
                GameManager.Instance.SuppressNextPlayClicks(12);

                // Clock hit 0 while this banner was up — end quarter instead of
                // ReadyNextPlay (which would race EndQuarter / double-init formations).
                if (GameManager.Instance.NotifyPlayCompleted())
                    return;
            }

            var ui = UnityEngine.Object.FindAnyObjectByType<PlayCallingUI>();
            if (ui != null)
                ui.CompleteCurrentPlay();
            else if (GameManager.Instance != null)
                GameManager.Instance.ReadyNextPlay();
        }

        void Hide()
        {
            awaitingNextPlayClick = false;
            ClearKickoffIntro();
            if (label != null)
            {
                label.text = "";
                label.color = Color.white;
                label.gameObject.SetActive(false);
            }
            if (hintLabel != null)
                hintLabel.gameObject.SetActive(false);
        }

        static Color ColorForTone(BannerTone tone) => tone switch
        {
            BannerTone.Tip => new Color(1f, 0.92f, 0.35f),
            BannerTone.Turnover => new Color(1f, 0.45f, 0.35f),
            BannerTone.Positive => new Color(0.45f, 1f, 0.55f),
            _ => Color.white
        };
    }

    public enum BannerTone
    {
        Neutral,
        Tip,
        Turnover,
        Positive
    }
}
