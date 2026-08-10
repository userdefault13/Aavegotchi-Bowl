using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.Core;
using RetroBowl.UI;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// After a TD: choose PAT vs 2PT (player) or auto-XP (AI), then kickoff.
    /// Also starts kickoff after field goals.
    /// </summary>
    public class PostScoreFlow : MonoBehaviour
    {
        public static PostScoreFlow Instance { get; private set; }

        GameObject choiceRoot;
        TextMeshProUGUI titleLabel;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var host = GameManager.Instance != null
                ? GameManager.Instance.gameObject
                : new GameObject("PostScoreFlow");
            if (host.GetComponent<PostScoreFlow>() == null)
                host.AddComponent<PostScoreFlow>();
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Called instead of a normal ReadyNextPlay when a score try / kickoff is pending.
        /// Returns true if this flow consumed the continue.
        /// </summary>
        public static bool TryHandleAfterPlayBanner()
        {
            EnsureExists();
            return Instance.HandleInternal();
        }

        /// <summary>
        /// Week-game open: coin toss → kickoff (receive or kick from the toss).
        /// </summary>
        public static void BeginOpeningKickoff()
        {
            EnsureExists();
            Instance.BeginOpeningKickoffInternal();
        }

        void BeginOpeningKickoffInternal()
        {
            HideTryChoice();
            if (KickingController.Instance != null)
                KickingController.Instance.Cancel();

            // Empty field during coin toss.
            FormationRoster.HideForKickoffIntro();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.waitingForNextPlay = true;
                GameManager.Instance.isPreSnap = true;
                GameManager.Instance.isKicking = false;
            }

            // Visitor calls → winner receive/kick → other picks direction → kickoff.
            CoinTossFlow.Begin();
        }

        bool HandleInternal()
        {
            if (FieldManager.Instance == null || GameManager.Instance == null)
                return false;

            // Failed 2-point play → kickoff (banner may already say NO GOOD).
            if (FieldManager.Instance.JustFailedTwoPoint
                || (FieldManager.Instance.IsTwoPointAttempt
                    && !FieldManager.Instance.JustScoredTouchdown
                    && !FieldManager.Instance.JustConvertedTwoPoint
                    && !FieldManager.Instance.PendingTryAfterTd))
            {
                if (FieldManager.Instance.IsTwoPointAttempt)
                    FieldManager.Instance.FailTwoPointAttempt();
                BeginKickoff();
                return true;
            }

            // Successful 2-point → kickoff.
            if (FieldManager.Instance.JustConvertedTwoPoint
                && FieldManager.Instance.PendingKickoff)
            {
                BeginKickoff();
                return true;
            }

            if (FieldManager.Instance.PendingTryAfterTd)
            {
                if (FieldManager.Instance.LastScorerWasPlayer)
                {
                    ShowTryChoice();
                    return true;
                }

                // AI auto extra point.
                AutoResolveAiExtraPoint();
                return true;
            }

            if (FieldManager.Instance.PendingKickoff)
            {
                BeginKickoff();
                return true;
            }

            return false;
        }

        void ShowTryChoice()
        {
            EnsureChoiceUi();
            choiceRoot.SetActive(true);
            if (titleLabel != null)
                titleLabel.text = "TOUCHDOWN!\nEXTRA POINT OR 2-POINT?";
            if (GameManager.Instance != null)
            {
                GameManager.Instance.isPreSnap = true;
                GameManager.Instance.waitingForNextPlay = false;
            }
        }

        void HideTryChoice()
        {
            if (choiceRoot != null)
                choiceRoot.SetActive(false);
        }

        public void ChooseExtraPoint()
        {
            HideTryChoice();
            if (FieldManager.Instance == null) return;
            FieldManager.Instance.SpotForExtraPoint();
            // Don't let the choice click / key become the kick confirm.
            if (GameManager.Instance != null)
                GameManager.Instance.SuppressNextPlayClicks(12);
            // Begin parks tee + kicker, shows aim/power; approach/flight on confirm.
            KickingController.Begin(KickMode.ExtraPoint);
        }

        public void ChooseTwoPoint()
        {
            HideTryChoice();
            if (FieldManager.Instance == null || GameManager.Instance == null) return;

            FieldManager.Instance.SpotForTwoPoint();
            GameManager.Instance.waitingForNextPlay = false;
            GameManager.Instance.SkipPostScoreFlowOnce();
            GameManager.Instance.SuppressNextPlayClicks(12);
            GameManager.Instance.ReadyNextPlay();

            var playUi = Object.FindAnyObjectByType<PlayCallingUI>();
            if (playUi != null)
                playUi.ForceResetSelection();

            PlayBanner.Show("GO FOR TWO!", 1.15f, BannerTone.Positive);
        }

        void AutoResolveAiExtraPoint()
        {
            HideTryChoice();
            bool good = Random.value > 0.08f;
            FieldManager.Instance.ResolveExtraPoint(good);
            string msg = good ? "EXTRA POINT\nGOOD" : "EXTRA POINT\nNO GOOD";
            PlayBanner.ShowPlayOver(msg, good ? BannerTone.Positive : BannerTone.Neutral);
            // Next continue → kickoff via PendingKickoff.
        }

        void BeginKickoff()
        {
            HideTryChoice();
            if (FieldManager.Instance == null) return;

            // Receiving team is the non-scorer (or FG defense).
            bool receiverIsPlayer = !FieldManager.Instance.LastScorerWasPlayer;
            FieldManager.Instance.PrepareKickoff(receiverIsPlayer);
            FormationRoster.PlaceKickoffReturn(receiverIsPlayer);
            if (GameManager.Instance != null)
                GameManager.Instance.SuppressNextPlayClicks(8);
            KickingController.Begin(KickMode.Kickoff);
        }

        void Update()
        {
            if (choiceRoot == null || !choiceRoot.activeSelf) return;
            if (GameManager.Instance != null && GameManager.Instance.SuppressPlayClick)
                return;

            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                ChooseExtraPoint();
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                ChooseTwoPoint();
        }

        void EnsureChoiceUi()
        {
            if (choiceRoot != null) return;

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("PostScoreCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 60;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            choiceRoot = new GameObject("TryChoiceUi");
            choiceRoot.transform.SetParent(canvas.transform, false);
            var rootRt = choiceRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            titleLabel = MakeLabel(choiceRoot.transform, "TryTitle", new Vector2(0.5f, 0.72f), 44f);

            MakeButton(choiceRoot.transform, "BtnXp", new Vector2(0.5f, 0.48f), "1  ·  EXTRA POINT", ChooseExtraPoint);
            MakeButton(choiceRoot.transform, "Btn2pt", new Vector2(0.5f, 0.34f), "2  ·  GO FOR TWO", ChooseTwoPoint);

            var hint = MakeLabel(choiceRoot.transform, "TryHint", new Vector2(0.5f, 0.18f), 24f);
            hint.text = "PRESS 1 OR 2";
            choiceRoot.SetActive(false);
        }

        static TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 anchor, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1000f, 120f);
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

        static void MakeButton(Transform parent, string name, Vector2 anchor, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(420f, 64f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.14f, 0.2f, 0.92f);
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            var trt = textGo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            GameFonts.Apply(tmp);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 28f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.text = label;
            tmp.raycastTarget = false;
        }

        public void Cancel()
        {
            HideTryChoice();
            if (CoinTossFlow.Instance != null)
                CoinTossFlow.Instance.Cancel();
        }
    }
}
