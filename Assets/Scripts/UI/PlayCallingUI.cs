using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using RetroBowl.Core;
using RetroBowl.Gameplay;
using RetroBowl.Managers;
using RetroBowl.UI.Career;

namespace RetroBowl.UI
{
    /// <summary>
    /// NES Tecmo Super Bowl play-select modal: RUN (4) + PASS (4) diagram cards.
    /// Combos: K + D-pad = run · J + D-pad = pass (instant call).
    /// Offense: confirm → SnapCadence (+ AI secret defense guess).
    /// Defense: lock guess of the same 8 plays → match on snap = blitz boost.
    /// F = field goal, T = punt (offense only).
    /// </summary>
    public class PlayCallingUI : MonoBehaviour
    {
        public static PlayCallingUI Instance { get; private set; }

        enum ModalMode
        {
            OffenseCall,
            DefenseGuess
        }

        [Header("Legacy (hidden — modal builds at runtime)")]
        public GameObject playSelectionPanel;
        public Button passPlayButton;
        public Button runPlayButton;
        public Button fieldGoalButton;
        public Button puntButton;
        public TextMeshProUGUI situationText;
        public TextMeshProUGUI recommendationText;

        /// <summary>Retro Bowl modal chrome — blue transparent + white border.</summary>
        static readonly Color ModalFill = CareerUiKit.TrainModalFill;
        static readonly Color ModalDim = new Color(0.05f, 0.1f, 0.28f, 0.22f);
        /// <summary>Play cards match career OutlinedButton (blue + white border).</summary>
        static readonly Color CardBg = CareerUiKit.TrainBtnFill;
        static readonly Color FieldGreen = new Color(0.08f, 0.48f, 0.18f, 1f);
        static readonly Color RoutePink = new Color(1f, 0.35f, 0.7f, 1f);
        static readonly Color AccentBlue = new Color(0.45f, 0.85f, 1f, 1f);
        static readonly Color AccentPink = new Color(1f, 0.45f, 0.75f, 1f);
        /// <summary>NES Tecmo yellow select frame.</summary>
        static readonly Color SelectCyan = new Color(1f, 0.92f, 0.15f, 1f);
        static readonly Color TitleBar = new Color(0.1f, 0.18f, 0.48f, 1f);
        static readonly Color CardOutline = Color.white;

        static readonly KeyCode[] DirKeys =
        {
            KeyCode.LeftArrow, KeyCode.UpArrow, KeyCode.RightArrow, KeyCode.DownArrow
        };

        /// <summary>
        /// Column glyphs — LiberationSans has ←→↑↓ (◀> often missing → tofu).
        /// Keyboard A/D called out for left/right combos.
        /// </summary>
        static readonly string[] DirGlyphs = { "← A", "↑ W", "→ D", "↓ S" };

        bool isPlaySelected;
        bool panelVisible;
        ModalMode modalMode = ModalMode.OffenseCall;
        /// <summary>
        /// Ignore confirm / Tecmo "same column again" until this time so the
        /// Space/Enter/A that dismissed the play banner cannot also call a play.
        /// </summary>
        float ignoreInputUntil;

        /// <summary>True while the Tecmo play-call modal is on screen.</summary>
        public bool IsModalOpen => panelVisible;

        GameObject playCallCanvas;
        GameObject modalRoot;
        /// <summary>Blue white-bordered content box (play cards live here).</summary>
        Transform modalPanel;
        TextMeshProUGUI headerLeft;
        TextMeshProUGUI hintLabel;
        TextMeshProUGUI headerCenter;
        TextMeshProUGUI headerRight;
        TextMeshProUGUI footerLeft;
        TextMeshProUGUI footerRight;
        Image fieldBarFill;
        RectTransform fieldBarBall;

        struct PlayCard
        {
            public OffensivePlay Play;
            public GameObject Root;
            public Image Frame;
            public Image SelectRing;
            public bool IsRun;
            public int Col;
        }

        readonly List<PlayCard> cards = new();
        int cursor; // 0-3 run, 4-7 pass

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            HideLegacyPanelChrome();
            // Rebuild canvas so modal chrome picks up blue box styling after script reloads.
            DestroyOrphanPlayCallCanvases();
            playCallCanvas = null;
            modalRoot = null;
            modalPanel = null;
            cards.Clear();
            EnsureModal();
            HidePlaySelection();
        }

