using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.Core;
using RetroBowl.Managers;
using RetroBowl.UI.Career;
using RetroBowl.UI;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Pre-game coin toss (NFL-style, simplified):
    ///   1. Visiting team calls heads/tails (UNI visitor; USDC is home).
    ///   2. Winner chooses Receive or Kick (kick ⇒ receive to open the 2nd half).
    ///   3. The other team chooses kick direction (L→R or R→L).
    /// </summary>
    public class CoinTossFlow : MonoBehaviour
    {
        public static CoinTossFlow Instance { get; private set; }

        enum Phase
        {
            Call,
            FlipReveal,
            WinnerChoice,
            AiChoiceSummary,
            DirectionChoice,
            ReadyToStart
        }

        Phase phase;
        GameObject root;
        TextMeshProUGUI titleLabel;
        TextMeshProUGUI bodyLabel;
        TextMeshProUGUI hintLabel;
        GameObject btnA;
        GameObject btnB;
        TextMeshProUGUI btnALabel;
        TextMeshProUGUI btnBLabel;
        int focusBtn;
        /// <summary>Ignore continue clicks until after this frame (avoids button click double-advance).</summary>
        int continueArmedFrame;

        bool visitorCalledHeads;
        bool coinIsHeads;
        bool playerWonToss;
        bool playerChoosesReceiveKick;
        bool firstHalfReceiverIsPlayer;
        int openingKickDirection = 1;

        string PlayerAbbrev => TeamManager.Instance != null
            ? TeamManager.Instance.PlayerTeamAbbrev()
            : "USDC";
        string OppAbbrev => TeamManager.Instance != null
            ? TeamManager.Instance.OpponentTeamAbbrev()
            : "UNI";
        string HomeAbbrev => TeamManager.Instance != null
            ? TeamManager.Instance.HomeTeamAbbrev()
            : PlayerAbbrev;
        string VisitorAbbrev => TeamManager.Instance != null
            ? TeamManager.Instance.VisitorTeamAbbrev()
            : OppAbbrev;
        bool PlayerIsHome => TeamManager.Instance == null || TeamManager.Instance.playerIsHome;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var host = GameManager.Instance != null
                ? GameManager.Instance.gameObject
                : new GameObject("CoinTossFlow");
            if (host.GetComponent<CoinTossFlow>() == null)
                host.AddComponent<CoinTossFlow>();
        }

        public static void Begin()
        {
            EnsureExists();
            Instance.BeginInternal();
        }

        void Awake() => Instance = this;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void BeginInternal()
        {
            EnsureUi();
            phase = Phase.Call;
            playerWonToss = false;
            firstHalfReceiverIsPlayer = true;
            openingKickDirection = 1;

            if (KickingController.Instance != null)
                KickingController.Instance.Cancel();
            FormationRoster.HideForKickoffIntro();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.waitingForNextPlay = true;
                GameManager.Instance.isPreSnap = true;
                GameManager.Instance.isKicking = false;
                GameManager.Instance.SuppressNextPlayClicks(10);
            }

            ShowCallPhase();
        }

        public void Cancel()
        {
            if (root != null)
                root.SetActive(false);
            phase = Phase.ReadyToStart;
        }

        void ShowCallPhase()
        {
            phase = Phase.Call;
            root.SetActive(true);
            titleLabel.text = "COIN TOSS";
            // USDC home / UNI visitor — visiting team calls.
            if (PlayerIsHome)
            {
                bodyLabel.text = $"HOME {HomeAbbrev}  ·  VISITOR {VisitorAbbrev}\nVISITOR CALLS THE TOSS...";
                hintLabel.text = "";
                SetButtons(null, null, false);
                CancelInvoke(nameof(AutoVisitorCall));
                Invoke(nameof(AutoVisitorCall), 0.85f);
            }
            else
            {
                bodyLabel.text = $"VISITOR ({VisitorAbbrev}) CALLS\nHEADS OR TAILS?";
                hintLabel.text = "PRESS 1 OR 2";
                SetButtons("1  ·  HEADS", "2  ·  TAILS", true);
            }
        }

        /// <summary>UNI (visitor) auto-calls when the player is home.</summary>
        void AutoVisitorCall()
        {
            if (phase != Phase.Call || root == null || !root.activeSelf) return;
            ResolveVisitorCall(heads: Random.value < 0.5f);
        }

        void OnCallHeads() => ResolveVisitorCall(heads: true);
        void OnCallTails() => ResolveVisitorCall(heads: false);

        void ResolveVisitorCall(bool heads)
        {
            if (phase != Phase.Call) return;
            visitorCalledHeads = heads;
            coinIsHeads = Random.value < 0.5f;
            bool visitorWon = visitorCalledHeads == coinIsHeads;
            // Visitor won ⇒ player lost (and vice versa), since player is home.
            playerWonToss = PlayerIsHome ? !visitorWon : visitorWon;
            playerChoosesReceiveKick = playerWonToss;

            phase = Phase.FlipReveal;
            string face = coinIsHeads ? "HEADS" : "TAILS";
            string call = visitorCalledHeads ? "HEADS" : "TAILS";
            string winner = playerWonToss ? PlayerAbbrev : OppAbbrev;
            titleLabel.text = face + "!";
            bodyLabel.text = $"{VisitorAbbrev} CALLED {call}\n{winner} WINS THE TOSS";
            hintLabel.text = "CLICK / SPACE TO CONTINUE";
            SetButtons(null, null, false);
            ArmContinue();
        }

        void ArmContinue() => continueArmedFrame = Time.frameCount + 1;

        bool ContinueReady => Time.frameCount > continueArmedFrame;

        void ShowWinnerChoicePlayer()
        {
            phase = Phase.WinnerChoice;
            titleLabel.text = $"{PlayerAbbrev} WINS TOSS";
            bodyLabel.text = "RECEIVE OR KICK?\n(KICK → RECEIVE 2ND HALF)";
            hintLabel.text = "PRESS 1 OR 2";
            SetButtons("1  ·  RECEIVE", "2  ·  KICK", true);
        }

        void OnChooseReceive() => ResolveWinnerChoice(receive: true);
        void OnChooseKick() => ResolveWinnerChoice(receive: false);

        void ResolveAiReceiveKickChoice()
        {
            // Opponent won — prefer receive (~65%).
            bool aiReceivesFirst = Random.value < 0.65f;
            firstHalfReceiverIsPlayer = !aiReceivesFirst;

            if (FieldManager.Instance != null)
            {
                FieldManager.Instance.SecondHalfReceiverIsPlayer = !firstHalfReceiverIsPlayer;
                FieldManager.Instance.HasSecondHalfKickoffPending = true;
            }

            phase = Phase.AiChoiceSummary;
            titleLabel.text = $"{OppAbbrev} WINS TOSS";
            bodyLabel.text = aiReceivesFirst
                ? $"{OppAbbrev} ELECTS TO RECEIVE\n{PlayerAbbrev} RECEIVES 2ND HALF"
                : $"{OppAbbrev} ELECTS TO KICK\n{OppAbbrev} RECEIVES 2ND HALF";
            hintLabel.text = "CLICK / SPACE — CHOOSE DIRECTION";
            SetButtons(null, null, false);
            ArmContinue();
        }

        void ResolveWinnerChoice(bool receive)
        {
            if (phase != Phase.WinnerChoice || !playerChoosesReceiveKick) return;

            firstHalfReceiverIsPlayer = receive;
            if (FieldManager.Instance != null)
            {
                // Kick ⇒ player receives 2nd half; Receive ⇒ opponent receives 2nd half.
                FieldManager.Instance.SecondHalfReceiverIsPlayer = !receive;
                FieldManager.Instance.HasSecondHalfKickoffPending = true;
            }

            // Other team (AI) chooses direction.
            openingKickDirection = Random.value < 0.5f ? 1 : -1;
            if (FieldManager.Instance != null)
                FieldManager.Instance.FirstHalfKickDirection = openingKickDirection;

            string dir = openingKickDirection > 0 ? "LEFT → RIGHT" : "RIGHT → LEFT";
            titleLabel.text = "DIRECTION";
            bodyLabel.text = receive
                ? $"{PlayerAbbrev} WILL RECEIVE\n{OppAbbrev} CHOOSES {dir}"
                : $"{PlayerAbbrev} WILL KICK\n{OppAbbrev} CHOOSES {dir}";
            hintLabel.text = "CLICK / SPACE TO START";
            SetButtons(null, null, false);
            phase = Phase.ReadyToStart;
            ArmContinue();
        }

        void ShowDirectionChoicePlayer()
        {
            phase = Phase.DirectionChoice;
            string role = firstHalfReceiverIsPlayer ? "RECEIVE" : "KICK";
            titleLabel.text = $"{PlayerAbbrev} CHOOSES DIRECTION";
            bodyLabel.text = $"YOU WILL {role} FIRST HALF\nPICK KICK DIRECTION";
            hintLabel.text = "PRESS 1 OR 2";
            SetButtons("1  ·  KICK LEFT → RIGHT", "2  ·  KICK RIGHT → LEFT", true);
        }

        void OnDirLeftToRight() => ResolveDirection(1);
        void OnDirRightToLeft() => ResolveDirection(-1);

        void ResolveDirection(int kickDir)
        {
            if (phase != Phase.DirectionChoice) return;
            openingKickDirection = kickDir > 0 ? 1 : -1;
            if (FieldManager.Instance != null)
                FieldManager.Instance.FirstHalfKickDirection = openingKickDirection;

            string dir = openingKickDirection > 0 ? "LEFT → RIGHT" : "RIGHT → LEFT";
            string recv = firstHalfReceiverIsPlayer ? PlayerAbbrev : OppAbbrev;
            titleLabel.text = "KICKOFF";
            bodyLabel.text = $"{recv} RECEIVES\nKICK {dir}";
            hintLabel.text = "CLICK / SPACE TO START";
            SetButtons(null, null, false);
            phase = Phase.ReadyToStart;
            ArmContinue();
        }

        void StartOpeningKickoff()
        {
            if (root != null)
                root.SetActive(false);

            if (FieldManager.Instance == null) return;

            FieldManager.Instance.PrepareOpeningKickoff(
                firstHalfReceiverIsPlayer,
                openingKickDirection);
            FormationRoster.PlaceKickoffReturn(firstHalfReceiverIsPlayer);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.waitingForNextPlay = false;
                GameManager.Instance.SuppressNextPlayClicks(12);
            }

            string bubble = firstHalfReceiverIsPlayer ? "RECEIVE" : "KICKOFF";
            PlayBanner.Show($"GET READY!\n{bubble}", 1.1f, BannerTone.Neutral);
            KickingController.Begin(KickMode.Kickoff);
        }

        /// <summary>Halftime — deferred receive from the coin toss.</summary>
        public static bool TryBeginSecondHalfKickoff()
        {
            EnsureExists();
            return Instance.TryBeginSecondHalfKickoffInternal();
        }

        bool TryBeginSecondHalfKickoffInternal()
        {
            if (FieldManager.Instance == null
                || !FieldManager.Instance.HasSecondHalfKickoffPending)
                return false;

            bool recv = FieldManager.Instance.SecondHalfReceiverIsPlayer;
            int firstDir = FieldManager.Instance.FirstHalfKickDirection;
            // Switch ends — kick the opposite way.
            int kickDir = -FieldManager.NormDir(firstDir);

            FieldManager.Instance.HasSecondHalfKickoffPending = false;
            FieldManager.Instance.PrepareOpeningKickoff(recv, kickDir);
            FormationRoster.PlaceKickoffReturn(recv);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.waitingForNextPlay = false;
                GameManager.Instance.isPreSnap = true;
                GameManager.Instance.SuppressNextPlayClicks(12);
            }

            string who = recv ? PlayerAbbrev : OppAbbrev;
            PlayBanner.Show($"2ND HALF\n{who} RECEIVES", 1.35f, BannerTone.Neutral);
            KickingController.Begin(KickMode.Kickoff);
            return true;
        }

        void Update()
        {
            if (root == null || !root.activeSelf) return;

            // Choice phases (1/2 buttons) respect suppress so the confirm that opened
            // this screen cannot also pick HEADS. Continue-only phases always accept input —
            // PointerTapDown rejects clicks over our dim overlay, so we use raw click here.
            bool suppress = GameManager.Instance != null && GameManager.Instance.SuppressPlayClick;
            bool menuConfirm = TecmoInput.MenuConfirmDown();
            bool one = Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
            bool two = Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
            bool continuePress = ContinueDown();

            if (TecmoInput.MoveUpDown() || TecmoInput.MoveLeftDown())
                SetFocusBtn(0);
            else if (TecmoInput.MoveDownDown() || TecmoInput.MoveRightDown())
                SetFocusBtn(1);

            switch (phase)
            {
                case Phase.Call:
                    // Home team (USDC) waits for visitor AI auto-call.
                    if (PlayerIsHome) return;
                    if (suppress) return;
                    if (one || (menuConfirm && focusBtn == 0)) OnCallHeads();
                    else if (two || (menuConfirm && focusBtn == 1)) OnCallTails();
                    break;

                case Phase.FlipReveal:
                    if (ContinueReady && continuePress)
                    {
                        if (playerChoosesReceiveKick)
                            ShowWinnerChoicePlayer();
                        else
                            ResolveAiReceiveKickChoice();
                    }
                    break;

                case Phase.WinnerChoice:
                    if (suppress) return;
                    if (one || (menuConfirm && focusBtn == 0)) OnChooseReceive();
                    else if (two || (menuConfirm && focusBtn == 1)) OnChooseKick();
                    break;

                case Phase.AiChoiceSummary:
                    if (ContinueReady && continuePress)
                        ShowDirectionChoicePlayer();
                    break;

                case Phase.DirectionChoice:
                    if (suppress) return;
                    if (one || (menuConfirm && focusBtn == 0)) OnDirLeftToRight();
                    else if (two || (menuConfirm && focusBtn == 1)) OnDirRightToLeft();
                    break;

                case Phase.ReadyToStart:
                    if (ContinueReady && continuePress)
                        StartOpeningKickoff();
                    break;
            }
        }

        /// <summary>
        /// Advance "CLICK / SPACE" steps. Uses raw mouse down — ConfirmDown/PointerTapDown
        /// ignore clicks over UI, and our full-screen dim counts as UI.
        /// </summary>
        static bool ContinueDown()
        {
            if (Input.GetMouseButtonDown(0)
                || Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.Alpha1)
                || Input.GetKeyDown(KeyCode.Alpha2)
                || Input.GetKeyDown(KeyCode.Keypad1)
                || Input.GetKeyDown(KeyCode.Keypad2))
                return true;

            return TecmoInput.MenuConfirmDown();
        }

        void EnsureUi()
        {
            if (root != null) return;

            // Own canvas so HUD / play-call UI cannot sit above and steal focus.
            var canvasGo = new GameObject("CoinTossCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 70;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            root = new GameObject("CoinTossUi");
            root.transform.SetParent(canvas.transform, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            var dim = new GameObject("Dim");
            dim.transform.SetParent(root.transform, false);
            var dimRt = dim.AddComponent<RectTransform>();
            dimRt.anchorMin = Vector2.zero;
            dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = Vector2.zero;
            dimRt.offsetMax = Vector2.zero;
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0.02f, 0.03f, 0.06f, 0.72f);
            // Visual only — raycast would make PointerTapDown ignore clicks on this screen.
            dimImg.raycastTarget = false;

            titleLabel = MakeLabel(root.transform, "TossTitle", new Vector2(0.5f, 0.78f), 48f);
            bodyLabel = MakeLabel(root.transform, "TossBody", new Vector2(0.5f, 0.58f), 28f);
            bodyLabel.rectTransform.sizeDelta = new Vector2(1100f, 140f);

            // Same size, evenly spaced — highlight must not scale either button.
            btnA = MakeButton(root.transform, "TossBtnA", new Vector2(0.5f, 0.40f), ButtonSize, "1", OnBtnA);
            btnB = MakeButton(root.transform, "TossBtnB", new Vector2(0.5f, 0.26f), ButtonSize, "2", OnBtnB);
            btnALabel = btnA.GetComponentInChildren<TextMeshProUGUI>();
            btnBLabel = btnB.GetComponentInChildren<TextMeshProUGUI>();

            hintLabel = MakeLabel(root.transform, "TossHint", new Vector2(0.5f, 0.12f), 22f);
            root.SetActive(false);
        }

        static readonly Vector2 ButtonSize = new Vector2(520f, 72f);

        void OnBtnA()
        {
            if (GameManager.Instance != null && GameManager.Instance.SuppressPlayClick) return;
            switch (phase)
            {
                case Phase.Call:
                    if (!PlayerIsHome) OnCallHeads();
                    break;
                case Phase.WinnerChoice: OnChooseReceive(); break;
                case Phase.DirectionChoice: OnDirLeftToRight(); break;
            }
        }

        void OnBtnB()
        {
            if (GameManager.Instance != null && GameManager.Instance.SuppressPlayClick) return;
            switch (phase)
            {
                case Phase.Call:
                    if (!PlayerIsHome) OnCallTails();
                    break;
                case Phase.WinnerChoice: OnChooseKick(); break;
                case Phase.DirectionChoice: OnDirRightToLeft(); break;
            }
        }

        void SetFocusBtn(int index)
        {
            focusBtn = index == 1 ? 1 : 0;
            RefreshFocusHighlight();
        }

        void RefreshFocusHighlight()
        {
            bool aOn = btnA != null && btnA.activeSelf;
            bool bOn = btnB != null && btnB.activeSelf;
            if (!aOn && !bOn) return;
            if (!aOn) focusBtn = 1;
            if (!bOn) focusBtn = 0;
            // Color-only highlight (no scale) so both buttons stay the same size.
            ApplyTossHighlight(btnA, aOn && focusBtn == 0);
            ApplyTossHighlight(btnB, bOn && focusBtn == 1);
        }

        static void ApplyTossHighlight(GameObject go, bool focused)
        {
            if (go == null) return;
            var rt = go.transform as RectTransform;
            if (rt != null)
            {
                rt.localScale = Vector3.one;
                rt.sizeDelta = ButtonSize;
            }

            var outline = go.GetComponent<Outline>();
            if (outline == null)
                outline = go.AddComponent<Outline>();
            outline.effectColor = focused ? CareerUiKit.RbYellow : Color.white;
            outline.effectDistance = new Vector2(3f, -3f);
            outline.enabled = true;

            var img = go.GetComponent<Image>();
            if (img != null)
                img.color = focused
                    ? new Color(0.18f, 0.2f, 0.28f, 0.96f)
                    : new Color(0.12f, 0.14f, 0.2f, 0.94f);
        }

        void SetButtons(string a, string b, bool show)
        {
            if (btnA != null) btnA.SetActive(show && !string.IsNullOrEmpty(a));
            if (btnB != null) btnB.SetActive(show && !string.IsNullOrEmpty(b));
            if (btnALabel != null && a != null)
            {
                btnALabel.text = a;
                btnALabel.fontSize = a.Length > 22 ? 22f : 26f;
            }
            if (btnBLabel != null && b != null)
            {
                btnBLabel.text = b;
                btnBLabel.fontSize = b.Length > 22 ? 22f : 26f;
            }
            focusBtn = 0;
            RefreshFocusHighlight();
        }

        static TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 anchor, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1000f, 100f);
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

        static GameObject MakeButton(Transform parent, string name, Vector2 anchor, Vector2 size, string label,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
            var img = go.AddComponent<Image>();
            img.color = new Color(0.12f, 0.14f, 0.2f, 0.94f);
            img.raycastTarget = true;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3f, -3f);
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
            tmp.fontSize = 26f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.text = label;
            tmp.raycastTarget = false;
            return go;
        }
    }
}