        void OnDisable()
        {
            HidePlaySelection();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Root overlay canvas is not a child — destroy explicitly.
            if (playCallCanvas != null)
            {
                Object.Destroy(playCallCanvas);
                playCallCanvas = null;
            }
            modalRoot = null;
            modalPanel = null;
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Playing)
            {
                if (panelVisible) HidePlaySelection();
                return;
            }

            if (GameManager.Instance.waitingForNextPlay)
            {
                if (panelVisible) HidePlaySelection();
                return;
            }

            if (!GameManager.Instance.CanStartPlay())
            {
                if (panelVisible) HidePlaySelection();
                return;
            }

            bool playerOffense = FieldManager.Instance != null
                && FieldManager.Instance.isPlayerPossession;
            bool playerDefense = FieldManager.Instance != null
                && !FieldManager.Instance.isPlayerPossession
                && GameRules.EnableAiOffenseWhenDefending;

            bool waitingOffenseCall = !isPlaySelected
                && GameManager.Instance.isPreSnap
                && playerOffense;

            bool waitingDefenseGuess = GameManager.Instance.isPreSnap
                && playerDefense
                && !Playbook.HasDefenseGuess
                && !(SnapCadence.Instance != null && SnapCadence.Instance.IsActive);

            if (waitingOffenseCall)
            {
                modalMode = ModalMode.OffenseCall;
                ShowPlaySelection();
                HandleKeys();
                RefreshHeaderFooter();
            }
            else if (waitingDefenseGuess)
            {
                modalMode = ModalMode.DefenseGuess;
                ShowPlaySelection();
                HandleKeys();
                RefreshHeaderFooter();
            }
            else if (panelVisible)
            {
                HidePlaySelection();
            }
        }

        void HideLegacyPanelChrome()
        {
            if (passPlayButton != null) passPlayButton.gameObject.SetActive(false);
            if (runPlayButton != null) runPlayButton.gameObject.SetActive(false);
            if (fieldGoalButton != null) fieldGoalButton.gameObject.SetActive(false);
            if (puntButton != null) puntButton.gameObject.SetActive(false);
            if (situationText != null) situationText.gameObject.SetActive(false);
            if (recommendationText != null) recommendationText.gameObject.SetActive(false);
            if (playSelectionPanel != null)
                playSelectionPanel.SetActive(false);
        }

        void HandleKeys()
        {
            if (Time.unscaledTime < ignoreInputUntil)
                return;

            if (modalMode == ModalMode.OffenseCall)
            {
                bool twoPoint = FieldManager.Instance != null
                                && FieldManager.Instance.IsTwoPointAttempt;
                if (!twoPoint && Input.GetKeyDown(KeyCode.F))
                {
                    OnFieldGoalSelected();
                    return;
                }

                if (!twoPoint && Input.GetKeyDown(KeyCode.T))
                {
                    OnPuntSelected();
                    return;
                }
            }

            // Tecmo play call: K + D-pad/WASD = RUN · J + D-pad/WASD = PASS.
            // Either order: hold face then tap dir, or hold dir then tap face.
            // Left/Right = ←/A and →/D (KeyCode.A / KeyCode.D).
            int comboCol = ReadDirectionColumnDown();
            if (comboCol >= 0)
            {
                if (PlayCallRunHeld())
                {
                    CallSlotByCombo(isRun: true, comboCol);
                    return;
                }

                if (PlayCallPassHeld())
                {
                    CallSlotByCombo(isRun: false, comboCol);
                    return;
                }
            }

            int heldCol = ReadDirectionColumnHeld();
            if (heldCol >= 0)
            {
                if (PlayCallRunDown())
                {
                    CallSlotByCombo(isRun: true, heldCol);
                    return;
                }

                if (PlayCallPassDown())
                {
                    CallSlotByCombo(isRun: false, heldCol);
                    return;
                }
            }

            // Navigate without face buttons: arrows move / same column again confirms.
            for (int i = 0; i < DirKeys.Length; i++)
            {
                if (!Input.GetKeyDown(DirKeys[i])) continue;
                int rowBase = cursor >= 4 ? 4 : 0;
                int target = rowBase + i;
                if (cursor == target)
                {
                    ConfirmFromCursor();
                    return;
                }

                SetCursor(target);
                return;
            }

            // WASD alone moves cursor; with K/J held they are combo dirs (handled above).
            if (!PlayCallRunHeld() && !PlayCallPassHeld())
            {
                if (TecmoInput.MoveLeftDown()) { MoveCursor(-1, 0); return; }
                if (TecmoInput.MoveRightDown()) { MoveCursor(1, 0); return; }
                if (TecmoInput.MoveUpDown()) { MoveCursor(0, -1); return; }
                if (TecmoInput.MoveDownDown()) { MoveCursor(0, 1); return; }
            }

            if (Input.GetKeyDown(KeyCode.Tab)
                || (UnityEngine.InputSystem.Gamepad.current != null
                    && UnityEngine.InputSystem.Gamepad.current.buttonNorth.wasPressedThisFrame))
            {
                SetCursor(cursor < 4 ? cursor + 4 : cursor - 4);
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i))
                {
                    if (i < cards.Count)
                    {
                        SetCursor(i);
                        ConfirmCard(cards[i].Play);
                    }
                    return;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha5 + i) || Input.GetKeyDown(KeyCode.Keypad5 + i))
                {
                    int idx = 4 + i;
                    if (idx < cards.Count)
                    {
                        SetCursor(idx);
                        ConfirmCard(cards[idx].Play);
                    }
                    return;
                }
            }

            if (TecmoInput.MenuConfirmDown())
                ConfirmFromCursor();
        }

        /// <summary>
        /// NES D-pad column: Left=0 · Up=1 · Right=2 · Down=3.
        /// Arrows, WASD (A/D = left/right), or gamepad D-pad edge.
        /// </summary>
        static int ReadDirectionColumnDown()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)
                || Input.GetKeyDown(KeyCode.A)
                || TecmoInput.MoveLeftDown())
                return 0;
            if (Input.GetKeyDown(KeyCode.UpArrow)
                || Input.GetKeyDown(KeyCode.W)
                || TecmoInput.MoveUpDown())
                return 1;
            if (Input.GetKeyDown(KeyCode.RightArrow)
                || Input.GetKeyDown(KeyCode.D)
                || TecmoInput.MoveRightDown())
                return 2;
            if (Input.GetKeyDown(KeyCode.DownArrow)
                || Input.GetKeyDown(KeyCode.S)
                || TecmoInput.MoveDownDown())
                return 3;
            return -1;
        }

        static int ReadDirectionColumnHeld()
        {
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)
                || (pad != null && pad.dpad.left.isPressed))
                return 0;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)
                || (pad != null && pad.dpad.up.isPressed))
                return 1;
            if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)
                || (pad != null && pad.dpad.right.isPressed))
                return 2;
            if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)
                || (pad != null && pad.dpad.down.isPressed))
                return 3;
            return -1;
        }

        void CallSlotByCombo(bool isRun, int col)
        {
            col = Mathf.Clamp(col, 0, 3);
            for (int i = 0; i < cards.Count; i++)
            {
                if (cards[i].IsRun == isRun && cards[i].Col == col)
                {
                    SetCursor(i);
                    ConfirmCard(cards[i].Play);
                    return;
                }
            }
        }

        void ConfirmFromCursor()
        {
            if (cursor >= 0 && cursor < cards.Count)
                ConfirmCard(cards[cursor].Play);
        }

        void ConfirmCard(OffensivePlay play)
        {
            if (Time.unscaledTime < ignoreInputUntil)
                return;

            if (modalMode == ModalMode.DefenseGuess)
                ConfirmDefenseGuess(play);
            else
                ConfirmPlay(play);
        }

        void MoveCursor(int dx, int dy)
        {
            int col = cursor % 4;
            int row = cursor / 4;
            col = Mathf.Clamp(col + dx, 0, 3);
            row = Mathf.Clamp(row + dy, 0, 1);
            SetCursor(row * 4 + col);
        }

        void SetCursor(int index)
        {
            if (cards.Count == 0) return;
            cursor = Mathf.Clamp(index, 0, cards.Count - 1);
            for (int i = 0; i < cards.Count; i++)
            {
                bool on = i == cursor;
                if (cards[i].SelectRing != null)
                    cards[i].SelectRing.enabled = on;
                if (cards[i].Frame != null)
                {
                    // Keep blue card fill — selection is yellow border only.
                    cards[i].Frame.color = CardBg;
                    var outline = cards[i].Frame.GetComponent<Outline>();
                    if (outline != null)
                    {
                        outline.effectColor = on ? SelectCyan : CardOutline;
                        outline.effectDistance = on ? new Vector2(5f, -5f) : new Vector2(2f, -2f);
                    }
                    MenuCursor.ApplyHighlight(cards[i].Frame.gameObject, on);
                }
            }
        }

        void ShowPlaySelection()
        {
            EnsureModal();
            if (modalRoot == null) return;

            if (playCallCanvas != null && !playCallCanvas.activeSelf)
                playCallCanvas.SetActive(true);

            if (!modalRoot.activeSelf)
            {
                RebuildCards();
                cursor = 0;
                SetCursor(0);
                modalRoot.SetActive(true);
                // Banner / kickoff continue uses the same confirm keys — eat that press.
                ignoreInputUntil = Time.unscaledTime + 0.25f;
            }

            panelVisible = true;
            RefreshHeaderFooter();
        }

        void HidePlaySelection()
        {
            if (modalRoot != null)
                modalRoot.SetActive(false);
            if (playCallCanvas != null)
                playCallCanvas.SetActive(false);
            panelVisible = false;
        }

        /// <summary>Hide blue dim without clearing the selected play / cadence.</summary>
        public void HideModalNow() => HidePlaySelection();

        /// <summary>
        /// Stop taking play calls without disabling the Match Canvas (HUD / menus share that GO).
        /// </summary>
        public void SuspendForTraining()
        {
            HidePlaySelection();
            enabled = false;
        }

        public void ResumeFromTraining()
        {
            enabled = true;
            isPlaySelected = false;
            HidePlaySelection();
        }

        /// <summary>Kill leftover blue dim canvases from earlier hosts / hot reloads.</summary>
        public static void DestroyOrphanPlayCallCanvases()
        {
            // Rename before Destroy — Find still sees pending-destroy objects in the same frame.
            var stale = GameObject.Find("PlayCallCanvas");
            while (stale != null)
            {
                stale.name = "PlayCallCanvas_DESTROYING";
                Object.Destroy(stale);
                stale = GameObject.Find("PlayCallCanvas");
            }
        }

        void RefreshHeaderFooter()
        {
            string home = TeamManager.Instance != null
                ? TeamManager.Instance.PlayerTeamAbbrev()
                : "HOME";
            string away = TeamManager.Instance != null
                ? TeamManager.Instance.OpponentTeamAbbrev()
                : "AWAY";

            bool guess = modalMode == ModalMode.DefenseGuess;

            if (headerLeft != null)
                headerLeft.text = guess ? $"{home} GUESS" : $"{home} SELECT";

            if (headerRight != null)
                headerRight.text = guess ? $"{away} CALLS" : $"{away} READY";

            if (headerCenter != null && GameManager.Instance != null)
            {
                string clock = GameManager.Instance.GetFormattedTime();
                headerCenter.text =
                    $"{clock}\n{GameManager.Instance.playerScore}-{GameManager.Instance.opponentScore}";
            }

            if (footerLeft != null && FieldManager.Instance != null)
            {
                string dd = FieldManager.Instance.GetDownAndDistance();
                // Compact "1 DOWN 10"
                footerLeft.text = dd.ToUpperInvariant().Replace("AND", "").Replace("  ", " ");
            }

            if (footerRight != null && GameManager.Instance != null)
            {
                if (GameManager.Instance.isPracticeMode)
                    footerRight.text = "PRAC";
                else if (GameManager.Instance.isOvertime)
                    footerRight.text = "OT";
                else
                    footerRight.text = $"{GameManager.Instance.currentQuarter} QTR";
            }

            if (hintLabel != null)
            {
                bool twoPoint = FieldManager.Instance != null
                                && FieldManager.Instance.IsTwoPointAttempt;
                if (guess)
                {
                    hintLabel.text = "GUESS: K+DIR = RUN  ·  J+DIR = PASS  ·  ENTER  ·  1-8";
                }
                else if (twoPoint)
                {
                    hintLabel.text = "2-POINT TRY — K+WASD RUN  ·  J+WASD PASS  ·  ENTER";
                }
                else
                {
                    hintLabel.text =
                        "K+WASD RUN  ·  J+WASD PASS  ·  ←A ↑W →D ↓S  ·  ENTER  ·  F FG  ·  T PUNT";
                }
            }

            UpdateFieldBar();
        }

        void UpdateFieldBar()
        {
            if (fieldBarFill == null || FieldManager.Instance == null) return;
            float yard = Mathf.Clamp01(FieldManager.Instance.currentYardLine / 100f);
            var rt = fieldBarFill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(yard, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            if (fieldBarBall != null)
            {
                fieldBarBall.anchorMin = new Vector2(yard, 0.5f);
                fieldBarBall.anchorMax = new Vector2(yard, 0.5f);
                fieldBarBall.anchoredPosition = Vector2.zero;
            }
        }

        void EnsureModal()
        {
            if (modalRoot != null) return;

            // IMPORTANT: PlayCallingUI lives on MatchScene's main Canvas with GameHUD.
            // Never nest another Screen Space Overlay canvas under it — that collapses
            // TMP layout into vertical letter stacks and jumbles card graphics.
            var canvasGo = new GameObject("PlayCallCanvas");
            playCallCanvas = canvasGo;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 58;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            modalRoot = new GameObject("TecmoPlayCallModal");
            modalRoot.transform.SetParent(canvas.transform, false);
            var rootRt = modalRoot.AddComponent<RectTransform>();
            Stretch(rootRt);

            // Soft dim only — field stays visible behind the blue box.
            var dimGo = new GameObject("Dim", typeof(RectTransform), typeof(Image));
            dimGo.transform.SetParent(modalRoot.transform, false);
            Stretch(dimGo.GetComponent<RectTransform>());
            var dimImg = dimGo.GetComponent<Image>();
            dimImg.color = ModalDim;
            dimImg.raycastTarget = true;

            // Retro Bowl select-play box: blue transparent fill + white border.
            var panelGo = new GameObject("SelectPlayBox", typeof(RectTransform), typeof(Image));
            panelGo.transform.SetParent(modalRoot.transform, false);
            modalPanel = panelGo.transform;
            var panelRt = panelGo.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.05f, 0.06f);
            panelRt.anchorMax = new Vector2(0.95f, 0.94f);
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;
            var panelImg = panelGo.GetComponent<Image>();
            panelImg.color = ModalFill;
            panelImg.raycastTarget = true;
            var outline = panelGo.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(4f, -4f);
            CareerUiKit.StyleTrainModal(panelGo, ModalFill);

            // Header
            headerLeft = MakeLabel(modalPanel, "HeaderLeft",
                new Vector2(0.02f, 0.9f), new Vector2(0.32f, 0.98f), 36f, TextAlignmentOptions.MidlineLeft);
            headerLeft.color = AccentBlue;

            headerCenter = MakeLabel(modalPanel, "HeaderCenter",
                new Vector2(0.38f, 0.88f), new Vector2(0.62f, 0.98f), 28f, TextAlignmentOptions.Center);
            headerCenter.color = Color.white;

            headerRight = MakeLabel(modalPanel, "HeaderRight",
                new Vector2(0.68f, 0.9f), new Vector2(0.98f, 0.98f), 36f, TextAlignmentOptions.MidlineRight);
            headerRight.color = AccentPink;

            // Row labels — RUN = K · PASS = J
            var runLbl = MakeLabel(modalPanel, "RunLabel",
                new Vector2(0.02f, 0.55f), new Vector2(0.08f, 0.85f), 26f, TextAlignmentOptions.Center);
            runLbl.text = "RUN\nK";
            runLbl.color = SelectCyan;

            var passLbl = MakeLabel(modalPanel, "PassLabel",
                new Vector2(0.02f, 0.22f), new Vector2(0.08f, 0.52f), 24f, TextAlignmentOptions.Center);
            passLbl.text = "PASS\nJ";
            passLbl.color = AccentPink;

            var choose = MakeLabel(modalPanel, "ChooseBanner",
                new Vector2(0.25f, 0.175f), new Vector2(0.75f, 0.215f), 22f, TextAlignmentOptions.Center);
            choose.text = "*  CHOOSE A PLAY  *";
            choose.color = SelectCyan;

            // Footer
            footerLeft = MakeLabel(modalPanel, "FooterLeft",
                new Vector2(0.02f, 0.02f), new Vector2(0.22f, 0.12f), 26f, TextAlignmentOptions.MidlineLeft);

            var barHost = new GameObject("FieldBar", typeof(RectTransform), typeof(Image));
            barHost.transform.SetParent(modalPanel, false);
            var barRt = barHost.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.28f, 0.035f);
            barRt.anchorMax = new Vector2(0.72f, 0.1f);
            barRt.offsetMin = Vector2.zero;
            barRt.offsetMax = Vector2.zero;
            barHost.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.32f, 1f);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(barHost.transform, false);
            fieldBarFill = fillGo.GetComponent<Image>();
            fieldBarFill.color = new Color(0.25f, 0.7f, 0.35f, 0.85f);
            Stretch(fillGo.GetComponent<RectTransform>());

            var ballGo = new GameObject("Ball", typeof(RectTransform), typeof(Image));
            ballGo.transform.SetParent(barHost.transform, false);
            fieldBarBall = ballGo.GetComponent<RectTransform>();
            fieldBarBall.sizeDelta = new Vector2(14f, 14f);
            ballGo.GetComponent<Image>().color = Color.white;

            footerRight = MakeLabel(modalPanel, "FooterRight",
                new Vector2(0.78f, 0.02f), new Vector2(0.98f, 0.12f), 26f, TextAlignmentOptions.MidlineRight);

            hintLabel = MakeLabel(modalPanel, "Hint",
                new Vector2(0.1f, 0.12f), new Vector2(0.9f, 0.17f), 16f, TextAlignmentOptions.Center);
            hintLabel.color = new Color(0.85f, 0.85f, 0.9f, 1f);
            hintLabel.text = "K+WASD RUN  ·  J+WASD PASS  ·  ←A ↑W →D ↓S  ·  ENTER  ·  F FG  ·  T PUNT";
        }

        /// <summary>Play-call run face — K on keyboard, South (A) on pad.</summary>
        static bool PlayCallRunHeld()
        {
            if (Input.GetKey(KeyCode.K)) return true;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            return pad != null && pad.buttonSouth.isPressed;
        }

        static bool PlayCallRunDown()
        {
            if (Input.GetKeyDown(KeyCode.K)) return true;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        /// <summary>Play-call pass face — J on keyboard, East (B) on pad.</summary>
        static bool PlayCallPassHeld() => TecmoInput.BHeld();

        static bool PlayCallPassDown() => TecmoInput.BDown();

        void RebuildCards()
        {
            foreach (var c in cards)
            {
                if (c.Root != null)
                    Destroy(c.Root);
            }
            cards.Clear();

            var runs = Playbook.ModalRunPlays;
            var passes = Playbook.ModalPassPlays;

            BuildRow(runs, isRun: true, rowYMin: 0.52f, rowYMax: 0.88f);
            BuildRow(passes, isRun: false, rowYMin: 0.2f, rowYMax: 0.5f);
        }

        void BuildRow(OffensivePlay[] plays, bool isRun, float rowYMin, float rowYMax)
        {
            if (plays == null) return;
            const float x0 = 0.1f;
            const float x1 = 0.98f;
            float slotW = (x1 - x0) / 4f;

            for (int i = 0; i < plays.Length && i < 4; i++)
            {
                if (plays[i] == null) continue;
                float xmin = x0 + slotW * i + 0.01f;
                float xmax = x0 + slotW * (i + 1) - 0.01f;
                var card = CreateCard(plays[i], isRun, i, xmin, xmax, rowYMin, rowYMax, DirGlyphs[i]);
                cards.Add(card);
            }
        }

        PlayCard CreateCard(OffensivePlay play, bool isRun, int col,
            float xmin, float xmax, float ymin, float ymax, string padHint)
        {
            var go = new GameObject(play.Id, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(modalPanel != null ? modalPanel : modalRoot.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(xmin, ymin);
            rt.anchorMax = new Vector2(xmax, ymax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var frame = go.GetComponent<Image>();
            frame.color = CardBg;
            var frameOutline = go.AddComponent<Outline>();
            frameOutline.effectColor = CardOutline;
            frameOutline.effectDistance = new Vector2(2f, -2f);

            var ringGo = new GameObject("Select", typeof(RectTransform), typeof(Image));
            ringGo.transform.SetParent(go.transform, false);
            Stretch(ringGo.GetComponent<RectTransform>());
            var ring = ringGo.GetComponent<Image>();
            ring.color = new Color(SelectCyan.r, SelectCyan.g, SelectCyan.b, 0.18f);
            ring.enabled = false;
            ring.raycastTarget = false;
            var ringOutline = ringGo.AddComponent<Outline>();
            ringOutline.effectColor = SelectCyan;
            ringOutline.effectDistance = new Vector2(3f, -3f);

            // Title bar
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Image));
            titleGo.transform.SetParent(go.transform, false);
            var titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0.04f, 0.82f);
            titleRt.anchorMax = new Vector2(0.96f, 0.96f);
            titleRt.offsetMin = Vector2.zero;
            titleRt.offsetMax = Vector2.zero;
            titleGo.GetComponent<Image>().color = TitleBar;

            var title = MakeLabel(titleGo.transform, "TitleText",
                Vector2.zero, Vector2.one, 16f, TextAlignmentOptions.Center);
            Stretch(title.rectTransform);
            title.text = ShortTitle(play);
            title.fontSize = 15f;
            title.color = Color.white;

            // Field
            var fieldGo = new GameObject("Field", typeof(RectTransform), typeof(Image));
            fieldGo.transform.SetParent(go.transform, false);
            var fieldRt = fieldGo.GetComponent<RectTransform>();
            fieldRt.anchorMin = new Vector2(0.06f, 0.26f);
            fieldRt.anchorMax = new Vector2(0.94f, 0.8f);
            fieldRt.offsetMin = Vector2.zero;
            fieldRt.offsetMax = Vector2.zero;
            fieldGo.GetComponent<Image>().color = FieldGreen;
            fieldGo.GetComponent<Image>().raycastTarget = false;

            DrawPlayDiagram(fieldGo.transform, play);

            // Combo: face (K/J) + WASD/arrows — left=A · right=D.
            var padSprite = PlayDiagramUi.GetPadSprite(isRun, col);
            if (padSprite != null)
            {
                var padIcon = PlayDiagramUi.MakePadIcon(go.transform, padSprite,
                    new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.24f));
                PadHighlightSwapper.Attach(go, padIcon, padSprite,
                    PlayDiagramUi.GetPadSprite(isRun, col, highlighted: true));
            }
            else
            {
                string face = isRun ? "K" : "J";
                var pad = MakeLabel(go.transform, "Pad",
                    new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.24f), 17f, TextAlignmentOptions.Center);
                pad.text = $"{face} + {padHint}";
                pad.color = isRun ? SelectCyan : AccentPink;
            }

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = frame;
            var captured = play;
            btn.onClick.AddListener(() =>
            {
                for (int i = 0; i < cards.Count; i++)
                {
                    if (cards[i].Play == captured)
                    {
                        SetCursor(i);
                        break;
                    }
                }
                ConfirmCard(captured);
            });

            return new PlayCard
            {
                Play = play,
                Root = go,
                Frame = frame,
                SelectRing = ring,
                IsRun = isRun,
                Col = col
            };
        }

        static string ShortTitle(OffensivePlay play)
        {
            if (play == null) return "";
            string n = play.DisplayName ?? play.Id ?? "";
            if (n.Length > 10)
                n = n.Substring(0, 10);
            return n.ToUpperInvariant();
        }

        static void DrawPlayDiagram(Transform field, OffensivePlay play)
        {
            // Static formation dots (normalized in field).
            // X: LOS near left (0.28), downfield → right. Y: top WR = 0.85.
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
                // Playbook offsets are cumulative from snap: +X downfield, +Y across (top of diagram).
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

            // Field card is roughly this size once laid out; fixed ref keeps arrows stable on first frame.
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

        static TextMeshProUGUI MakeLabel(Transform parent, string name,
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
            tmp.alignment = align;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
            GameFonts.Apply(tmp);
            return tmp;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void ConfirmPlay(OffensivePlay play)
        {
            if (isPlaySelected || play == null) return;
            if (GameManager.Instance != null && !GameManager.Instance.CanStartPlay())
            {
                HidePlaySelection();
                return;
            }

            Playbook.Select(play);
            // AI secretly guesses from the same 8 — match on snap = their blitz.
            DefenseGuessBlitz.Clear();
            Playbook.ClearDefenseGuess();
            Playbook.PickAiDefenseGuess();
            isPlaySelected = true;
            HidePlaySelection();

            Debug.Log(
                $"Play selected: {play.DisplayName} [{play.Id}]; AI D-guess: {Playbook.DefenseGuess?.DisplayName} [{Playbook.DefenseGuess?.Id}]");

            var cadence = EnsureCadence();
            cadence.Begin();

            var preview = EnsurePreview();
            preview.PrepareSelectedPlayRoutes();
        }

        void ConfirmDefenseGuess(OffensivePlay play)
        {
            if (play == null || Playbook.HasDefenseGuess) return;
            if (GameManager.Instance != null && !GameManager.Instance.CanStartPlay())
            {
                HidePlaySelection();
                return;
            }

            Playbook.SetDefenseGuess(play);
            HidePlaySelection();
            Debug.Log($"Defense guess locked: {play.DisplayName}");
        }

        static SnapCadence EnsureCadence()
        {
            var cadence = SnapCadence.Instance;
            if (cadence != null) return cadence;

            var host = GameManager.Instance != null ? GameManager.Instance.gameObject : null;
            if (host == null) host = Instance != null ? Instance.gameObject : null;
            if (host == null)
            {
                var go = new GameObject("SnapCadenceHost");
                return go.AddComponent<SnapCadence>();
            }

            cadence = host.GetComponent<SnapCadence>();
            if (cadence == null)
                cadence = host.AddComponent<SnapCadence>();
            return cadence;
        }

        static PlayRoutePreview EnsurePreview()
        {
            var preview = PlayRoutePreview.Instance;
            if (preview != null) return preview;

            var host = GameManager.Instance != null ? GameManager.Instance.gameObject : null;
            if (host == null) host = Instance != null ? Instance.gameObject : null;
            if (host == null)
            {
                var go = new GameObject("PlayRoutePreviewHost");
                return go.AddComponent<PlayRoutePreview>();
            }

            preview = host.GetComponent<PlayRoutePreview>();
            if (preview == null)
                preview = host.AddComponent<PlayRoutePreview>();
            return preview;
        }

        void OnFieldGoalSelected()
        {
            if (modalMode != ModalMode.OffenseCall || isPlaySelected) return;
            if (FieldManager.Instance != null && FieldManager.Instance.IsTwoPointAttempt)
                return;
            if (FieldManager.Instance != null && FieldManager.Instance.GetYardsToEndzone() >= 45)
            {
                Debug.Log("Field Goal not available from here.");
                return;
            }
            if (GameManager.Instance != null && !GameManager.Instance.CanStartPlay())
                return;

            Playbook.Clear();
            isPlaySelected = true;
            HidePlaySelection();
            Debug.Log("Field Goal — kick mini-game");
            KickingController.Begin(KickMode.FieldGoal);
        }

        void OnPuntSelected()
        {
            if (modalMode != ModalMode.OffenseCall || isPlaySelected) return;
            if (FieldManager.Instance != null && FieldManager.Instance.IsTwoPointAttempt)
                return;
            if (GameManager.Instance != null && !GameManager.Instance.CanStartPlay())
                return;

            Playbook.Clear();
            isPlaySelected = true;
            HidePlaySelection();
            Debug.Log("Punt — kick mini-game");
            KickingController.Begin(KickMode.Punt);
        }

        /// <summary>Called from PlayBanner when player clicks for next play.</summary>
        public void CompleteCurrentPlay()
        {
            CancelInvoke(nameof(ResetPlaySelection));

            if (PostScoreFlow.TryHandleAfterPlayBanner())
            {
                isPlaySelected = true;
                HidePlaySelection();
                return;
            }

            ResetPlaySelection();
        }

        /// <summary>Clear selection without advancing downs (quarter / game ended).</summary>
        public void ForceResetSelection()
        {
            CancelInvoke(nameof(ResetPlaySelection));
            isPlaySelected = false;
            Playbook.Clear();
            HidePlaySelection();

            if (SnapCadence.Instance != null)
                SnapCadence.Instance.Cancel();
            if (PlayRoutePreview.Instance != null)
                PlayRoutePreview.Instance.Invalidate();
        }

        void ResetPlaySelection()
        {
            isPlaySelected = false;
            Playbook.Clear();
            cursor = 0;

            if (SnapCadence.Instance != null)
                SnapCadence.Instance.Cancel();
            if (PlayRoutePreview.Instance != null)
                PlayRoutePreview.Instance.Invalidate();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.waitingForNextPlay = false;
                GameManager.Instance.inTackleBattle = false;
                GameManager.Instance.ReadyNextPlay();
            }
        }
    }
}
