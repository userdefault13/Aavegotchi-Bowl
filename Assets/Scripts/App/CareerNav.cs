using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using RetroBowl.Core;
using RetroBowl.Data;
using RetroBowl.Gameplay;
using RetroBowl.Managers;
using RetroBowl.UI;
using RetroBowl.UI.Career;

namespace RetroBowl.App
{
    /// <summary>CareerScene screen stack — onboarding + hub + Week (PreMatch) + rooms.</summary>
    public class CareerNav : MonoBehaviour
    {
        public static CareerNav Instance { get; private set; }

        static readonly string[] FavCities =
            { "Arizona", "Philadelphia", "Dallas", "New York", "Chicago", "Miami", "Seattle", "Denver" };
        static readonly string[] FavNames =
            { "Rattlers", "Eagles", "Cowboys", "Giants", "Bears", "Sharks", "Storm", "Titans" };

        public CareerScreen Current { get; private set; } = CareerScreen.SaveSelect;
        readonly Stack<CareerScreen> backStack = new Stack<CareerScreen>();

        Canvas canvas;
        GameObject navBar;
        GameObject modalHost;
        readonly Dictionary<CareerScreen, GameObject> panels = new Dictionary<CareerScreen, GameObject>();

        // Save select
        readonly TextMeshProUGUI[] slotSummaries = new TextMeshProUGUI[SaveService.SlotCount];
        readonly TextMeshProUGUI[] slotActions = new TextMeshProUGUI[SaveService.SlotCount];
        readonly Button[] slotDeleteBtns = new Button[SaveService.SlotCount];
        readonly Button[] slotSummaryBtns = new Button[SaveService.SlotCount];
        readonly Button[] slotActionBtns = new Button[SaveService.SlotCount];
        readonly MenuCursor menuCursor = new MenuCursor();
        Button welcomeContinueBtn;
        Button newCareerBackBtn;
        Button newCareerGenBtn;
        Button newCareerContBtn;
        Button newCareerChangeBtn;

        // New career
        TMP_InputField firstNameField;
        TMP_InputField lastNameField;
        TextMeshProUGUI favTeamLabel;
        TextMeshProUGUI faceLabel;
        Image faceFill;
        Image startFavCheck;
        TextMeshProUGUI startFavMark;
        int favTeamIndex;
        int faceId;
        bool startWithFavorite = true;
        TextMeshProUGUI newsBody;

        // Home
        TextMeshProUGUI homeCredits;
        TextMeshProUGUI homeYear;
        TextMeshProUGUI homeTicker;
        TextMeshProUGUI homeTeamPlate;
        Image homeTeamPortrait;
        TextMeshProUGUI homeFans;
        Image homeFansFill;
        TextMeshProUGUI homeMorale;
        TextMeshProUGUI homeOffense;
        TextMeshProUGUI homeDefense;
        TextMeshProUGUI homeTip;
        Image homeFoBtn;
        Image homeRosterBtn;
        Image homeContinueBtn;
        readonly TextMeshProUGUI[] homeStandNames = new TextMeshProUGUI[4];
        readonly TextMeshProUGUI[] homeStandW = new TextMeshProUGUI[4];
        readonly TextMeshProUGUI[] homeStandL = new TextMeshProUGUI[4];
        readonly TextMeshProUGUI[] homeStandT = new TextMeshProUGUI[4];

        // Week / PreMatch
        TextMeshProUGUI weekTitle;
        TextMeshProUGUI leftTeamName;
        TextMeshProUGUI leftRecord;
        TextMeshProUGUI leftOffense;
        TextMeshProUGUI leftDefense;
        Image leftSprite;
        TextMeshProUGUI rightTeamName;
        TextMeshProUGUI rightRecord;
        TextMeshProUGUI rightOffense;
        TextMeshProUGUI rightDefense;
        Image rightSprite;
        Image homeUniformBtn;
        Image awayUniformBtn;
        TextMeshProUGUI ballLabel;
        bool useHomeUniform = true;

        // Roster / profile
        PlayerData selectedPlayer;
        readonly List<PlayerData> profileRoster = new List<PlayerData>();
        int profileIndex;
        Transform rosterGrid;
        TextMeshProUGUI rosterCredits;
        TextMeshProUGUI rosterPlayerCount;
        TextMeshProUGUI rosterCapLabel;
        Image rosterCapFill;
        TextMeshProUGUI rosterMoraleBox;
        TextMeshProUGUI rosterOffenseBox;
        TextMeshProUGUI rosterDefenseBox;
        TextMeshProUGUI profileTitle;
        TextMeshProUGUI profilePosTitle;
        TextMeshProUGUI profileAge;
        TextMeshProUGUI profileMorale;
        TextMeshProUGUI profileCondition;
        TextMeshProUGUI profileContract;
        TextMeshProUGUI profileRating;
        TextMeshProUGUI profilePotential;
        TMP_InputField profileTitleField;
        readonly TextMeshProUGUI[] profileAttrLabels = new TextMeshProUGUI[5];
        readonly Image[] profileAttrFills = new Image[5];
        TextMeshProUGUI foBody;
        TextMeshProUGUI foCredits;
        TextMeshProUGUI foCapLabel;
        Image foCapFill;
        TextMeshProUGUI foStadiumLabel;
        Image foStadiumFill;
        TextMeshProUGUI foTrainingLabel;
        Image foTrainingFill;
        TextMeshProUGUI foRehabLabel;
        Image foRehabFill;
        TextMeshProUGUI foMoraleBox;
        TextMeshProUGUI foOwnerName;
        TextMeshProUGUI foHcName;
        TextMeshProUGUI foOcName;
        TextMeshProUGUI foDcName;
        Image foOwnerPortrait;
        Image foHcPortrait;
        Image foOcPortrait;
        Image foDcPortrait;
        TextMeshProUGUI foOffenseBox;
        TextMeshProUGUI foDefenseBox;
        TextMeshProUGUI foDraftPicks;

        // Staff profile
        int staffIndex = 1; // default OC to match Retro Bowl staff land
        int staffRegime = 1; // 0 light · 1 normal · 2 hard
        TextMeshProUGUI staffCredits;
        TextMeshProUGUI staffRoleTitle;
        TextMeshProUGUI staffCardName;
        Image staffCardPortrait;
        TextMeshProUGUI staffPosTag;
        TextMeshProUGUI staffDetails;
        TextMeshProUGUI staffNameValue;
        TextMeshProUGUI staffMoraleValue;
        TextMeshProUGUI staffAgeValue;
        TextMeshProUGUI staffTraitValue;
        TextMeshProUGUI staffContractValue;
        TextMeshProUGUI staffXpLabel;
        Image staffXpFill;
        Image staffRegimeLight;
        Image staffRegimeNormal;
        Image staffRegimeHard;

        readonly PlayDiagramUi.SlotCard[] playbookRunCards = new PlayDiagramUi.SlotCard[Playbook.ModalSlotCount];
        readonly PlayDiagramUi.SlotCard[] playbookPassCards = new PlayDiagramUi.SlotCard[Playbook.ModalSlotCount];
        TextMeshProUGUI postMatchBody;
        TextMeshProUGUI preMatchLegacyBody;
        Transform draftContent;
        Transform faGrid;
        TextMeshProUGUI faCredits;
        TextMeshProUGUI faCapLabel;
        Image faCapFill;
        readonly List<FreeAgentOffer> faPool = new List<FreeAgentOffer>();
        int faPage;
        const int FaPerPage = 10;
        Transform leagueContent;
        Transform statsContent;
        Slider musicSlider;
        Slider sfxSlider;
        Slider stickSensSlider;
        TextMeshProUGUI stickSensLabel;

        GameObject activeModal;

        int playbookCursorRow;
        int playbookCursorCol;
        // Edge tracker for playbook WASD / arrows (held → rising edge).
        bool pbKeyUp, pbKeyDown, pbKeyLeft, pbKeyRight;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            BootLoader.EnsureAppRoot();
            if (Instance == null)
            {
                var root = GameObject.Find("AppRoot") ?? new GameObject("AppRoot");
                if (root.GetComponent<CareerNav>() == null)
                    root.AddComponent<CareerNav>();
            }
        }

        /// <summary>Clear singleton (edit-mode preview teardown before Play Mode bootstrap).</summary>
        public static void ClearInstance()
        {
            Instance = null;
        }

        /// <summary>True after <see cref="EnsureBuilt"/> created the career canvas.</summary>
        public bool HasBuiltUi => canvas != null;

        /// <summary>Runtime / editor canvas root (null until built).</summary>
        public Canvas UiCanvas => canvas;

        /// <summary>
        /// Rebuild every career panel from code. Used by the editor preview baker.
        /// </summary>
        public void RebuildUiFromCode()
        {
            TearDownBuiltUi();
            EnsureBuilt();
        }

        /// <summary>
        /// Show one screen in the built canvas (editor preview + runtime).
        /// Does not push back-stack / cursor logic.
        /// </summary>
        public void PreviewScreen(CareerScreen screen)
        {
            EnsureBuilt();
            if (canvas != null)
                canvas.gameObject.SetActive(true);

            Current = screen;
            foreach (var kv in panels)
            {
                if (kv.Value != null)
                    kv.Value.SetActive(kv.Key == screen);
            }

            if (navBar != null)
                navBar.SetActive(false);
            ClearModal();
            menuCursor.SetActive(false);

            try
            {
                RefreshScreen(screen);
            }
            catch (System.Exception e)
            {
                // Edit-mode preview may lack SeasonManager / roster data.
                Debug.LogWarning($"CareerNav.PreviewScreen refresh skipped for {screen}: {e.Message}");
            }
        }

        void TearDownBuiltUi()
        {
            if (canvas != null)
            {
                if (Application.isPlaying)
                    Destroy(canvas.gameObject);
                else
                    DestroyImmediate(canvas.gameObject);
            }

            canvas = null;
            navBar = null;
            modalHost = null;
            panels.Clear();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void HideAll()
        {
            foreach (var kv in panels)
            {
                if (kv.Value != null)
                    kv.Value.SetActive(false);
            }
            if (navBar != null) navBar.SetActive(false);
            if (canvas != null) canvas.gameObject.SetActive(false);
            ClearModal();
            menuCursor.SetActive(false);
        }

        public void Show(CareerScreen screen) => ShowInternal(screen, isBack: false);

        void ShowInternal(CareerScreen screen, bool isBack)
        {
            EnsureBuilt();
            if (canvas != null)
                canvas.gameObject.SetActive(true);

            if (!isBack && screen == CareerScreen.Home)
                backStack.Clear();
            else if (!isBack && screen != Current && ShouldTrackBack(Current, screen))
                backStack.Push(Current);

            Current = screen;
            foreach (var kv in panels)
                kv.Value.SetActive(kv.Key == screen);

            if (screen == CareerScreen.Playbook)
            {
                // Clear held-key edges so a key held during Front Office confirm doesn't auto-move.
                pbKeyUp = pbKeyDown = pbKeyLeft = pbKeyRight = false;
            }

            bool hideChrome = screen == CareerScreen.SaveSelect
                              || screen == CareerScreen.Welcome
                              || screen == CareerScreen.NewCareer
                              || screen == CareerScreen.News
                              || screen == CareerScreen.PreMatch
                              || screen == CareerScreen.ChooseTeam;
            if (navBar != null)
                navBar.SetActive(!hideChrome && screen != CareerScreen.Home
                                 && screen != CareerScreen.FrontOffice
                                 && screen != CareerScreen.Playbook
                                 && screen != CareerScreen.Roster
                                 && screen != CareerScreen.PlayerProfile
                                 && screen != CareerScreen.Draft
                                 && screen != CareerScreen.FreeAgents
                                 && screen != CareerScreen.Training
                                 && screen != CareerScreen.League
                                 && screen != CareerScreen.Stats
                                 && screen != CareerScreen.StaffProfile
                                 && screen != CareerScreen.Playoffs
                                 && screen != CareerScreen.Xp
                                 && screen != CareerScreen.HallOfFame
                                 && screen != CareerScreen.Options
                                 && screen != CareerScreen.Details
                                 && screen != CareerScreen.PostMatch);

            ClearModal();
            RefreshScreen(screen);
            MaybeShowTutorialModal(screen);
            BindMenuCursor(screen);
        }

        static bool ShouldTrackBack(CareerScreen from, CareerScreen to)
        {
            if (from == to) return false;
            // Fresh boot / slot pick — don't stack junk behind onboarding.
            if (from == CareerScreen.SaveSelect) return false;
            return true;
        }

        /// <summary>Pop nested navigation, or fall back to a sensible parent screen.</summary>
        public void GoBack()
        {
            while (backStack.Count > 0)
            {
                var target = backStack.Pop();
                if (target != Current)
                {
                    ShowInternal(target, isBack: true);
                    return;
                }
            }

            ShowInternal(DefaultParent(Current), isBack: true);
        }

        static CareerScreen DefaultParent(CareerScreen screen)
        {
            switch (screen)
            {
                case CareerScreen.Playbook:
                case CareerScreen.Draft:
                case CareerScreen.FreeAgents:
                    return CareerScreen.FrontOffice;
                case CareerScreen.StaffProfile:
                case CareerScreen.PlayerProfile:
                    return CareerScreen.Roster;
                case CareerScreen.Stats:
                    return CareerScreen.PlayerProfile;
                case CareerScreen.Playoffs:
                    return CareerScreen.League;
                case CareerScreen.Xp:
                    return CareerScreen.Training;
                case CareerScreen.ChooseTeam:
                    return CareerScreen.NewCareer;
                case CareerScreen.NewCareer:
                    return CareerScreen.Welcome;
                case CareerScreen.News:
                    return CareerScreen.Home;
                case CareerScreen.Welcome:
                    return CareerScreen.SaveSelect;
                default:
                    return CareerScreen.Home;
            }
        }

        /// <summary>Standard bottom-left BACK for nested career rooms.</summary>
        Button AddBackButton(Transform parent, Vector2? anchor = null, Vector2? size = null)
        {
            return CareerUiKit.OutlinedButton(
                parent,
                "Back",
                anchor ?? new Vector2(0.08f, 0.08f),
                size ?? new Vector2(160f, 48f),
                "BACK",
                GoBack);
        }

        void Update()
        {
            if (canvas == null || !canvas.gameObject.activeInHierarchy)
                return;

            // Career menus own WASD — don't let StandaloneInputModule also walk Selectables.
            if (EventSystem.current != null && EventSystem.current.sendNavigationEvents)
                EventSystem.current.sendNavigationEvents = false;

            if (EscapeDown())
            {
                HandleEscape();
                return;
            }

            // Tutorial / confirm / options modals own the cursor while open.
            if (activeModal != null)
            {
                menuCursor.Tick();
                SamplePlaybookKeys(); // keep edge state fresh so a held key doesn't fire on dismiss
                return;
            }

            bool onPlaybook = Current == CareerScreen.Playbook || IsPlaybookPanelVisible();
            if (onPlaybook)
            {
                TickPlaybookOnly();
                return;
            }

            SamplePlaybookKeys(); // not on playbook — still track edges so entry isn't sticky
            menuCursor.Tick();
        }

        /// <summary>
        /// Playbook navigation — held-key edge detect (GetKey / Keyboard.isPressed), not GetKeyDown.
        /// Editor Game-view focus often drops wasPressedThisFrame while isPressed still works
        /// after clicking UI; Front Office confirm → Playbook was hitting that gap.
        /// </summary>
        void TickPlaybookOnly()
        {
            if (!menuCursor.IsActive)
                BindPlaybookCursor();

            if (PlaybookOptionsKeyDown())
            {
                playbookCursorRow = menuCursor.Row;
                playbookCursorCol = menuCursor.Col;
                ShowPlaybookSlotOptions(menuCursor.Row == 0, menuCursor.Col);
                SamplePlaybookKeys();
                return;
            }

            bool up, down, left, right;
            SamplePlaybookKeys(out up, out down, out left, out right);

            if (up) menuCursor.Move(-1, 0);
            else if (down) menuCursor.Move(1, 0);
            else if (left) menuCursor.Move(0, -1);
            else if (right) menuCursor.Move(0, 1);

            playbookCursorRow = menuCursor.Row;
            playbookCursorCol = menuCursor.Col;
        }

        /// <summary>ESC / Select — same as the on-screen BACK button (dismiss modal first).</summary>
        void HandleEscape()
        {
            if (activeModal != null)
            {
                ClearModal();
                return;
            }

            // No parent from the slot picker.
            if (Current == CareerScreen.SaveSelect)
                return;

            if (Current == CareerScreen.PlayerProfile)
            {
                OnProfileBack();
                return;
            }

            GoBack();
        }

        static bool EscapeDown()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                return true;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }

        /// <summary>True while the career canvas is up — match pause should not steal ESC.</summary>
        public bool IsUiOpen =>
            canvas != null && canvas.gameObject.activeInHierarchy;

        void SamplePlaybookKeys()
        {
            SamplePlaybookKeys(out _, out _, out _, out _);
        }

        void SamplePlaybookKeys(out bool upEdge, out bool downEdge, out bool leftEdge, out bool rightEdge)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            bool up = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)
                      || (kb != null && (kb.wKey.isPressed || kb.upArrowKey.isPressed));
            bool down = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)
                        || (kb != null && (kb.sKey.isPressed || kb.downArrowKey.isPressed));
            bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)
                        || (kb != null && (kb.aKey.isPressed || kb.leftArrowKey.isPressed));
            bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)
                         || (kb != null && (kb.dKey.isPressed || kb.rightArrowKey.isPressed));

            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad != null)
            {
                up |= pad.dpad.up.isPressed;
                down |= pad.dpad.down.isPressed;
                left |= pad.dpad.left.isPressed;
                right |= pad.dpad.right.isPressed;
            }

            upEdge = up && !pbKeyUp;
            downEdge = down && !pbKeyDown;
            leftEdge = left && !pbKeyLeft;
            rightEdge = right && !pbKeyRight;
            pbKeyUp = up;
            pbKeyDown = down;
            pbKeyLeft = left;
            pbKeyRight = right;
        }

        bool IsPlaybookPanelVisible()
        {
            return panels != null
                   && panels.TryGetValue(CareerScreen.Playbook, out var panel)
                   && panel != null
                   && panel.activeInHierarchy;
        }

        /// <summary>J on the keyboard, East (B) on a pad — same face button the match modal uses for pass.</summary>
        static bool PlaybookOptionsKeyDown()
        {
            if (Input.GetKeyDown(KeyCode.J)) return true;
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.jKey.wasPressedThisFrame) return true;
            var pad = UnityEngine.InputSystem.Gamepad.current;
            return pad != null && pad.buttonEast.wasPressedThisFrame;
        }

        void BindMenuCursor(CareerScreen screen)
        {
            menuCursor.SetActive(false);

            if (activeModal != null)
            {
                BindModalCursor();
                return;
            }

            switch (screen)
            {
                case CareerScreen.SaveSelect:
                    BindSaveSelectCursor();
                    break;

                case CareerScreen.Welcome:
                case CareerScreen.News:
                case CareerScreen.StaffProfile:
                case CareerScreen.Playoffs:
                case CareerScreen.HallOfFame:
                case CareerScreen.Details:
                    BindPanelCursor(screen, columns: 1, preferLast: true);
                    break;

                case CareerScreen.Home:
                    BindPanelCursor(screen, columns: 5, preferLast: true);
                    break;

                case CareerScreen.NewCareer:
                    BindNewCareerCursor();
                    break;

                case CareerScreen.ChooseTeam:
                    BindPanelCursor(screen, columns: 1, preferLast: false);
                    break;

                case CareerScreen.Roster:
                    // Cards (5-col) + Home/Staff footer — collect all under panel.
                    BindPanelCursor(screen, columns: 5, preferLast: false);
                    break;

                case CareerScreen.PlayerProfile:
                    BindPanelCursor(screen, columns: 6, preferLast: false);
                    break;

                case CareerScreen.FrontOffice:
                    BindPanelCursor(screen, columns: 1, preferLast: false);
                    break;

                case CareerScreen.Playbook:
                    // Same MenuCursor grid binding as Save Select (2 rows × 4 cols).
                    BindPlaybookCursor();
                    break;

                case CareerScreen.Draft:
                case CareerScreen.League:
                case CareerScreen.Stats:
                    BindPanelCursor(screen, columns: 1, preferLast: false);
                    break;

                case CareerScreen.FreeAgents:
                    BindPanelCursor(screen, columns: 5, preferLast: false);
                    break;

                case CareerScreen.Training:
                case CareerScreen.Xp:
                case CareerScreen.PostMatch:
                case CareerScreen.Options:
                    BindPanelCursor(screen, columns: 1, preferLast: false);
                    break;

                case CareerScreen.PreMatch:
                    BindPanelCursor(screen, columns: 2, preferLast: true);
                    break;

                default:
                    BindPanelCursor(screen, columns: 1, preferLast: false);
                    break;
            }
        }

        void BindPanelCursor(CareerScreen screen, int columns, bool preferLast)
        {
            if (!panels.TryGetValue(screen, out var panel) || panel == null)
                return;

            var btns = panel.GetComponentsInChildren<Button>(true);
            int start = 0;
            if (preferLast && btns != null && btns.Length > 0)
                start = btns.Length - 1;

            // Prefer CONTINUE / PLAY / named action when present.
            for (int i = 0; i < btns.Length; i++)
            {
                if (btns[i] == null) continue;
                string n = btns[i].gameObject.name;
                if (n == "Cont" || n == "Continue" || n == "Play" || n == "Act0")
                {
                    start = i;
                    break;
                }
            }

            menuCursor.BindButtons(btns, columns, start);
            menuCursor.SetActive(true);
        }

        void BuildPlaybook()
        {
            var p = MakeBlueScreen(CareerScreen.Playbook);
            CareerUiKit.StarTitle(p.transform, "Title", new Vector2(0.5f, 0.955f), "PLAYBOOK", 38f);

            CareerUiKit.Label(p.transform, "Hint", new Vector2(0.5f, 0.905f), new Vector2(1100f, 28f), 16f).text =
                "WASD MOVES SELECTION  ·  J OPENS OPTIONS  ·  ON/OFF SETS MATCH / FACILITY SLOTS";

            // Card layout host only — the blue field behind it stays visible.
            var modal = new GameObject("Modal", typeof(RectTransform));
            modal.transform.SetParent(p.transform, false);
            var modalRt = modal.GetComponent<RectTransform>();
            modalRt.anchorMin = new Vector2(0.04f, 0.12f);
            modalRt.anchorMax = new Vector2(0.96f, 0.88f);
            modalRt.offsetMin = Vector2.zero;
            modalRt.offsetMax = Vector2.zero;

            var runLbl = PlayDiagramUi.MakeLabel(modal.transform, "RunLbl",
                new Vector2(0.02f, 0.57f), new Vector2(0.08f, 0.87f), 26f, TextAlignmentOptions.Center);
            runLbl.text = "RUN\nK";
            runLbl.fontStyle = FontStyles.Bold;
            runLbl.color = Color.white;

            var passLbl = PlayDiagramUi.MakeLabel(modal.transform, "PassLbl",
                new Vector2(0.02f, 0.19f), new Vector2(0.08f, 0.49f), 26f, TextAlignmentOptions.Center);
            passLbl.text = "PASS\nJ";
            passLbl.fontStyle = FontStyles.Bold;
            passLbl.color = Color.white;

            const float x0 = 0.1f;
            const float x1 = 0.98f;
            float slotW = (x1 - x0) / Playbook.ModalSlotCount;

            for (int i = 0; i < Playbook.ModalSlotCount; i++)
            {
                int slot = i;
                float xmin = x0 + slotW * i + 0.01f;
                float xmax = x0 + slotW * (i + 1) - 0.01f;

                playbookRunCards[i] = PlayDiagramUi.CreateSlotCard(
                    modal.transform, "RunCard" + i,
                    new Vector2(xmin, 0.54f), new Vector2(xmax, 0.9f),
                    isRun: true, col: i,
                    onCycle: () => ShowPlaybookSlotOptions(true, slot),
                    onToggle: () => { Playbook.ToggleActiveRunSlot(slot); RefreshPlaybook(); });

                playbookPassCards[i] = PlayDiagramUi.CreateSlotCard(
                    modal.transform, "PassCard" + i,
                    new Vector2(xmin, 0.16f), new Vector2(xmax, 0.52f),
                    isRun: false, col: i,
                    onCycle: () => ShowPlaybookSlotOptions(false, slot),
                    onToggle: () => { Playbook.ToggleActivePassSlot(slot); RefreshPlaybook(); });
            }

            // Selection starts on first run card once BindPlaybookCursor runs from Show().
            var footerHint = PlayDiagramUi.MakeLabel(modal.transform, "FooterHint",
                new Vector2(0.1f, 0.02f), new Vector2(0.9f, 0.12f), 16f, TextAlignmentOptions.Center);
            footerHint.text = "MATCH CALL: K+WASD RUN  ·  J+WASD PASS";
            footerHint.color = new Color(0.85f, 0.85f, 0.9f, 1f);

            var backBtn = CareerUiKit.OutlinedButton(p.transform, "Back", new Vector2(0.08f, 0.055f),
                new Vector2(160f, 52f), "BACK", GoBack);
            backBtn.navigation = new Navigation { mode = Navigation.Mode.None };

            var homeBtn = CareerUiKit.OutlinedButton(p.transform, "Home", new Vector2(0.2f, 0.055f),
                new Vector2(70f, 52f), "H", () => Show(CareerScreen.Home));
            homeBtn.navigation = new Navigation { mode = Navigation.Mode.None };

            RefreshPlaybook();
        }

        void RefreshPlaybook()
        {
            for (int i = 0; i < Playbook.ModalSlotCount; i++)
            {
                bool runOn = Playbook.IsRunSlotActive(i);
                var run = runOn ? Playbook.GetRunSlotPlay(i) : null;
                PlayDiagramUi.SetSlotVisual(playbookRunCards[i], run, runOn, isRun: true,
                    flipped: Playbook.IsRunSlotFlipped(i));

                bool passOn = Playbook.IsPassSlotActive(i);
                var pass = passOn ? Playbook.GetPassSlotPlay(i) : null;
                PlayDiagramUi.SetSlotVisual(playbookPassCards[i], pass, passOn, isRun: false,
                    flipped: Playbook.IsPassSlotFlipped(i));
            }
        }

        void BindModalCursor()
        {
            if (activeModal == null) return;
            menuCursor.BindUnder(activeModal.transform, columns: 2, startIndex: 0);
            // Prefer OK (usually last / first outlined).
            var btns = activeModal.GetComponentsInChildren<Button>(true);
            int start = 0;
            for (int i = 0; i < btns.Length; i++)
            {
                if (btns[i] != null && btns[i].gameObject.name == "OK")
                {
                    start = i;
                    break;
                }
            }
            menuCursor.BindButtons(btns, 2, start);
            menuCursor.SetActive(true);
        }

        void BindPlaybookCursor()
        {
            int slots = Playbook.ModalSlotCount;
            if (playbookRunCards == null || playbookPassCards == null || slots <= 0)
                return;

            // Identical pattern to BindSaveSelectCursor — FromButton + OnConfirm.
            var cells = new MenuCursor.Cell[2, slots];
            for (int i = 0; i < slots; i++)
            {
                int slot = i;
                var runBtn = playbookRunCards[i].MainButton;
                var passBtn = playbookPassCards[i].MainButton;
                if (runBtn != null) runBtn.interactable = true;
                if (passBtn != null) passBtn.interactable = true;

                // OnConfirm is a no-op so Space/Enter (MenuConfirm) does not open the options
                // modal — that stole WASD after arriving from Front Office. J / click open options.
                var run = MenuCursor.Cell.FromButton(runBtn, () => { });
                run.Visual = playbookRunCards[i].Root != null ? playbookRunCards[i].Root : run.Visual;
                run.Enabled = run.Visual != null;
                cells[0, i] = run;

                var pass = MenuCursor.Cell.FromButton(passBtn, () => { });
                pass.Visual = playbookPassCards[i].Root != null ? playbookPassCards[i].Root : pass.Visual;
                pass.Enabled = pass.Visual != null;
                cells[1, i] = pass;
            }

            playbookCursorRow = Mathf.Clamp(playbookCursorRow, 0, 1);
            playbookCursorCol = Mathf.Clamp(playbookCursorCol, 0, slots - 1);
            menuCursor.Bind(cells, playbookCursorRow, playbookCursorCol);
            menuCursor.SetActive(true);
        }

        void BindSaveSelectCursor()
        {
            int n = SaveService.SlotCount;
            var cells = new MenuCursor.Cell[n, 3];
            for (int i = 0; i < n; i++)
            {
                int slot = i;
                cells[i, 0] = MenuCursor.Cell.FromButton(slotDeleteBtns[i], () =>
                {
                    SaveService.Instance?.ClearSlot(slot);
                    RefreshSaveSelect();
                    BindSaveSelectCursor();
                });
                cells[i, 1] = MenuCursor.Cell.FromButton(slotSummaryBtns[i], () => OnSlotAction(slot));
                cells[i, 2] = MenuCursor.Cell.FromButton(slotActionBtns[i], () => OnSlotAction(slot));
            }

            int startRow = 0;
            int startCol = 2;
            var save = SaveService.Instance;
            if (save != null)
            {
                for (int i = 0; i < n; i++)
                {
                    if (!save.SlotExists(i)) continue;
                    startRow = i;
                    startCol = 2;
                    break;
                }
            }

            menuCursor.Bind(cells, startRow, startCol);
            menuCursor.SetActive(true);
        }

        void BindNewCareerCursor()
        {
            var cells = new MenuCursor.Cell[2, 3];
            cells[0, 1] = MenuCursor.Cell.FromButton(newCareerChangeBtn);
            cells[1, 0] = MenuCursor.Cell.FromButton(newCareerBackBtn);
            cells[1, 1] = MenuCursor.Cell.FromButton(newCareerGenBtn);
            cells[1, 2] = MenuCursor.Cell.FromButton(newCareerContBtn);
            menuCursor.Bind(cells, 1, 2);
            menuCursor.SetActive(true);
        }

        void EnsureBuilt()
        {
            if (canvas != null) return;
            canvas = CareerUiKit.EnsureCanvas();
            modalHost = new GameObject("ModalHost");
            modalHost.transform.SetParent(canvas.transform, false);
            var mrt = modalHost.AddComponent<RectTransform>();
            CareerUiKit.Stretch(mrt);
            modalHost.transform.SetAsLastSibling();

            BuildNavBar(canvas.transform);
            BuildSaveSelect();
            BuildWelcome();
            BuildNewCareer();
            BuildNews();
            BuildHome();
            BuildChooseTeam();
            BuildRoster();
            BuildPlayerProfile();
            BuildStaffProfile();
            BuildDraft();
            BuildFreeAgents();
            BuildFrontOffice();
            BuildPlaybook();
            BuildTraining();
            BuildXp();
            BuildLeague();
            BuildPlayoffs();
            BuildPreMatch();
            BuildPostMatch();
            BuildStats();
            BuildHallOfFame();
            BuildOptions();
            BuildDetails();
            HideAll();
            if (canvas != null)
                GameFonts.ApplyAllUnder(canvas.transform);
        }

        void BuildNavBar(Transform parent)
        {
            navBar = new GameObject("NavBar");
            navBar.transform.SetParent(parent, false);
            var rt = navBar.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0.92f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            navBar.AddComponent<Image>().color = new Color(0.08f, 0.1f, 0.14f, 0.98f);

            float x = 0.06f;
            void Nav(string label, CareerScreen s)
            {
                CareerUiKit.Button(navBar.transform, "Nav_" + s, new Vector2(x, 0.5f), new Vector2(110f, 36f), label, () => Show(s));
                x += 0.07f;
            }

            Nav("HOME", CareerScreen.Home);
            Nav("ROSTER", CareerScreen.Roster);
            Nav("DRAFT", CareerScreen.Draft);
            Nav("FA", CareerScreen.FreeAgents);
            Nav("OFFICE", CareerScreen.FrontOffice);
            Nav("TRAIN", CareerScreen.Training);
            Nav("LEAGUE", CareerScreen.League);
            Nav("STATS", CareerScreen.Stats);
            Nav("OPTS", CareerScreen.Options);
        }

        GameObject MakeBlueScreen(CareerScreen id)
        {
            var panel = CareerUiKit.SolidPanel(canvas.transform, id.ToString(), CareerUiKit.RbBlue);
            CareerUiKit.AddBlueFieldDecor(panel.transform);
            panels[id] = panel;
            return panel;
        }

        GameObject MakeScreen(CareerScreen id, string title)
        {
            var panel = CareerUiKit.Panel(canvas.transform, id.ToString());
            CareerUiKit.Label(panel.transform, "Title", new Vector2(0.5f, 0.86f), new Vector2(900f, 60f), 40f).text = title;
            panels[id] = panel;
            return panel;
        }

        // ───────── Save Select ─────────

        void BuildSaveSelect()
        {
            var p = MakeBlueScreen(CareerScreen.SaveSelect);
            CareerUiKit.AddBlueFieldDecor(p.transform);
            CareerUiKit.StarTitle(p.transform, "Title", new Vector2(0.5f, 0.92f), "AAVEGOTCHI BOWL", 38f);

            for (int i = 0; i < SaveService.SlotCount; i++)
            {
                int slot = i;
                float y = 0.74f - i * 0.14f;

                slotDeleteBtns[i] = CareerUiKit.OutlinedButton(p.transform, "Del" + i,
                    new Vector2(0.14f, y), new Vector2(56f, 56f), "X",
                    () =>
                    {
                        SaveService.Instance?.ClearSlot(slot);
                        RefreshSaveSelect();
                        if (Current == CareerScreen.SaveSelect)
                            BindSaveSelectCursor();
                    });

                var box = CareerUiKit.BorderedBox(p.transform, "Sum" + i, new Vector2(0.48f, y), new Vector2(640f, 78f),
                    CareerUiKit.RbBlueDark);
                var boxOutline = box.GetComponent<Outline>();
                if (boxOutline != null)
                    boxOutline.effectDistance = new Vector2(3f, -3f);
                var sum = CareerUiKit.Label(box.transform, "Txt", new Vector2(0.5f, 0.5f), new Vector2(600f, 68f), 22f);
                sum.text = $"SAVE {i + 1}";
                slotSummaries[i] = sum;

                // Summary is focusable / clickable → same as CONTINUE / NEW GAME.
                var sumBtn = box.AddComponent<Button>();
                sumBtn.targetGraphic = box.GetComponent<Image>();
                sumBtn.onClick.AddListener(() => OnSlotAction(slot));
                slotSummaryBtns[i] = sumBtn;

                slotActionBtns[i] = CareerUiKit.OutlinedButton(p.transform, "Act" + i,
                    new Vector2(0.86f, y), new Vector2(200f, 56f),
                    "NEW GAME", () => OnSlotAction(slot));
                slotActions[i] = slotActionBtns[i].GetComponentInChildren<TextMeshProUGUI>();
            }
        }

        void OnSlotAction(int slot)
        {
            var save = SaveService.Instance;
            if (save == null) return;
            if (save.SlotExists(slot))
            {
                save.LoadCareer(slot);
                Show(CareerScreen.Home);
            }
            else
            {
                save.BeginNewGame(slot);
                Show(CareerScreen.Welcome);
            }
        }

        void RefreshSaveSelect()
        {
            var save = SaveService.Instance;
            for (int i = 0; i < SaveService.SlotCount; i++)
            {
                var meta = save != null ? save.GetSlotMeta(i) : default;
                if (slotSummaries[i] != null)
                {
                    slotSummaries[i].text = meta.exists
                        ? $"SAVE {i + 1}\n{meta.SummaryLine}"
                        : $"SAVE {i + 1}";
                }
                if (slotActions[i] != null)
                    slotActions[i].text = meta.exists ? "CONTINUE" : "NEW GAME";
            }
        }

        // ───────── Welcome / NewCareer / News ─────────

        void BuildWelcome()
        {
            var p = MakeBlueScreen(CareerScreen.Welcome);
            CareerUiKit.AddBlueFieldDecor(p.transform);
            CareerUiKit.StarTitle(p.transform, "Title", new Vector2(0.5f, 0.9f), "WELCOME", 38f);

            var box = CareerUiKit.SectionPanel(p.transform, "Box", new Vector2(0.5f, 0.52f), new Vector2(920f, 380f),
                "AAVEGOTCHI BOWL", out _);
            var body = CareerUiKit.Label(box.transform, "Body", new Vector2(0.5f, 0.48f), new Vector2(820f, 300f), 26f);
            body.textWrappingMode = TextWrappingModes.Normal;
            body.text =
                "You are about to start your first season as the head coach of a pro football team.\n\n" +
                "The path to success is an arduous one — but if you believe in yourself and can inspire your gotchis, glory awaits!";
            welcomeContinueBtn = CareerUiKit.OutlinedButton(p.transform, "Cont",
                new Vector2(0.86f, 0.1f), new Vector2(240f, 56f), "CONTINUE",
                () => Show(CareerScreen.NewCareer), CareerUiKit.RbYellow);
            AddBackButton(p.transform);
        }

        void BuildNewCareer()
        {
            var p = MakeBlueScreen(CareerScreen.NewCareer);
            CareerUiKit.AddBlueFieldDecor(p.transform);
            CareerUiKit.StarTitle(p.transform, "Title", new Vector2(0.5f, 0.93f), "NEW CAREER", 40f);

            // ── YOUR NAME ──
            var nameBox = CareerUiKit.SectionPanel(p.transform, "NameBox", new Vector2(0.34f, 0.68f),
                new Vector2(560f, 200f), "YOUR NAME", out _);

            CareerUiKit.Label(nameBox.transform, "Fn", new Vector2(0.2f, 0.58f), new Vector2(170f, 28f), 18f,
                TextAlignmentOptions.MidlineRight).text = "FIRST NAME";
            firstNameField = CareerUiKit.InputField(nameBox.transform, "First", new Vector2(0.68f, 0.58f),
                new Vector2(280f, 40f), "First");
            firstNameField.text = "Coach";
            StyleCareerInput(firstNameField);

            CareerUiKit.Label(nameBox.transform, "Ln", new Vector2(0.2f, 0.24f), new Vector2(170f, 28f), 18f,
                TextAlignmentOptions.MidlineRight).text = "LAST NAME";
            lastNameField = CareerUiKit.InputField(nameBox.transform, "Last", new Vector2(0.68f, 0.24f),
                new Vector2(280f, 40f), "Last");
            lastNameField.text = "Gotchi";
            StyleCareerInput(lastNameField);

            // ── FACE (coach portrait) ──
            var faceBox = CareerUiKit.SectionPanel(p.transform, "Face", new Vector2(0.78f, 0.68f),
                new Vector2(260f, 200f), "FACE", out _);
            faceFill = CareerUiKit.BorderedBox(faceBox.transform, "Portrait", new Vector2(0.5f, 0.48f),
                new Vector2(140f, 110f), new Color(0.25f, 0.7f, 0.45f)).GetComponent<Image>();
            var faceOutline = faceFill.GetComponent<Outline>();
            if (faceOutline != null)
                faceOutline.effectDistance = new Vector2(2.5f, -2.5f);
            // Simple gotchi-ish eyes on the portrait stub.
            CareerUiKit.Label(faceFill.transform, "Eyes", new Vector2(0.5f, 0.55f), new Vector2(120f, 40f), 28f).text =
                "◉  ◉";
            CareerUiKit.Label(faceFill.transform, "Smile", new Vector2(0.5f, 0.28f), new Vector2(80f, 28f), 22f).text =
                "‿";
            faceLabel = CareerUiKit.Label(faceBox.transform, "FaceId", new Vector2(0.5f, 0.1f), new Vector2(200f, 24f), 18f);
            faceLabel.text = "FACE 1";

            // ── FAVORITE TEAM ──
            var favBox = CareerUiKit.SectionPanel(p.transform, "Fav", new Vector2(0.5f, 0.36f),
                new Vector2(960f, 200f), "FAVORITE TEAM", out _);

            favTeamLabel = CareerUiKit.TeamPlate(favBox.transform, "Team", new Vector2(0.34f, 0.58f),
                new Vector2(420f, 56f));
            newCareerChangeBtn = CareerUiKit.OutlinedButton(favBox.transform, "Change",
                new Vector2(0.72f, 0.58f), new Vector2(180f, 52f), "CHANGE",
                () =>
                {
                    favTeamIndex = (favTeamIndex + 1) % FavCities.Length;
                    RefreshNewCareer();
                });

            CareerUiKit.Label(favBox.transform, "StartLab", new Vector2(0.4f, 0.22f), new Vector2(480f, 32f), 20f,
                TextAlignmentOptions.MidlineRight).text = "Start with favorite team?";
            var check = CareerUiKit.Checkbox(favBox.transform, "Check", new Vector2(0.72f, 0.22f), new Vector2(40f, 40f));
            startFavCheck = check.box;
            startFavMark = check.mark;
            check.button.onClick.AddListener(() =>
            {
                startWithFavorite = !startWithFavorite;
                RefreshNewCareer();
            });

            newCareerBackBtn = AddBackButton(p.transform, new Vector2(0.1f, 0.09f), new Vector2(170f, 52f));
            newCareerGenBtn = CareerUiKit.OutlinedButton(p.transform, "Gen", new Vector2(0.42f, 0.09f),
                new Vector2(280f, 52f), "GENERATE",
                () =>
                {
                    faceId = (faceId + 1) % 8;
                    firstNameField.text = RandomFirst();
                    lastNameField.text = RandomLast();
                    RefreshNewCareer();
                });
            newCareerContBtn = CareerUiKit.OutlinedButton(p.transform, "Cont", new Vector2(0.84f, 0.09f),
                new Vector2(240f, 56f), "CONTINUE", ConfirmNewCareer, CareerUiKit.RbYellow);

            RefreshNewCareer();
        }

        static void StyleCareerInput(TMP_InputField field)
        {
            if (field == null) return;
            var img = field.GetComponent<Image>();
            if (img != null)
                img.color = CareerUiKit.RbInputFill;
            if (field.textComponent != null)
            {
                field.textComponent.fontSize = 24f;
                field.textComponent.fontStyle = FontStyles.Bold;
            }
        }

        void ConfirmNewCareer()
        {
            var save = SaveService.Instance;
            if (save == null || !save.HasActiveSlot) return;

            string first = firstNameField != null ? firstNameField.text : "Coach";
            string last = lastNameField != null ? lastNameField.text : "";
            string favCity = FavCities[favTeamIndex];
            string favName = FavNames[favTeamIndex];
            save.SaveCoachProfile(first, last, faceId, favCity, favName, startWithFavorite);

            string city = favCity;
            string name = favName;
            if (!startWithFavorite)
            {
                int other = (favTeamIndex + 3) % FavCities.Length;
                city = FavCities[other];
                name = FavNames[other];
            }

            if (TeamManager.Instance != null)
            {
                TeamManager.Instance.playerTeam = new TeamData(city, name,
                    new Color(0.15f, 0.45f, 0.9f), Color.white);
                TeamManager.Instance.playerTeam.GenerateRoster();
            }

            save.MarkTeamChosen(city, name);
            save.SaveCareer();
            Show(CareerScreen.News);
        }

        void RefreshNewCareer()
        {
            if (favTeamLabel != null)
                favTeamLabel.text = FavCities[favTeamIndex].ToUpperInvariant();
            if (faceLabel != null)
                faceLabel.text = $"FACE {faceId + 1}";
            if (faceFill != null)
            {
                float h = (faceId * 0.12f) % 1f;
                faceFill.color = Color.HSVToRGB(h, 0.5f, 0.82f);
            }
            if (startFavCheck != null)
                startFavCheck.color = startWithFavorite ? CareerUiKit.RbYellow : Color.white;
            if (startFavMark != null)
                startFavMark.text = startWithFavorite ? "X" : "";
        }

        void BuildNews()
        {
            var p = MakeBlueScreen(CareerScreen.News);
            CareerUiKit.AddBlueFieldDecor(p.transform);
            CareerUiKit.StarTitle(p.transform, "Title", new Vector2(0.5f, 0.88f), "NEWS", 42f);

            // Plain framed story box — matches classic Retro Bowl news (no section notch).
            var box = CareerUiKit.BorderedBox(p.transform, "Box", new Vector2(0.5f, 0.52f), new Vector2(1000f, 380f),
                CareerUiKit.RbPanel);
            var outline = box.GetComponent<Outline>();
            if (outline != null)
                outline.effectDistance = new Vector2(3f, -3f);

            newsBody = CareerUiKit.Label(box.transform, "Body", new Vector2(0.5f, 0.5f), new Vector2(900f, 320f), 26f);
            newsBody.alignment = TextAlignmentOptions.Center;
            newsBody.textWrappingMode = TextWrappingModes.Normal;
            newsBody.lineSpacing = 8f;

            CareerUiKit.OutlinedButton(p.transform, "Cont", new Vector2(0.88f, 0.1f), new Vector2(240f, 56f),
                "CONTINUE", () => Show(CareerScreen.Home));
        }

        static readonly string[] NewsColleges =
        {
            "Stanford", "Michigan", "Alabama", "Oregon", "Notre Dame",
            "USC", "Georgia", "Ohio State", "Penn State", "Gotchi U"
        };

        void RefreshNews()
        {
            var save = SaveService.Instance;
            var t = TeamManager.Instance?.playerTeam;
            string team = t != null ? t.cityName : "the team";
            string coach = save != null ? $"{save.CoachFirst} {save.CoachLast}".Trim() : "Coach";
            if (string.IsNullOrWhiteSpace(coach))
                coach = "Coach";
            string last = save != null && !string.IsNullOrEmpty(save.CoachLast)
                ? save.CoachLast
                : (coach.Contains(" ") ? coach.Substring(coach.LastIndexOf(' ') + 1) : coach);
            string college = NewsColleges[Mathf.Abs(coach.GetHashCode()) % NewsColleges.Length];

            if (newsBody != null)
            {
                newsBody.text =
                    $"With just 1 week left in the regular season {team} have appointed {coach} as their new head coach.\n\n" +
                    $"{last} has worked at the college level for several years and was excelling as the head coach of {college} before taking the reins at {team}.";
            }
        }

        // ───────── Home ─────────

        void BuildHome()
        {
            var p = MakeBlueScreen(CareerScreen.Home);
            CareerUiKit.AddBlueFieldDecor(p.transform);

            // ── Header ──
            var homeCc = CareerUiKit.CreditsPill(p.transform, "Credits", new Vector2(0.08f, 0.94f));
            homeCredits = homeCc.amount;
            homeCredits.text = "0";
            BindCreditsPill(homeCc.button);

            CareerUiKit.StarTitle(p.transform, "Title", new Vector2(0.5f, 0.94f), "AAVEGOTCHI BOWL", 34f);

            var yearPill = CareerUiKit.BorderedBox(p.transform, "Year", new Vector2(0.92f, 0.94f),
                new Vector2(80f, 42f), CareerUiKit.RbBlueDark);
            homeYear = CareerUiKit.Label(yearPill.transform, "Y", new Vector2(0.5f, 0.5f), new Vector2(70f, 34f), 22f);
            homeYear.text = "Y1";
            homeYear.fontStyle = FontStyles.Bold;

            // ── News ticker ──
            var ticker = CareerUiKit.BorderedBox(p.transform, "Ticker", new Vector2(0.5f, 0.84f), new Vector2(1080f, 64f),
                CareerUiKit.RbYellow);
            var tickOutline = ticker.GetComponent<Outline>();
            if (tickOutline != null) tickOutline.effectDistance = new Vector2(2.5f, -2.5f);
            homeTicker = CareerUiKit.Label(ticker.transform, "Txt", new Vector2(0.5f, 0.5f), new Vector2(1020f, 52f), 22f);
            homeTicker.color = Color.black;
            homeTicker.fontStyle = FontStyles.Bold;

            // ── Left: Division standings ──
            var stand = CareerUiKit.SectionPanel(p.transform, "Stand", new Vector2(0.28f, 0.48f),
                new Vector2(500f, 340f), "DIVISION", out _);
            CareerUiKit.OutlinedButton(stand.transform, "StandInfo", new Vector2(0.92f, 0.92f), new Vector2(40f, 36f), "i",
                () => Show(CareerScreen.League));

            CareerUiKit.Label(stand.transform, "HName", new Vector2(0.28f, 0.82f), new Vector2(200f, 24f), 16f,
                TextAlignmentOptions.MidlineLeft).text = "TEAM";
            CareerUiKit.Label(stand.transform, "HW", new Vector2(0.68f, 0.82f), new Vector2(40f, 24f), 16f).text = "W";
            CareerUiKit.Label(stand.transform, "HL", new Vector2(0.8f, 0.82f), new Vector2(40f, 24f), 16f).text = "L";
            CareerUiKit.Label(stand.transform, "HT", new Vector2(0.92f, 0.82f), new Vector2(40f, 24f), 16f).text = "T";

            float[] rowY = { 0.66f, 0.5f, 0.34f, 0.18f };
            for (int i = 0; i < 4; i++)
            {
                homeStandNames[i] = CareerUiKit.Label(stand.transform, "N" + i, new Vector2(0.3f, rowY[i]),
                    new Vector2(240f, 28f), 20f, TextAlignmentOptions.MidlineLeft);
                homeStandNames[i].fontStyle = FontStyles.Bold;
                homeStandNames[i].text = "—";

                homeStandW[i] = CareerUiKit.Label(stand.transform, "W" + i, new Vector2(0.68f, rowY[i]),
                    new Vector2(40f, 28f), 20f);
                homeStandL[i] = CareerUiKit.Label(stand.transform, "L" + i, new Vector2(0.8f, rowY[i]),
                    new Vector2(40f, 28f), 20f);
                homeStandT[i] = CareerUiKit.Label(stand.transform, "T" + i, new Vector2(0.92f, rowY[i]),
                    new Vector2(40f, 28f), 20f);
                homeStandW[i].text = homeStandL[i].text = homeStandT[i].text = "0";
                homeStandW[i].fontStyle = homeStandL[i].fontStyle = homeStandT[i].fontStyle = FontStyles.Bold;
            }

            // ── Right column: fans · team · morale · O/D ──
            var fansBox = CareerUiKit.SectionPanel(p.transform, "Fans", new Vector2(0.72f, 0.68f),
                new Vector2(420f, 100f), "FANS", out _);
            homeFans = CareerUiKit.Label(fansBox.transform, "Pct", new Vector2(0.88f, 0.42f), new Vector2(80f, 28f), 20f);
            homeFans.text = "40%";
            homeFans.fontStyle = FontStyles.Bold;
            homeFansFill = CareerUiKit.ProgressBar(fansBox.transform, "Bar", new Vector2(0.42f, 0.38f),
                new Vector2(280f, 22f), 0.4f, CareerUiKit.RbYellow);

            var teamCard = CareerUiKit.BorderedBox(p.transform, "TeamCard", new Vector2(0.62f, 0.42f),
                new Vector2(200f, 200f), CareerUiKit.RbOffenseCard);
            var teamOutline = teamCard.GetComponent<Outline>();
            if (teamOutline != null) teamOutline.effectDistance = new Vector2(2.5f, -2.5f);
            homeTeamPortrait = CareerUiKit.BorderedBox(teamCard.transform, "Port", new Vector2(0.5f, 0.58f),
                new Vector2(140f, 110f), new Color(0.18f, 0.38f, 0.72f)).GetComponent<Image>();
            CareerUiKit.Label(homeTeamPortrait.transform, "Eyes", new Vector2(0.5f, 0.55f), new Vector2(90f, 28f), 20f)
                .text = "●  ●";
            homeTeamPlate = CareerUiKit.Label(teamCard.transform, "Name", new Vector2(0.5f, 0.14f),
                new Vector2(180f, 36f), 18f);
            homeTeamPlate.text = "YOUR TEAM";
            homeTeamPlate.fontStyle = FontStyles.Bold;
            homeTeamPlate.overflowMode = TextOverflowModes.Ellipsis;

            homeMorale = CareerUiKit.FooterStatBox(p.transform, "Morale", new Vector2(0.82f, 0.5f),
                new Vector2(160f, 90f), "MORALE");
            homeOffense = CareerUiKit.FooterStatBox(p.transform, "Offense", new Vector2(0.74f, 0.28f),
                new Vector2(160f, 78f), "OFFENSE");
            homeDefense = CareerUiKit.FooterStatBox(p.transform, "Defense", new Vector2(0.9f, 0.28f),
                new Vector2(160f, 78f), "DEFENSE");

            // ── Tip bar ──
            var tipBar = CareerUiKit.BorderedBox(p.transform, "Tip", new Vector2(0.5f, 0.16f), new Vector2(1080f, 40f),
                CareerUiKit.RbTipBar);
            homeTip = CareerUiKit.Label(tipBar.transform, "T", new Vector2(0.5f, 0.5f), new Vector2(1020f, 32f), 18f);
            homeTip.color = CareerUiKit.RbYellow;
            homeTip.text = "TIP! TAKE CARE OF YOUR KEY PLAYERS AND THE REST WILL FOLLOW.";

            // ── Footer ──
            CareerUiKit.OutlinedButton(p.transform, "Opts", new Vector2(0.06f, 0.06f), new Vector2(70f, 56f), "OPT",
                () => Show(CareerScreen.Options));
            homeFoBtn = CareerUiKit.OutlinedButton(p.transform, "FO", new Vector2(0.22f, 0.06f), new Vector2(220f, 56f),
                "FRONT OFFICE", () => Show(CareerScreen.FrontOffice), CareerUiKit.RbYellow).GetComponent<Image>();
            homeRosterBtn = CareerUiKit.OutlinedButton(p.transform, "Roster", new Vector2(0.42f, 0.06f), new Vector2(160f, 56f),
                "ROSTER", () => Show(CareerScreen.Roster)).GetComponent<Image>();
            CareerUiKit.OutlinedButton(p.transform, "Train", new Vector2(0.56f, 0.06f), new Vector2(140f, 56f), "TRAIN",
                () => Show(CareerScreen.Training));
            CareerUiKit.OutlinedButton(p.transform, "Hof", new Vector2(0.68f, 0.06f), new Vector2(100f, 56f), "HOF",
                () => Show(CareerScreen.HallOfFame));
            homeContinueBtn = CareerUiKit.OutlinedButton(p.transform, "Cont", new Vector2(0.88f, 0.06f), new Vector2(200f, 56f),
                "CONTINUE >", OnHomeContinue).GetComponent<Image>();

            RefreshHome();
        }

        void OnHomeContinue()
        {
            var phase = SaveService.Instance != null
                ? SaveService.Instance.TutorialPhase
                : CareerTutorialPhase.Complete;

            // Deep Training Facility deferred — Home CONTINUE always opens Week / PreMatch.
            if (phase == CareerTutorialPhase.ControlsBasics
                || phase == CareerTutorialPhase.TrainingFacility)
            {
                SaveService.Instance?.SetTutorial(CareerTutorialPhase.Complete);
            }

            Show(CareerScreen.PreMatch);
        }

        void RefreshHome()
        {
            var s = SeasonManager.Instance;
            var t = TeamManager.Instance?.playerTeam;
            var save = SaveService.Instance;
            string city = t != null ? t.cityName.ToUpperInvariant() : "YOUR TEAM";
            string teamFull = t != null ? $"{t.cityName} {t.teamName}".ToUpperInvariant() : "YOUR TEAM";
            int week = s != null ? s.currentWeek : 1;
            int season = s != null ? s.currentSeason : 1;
            int wins = s != null ? s.playerWins : 0;
            int losses = s != null ? s.playerLosses : 0;
            string rec = $"{wins}-{losses}";
            int morale = s != null ? Mathf.RoundToInt(s.teamMorale) : 70;
            int fans = save != null ? save.FansPercent : 40;
            int cc = save != null ? save.Credits : 3;

            if (homeCredits != null) homeCredits.text = cc.ToString();
            if (homeYear != null) homeYear.text = $"Y{season}";
            if (homeTicker != null)
            {
                string opp = TeamManager.Instance?.opponentTeam != null
                    ? TeamManager.Instance.opponentTeam.cityName.ToUpperInvariant()
                    : "TBD";
                homeTicker.text = $"WEEK {week}  vs {opp}     ·     RECORD {rec}     ·     {teamFull}";
            }

            // Division: you + 3 rivals (skip your city).
            string[] rivals = { "DALLAS", "NEW YORK", "WASHINGTON", "CHICAGO", "MIAMI", "SEATTLE" };
            var names = new List<string> { city };
            foreach (var r in rivals)
            {
                if (names.Count >= 4) break;
                if (r == city) continue;
                names.Add(r);
            }
            while (names.Count < 4)
                names.Add("RIVAL " + names.Count);

            for (int i = 0; i < 4; i++)
            {
                if (homeStandNames[i] != null)
                    homeStandNames[i].text = names[i];
                if (i == 0)
                {
                    if (homeStandW[i] != null) homeStandW[i].text = wins.ToString();
                    if (homeStandL[i] != null) homeStandL[i].text = losses.ToString();
                    if (homeStandT[i] != null) homeStandT[i].text = "0";
                    if (homeStandNames[i] != null) homeStandNames[i].color = CareerUiKit.RbYellow;
                }
                else
                {
                    // Stub rival records until league sim wires them.
                    int rw = (i * 2 + week) % 5;
                    int rl = (i + 1) % 4;
                    if (homeStandW[i] != null) homeStandW[i].text = rw.ToString();
                    if (homeStandL[i] != null) homeStandL[i].text = rl.ToString();
                    if (homeStandT[i] != null) homeStandT[i].text = "0";
                    if (homeStandNames[i] != null) homeStandNames[i].color = Color.white;
                }
            }

            if (homeFans != null) homeFans.text = $"{fans}%";
            if (homeFansFill != null)
                CareerUiKit.SetProgress(homeFansFill, Mathf.Clamp01(fans / 100f));

            if (homeTeamPlate != null)
                homeTeamPlate.text = t != null ? t.teamName.ToUpperInvariant() : "TEAM";
            if (homeTeamPortrait != null && t != null)
                homeTeamPortrait.color = t.primaryColor;

            string moodIcon = morale >= 70 ? "☺" : morale >= 40 ? "😐" : "☹";
            if (homeMorale != null) homeMorale.text = $"{morale}%  {moodIcon}";
            float oStars = OffenseStars(t);
            float dStars = DefenseStars(t);
            if (homeOffense != null) homeOffense.text = CareerUiKit.StarString(oStars);
            if (homeDefense != null) homeDefense.text = CareerUiKit.StarString(dStars);
            if (homeTip != null)
                homeTip.text = "TIP! TAKE CARE OF YOUR KEY PLAYERS AND THE REST WILL FOLLOW.";

            var phase = save != null ? save.TutorialPhase : CareerTutorialPhase.Complete;
            if (homeFoBtn != null)
                homeFoBtn.color = phase == CareerTutorialPhase.NeedFrontOffice
                    ? CareerUiKit.RbYellow
                    : CareerUiKit.RbBlueDark;
            if (homeRosterBtn != null)
                homeRosterBtn.color = phase == CareerTutorialPhase.RosterNudge
                    ? CareerUiKit.RbYellow
                    : CareerUiKit.RbBlueDark;
            if (homeContinueBtn != null)
                homeContinueBtn.color = phase >= CareerTutorialPhase.ControlsBasics
                    ? CareerUiKit.RbYellow
                    : CareerUiKit.RbBlueDark;
        }

        // ───────── Week / PreMatch ─────────

        void BuildPreMatch()
        {
            var p = MakeBlueScreen(CareerScreen.PreMatch);

            weekTitle = CareerUiKit.Label(p.transform, "WeekTitle", new Vector2(0.5f, 0.92f), new Vector2(600f, 48f), 40f);
            weekTitle.text = "* * *  WEEK 1  * * *";

            CareerUiKit.OutlinedButton(p.transform, "Sim", new Vector2(0.88f, 0.92f), new Vector2(200f, 48f), "SIM GAME >",
                SimFromWeek);

            // Left = player
            leftSprite = CareerUiKit.BorderedBox(p.transform, "LeftSprite", new Vector2(0.22f, 0.58f),
                new Vector2(200f, 260f), new Color(0.2f, 0.35f, 0.85f)).GetComponent<Image>();
            CareerUiKit.Label(leftSprite.transform, "Sil", new Vector2(0.5f, 0.5f), new Vector2(160f, 40f), 18f).text =
                "PLAYER";
            leftTeamName = CareerUiKit.Label(p.transform, "LeftName", new Vector2(0.22f, 0.32f), new Vector2(320f, 36f), 26f);
            leftRecord = CareerUiKit.Label(p.transform, "LeftRec", new Vector2(0.22f, 0.26f), new Vector2(200f, 28f), 22f);
            leftOffense = CareerUiKit.Label(p.transform, "LeftO", new Vector2(0.22f, 0.2f), new Vector2(280f, 24f), 18f);
            leftDefense = CareerUiKit.Label(p.transform, "LeftD", new Vector2(0.22f, 0.15f), new Vector2(280f, 24f), 18f);

            // Right = opponent
            rightSprite = CareerUiKit.BorderedBox(p.transform, "RightSprite", new Vector2(0.78f, 0.58f),
                new Vector2(200f, 260f), new Color(0.15f, 0.45f, 0.28f)).GetComponent<Image>();
            CareerUiKit.Label(rightSprite.transform, "Sil", new Vector2(0.5f, 0.5f), new Vector2(160f, 40f), 18f).text =
                "OPP";
            rightTeamName = CareerUiKit.Label(p.transform, "RightName", new Vector2(0.78f, 0.32f), new Vector2(320f, 36f), 26f);
            rightRecord = CareerUiKit.Label(p.transform, "RightRec", new Vector2(0.78f, 0.26f), new Vector2(200f, 28f), 22f);
            rightOffense = CareerUiKit.Label(p.transform, "RightO", new Vector2(0.78f, 0.2f), new Vector2(280f, 24f), 18f);
            rightDefense = CareerUiKit.Label(p.transform, "RightD", new Vector2(0.78f, 0.15f), new Vector2(280f, 24f), 18f);

            var uniBox = CareerUiKit.BorderedBox(p.transform, "Uniforms", new Vector2(0.5f, 0.58f), new Vector2(260f, 110f),
                CareerUiKit.RbPanel);
            CareerUiKit.Label(uniBox.transform, "UTitle", new Vector2(0.5f, 0.82f), new Vector2(220f, 24f), 18f).text =
                "UNIFORMS";
            awayUniformBtn = CareerUiKit.OutlinedButton(uniBox.transform, "Away", new Vector2(0.3f, 0.35f),
                new Vector2(100f, 40f), "AWAY", () => SetUniform(false)).GetComponent<Image>();
            homeUniformBtn = CareerUiKit.OutlinedButton(uniBox.transform, "Home", new Vector2(0.7f, 0.35f),
                new Vector2(100f, 40f), "HOME", () => SetUniform(true)).GetComponent<Image>();

            var ballBox = CareerUiKit.BorderedBox(p.transform, "Ball", new Vector2(0.5f, 0.38f), new Vector2(220f, 120f),
                CareerUiKit.RbPanel);
            ballLabel = CareerUiKit.Label(ballBox.transform, "BTitle", new Vector2(0.5f, 0.82f), new Vector2(180f, 24f), 18f);
            ballLabel.text = "BALL 1";
            var ballStub = CareerUiKit.BorderedBox(ballBox.transform, "BallVis", new Vector2(0.5f, 0.38f),
                new Vector2(90f, 50f), new Color(0.55f, 0.32f, 0.12f));
            CareerUiKit.Label(ballStub.transform, "Oval", new Vector2(0.5f, 0.5f), new Vector2(70f, 30f), 14f).text = "●";

            CareerUiKit.OutlinedButton(p.transform, "Back", new Vector2(0.08f, 0.08f), new Vector2(160f, 48f), "BACK",
                GoBack);
            CareerUiKit.OutlinedButton(p.transform, "Play", new Vector2(0.88f, 0.08f), new Vector2(220f, 56f), "PLAY >",
                PlayWeekGame, CareerUiKit.RbYellow);

            // Keep a hidden legacy field so older refresh paths don't NRE.
            preMatchLegacyBody = CareerUiKit.Label(p.transform, "Legacy", new Vector2(0.5f, -1f), new Vector2(10f, 10f), 1f);
            preMatchLegacyBody.gameObject.SetActive(false);
        }

        void SetUniform(bool home)
        {
            useHomeUniform = home;
            RefreshUniformButtons();
            if (leftSprite != null)
            {
                leftSprite.color = useHomeUniform
                    ? new Color(0.2f, 0.35f, 0.85f)
                    : new Color(0.85f, 0.85f, 0.9f);
            }
        }

        void RefreshUniformButtons()
        {
            if (homeUniformBtn != null)
                homeUniformBtn.color = useHomeUniform ? CareerUiKit.RbYellow : CareerUiKit.RbBlueDark;
            if (awayUniformBtn != null)
                awayUniformBtn.color = !useHomeUniform ? CareerUiKit.RbYellow : CareerUiKit.RbBlueDark;
        }

        void PlayWeekGame()
        {
            WeatherSystem.EnsureExists();
            WeatherSystem.Instance?.RollForMatch();
            SceneFlow.Instance?.StartMatch(MatchLaunchArgs.WeekGame());
        }

        void SimFromWeek()
        {
            SimWeek();
        }

        void RefreshPreMatch()
        {
            if (TeamManager.Instance != null)
                TeamManager.Instance.GenerateNewOpponent();

            var s = SeasonManager.Instance;
            var player = TeamManager.Instance?.playerTeam;
            var opp = TeamManager.Instance?.opponentTeam;
            int week = s != null ? Mathf.Clamp(s.currentWeek, 1, s.totalWeeks) : 1;

            if (weekTitle != null)
                weekTitle.text = $"* * *  WEEK {week}  * * *";

            if (leftTeamName != null)
                leftTeamName.text = player != null ? player.cityName.ToUpperInvariant() : "HOME";
            if (leftRecord != null)
                leftRecord.text = s != null ? s.GetRecord() : "0-0";
            if (leftOffense != null)
                leftOffense.text = $"OFFENSE  {CareerUiKit.StarString(OffenseStars(player))}";
            if (leftDefense != null)
                leftDefense.text = $"DEFENSE  {CareerUiKit.StarString(DefenseStars(player))}";

            if (rightTeamName != null)
                rightTeamName.text = opp != null ? opp.cityName.ToUpperInvariant() : "AWAY";
            if (rightRecord != null)
            {
                // Stub opponent record near .500 with noise.
                int ow = Random.Range(2, 10);
                int ol = Random.Range(2, 10);
                rightRecord.text = $"{ow}-{ol}";
            }
            if (rightOffense != null)
                rightOffense.text = $"OFFENSE  {CareerUiKit.StarString(OffenseStars(opp))}";
            if (rightDefense != null)
                rightDefense.text = $"DEFENSE  {CareerUiKit.StarString(DefenseStars(opp))}";

            if (rightSprite != null && opp != null)
                rightSprite.color = Color.Lerp(opp.primaryColor, Color.white, 0.15f);

            SetUniform(useHomeUniform);
            if (ballLabel != null) ballLabel.text = "BALL 1";

            if (preMatchLegacyBody != null)
            {
                string oppLabel = opp != null ? $"{opp.cityName} {opp.teamName}" : "OPPONENT";
                preMatchLegacyBody.text = $"vs {oppLabel}";
            }
        }

        // ───────── Roster / Profile / FO ─────────

        void BuildRoster()
        {
            var p = MakeBlueScreen(CareerScreen.Roster);
            CareerUiKit.AddBlueFieldDecor(p.transform);

            // ── Header ──
            CareerUiKit.StarTitle(p.transform, "Title", new Vector2(0.5f, 0.93f), "ROSTER", 42f);

            var rosterCc = CareerUiKit.CreditsPill(p.transform, "Credits", new Vector2(0.08f, 0.93f));
            rosterCredits = rosterCc.amount;
            rosterCredits.text = "0";
            BindCreditsPill(rosterCc.button);

            CareerUiKit.OutlinedButton(p.transform, "Info", new Vector2(0.94f, 0.93f), new Vector2(52f, 48f), "i",
                () => PlayBanner.Show("Tap a player to open his profile.", 1.4f, BannerTone.Neutral));

            rosterPlayerCount = CareerUiKit.Label(p.transform, "Count", new Vector2(0.5f, 0.86f),
                new Vector2(420f, 34f), 24f);
            rosterPlayerCount.text = "PLAYERS  0 / 10";
            rosterPlayerCount.fontStyle = FontStyles.Bold;

            // ── Card grid (middle band) — no ScrollList offsets that clip the header ──
            var gridGo = new GameObject("RosterGrid");
            gridGo.transform.SetParent(p.transform, false);
            var grt = gridGo.AddComponent<RectTransform>();
            grt.anchorMin = new Vector2(0.05f, 0.2f);
            grt.anchorMax = new Vector2(0.95f, 0.82f);
            grt.offsetMin = Vector2.zero;
            grt.offsetMax = Vector2.zero;

            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(188f, 236f);
            grid.spacing = new Vector2(16f, 14f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            rosterGrid = gridGo.transform;

            // ── Footer (Retro Bowl) ──
            // Left: staff + home stacked
            CareerUiKit.OutlinedButton(p.transform, "Staff", new Vector2(0.06f, 0.12f), new Vector2(70f, 52f), "⛑",
                () => Show(CareerScreen.StaffProfile));
            CareerUiKit.OutlinedButton(p.transform, "Home", new Vector2(0.06f, 0.04f), new Vector2(70f, 52f), "H",
                () => Show(CareerScreen.Home));

            var capBox = CareerUiKit.BorderedBox(p.transform, "CapBox", new Vector2(0.36f, 0.08f),
                new Vector2(460f, 78f), CareerUiKit.RbPanel);
            var capOutline = capBox.GetComponent<Outline>();
            if (capOutline != null) capOutline.effectDistance = new Vector2(2.5f, -2.5f);
            rosterCapLabel = CareerUiKit.Label(capBox.transform, "CapLab", new Vector2(0.5f, 0.72f),
                new Vector2(430f, 26f), 18f);
            rosterCapLabel.text = "SALARY CAP  43M / 150M";
            rosterCapLabel.fontStyle = FontStyles.Bold;
            rosterCapFill = CareerUiKit.ProgressBar(capBox.transform, "CapBar", new Vector2(0.5f, 0.28f),
                new Vector2(410f, 20f), 0.28f, CareerUiKit.RbYellow);

            rosterMoraleBox = CareerUiKit.FooterStatBox(p.transform, "Morale", new Vector2(0.64f, 0.08f),
                new Vector2(140f, 78f), "MORALE");
            rosterOffenseBox = CareerUiKit.FooterStatBox(p.transform, "Offense", new Vector2(0.76f, 0.08f),
                new Vector2(140f, 78f), "OFFENSE");
            rosterDefenseBox = CareerUiKit.FooterStatBox(p.transform, "Defense", new Vector2(0.88f, 0.08f),
                new Vector2(140f, 78f), "DEFENSE");

            RefreshRoster();
        }

        void BuildPlayerProfile()
        {
            var p = MakeBlueScreen(CareerScreen.PlayerProfile);
            CareerUiKit.AddBlueFieldDecor(p.transform);

            profileTitle = CareerUiKit.StarTitle(p.transform, "Title", new Vector2(0.5f, 0.94f), "QB - PLAYER", 34f);
            if (profileTitle != null)
                profileTitle.rectTransform.sizeDelta = new Vector2(700f, 52f);
            CareerUiKit.OutlinedButton(p.transform, "Edit", new Vector2(0.94f, 0.94f), new Vector2(52f, 48f), "✎",
                () => PlayBanner.Show("RENAME PLAYER — stub", 1f));

            CareerUiKit.OutlinedButton(p.transform, "Prev", new Vector2(0.05f, 0.52f), new Vector2(64f, 72f), "◀",
                () => CycleProfilePlayer(-1));
            CareerUiKit.OutlinedButton(p.transform, "Next", new Vector2(0.95f, 0.52f), new Vector2(64f, 72f), ">",
                () => CycleProfilePlayer(1));

            // Left — position details + bio fields
            var left = CareerUiKit.SectionPanel(p.transform, "Details", new Vector2(0.28f, 0.52f),
                new Vector2(440f, 420f), "QUARTERBACK", out profilePosTitle);
            CareerUiKit.OutlinedButton(left.transform, "Info", new Vector2(0.92f, 0.9f), new Vector2(40f, 36f), "i",
                ShowPlayerProfileInfo);

            profileAge = AddProfileBioRow(left.transform, "Age", 0.8f, "AGE");
            profileMorale = AddProfileBioRow(left.transform, "Morale", 0.66f, "MORALE");
            profileCondition = AddProfileBioRow(left.transform, "Cond", 0.52f, "CONDITION");
            profileContract = AddProfileBioRow(left.transform, "Contract", 0.38f, "CONTRACT");

            var ratingLab = CareerUiKit.Label(left.transform, "RatingLab", new Vector2(0.22f, 0.26f),
                new Vector2(140f, 24f), 16f, TextAlignmentOptions.MidlineLeft);
            ratingLab.text = "RATING";
            profileRating = CareerUiKit.Label(left.transform, "Rating", new Vector2(0.68f, 0.26f),
                new Vector2(240f, 28f), 22f, TextAlignmentOptions.MidlineLeft);
            profileRating.fontStyle = FontStyles.Bold;

            var potLab = CareerUiKit.Label(left.transform, "PotLab", new Vector2(0.22f, 0.16f),
                new Vector2(140f, 24f), 16f, TextAlignmentOptions.MidlineLeft);
            potLab.text = "POTENTIAL";
            profilePotential = CareerUiKit.Label(left.transform, "Potential", new Vector2(0.68f, 0.16f),
                new Vector2(240f, 28f), 22f, TextAlignmentOptions.MidlineLeft);
            profilePotential.fontStyle = FontStyles.Bold;

            CareerUiKit.Label(left.transform, "TitleLab", new Vector2(0.22f, 0.06f),
                new Vector2(140f, 22f), 16f, TextAlignmentOptions.MidlineLeft).text = "TITLE";
            profileTitleField = CareerUiKit.InputField(left.transform, "TitleField", new Vector2(0.68f, 0.06f),
                new Vector2(240f, 34f), "");
            StyleCareerInput(profileTitleField);

            // Right — attributes
            var right = CareerUiKit.SectionPanel(p.transform, "Attrs", new Vector2(0.72f, 0.52f),
                new Vector2(440f, 420f), "ATTRIBUTES", out _);
            CareerUiKit.OutlinedButton(right.transform, "Info", new Vector2(0.92f, 0.9f), new Vector2(40f, 36f), "i",
                ShowPlayerProfileInfo);

            float[] ys = { 0.78f, 0.62f, 0.46f, 0.3f, 0.14f };
            for (int i = 0; i < 5; i++)
            {
                profileAttrLabels[i] = CareerUiKit.Label(right.transform, "ALab" + i,
                    new Vector2(0.5f, ys[i] + 0.06f), new Vector2(380f, 22f), 16f,
                    TextAlignmentOptions.MidlineLeft);
                profileAttrLabels[i].text = "—";
                profileAttrFills[i] = CareerUiKit.ProgressBar(right.transform, "ABar" + i,
                    new Vector2(0.5f, ys[i] - 0.02f), new Vector2(360f, 22f), 0.5f, CareerUiKit.RbYellow);
            }

            // Footer — Retro Bowl action row
            CareerUiKit.OutlinedButton(p.transform, "Back", new Vector2(0.06f, 0.07f), new Vector2(70f, 56f), "←",
                OnProfileBack);
            CareerUiKit.OutlinedButton(p.transform, "Rest", new Vector2(0.28f, 0.07f), new Vector2(150f, 52f), "REST",
                () => PlayBanner.Show("REST — stub", 1f));
            CareerUiKit.OutlinedButton(p.transform, "Stats", new Vector2(0.44f, 0.07f), new Vector2(150f, 52f), "STATS",
                () => Show(CareerScreen.Stats));
            CareerUiKit.OutlinedButton(p.transform, "Meet", new Vector2(0.6f, 0.07f), new Vector2(160f, 52f), "MEETING",
                () => PlayBanner.Show("MEETING — stub", 1f));
            CareerUiKit.OutlinedButton(p.transform, "Trade", new Vector2(0.76f, 0.07f), new Vector2(150f, 52f), "TRADE",
                () => PlayBanner.Show("TRADE — stub", 1f));
            CareerUiKit.OutlinedButton(p.transform, "Home", new Vector2(0.94f, 0.07f), new Vector2(70f, 56f), "H",
                () => Show(CareerScreen.Home));
        }

        static TextMeshProUGUI AddProfileBioRow(Transform parent, string id, float yAnchor, string label)
        {
            CareerUiKit.Label(parent, id + "Lab", new Vector2(0.22f, yAnchor), new Vector2(160f, 26f), 16f,
                TextAlignmentOptions.MidlineLeft).text = label;
            var val = CareerUiKit.Label(parent, id + "Val", new Vector2(0.68f, yAnchor), new Vector2(240f, 28f), 22f,
                TextAlignmentOptions.MidlineLeft);
            val.text = "—";
            val.fontStyle = FontStyles.Bold;
            return val;
        }

        void ShowPlayerProfileInfo()
        {
            ShowModal(
                "Here you can view player details and attributes. Keep an eye on morale and condition.\n\nPoor morale can lead to game penalties and problems off the field.\n\nLow condition can lead to injury or worse - fumbles!",
                "OK",
                ClearModal);
        }

        void CycleProfilePlayer(int delta)
        {
            EnsureProfileRoster();
            if (profileRoster.Count == 0) return;
            profileIndex = (profileIndex + delta + profileRoster.Count) % profileRoster.Count;
            selectedPlayer = profileRoster[profileIndex];
            RefreshProfile();
        }

        void EnsureProfileRoster()
        {
            if (profileRoster.Count > 0) return;
            var roster = TeamManager.Instance?.playerTeam?.roster;
            if (roster == null || roster.Count == 0) return;
            profileRoster.AddRange(CollectKeyPlayers(roster));
        }

        void OnProfileBack()
        {
            var phase = SaveService.Instance != null
                ? SaveService.Instance.TutorialPhase
                : CareerTutorialPhase.Complete;
            // After profile tip, RB returns to Home for the field-controls modal.
            if (phase == CareerTutorialPhase.ControlsBasics)
            {
                Show(CareerScreen.Home);
                return;
            }
            GoBack();
        }

        void BuildStaffProfile()
        {
            var p = MakeBlueScreen(CareerScreen.StaffProfile);
            CareerUiKit.AddBlueFieldDecor(p.transform);
            CareerUiKit.StarTitle(p.transform, "Title", new Vector2(0.5f, 0.94f), "STAFF", 40f);

            var staffCc = CareerUiKit.CreditsPill(p.transform, "Credits", new Vector2(0.08f, 0.94f));
            staffCredits = staffCc.amount;
            staffCredits.text = "0";
            BindCreditsPill(staffCc.button);

            CareerUiKit.OutlinedButton(p.transform, "Edit", new Vector2(0.94f, 0.94f), new Vector2(52f, 48f), "✎",
                () => PlayBanner.Show("RENAME STAFF — stub", 1f));

            // Side arrows
            CareerUiKit.OutlinedButton(p.transform, "Prev", new Vector2(0.06f, 0.52f), new Vector2(64f, 72f), "◀",
                () =>
                {
                    staffIndex = (staffIndex + 2) % 3;
                    RefreshStaffProfile();
                });
            CareerUiKit.OutlinedButton(p.transform, "Next", new Vector2(0.94f, 0.52f), new Vector2(64f, 72f), ">",
                () =>
                {
                    staffIndex = (staffIndex + 1) % 3;
                    RefreshStaffProfile();
                });

            // Main panel
            var panel = CareerUiKit.SectionPanel(p.transform, "Panel", new Vector2(0.5f, 0.52f),
                new Vector2(920f, 520f), "OFFENSIVE COORDINATOR", out staffRoleTitle);

            // Left staff card (upper-left of panel)
            var card = CareerUiKit.CoordinatorCard(panel.transform, "Card", new Vector2(0.18f, 0.68f),
                new Vector2(200f, 220f), offense: true, "OF");
            staffCardPortrait = card.portrait;
            staffCardName = card.nameLab;
            staffPosTag = card.posLab;
            staffCardName.text = "—";

            // Right-side bio fields (NAME / MORALE / AGE / TRAIT / CONTRACT)
            var bio = CareerUiKit.BorderedBox(panel.transform, "Bio", new Vector2(0.68f, 0.68f),
                new Vector2(520f, 230f), CareerUiKit.RbBlueDark);
            var bioOutline = bio.GetComponent<Outline>();
            if (bioOutline != null)
                bioOutline.effectDistance = new Vector2(2f, -2f);

            staffNameValue = AddStaffBioRow(bio.transform, "Name", 0.86f, "NAME", "—");
            staffMoraleValue = AddStaffBioRow(bio.transform, "Morale", 0.68f, "MORALE", "—");
            staffAgeValue = AddStaffBioRow(bio.transform, "Age", 0.5f, "AGE", "—");
            staffTraitValue = AddStaffBioRow(bio.transform, "Trait", 0.32f, "TRAIT", "—");
            staffContractValue = AddStaffBioRow(bio.transform, "Contract", 0.14f, "CONTRACT", "—");

            CareerUiKit.OutlinedButton(bio.transform, "InfoName", new Vector2(0.94f, 0.86f), new Vector2(36f, 32f), "i",
                () => PlayBanner.Show("Staff identity and morale affect team performance.", 1.4f));
            CareerUiKit.OutlinedButton(bio.transform, "InfoTrait", new Vector2(0.94f, 0.32f), new Vector2(36f, 32f), "i",
                () => PlayBanner.Show("Traits unlock as staff gain XP.", 1.3f));

            // XP section — full width under bio/card
            var xpBox = CareerUiKit.SectionPanel(panel.transform, "Xp", new Vector2(0.5f, 0.38f),
                new Vector2(840f, 88f), "XP LEVEL 1", out staffXpLabel);
            staffXpFill = CareerUiKit.ProgressBar(xpBox.transform, "XpBar", new Vector2(0.45f, 0.35f),
                new Vector2(680f, 24f), 0.05f, CareerUiKit.RbYellow);
            CareerUiKit.OutlinedButton(xpBox.transform, "XpInfo", new Vector2(0.92f, 0.35f), new Vector2(44f, 40f), "i",
                () => PlayBanner.Show("Staff XP unlocks stronger traits over seasons.", 1.6f));

            // Training regime
            var regimeBox = CareerUiKit.SectionPanel(panel.transform, "Regime", new Vector2(0.5f, 0.16f),
                new Vector2(840f, 100f), "TRAINING REGIME", out _);
            var lightBtn = CareerUiKit.OutlinedButton(regimeBox.transform, "Light", new Vector2(0.22f, 0.38f),
                new Vector2(180f, 48f), "LIGHT", () => SetStaffRegime(0));
            var normalBtn = CareerUiKit.OutlinedButton(regimeBox.transform, "Normal", new Vector2(0.5f, 0.38f),
                new Vector2(180f, 48f), "NORMAL", () => SetStaffRegime(1));
            var hardBtn = CareerUiKit.OutlinedButton(regimeBox.transform, "Hard", new Vector2(0.78f, 0.38f),
                new Vector2(180f, 48f), "HARD", () => SetStaffRegime(2));
            staffRegimeLight = lightBtn.GetComponent<Image>();
            staffRegimeNormal = normalBtn.GetComponent<Image>();
            staffRegimeHard = hardBtn.GetComponent<Image>();

            // Footer
            CareerUiKit.OutlinedButton(p.transform, "Back", new Vector2(0.08f, 0.06f), new Vector2(70f, 56f), "←",
                GoBack);
            CareerUiKit.OutlinedButton(p.transform, "Extend", new Vector2(0.5f, 0.06f), new Vector2(360f, 56f),
                "EXTEND CONTRACT",
                () => PlayBanner.Show("CONTRACT EXTENDED +1Y", 1.2f, BannerTone.Positive),
                CareerUiKit.RbYellow);
            CareerUiKit.OutlinedButton(p.transform, "Home", new Vector2(0.88f, 0.06f), new Vector2(70f, 56f), "H",
                () => Show(CareerScreen.Home));

            RefreshStaffProfile();
        }

        static TextMeshProUGUI AddStaffBioRow(Transform parent, string id, float yAnchor, string label, string value)
        {
            CareerUiKit.Label(parent, id + "Lab", new Vector2(0.22f, yAnchor), new Vector2(160f, 28f), 18f,
                TextAlignmentOptions.MidlineLeft).text = label;
            var val = CareerUiKit.Label(parent, id + "Val", new Vector2(0.68f, yAnchor), new Vector2(280f, 30f), 22f,
                TextAlignmentOptions.MidlineLeft);
            val.text = value;
            val.fontStyle = FontStyles.Bold;
            return val;
        }

        void SetStaffRegime(int regime)
        {
            staffRegime = Mathf.Clamp(regime, 0, 2);
            RefreshStaffRegimeButtons();
            string label = staffRegime == 0 ? "LIGHT" : staffRegime == 1 ? "NORMAL" : "HARD";
            PlayBanner.Show($"REGIME → {label}", 0.9f, BannerTone.Neutral);
        }

        void RefreshStaffRegimeButtons()
        {
            if (staffRegimeLight != null)
                staffRegimeLight.color = staffRegime == 0 ? CareerUiKit.RbYellow : CareerUiKit.RbBlueDark;
            if (staffRegimeNormal != null)
                staffRegimeNormal.color = staffRegime == 1 ? CareerUiKit.RbYellow : CareerUiKit.RbBlueDark;
            if (staffRegimeHard != null)
                staffRegimeHard.color = staffRegime == 2 ? CareerUiKit.RbYellow : CareerUiKit.RbBlueDark;
        }

        void RefreshStaffProfile()
        {
            var save = SaveService.Instance;
            int cc = save != null ? save.Credits : 0;
            if (staffCredits != null)
                staffCredits.text = cc.ToString();

            // 0 HC · 1 OC · 2 DC
            int idx = ((staffIndex % 3) + 3) % 3;
            string coachFirst = save != null ? save.CoachFirst : "Coach";
            string coachLast = save != null && !string.IsNullOrEmpty(save.CoachLast)
                ? save.CoachLast
                : "Gotchi";

            string role;
            string pos;
            string fullName;
            string last;
            bool offenseSide;
            int age;
            string morale;
            string trait;
            int contractY;
            int xpLevel;
            float xp01;

            switch (idx)
            {
                case 0:
                    role = "HEAD COACH";
                    pos = "HC";
                    fullName = $"{coachFirst} {coachLast}".Trim().ToUpperInvariant();
                    last = coachLast.ToUpperInvariant();
                    offenseSide = true;
                    age = 42;
                    morale = "GOOD";
                    trait = "MOTIVATOR";
                    contractY = 4;
                    xpLevel = 2;
                    xp01 = 0.35f;
                    break;
                case 2:
                    role = "DEFENSIVE COORDINATOR";
                    pos = "DF";
                    last = DcNameFrom(coachLast);
                    fullName = $"JORDAN {last}";
                    offenseSide = false;
                    age = 51;
                    morale = "GOOD";
                    trait = "COVER 2";
                    contractY = 3;
                    xpLevel = 1;
                    xp01 = 0.12f;
                    break;
                default:
                    role = "OFFENSIVE COORDINATOR";
                    pos = "OF";
                    last = OcNameFrom(coachLast);
                    fullName = $"COOPER {last}";
                    offenseSide = true;
                    age = 49;
                    morale = "EXCEPTIONAL";
                    trait = "NONE";
                    contractY = 2;
                    xpLevel = 1;
                    xp01 = 0.05f;
                    break;
            }

            if (staffRoleTitle != null)
                staffRoleTitle.text = role;
            if (staffCardName != null)
                staffCardName.text = last;
            if (staffPosTag != null)
                staffPosTag.text = pos;
            if (staffCardPortrait != null)
            {
                staffCardPortrait.color = offenseSide
                    ? Color.HSVToRGB(0.55f, 0.45f, 0.78f)
                    : Color.HSVToRGB(0.02f, 0.55f, 0.72f);
                // Tint parent card blue/red
                var cardImg = staffCardPortrait.transform.parent?.GetComponent<Image>();
                if (cardImg != null)
                    cardImg.color = offenseSide ? CareerUiKit.RbOffenseCard : CareerUiKit.RbDefenseCard;
            }

            if (staffNameValue != null)
                staffNameValue.text = fullName;
            if (staffMoraleValue != null)
                staffMoraleValue.text = $"{morale}  ☺";
            if (staffAgeValue != null)
                staffAgeValue.text = age.ToString();
            if (staffTraitValue != null)
                staffTraitValue.text = trait;
            if (staffContractValue != null)
                staffContractValue.text = $"{contractY}Y";

            if (staffXpLabel != null)
                staffXpLabel.text = $"XP LEVEL {xpLevel}";
            if (staffXpFill != null)
            {
                CareerUiKit.SetProgress(staffXpFill, xp01);
                staffXpFill.color = CareerUiKit.RbYellow;
            }

            RefreshStaffRegimeButtons();
        }


        void BuildFrontOffice()
        {
            var p = MakeBlueScreen(CareerScreen.FrontOffice);
            CareerUiKit.AddBlueFieldDecor(p.transform);
            CareerUiKit.StarTitle(p.transform, "Title", new Vector2(0.5f, 0.94f), "FRONT OFFICE", 38f);

            var foCc = CareerUiKit.CreditsPill(p.transform, "Credits", new Vector2(0.08f, 0.94f));
            foCredits = foCc.amount;
            foCredits.text = "0";
            BindCreditsPill(foCc.button);

            CareerUiKit.OutlinedButton(p.transform, "Info", new Vector2(0.94f, 0.94f), new Vector2(52f, 48f), "i",
                () => PlayBanner.Show("Upgrade facilities · hire staff · manage the cap.", 1.5f));

            // ── Left column: facilities ──
            var cap = CareerUiKit.FacilityRow(p.transform, "Cap", new Vector2(0.28f, 0.78f),
                new Vector2(520f, 90f), "SALARY CAP", withUpgrade: false, null);
            foCapLabel = cap.title;
            foCapFill = cap.fill;

            var stad = CareerUiKit.FacilityRow(p.transform, "Stadium", new Vector2(0.28f, 0.62f),
                new Vector2(520f, 90f), "STADIUM", withUpgrade: true,
                () => { SaveService.Instance?.UpgradeStadium(); RefreshFrontOffice(); }, costCc: 2);
            foStadiumLabel = stad.title;
            foStadiumFill = stad.fill;

            var train = CareerUiKit.FacilityRow(p.transform, "Training", new Vector2(0.28f, 0.46f),
                new Vector2(520f, 90f), "TRAINING FACILITIES", withUpgrade: true,
                () => { SaveService.Instance?.UpgradeTraining(); RefreshFrontOffice(); }, costCc: 3);
            foTrainingLabel = train.title;
            foTrainingFill = train.fill;

            var rehab = CareerUiKit.FacilityRow(p.transform, "Rehab", new Vector2(0.28f, 0.3f),
                new Vector2(520f, 90f), "REHAB FACILITIES", withUpgrade: true,
                () => { SaveService.Instance?.UpgradeRehab(); RefreshFrontOffice(); }, costCc: 3);
            foRehabLabel = rehab.title;
            foRehabFill = rehab.fill;

            // Morale (center-top-ish)
            foMoraleBox = CareerUiKit.FooterStatBox(p.transform, "Morale", new Vector2(0.58f, 0.78f),
                new Vector2(150f, 80f), "MORALE");

            // ── Right: Staff (Owner · HC · OF · DF) ──
            var staffBox = CareerUiKit.SectionPanel(p.transform, "Staff", new Vector2(0.78f, 0.58f),
                new Vector2(400f, 300f), "STAFF", out _);
            var cardSize = new Vector2(150f, 120f);

            var owner = CareerUiKit.CoordinatorCard(staffBox.transform, "Owner", new Vector2(0.28f, 0.68f),
                cardSize, offense: true, "OWN", CareerUiKit.RbStaffOwner);
            foOwnerPortrait = owner.portrait;
            foOwnerName = owner.nameLab;
            foOwnerName.text = "OWNER";
            owner.button.onClick.AddListener(() =>
                PlayBanner.Show("Franchise owner — rename from New Career / Staff.", 1.4f));

            var hc = CareerUiKit.CoordinatorCard(staffBox.transform, "HC", new Vector2(0.72f, 0.68f),
                cardSize, offense: true, "HC", CareerUiKit.RbStaffHc);
            foHcPortrait = hc.portrait;
            foHcName = hc.nameLab;
            foHcName.text = "COACH";
            hc.button.onClick.AddListener(() =>
            {
                staffIndex = 0;
                Show(CareerScreen.StaffProfile);
            });

            var oc = CareerUiKit.CoordinatorCard(staffBox.transform, "OC", new Vector2(0.28f, 0.28f),
                cardSize, offense: true, "OF");
            foOcPortrait = oc.portrait;
            foOcName = oc.nameLab;
            foOcName.text = "MCTYER";
            oc.button.onClick.AddListener(() =>
            {
                staffIndex = 1;
                Show(CareerScreen.StaffProfile);
            });

            var dc = CareerUiKit.CoordinatorCard(staffBox.transform, "DC", new Vector2(0.72f, 0.28f),
                cardSize, offense: false, "DF");
            foDcPortrait = dc.portrait;
            foDcName = dc.nameLab;
            foDcName.text = "GIVENS";
            dc.button.onClick.AddListener(() =>
            {
                staffIndex = 2;
                Show(CareerScreen.StaffProfile);
            });

            foOffenseBox = CareerUiKit.FooterStatBox(p.transform, "Offense", new Vector2(0.7f, 0.32f),
                new Vector2(150f, 70f), "OFFENSE");
            foDefenseBox = CareerUiKit.FooterStatBox(p.transform, "Defense", new Vector2(0.86f, 0.32f),
                new Vector2(150f, 70f), "DEFENSE");

            var draftBox = CareerUiKit.SectionPanel(p.transform, "Draft", new Vector2(0.78f, 0.18f),
                new Vector2(380f, 80f), "DRAFT PICKS", out _);
            foDraftPicks = CareerUiKit.Label(draftBox.transform, "Picks", new Vector2(0.45f, 0.4f),
                new Vector2(240f, 36f), 26f);
            foDraftPicks.text = "1 - 1 - 1";
            foDraftPicks.fontStyle = FontStyles.Bold;
            CareerUiKit.OutlinedButton(draftBox.transform, "DraftInfo", new Vector2(0.88f, 0.4f),
                new Vector2(44f, 40f), "i", () => Show(CareerScreen.Draft));

            // ── Footer ──
            CareerUiKit.OutlinedButton(p.transform, "Home", new Vector2(0.06f, 0.06f), new Vector2(70f, 56f), "H",
                () => Show(CareerScreen.Home));
            CareerUiKit.OutlinedButton(p.transform, "IncCap", new Vector2(0.32f, 0.06f), new Vector2(320f, 56f),
                "INCREASE SALARY CAP",
                () => { SaveService.Instance?.BumpSalaryCap(); RefreshFrontOffice(); },
                CareerUiKit.RbYellow);
            CareerUiKit.OutlinedButton(p.transform, "FA", new Vector2(0.62f, 0.06f), new Vector2(200f, 56f),
                "FREE AGENTS", () => Show(CareerScreen.FreeAgents));
            CareerUiKit.OutlinedButton(p.transform, "Staff", new Vector2(0.82f, 0.06f), new Vector2(200f, 56f),
                "STAFF HIRES", () => Show(CareerScreen.StaffProfile));
            CareerUiKit.OutlinedButton(p.transform, "Playbook", new Vector2(0.5f, 0.14f), new Vector2(200f, 40f),
                "PLAYBOOK", () => Show(CareerScreen.Playbook));
        }

        void RefreshFrontOffice()
        {
            var save = SaveService.Instance;
            int cc = save != null ? save.Credits : 0;
            int cap = save != null ? save.SalaryCapM : 43;
            int capMax = save != null ? save.SalaryCapMaxM : 150;
            int stad = save != null ? save.StadiumLevel : 1;
            int train = save != null ? save.TrainingLevel : 2;
            int rehab = save != null ? save.RehabLevel : 2;
            string coachFirst = save != null && !string.IsNullOrEmpty(save.CoachFirst)
                ? save.CoachFirst.ToUpperInvariant()
                : "OWNER";
            string coachLast = save != null && !string.IsNullOrEmpty(save.CoachLast)
                ? save.CoachLast.ToUpperInvariant()
                : "COACH";

            if (foCredits != null)
                foCredits.text = cc.ToString();

            if (foCapLabel != null)
                foCapLabel.text = $"SALARY CAP  {cap}M / {capMax}M";
            if (foCapFill != null)
            {
                CareerUiKit.SetProgress(foCapFill, capMax > 0 ? (float)cap / capMax : 0f);
                foCapFill.color = CareerUiKit.RbYellow;
            }

            if (foStadiumLabel != null)
                foStadiumLabel.text = "STADIUM";
            if (foStadiumFill != null)
            {
                CareerUiKit.SetProgress(foStadiumFill, stad / 5f);
                foStadiumFill.color = new Color(0.9f, 0.25f, 0.2f);
            }

            if (foTrainingLabel != null)
                foTrainingLabel.text = "TRAINING FACILITIES";
            if (foTrainingFill != null)
            {
                CareerUiKit.SetProgress(foTrainingFill, train / 5f);
                foTrainingFill.color = new Color(0.9f, 0.25f, 0.2f);
            }

            if (foRehabLabel != null)
                foRehabLabel.text = "REHAB FACILITIES";
            if (foRehabFill != null)
            {
                CareerUiKit.SetProgress(foRehabFill, rehab / 5f);
                foRehabFill.color = new Color(0.9f, 0.25f, 0.2f);
            }

            int morale = SeasonManager.Instance != null
                ? Mathf.RoundToInt(SeasonManager.Instance.teamMorale)
                : 79;
            if (foMoraleBox != null)
                foMoraleBox.text = $"{morale}%  ☺";

            var team = TeamManager.Instance?.playerTeam;
            if (foOffenseBox != null)
                foOffenseBox.text = CareerUiKit.StarString(OffenseStars(team), 5);
            if (foDefenseBox != null)
                foDefenseBox.text = CareerUiKit.StarString(DefenseStars(team), 5);

            // Staff cards: Owner · HC · OF · DF
            if (foOwnerName != null)
                foOwnerName.text = coachFirst;
            if (foHcName != null)
                foHcName.text = coachLast;
            if (foOcName != null)
                foOcName.text = OcNameFrom(coachLast);
            if (foDcName != null)
                foDcName.text = DcNameFrom(coachLast);
            if (foOwnerPortrait != null)
                foOwnerPortrait.color = Color.HSVToRGB(0.78f, 0.4f, 0.72f);
            if (foHcPortrait != null)
                foHcPortrait.color = Color.HSVToRGB(0.35f, 0.45f, 0.7f);
            if (foOcPortrait != null)
                foOcPortrait.color = Color.HSVToRGB(0.55f, 0.45f, 0.75f);
            if (foDcPortrait != null)
                foDcPortrait.color = Color.HSVToRGB(0.02f, 0.55f, 0.7f);

            if (foDraftPicks != null)
                foDraftPicks.text = "1 - 1 - 1";

            // Keep legacy text field harmless if anything still references it.
            if (foBody != null)
                foBody.text = "";
        }

        static string OcNameFrom(string seed)
        {
            string[] names = { "MCTYER", "TANNEY", "REED", "HOLT", "PARK" };
            int i = Mathf.Abs(seed.GetHashCode()) % names.Length;
            return names[i];
        }

        static string DcNameFrom(string seed)
        {
            string[] names = { "GIVENS", "BECKHAM", "CROSS", "NASH", "WADE" };
            int i = Mathf.Abs((seed + "D").GetHashCode()) % names.Length;
            return names[i];
        }

        void BuildDraft()
        {
            var p = MakeScreen(CareerScreen.Draft, "DRAFT BOARD");
            var scroll = CareerUiKit.ScrollList(p.transform, "DraftScroll", new Vector2(0.1f, 0.16f), new Vector2(0.9f, 0.78f));
            draftContent = scroll.content;
            AddBackButton(p.transform);
            CareerUiKit.OutlinedButton(p.transform, "Home", new Vector2(0.22f, 0.08f), new Vector2(64f, 52f), "H",
                () => Show(CareerScreen.Home));
        }

        void BuildFreeAgents()
        {
            var p = MakeBlueScreen(CareerScreen.FreeAgents);
            CareerUiKit.AddBlueFieldDecor(p.transform);
            CareerUiKit.StarTitle(p.transform, "Title", new Vector2(0.5f, 0.94f), "FREE AGENTS", 40f);

            var faCc = CareerUiKit.CreditsPill(p.transform, "Credits", new Vector2(0.08f, 0.94f));
            faCredits = faCc.amount;
            faCredits.text = "0";
            BindCreditsPill(faCc.button);

            CareerUiKit.OutlinedButton(p.transform, "Info", new Vector2(0.94f, 0.94f), new Vector2(52f, 48f), "i",
                () => PlayBanner.Show("Spend coaching credits to sign free agents. Watch the salary cap.", 1.6f));

            var gridGo = new GameObject("FaGrid");
            gridGo.transform.SetParent(p.transform, false);
            var grt = gridGo.AddComponent<RectTransform>();
            grt.anchorMin = new Vector2(0.04f, 0.2f);
            grt.anchorMax = new Vector2(0.96f, 0.88f);
            grt.offsetMin = Vector2.zero;
            grt.offsetMax = Vector2.zero;

            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(188f, 278f);
            grid.spacing = new Vector2(14f, 10f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;
            grid.childAlignment = TextAnchor.UpperCenter;
            grid.padding = new RectOffset(6, 6, 6, 6);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            faGrid = gridGo.transform;

            CareerUiKit.OutlinedButton(p.transform, "Prev", new Vector2(0.03f, 0.54f), new Vector2(48f, 64f), "◀",
                () =>
                {
                    if (faPage <= 0) return;
                    faPage--;
                    RefreshFreeAgents();
                });
            CareerUiKit.OutlinedButton(p.transform, "Next", new Vector2(0.97f, 0.54f), new Vector2(48f, 64f), ">",
                () =>
                {
                    int pages = Mathf.Max(1, Mathf.CeilToInt(faPool.Count / (float)FaPerPage));
                    if (faPage >= pages - 1) return;
                    faPage++;
                    RefreshFreeAgents();
                });

            var capBox = CareerUiKit.BorderedBox(p.transform, "CapBox", new Vector2(0.36f, 0.08f),
                new Vector2(460f, 78f), CareerUiKit.RbPanel);
            var capOutline = capBox.GetComponent<Outline>();
            if (capOutline != null) capOutline.effectDistance = new Vector2(2.5f, -2.5f);
            faCapLabel = CareerUiKit.Label(capBox.transform, "CapLab", new Vector2(0.5f, 0.72f),
                new Vector2(430f, 26f), 18f);
            faCapLabel.text = "SALARY CAP  43M / 150M";
            faCapLabel.fontStyle = FontStyles.Bold;
            faCapFill = CareerUiKit.ProgressBar(capBox.transform, "CapBar", new Vector2(0.5f, 0.28f),
                new Vector2(410f, 20f), 0.28f, CareerUiKit.RbYellow);

            CareerUiKit.OutlinedButton(p.transform, "Back", new Vector2(0.06f, 0.08f), new Vector2(70f, 56f), "←",
                GoBack);
            CareerUiKit.OutlinedButton(p.transform, "Roster", new Vector2(0.72f, 0.08f), new Vector2(180f, 56f),
                "ROSTER", () => Show(CareerScreen.Roster));
            CareerUiKit.OutlinedButton(p.transform, "Refresh", new Vector2(0.9f, 0.08f), new Vector2(180f, 56f),
                "REFRESH", () =>
                {
                    GenerateFreeAgentPool();
                    faPage = 0;
                    RefreshFreeAgents();
                    PlayBanner.Show("FREE AGENT BOARD REFRESHED", 1.1f);
                });

            GenerateFreeAgentPool();
            RefreshFreeAgents();
        }

        struct FreeAgentOffer
        {
            public string firstName;
            public string lastName;
            public string posTag;
            public PlayerPosition position;
            public float stars;
            public int salaryM;
            public int signCc;
            public bool offense;
            public bool special;
            public float morale01;
        }

        void GenerateFreeAgentPool()
        {
            faPool.Clear();
            // Seed board with Retro Bowl–style position mix, then pad to two pages.
            var seeds = new (string pos, PlayerPosition p, bool off, bool spec, string last)[]
            {
                ("QB", PlayerPosition.Quarterback, true, false, "Tate"),
                ("RB", PlayerPosition.RunningBack, true, false, "Holland"),
                ("TE", PlayerPosition.TightEnd, true, false, "Reiff"),
                ("WR", PlayerPosition.WideReceiver, true, false, "Triner"),
                ("OL", PlayerPosition.OffensiveLine, true, false, "Fisher"),
                ("DL", PlayerPosition.DefensiveLine, false, false, "Shazier"),
                ("LB", PlayerPosition.Linebacker, false, false, "Gustin"),
                ("DB", PlayerPosition.Cornerback, false, false, "Koch"),
                ("K", PlayerPosition.OffensiveLine, true, true, "Thuney"),
                ("WR", PlayerPosition.WideReceiver, true, false, "Bishop"),
                ("RB", PlayerPosition.RunningBack, true, false, "Vance"),
                ("QB", PlayerPosition.Quarterback, true, false, "Ortiz"),
                ("LB", PlayerPosition.Linebacker, false, false, "Quinn"),
                ("DL", PlayerPosition.DefensiveLine, false, false, "Hale"),
                ("TE", PlayerPosition.TightEnd, true, false, "Moss"),
                ("DB", PlayerPosition.Safety, false, false, "Reed"),
                ("OL", PlayerPosition.OffensiveLine, true, false, "Grant"),
                ("K", PlayerPosition.OffensiveLine, true, true, "Park"),
            };

            foreach (var s in seeds)
            {
                float stars = Random.Range(3.5f, 5.01f);
                stars = Mathf.Round(stars * 2f) / 2f;
                int salary = Mathf.Clamp(Mathf.RoundToInt(8f + stars * 5f + Random.Range(-3, 4)), 8, 40);
                int cost = Mathf.Clamp(Mathf.RoundToInt(salary * 0.7f + Random.Range(-2, 3)), 8, 30);
                faPool.Add(new FreeAgentOffer
                {
                    firstName = RandomFirst(),
                    lastName = s.last,
                    posTag = s.pos,
                    position = s.p,
                    stars = stars,
                    salaryM = salary,
                    signCc = cost,
                    offense = s.off,
                    special = s.spec,
                    morale01 = Random.Range(0.55f, 1f)
                });
            }
        }

        void RefreshFreeAgents()
        {
            if (faGrid == null) return;
            ClearChildren(faGrid);

            var save = SaveService.Instance;
            int cc = save != null ? save.Credits : 0;
            int cap = save != null ? save.SalaryCapM : 43;
            int capMax = save != null ? save.SalaryCapMaxM : 150;
            if (faCredits != null)
                faCredits.text = cc.ToString();
            if (faCapLabel != null)
                faCapLabel.text = $"SALARY CAP  {cap}M / {capMax}M";
            if (faCapFill != null)
                CareerUiKit.SetProgress(faCapFill, capMax > 0 ? (float)cap / capMax : 0f);

            if (faPool.Count == 0)
                GenerateFreeAgentPool();

            int pages = Mathf.Max(1, Mathf.CeilToInt(faPool.Count / (float)FaPerPage));
            faPage = Mathf.Clamp(faPage, 0, pages - 1);
            int start = faPage * FaPerPage;
            int end = Mathf.Min(start + FaPerPage, faPool.Count);

            for (int i = start; i < end; i++)
            {
                int idx = i;
                var offer = faPool[idx];
                CareerUiKit.FreeAgentCard(
                    faGrid,
                    "FA" + idx,
                    offer.posTag,
                    offer.lastName,
                    offer.stars,
                    offer.salaryM,
                    offer.signCc,
                    offer.offense,
                    offer.special,
                    offer.morale01,
                    () => TrySignFreeAgent(idx),
                    out var cardBtn);
                cardBtn.onClick.AddListener(() =>
                    PlayBanner.Show(
                        $"{offer.posTag} {offer.lastName.ToUpperInvariant()}  ·  ${offer.salaryM}M  ·  {offer.signCc} GLTR",
                        1.2f));
            }
        }

        void TrySignFreeAgent(int poolIndex)
        {
            if (poolIndex < 0 || poolIndex >= faPool.Count) return;
            var offer = faPool[poolIndex];
            var save = SaveService.Instance;
            if (save != null && !save.TrySpendCredits(offer.signCc))
            {
                PlayBanner.Show("NOT ENOUGH COACHING CREDITS", 1.3f, BannerTone.Tip);
                return;
            }

            // Stub: add a player onto the team roster when available.
            var team = TeamManager.Instance?.playerTeam;
            if (team?.roster != null)
            {
                string full = $"{offer.firstName} {offer.lastName}";
                team.roster.Add(new PlayerData(full, Random.Range(1, 99), offer.position));
            }

            faPool.RemoveAt(poolIndex);
            PlayBanner.Show($"SIGNED  {offer.lastName.ToUpperInvariant()}  ${offer.salaryM}M", 1.3f,
                BannerTone.Positive);
            RefreshFreeAgents();
        }

        void BuildTraining()
        {
            var p = MakeScreen(CareerScreen.Training, "TRAINING FACILITY");
            CareerUiKit.StyleTrainModal(p);

            CareerUiKit.Label(p.transform, "Body", new Vector2(0.5f, 0.7f), new Vector2(920f, 100f), 20f).text =
                "Practice field drills — Head Coach, OC, and DC offices.\nSet your active 8, pick a play, then work a receiver vs coverage.";

            CareerUiKit.OutlinedButton(p.transform, "Enter", new Vector2(0.5f, 0.52f), new Vector2(420f, 64f),
                "ENTER FACILITY",
                () => SceneFlow.Instance?.StartMatch(MatchLaunchArgs.TrainingFacility()),
                CareerUiKit.TrainBtnFill);

            CareerUiKit.OutlinedButton(p.transform, "HC", new Vector2(0.22f, 0.36f), new Vector2(260f, 48f),
                "HC OFFICE",
                () => SceneFlow.Instance?.StartMatch(
                    MatchLaunchArgs.TrainingFacility(TrainingOffice.HeadCoach)),
                CareerUiKit.TrainBtnFill);
            CareerUiKit.OutlinedButton(p.transform, "OC", new Vector2(0.5f, 0.36f), new Vector2(260f, 48f),
                "OC OFFICE",
                () => SceneFlow.Instance?.StartMatch(
                    MatchLaunchArgs.TrainingFacility(TrainingOffice.OffenseCoordinator)),
                CareerUiKit.TrainBtnFill);
            CareerUiKit.OutlinedButton(p.transform, "DC", new Vector2(0.78f, 0.36f), new Vector2(260f, 48f),
                "DC OFFICE",
                () => SceneFlow.Instance?.StartMatch(
                    MatchLaunchArgs.TrainingFacility(TrainingOffice.DefenseCoordinator)),
                CareerUiKit.TrainBtnFill);

            CareerUiKit.OutlinedButton(p.transform, "Practice", new Vector2(0.35f, 0.2f), new Vector2(240f, 44f),
                "SANDBOX",
                () => SceneFlow.Instance?.StartMatch(MatchLaunchArgs.Practice()),
                CareerUiKit.TrainBtnFill);
            CareerUiKit.OutlinedButton(p.transform, "Xp", new Vector2(0.65f, 0.2f), new Vector2(240f, 44f),
                "OPEN XP",
                () => Show(CareerScreen.Xp),
                CareerUiKit.TrainBtnFill);
            AddBackButton(p.transform);
        }

        void BuildXp()
        {
            var p = MakeScreen(CareerScreen.Xp, "XP");
            CareerUiKit.Label(p.transform, "Body", new Vector2(0.5f, 0.55f), new Vector2(900f, 160f), 22f).text =
                "AVAILABLE XP: 3\n\n+SPEED  +STRENGTH  +STAMINA\n(Stub spend — wires to PlayerData later)";
            CareerUiKit.Button(p.transform, "Spend", new Vector2(0.5f, 0.3f), new Vector2(280f, 48f), "SPEND +SPEED", () =>
            {
                var qb = TeamManager.Instance?.GetPlayerTeamQuarterback();
                if (qb != null) qb.stats.speed = Mathf.Min(99, qb.stats.speed + 1);
                PlayBanner.Show("QB SPEED +1", 1f);
            });
            AddBackButton(p.transform);
        }

        void BuildLeague()
        {
            var p = MakeScreen(CareerScreen.League, "LEAGUE");
            var scroll = CareerUiKit.ScrollList(p.transform, "LeagueScroll", new Vector2(0.1f, 0.16f), new Vector2(0.9f, 0.78f));
            leagueContent = scroll.content;
            CareerUiKit.Button(p.transform, "Playoffs", new Vector2(0.85f, 0.86f), new Vector2(160f, 40f), "PLAYOFFS",
                () => Show(CareerScreen.Playoffs));
            AddBackButton(p.transform);
        }

        void BuildPlayoffs()
        {
            var p = MakeScreen(CareerScreen.Playoffs, "PLAYOFFS");
            CareerUiKit.Label(p.transform, "Body", new Vector2(0.5f, 0.5f), new Vector2(900f, 240f), 22f).text =
                "BRACKET\n\nQualify with a winning record.\n(Stub bracket — unlocks near season end)";
            AddBackButton(p.transform);
        }

        void BuildPostMatch()
        {
            var p = MakeScreen(CareerScreen.PostMatch, "POST-MATCH");
            postMatchBody = CareerUiKit.Label(p.transform, "Body", new Vector2(0.5f, 0.55f), new Vector2(900f, 220f), 26f);
            CareerUiKit.Button(p.transform, "Xp", new Vector2(0.35f, 0.28f), new Vector2(200f, 48f), "XP",
                () => Show(CareerScreen.Xp));
            CareerUiKit.Button(p.transform, "Continue", new Vector2(0.65f, 0.28f), new Vector2(200f, 48f), "CONTINUE", () =>
            {
                SaveService.Instance?.SaveCareer();
                Show(CareerScreen.Home);
            });
            AddBackButton(p.transform);
        }

        void BuildStats()
        {
            var p = MakeScreen(CareerScreen.Stats, "STATS");
            var scroll = CareerUiKit.ScrollList(p.transform, "StatsScroll", new Vector2(0.1f, 0.16f), new Vector2(0.9f, 0.78f));
            statsContent = scroll.content;
            AddBackButton(p.transform);
            CareerUiKit.OutlinedButton(p.transform, "Home", new Vector2(0.22f, 0.08f), new Vector2(64f, 52f), "H",
                () => Show(CareerScreen.Home));
        }

        void BuildHallOfFame()
        {
            var p = MakeScreen(CareerScreen.HallOfFame, "HALL OF FAME");
            CareerUiKit.Label(p.transform, "Body", new Vector2(0.5f, 0.5f), new Vector2(900f, 260f), 22f).text =
                "FRANCHISE LEGENDS\n\n• First Touchdown\n• First Win\n• Perfect Season?\n\n(Achievements stub)";
            AddBackButton(p.transform);
        }

        void BuildOptions()
        {
            var p = MakeScreen(CareerScreen.Options, "OPTIONS");
            CareerUiKit.Label(p.transform, "MLab", new Vector2(0.5f, 0.74f), new Vector2(400f, 40f), 22f).text =
                "MUSIC VOLUME";
            musicSlider = MakeSlider(p.transform, "Music", new Vector2(0.5f, 0.67f));
            musicSlider.value = AudioManager.Instance != null ? AudioManager.Instance.musicVolume : 0.5f;
            musicSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetMusicVolume(v));

            CareerUiKit.Label(p.transform, "SLab", new Vector2(0.5f, 0.58f), new Vector2(400f, 40f), 22f).text =
                "SFX VOLUME";
            sfxSlider = MakeSlider(p.transform, "Sfx", new Vector2(0.5f, 0.51f));
            sfxSlider.value = AudioManager.Instance != null ? AudioManager.Instance.sfxVolume : 0.7f;
            sfxSlider.onValueChanged.AddListener(v => AudioManager.Instance?.SetSFXVolume(v));

            stickSensLabel = CareerUiKit.Label(
                p.transform, "StickLab", new Vector2(0.5f, 0.42f), new Vector2(520f, 40f), 22f);
            stickSensSlider = MakeSlider(p.transform, "StickSens", new Vector2(0.5f, 0.35f));
            stickSensSlider.minValue = TecmoInput.StickSensitivityMin;
            stickSensSlider.maxValue = TecmoInput.StickSensitivityMax;
            stickSensSlider.value = TecmoInput.StickSensitivity;
            RefreshStickSensLabel(TecmoInput.StickSensitivity);
            stickSensSlider.onValueChanged.AddListener(v =>
            {
                TecmoInput.StickSensitivity = v;
                RefreshStickSensLabel(v);
            });

            CareerUiKit.Button(p.transform, "Clear", new Vector2(0.5f, 0.2f), new Vector2(280f, 44f), "CLEAR SAVE", () =>
            {
                SaveService.Instance?.ClearSave();
                Show(CareerScreen.SaveSelect);
            });
            CareerUiKit.Button(p.transform, "Quit", new Vector2(0.5f, 0.1f), new Vector2(220f, 44f), "QUIT",
                () => SceneFlow.Instance?.QuitApp());
            AddBackButton(p.transform, new Vector2(0.5f, 0.02f), new Vector2(220f, 40f));
        }

        void RefreshStickSensLabel(float value)
        {
            if (stickSensLabel == null) return;
            stickSensLabel.text = $"STICK SENSITIVITY  {value:0.00}x";
        }

        static Slider MakeSlider(Transform parent, string name, Vector2 anchor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.sizeDelta = new Vector2(420f, 28f);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.22f, 0.28f);

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            var faRt = fillArea.AddComponent<RectTransform>();
            CareerUiKit.Stretch(faRt);
            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fRt = fill.AddComponent<RectTransform>();
            CareerUiKit.Stretch(fRt);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0.35f, 0.75f, 0.45f);

            var slider = go.AddComponent<Slider>();
            slider.fillRect = fRt;
            slider.targetGraphic = fillImg;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            return slider;
        }

        void BuildDetails()
        {
            var p = MakeScreen(CareerScreen.Details, "DETAILS");
            CareerUiKit.Label(p.transform, "Body", new Vector2(0.5f, 0.5f), new Vector2(1000f, 320f), 20f).text =
                "AAVEGOTCHI BOWL\nUnity teaching remake inspired by Retro Bowl feel.\n\n" +
                "Not affiliated with New Star Games.\nDo not ship ripped Retro Bowl assets.\n\n" +
                "Controls: WASD move · Hold LMB aim/throw · E/F stiff-arm · Space dive";
            AddBackButton(p.transform);
        }

        void BuildChooseTeam()
        {
            var p = MakeScreen(CareerScreen.ChooseTeam, "CHOOSE YOUR TEAM");
            for (int i = 0; i < FavCities.Length; i++)
            {
                int idx = i;
                float y = 0.72f - i * 0.075f;
                CareerUiKit.Button(p.transform, "Team" + i, new Vector2(0.5f, y), new Vector2(420f, 42f),
                    $"{FavCities[i]} {FavNames[i]}", () =>
                    {
                        if (SaveService.Instance != null && !SaveService.Instance.HasActiveSlot)
                            SaveService.Instance.BeginNewGame(0);
                        if (TeamManager.Instance != null)
                        {
                            TeamManager.Instance.playerTeam = new TeamData(FavCities[idx], FavNames[idx],
                                new Color(0.15f, 0.45f, 0.9f), Color.white);
                            TeamManager.Instance.playerTeam.GenerateRoster();
                        }
                        SaveService.Instance?.MarkTeamChosen(FavCities[idx], FavNames[idx]);
                        SaveService.Instance?.SaveCareer();
                        Show(CareerScreen.News);
                    });
            }

            AddBackButton(p.transform);
        }

        // ───────── Refresh / tutorial modals ─────────

        void RefreshScreen(CareerScreen screen)
        {
            switch (screen)
            {
                case CareerScreen.SaveSelect:
                    RefreshSaveSelect();
                    break;
                case CareerScreen.NewCareer:
                    RefreshNewCareer();
                    break;
                case CareerScreen.News:
                    RefreshNews();
                    break;
                case CareerScreen.Home:
                    RefreshHome();
                    break;
                case CareerScreen.Roster:
                    RefreshRoster();
                    break;
                case CareerScreen.PlayerProfile:
                    RefreshProfile();
                    break;
                case CareerScreen.FrontOffice:
                    RefreshFrontOffice();
                    break;
                case CareerScreen.StaffProfile:
                    RefreshStaffProfile();
                    break;
                case CareerScreen.Playbook:
                    RefreshPlaybook();
                    break;
                case CareerScreen.Draft:
                    FillStubList(draftContent, GenerateProspectNames(), "DRAFT PROSPECT");
                    break;
                case CareerScreen.FreeAgents:
                    RefreshFreeAgents();
                    break;
                case CareerScreen.League:
                    RefreshLeague();
                    break;
                case CareerScreen.PreMatch:
                    RefreshPreMatch();
                    break;
                case CareerScreen.PostMatch:
                    RefreshPostMatch();
                    break;
                case CareerScreen.Stats:
                    RefreshStats();
                    break;
            }
        }

        void MaybeShowTutorialModal(CareerScreen screen)
        {
            var save = SaveService.Instance;
            if (save == null || !save.HasActiveSlot) return;
            var phase = save.TutorialPhase;
            if (phase >= CareerTutorialPhase.Complete) return;

            string team = TeamManager.Instance?.playerTeam?.cityName ?? "your team";

            if (screen == CareerScreen.Home && phase == CareerTutorialPhase.HomeWelcome)
            {
                ShowModal(
                    $"Welcome to {team}!\n\nThis is your home screen. You can tap on the division table to view divisions and the schedule. When you are ready tap the F.OFFICE button.",
                    "OK",
                    () =>
                    {
                        save.SetTutorial(CareerTutorialPhase.NeedFrontOffice);
                        ClearModal();
                        RefreshHome();
                    },
                    "SKIP TUTORIAL",
                    () =>
                    {
                        save.SetTutorial(CareerTutorialPhase.Complete);
                        ClearModal();
                        RefreshHome();
                    });
            }
            else if (screen == CareerScreen.FrontOffice
                     && (phase == CareerTutorialPhase.NeedFrontOffice || phase == CareerTutorialPhase.FrontOfficeTip))
            {
                ShowModal(
                    "From the Front Office you can upgrade facilities and staff. Tapping an 'i' icon will bring up useful information.",
                    "OK",
                    () =>
                    {
                        save.SetTutorial(CareerTutorialPhase.RosterNudge);
                        ClearModal();
                    });
            }
            else if (screen == CareerScreen.Home && phase == CareerTutorialPhase.RosterNudge)
            {
                ShowModal(
                    "I'm sure you want to meet your players so let's check the ROSTER screen.",
                    "OK",
                    () =>
                    {
                        save.SetTutorial(CareerTutorialPhase.RosterTip);
                        ClearModal();
                        Show(CareerScreen.Roster);
                    });
            }
            else if (screen == CareerScreen.Roster && phase == CareerTutorialPhase.RosterTip)
            {
                ShowModal(
                    "These are your key players. You don't need to manage the entire roster, just take care of these guys and everyone else will fall into line.\n\nTap on a player to open his profile.",
                    "OK",
                    () =>
                    {
                        save.SetTutorial(CareerTutorialPhase.ProfileTip);
                        ClearModal();
                    });
            }
            else if (screen == CareerScreen.PlayerProfile && phase == CareerTutorialPhase.ProfileTip)
            {
                ShowModal(
                    "Here you can view player details and attributes. Keep an eye on morale and condition.\n\nPoor morale can lead to game penalties and problems off the field.\n\nLow condition can lead to injury or worse - fumbles!",
                    "OK",
                    () =>
                    {
                        save.SetTutorial(CareerTutorialPhase.ControlsBasics);
                        ClearModal();
                    });
            }
            else if (screen == CareerScreen.Home && phase == CareerTutorialPhase.ControlsBasics)
            {
                ShowModal(
                    "Before we head into a game let's go over the basics for controlling your players on the field.",
                    "OK",
                    () =>
                    {
                        // Training Facility deferred — unlock Week / PreMatch.
                        save.SetTutorial(CareerTutorialPhase.Complete);
                        ClearModal();
                        Show(CareerScreen.PreMatch);
                    });
            }
        }

        void ShowModal(string body, string ok, UnityEngine.Events.UnityAction onOk,
            string alt = null, UnityEngine.Events.UnityAction onAlt = null)
        {
            ClearModal();
            if (modalHost == null) return;
            modalHost.transform.SetAsLastSibling();
            activeModal = CareerUiKit.Modal(modalHost.transform, "TutModal", body, ok, onOk, alt, onAlt);
            BindModalCursor();
        }

        /// <summary>Mini modal for a playbook slot: flip the concept, swap the play, or back out.</summary>
        void ShowPlaybookSlotOptions(bool isRun, int slot)
        {
            playbookCursorRow = isRun ? 0 : 1;
            playbookCursorCol = Mathf.Clamp(slot, 0, Playbook.ModalSlotCount - 1);

            ClearModal();
            if (modalHost == null) return;
            modalHost.transform.SetAsLastSibling();

            var play = isRun ? Playbook.GetRunSlotPlay(slot) : Playbook.GetPassSlotPlay(slot);
            bool flipped = isRun ? Playbook.IsRunSlotFlipped(slot) : Playbook.IsPassSlotFlipped(slot);

            var root = new GameObject("SlotOptions");
            root.transform.SetParent(modalHost.transform, false);
            CareerUiKit.Stretch(root.AddComponent<RectTransform>());
            root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            var box = CareerUiKit.BorderedBox(root.transform, "Box", new Vector2(0.5f, 0.5f),
                new Vector2(520f, 320f), CareerUiKit.RbPanel);

            var title = CareerUiKit.Label(box.transform, "Title", new Vector2(0.5f, 0.82f),
                new Vector2(440f, 40f), 26f);
            title.text = play != null ? play.DisplayName : "(EMPTY SLOT)";
            title.fontStyle = FontStyles.Bold;

            var sub = CareerUiKit.Label(box.transform, "Sub", new Vector2(0.5f, 0.66f),
                new Vector2(440f, 30f), 16f);
            sub.text = $"{(isRun ? "RUN" : "PASS")} SLOT {slot + 1}  ·  {(flipped ? "FLIPPED" : "NORMAL")}";

            CareerUiKit.OutlinedButton(box.transform, "Flip", new Vector2(0.5f, 0.46f),
                new Vector2(340f, 50f), flipped ? "UNFLIP" : "FLIP",
                () =>
                {
                    if (isRun) Playbook.ToggleRunSlotFlip(slot);
                    else Playbook.TogglePassSlotFlip(slot);
                    RefreshPlaybook();
                    ClearModal();
                });

            CareerUiKit.OutlinedButton(box.transform, "Change", new Vector2(0.5f, 0.29f),
                new Vector2(340f, 50f), "CHANGE",
                () => ShowPlaybookLibrary(isRun, slot));

            CareerUiKit.OutlinedButton(box.transform, "OK", new Vector2(0.5f, 0.12f),
                new Vector2(340f, 50f), "CANCEL", ClearModal);

            activeModal = root;
            BindModalCursor();
        }

        /// <summary>Full library for the slot's side of the ball — pick one to fill the slot.</summary>
        void ShowPlaybookLibrary(bool isRun, int slot)
        {
            ClearModal();
            if (modalHost == null) return;
            modalHost.transform.SetAsLastSibling();

            var library = isRun ? Playbook.RunPlays : Playbook.PassPlays;
            string currentId = isRun ? Playbook.GetActiveRunId(slot) : Playbook.GetActivePassId(slot);

            var root = new GameObject("SlotLibrary");
            root.transform.SetParent(modalHost.transform, false);
            CareerUiKit.Stretch(root.AddComponent<RectTransform>());
            root.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);

            var box = CareerUiKit.BorderedBox(root.transform, "Box", new Vector2(0.5f, 0.5f),
                new Vector2(900f, 560f), CareerUiKit.RbPanel);

            var title = CareerUiKit.Label(box.transform, "Title", new Vector2(0.5f, 0.9f),
                new Vector2(800f, 44f), 28f);
            title.text = isRun ? "CHOOSE A RUN PLAY" : "CHOOSE A PASS PLAY";
            title.fontStyle = FontStyles.Bold;

            const int columns = 3;
            for (int i = 0; i < library.Length; i++)
            {
                var entry = library[i];
                if (entry == null) continue;

                int row = i / columns;
                int col = i % columns;
                float x = 0.2f + col * 0.3f;
                float y = 0.72f - row * 0.2f;
                bool isCurrent = entry.Id == currentId;

                var btn = CareerUiKit.OutlinedButton(box.transform, "Play" + i, new Vector2(x, y),
                    new Vector2(230f, 60f), entry.DisplayName,
                    () =>
                    {
                        if (isRun) Playbook.SetActiveRunSlot(slot, entry.Id);
                        else Playbook.SetActivePassSlot(slot, entry.Id);
                        RefreshPlaybook();
                        ClearModal();
                    });

                if (isCurrent)
                {
                    var outline = btn.GetComponent<Outline>();
                    if (outline != null) outline.effectColor = CareerUiKit.RbYellow;
                }
            }

            CareerUiKit.OutlinedButton(box.transform, "OK", new Vector2(0.5f, 0.08f),
                new Vector2(260f, 50f), "CANCEL",
                () => ShowPlaybookSlotOptions(isRun, slot));

            activeModal = root;
            BindModalCursor();
        }

        void BindCreditsPill(Button btn)
        {
            if (btn == null) return;
            btn.onClick.AddListener(ShowBuyGltrModal);
        }

        void ShowBuyGltrModal()
        {
            ClearModal();
            if (modalHost == null) return;
            modalHost.transform.SetAsLastSibling();
            activeModal = GaxBuyGltrModal.Show(modalHost.transform,
                onClosed: () =>
                {
                    activeModal = null;
                    BindMenuCursor(Current);
                },
                onCreditsChanged: () =>
                {
                    // Refresh whichever screen is showing credit pills.
                    switch (Current)
                    {
                        case CareerScreen.Home: RefreshHome(); break;
                        case CareerScreen.Roster: RefreshRoster(); break;
                        case CareerScreen.StaffProfile: RefreshStaffProfile(); break;
                        case CareerScreen.FrontOffice: RefreshFrontOffice(); break;
                        case CareerScreen.FreeAgents: RefreshFreeAgents(); break;
                    }
                });
            BindModalCursor();
        }

        void ClearModal()
        {
            bool hadModal = activeModal != null;
            if (activeModal != null)
            {
                Destroy(activeModal);
                activeModal = null;
            }

            // Restore screen cursor after dismissing a modal (not during HideAll).
            if (hadModal && canvas != null && canvas.gameObject.activeInHierarchy)
                BindMenuCursor(Current);
        }

        void RefreshRoster()
        {
            if (rosterGrid == null) return;
            ClearChildren(rosterGrid);

            var team = TeamManager.Instance?.playerTeam;
            var roster = team?.roster;
            // Editor preview / empty slot — build a throwaway roster so cards still show.
            if (roster == null || roster.Count == 0)
            {
                if (TeamManager.Instance != null)
                {
                    if (TeamManager.Instance.playerTeam == null)
                    {
                        TeamManager.Instance.playerTeam = new TeamData("Arizona", "Rattlers",
                            new Color(0.15f, 0.45f, 0.9f), Color.white);
                    }
                    if (TeamManager.Instance.playerTeam.roster == null
                        || TeamManager.Instance.playerTeam.roster.Count == 0)
                        TeamManager.Instance.playerTeam.GenerateRoster();
                    team = TeamManager.Instance.playerTeam;
                    roster = team.roster;
                }
                else
                {
                    var stub = new TeamData("Arizona", "Rattlers",
                        new Color(0.15f, 0.45f, 0.9f), Color.white);
                    stub.GenerateRoster();
                    team = stub;
                    roster = stub.roster;
                }
            }

            var keys = CollectKeyPlayers(roster);
            profileRoster.Clear();
            profileRoster.AddRange(keys);
            const int maxSlots = 10;

            if (rosterPlayerCount != null)
                rosterPlayerCount.text = $"PLAYERS  {keys.Count} / {maxSlots}";

            var save = SaveService.Instance;
            int cc = save != null ? save.Credits : 0;
            if (rosterCredits != null)
                rosterCredits.text = cc.ToString();

            int cap = save != null ? save.SalaryCapM : 43;
            int capMax = save != null ? save.SalaryCapMaxM : 150;
            if (rosterCapLabel != null)
                rosterCapLabel.text = $"SALARY CAP  {cap}M / {capMax}M";
            if (rosterCapFill != null)
                CareerUiKit.SetProgress(rosterCapFill, capMax > 0 ? (float)cap / capMax : 0f);

            int morale = SeasonManager.Instance != null
                ? Mathf.RoundToInt(SeasonManager.Instance.teamMorale)
                : 79;
            if (rosterMoraleBox != null)
                rosterMoraleBox.text = $"{morale}%  ☺";
            if (rosterOffenseBox != null)
                rosterOffenseBox.text = CareerUiKit.StarString(OffenseStars(team), 5);
            if (rosterDefenseBox != null)
                rosterDefenseBox.text = CareerUiKit.StarString(DefenseStars(team), 5);

            foreach (var player in keys)
            {
                var p = player;
                bool offense = IsOffensePosition(p.position);
                float stars = p.stats != null ? p.stats.StarRating : 1f;
                // Condition proxy — stamina / agility until real condition exists.
                float cond = p.stats != null ? Mathf.Clamp01(p.stats.StaminaStat / 99f) : 0.85f;
                float mood = p.stats != null ? Mathf.Clamp01(p.stats.awareness / 99f) : 0.75f;

                CareerUiKit.RosterPlayerCard(
                    rosterGrid,
                    p.playerName,
                    ShortPos(p.position),
                    LastName(p.playerName),
                    stars,
                    cond,
                    offense,
                    mood,
                    out var btn);
                btn.onClick.AddListener(() =>
                {
                    selectedPlayer = p;
                    profileIndex = profileRoster.IndexOf(p);
                    if (profileIndex < 0) profileIndex = 0;
                    Show(CareerScreen.PlayerProfile);
                });
            }

            // Pad remaining slots so the grid always shows 10 card frames.
            for (int i = keys.Count; i < maxSlots; i++)
                CareerUiKit.RosterEmptySlot(rosterGrid, "Empty" + i);
        }

        static List<PlayerData> CollectKeyPlayers(List<PlayerData> roster)
        {
            var keys = new List<PlayerData>();
            if (roster == null) return keys;

            void AddPos(PlayerPosition pos, int max = 1)
            {
                int n = 0;
                foreach (var p in roster)
                {
                    if (p.position != pos || keys.Contains(p)) continue;
                    keys.Add(p);
                    n++;
                    if (n >= max) return;
                }
            }

            // Retro Bowl “key players” strip — offense then defense, up to 10.
            AddPos(PlayerPosition.Quarterback);
            AddPos(PlayerPosition.RunningBack);
            AddPos(PlayerPosition.WideReceiver, 2);
            AddPos(PlayerPosition.TightEnd);
            AddPos(PlayerPosition.DefensiveLine);
            AddPos(PlayerPosition.Linebacker);
            AddPos(PlayerPosition.Cornerback);
            AddPos(PlayerPosition.Safety);
            AddPos(PlayerPosition.OffensiveLine);
            while (keys.Count > 10)
                keys.RemoveAt(keys.Count - 1);
            return keys;
        }

        static bool IsOffensePosition(PlayerPosition pos) => pos switch
        {
            PlayerPosition.DefensiveLine or PlayerPosition.Linebacker
                or PlayerPosition.Cornerback or PlayerPosition.Safety => false,
            _ => true
        };

        void RefreshProfile()
        {
            EnsureProfileRoster();
            if (selectedPlayer == null && profileRoster.Count > 0)
            {
                profileIndex = Mathf.Clamp(profileIndex, 0, profileRoster.Count - 1);
                selectedPlayer = profileRoster[profileIndex];
            }

            if (selectedPlayer == null)
            {
                if (profileTitle != null) profileTitle.text = "PLAYER";
                if (profilePosTitle != null) profilePosTitle.text = "PLAYER";
                if (profileAge != null) profileAge.text = "—";
                if (profileMorale != null) profileMorale.text = "—";
                if (profileCondition != null) profileCondition.text = "—";
                if (profileContract != null) profileContract.text = "—";
                if (profileRating != null) profileRating.text = "";
                if (profilePotential != null) profilePotential.text = "";
                for (int i = 0; i < profileAttrLabels.Length; i++)
                {
                    if (profileAttrLabels[i] != null) profileAttrLabels[i].text = "—";
                    if (profileAttrFills[i] != null) CareerUiKit.SetProgress(profileAttrFills[i], 0f);
                }
                return;
            }

            var s = selectedPlayer.stats;
            int age = 21 + (selectedPlayer.jerseyNumber % 16);
            float mood01 = s != null ? Mathf.Clamp01(s.awareness / 99f) : 0.75f;
            float cond01 = s != null ? Mathf.Clamp01(s.StaminaStat / 99f) : 0.85f;
            string morale = mood01 >= 0.7f ? "GOOD" : mood01 >= 0.4f ? "OK" : "POOR";
            string condition = cond01 >= 0.7f ? "GREAT" : cond01 >= 0.4f ? "OK" : "LOW";
            string moodIcon = mood01 >= 0.7f ? "☺" : mood01 >= 0.4f ? "😐" : "☹";
            int years = 2 + (selectedPlayer.jerseyNumber % 3);
            int salary = 4 + (selectedPlayer.jerseyNumber % 18);

            if (profileTitle != null)
                profileTitle.text =
                    $"{ShortPos(selectedPlayer.position)} - {selectedPlayer.playerName.ToUpperInvariant()}";
            if (profilePosTitle != null)
                profilePosTitle.text = FullPosName(selectedPlayer.position);
            if (profileAge != null)
                profileAge.text = age.ToString();
            if (profileMorale != null)
                profileMorale.text = $"{morale}  {moodIcon}";
            if (profileCondition != null)
                profileCondition.text = condition;
            if (profileContract != null)
                profileContract.text = $"${salary}M  ·  {years}Y";
            if (profileRating != null && s != null)
                profileRating.text = CareerUiKit.StarString(CareerUiKit.StarsFromOvr(s.GetOverallRating()));
            if (profilePotential != null && s != null)
                profilePotential.text = CareerUiKit.StarString(
                    Mathf.Min(5f, CareerUiKit.StarsFromOvr(s.GetOverallRating()) + 0.5f));

            var attrs = AttrRowsFor(selectedPlayer.position, s);
            for (int i = 0; i < profileAttrLabels.Length; i++)
            {
                if (i < attrs.Length)
                {
                    if (profileAttrLabels[i] != null)
                    {
                        profileAttrLabels[i].text = attrs[i].label;
                        profileAttrLabels[i].gameObject.SetActive(true);
                    }
                    if (profileAttrFills[i] != null)
                    {
                        profileAttrFills[i].gameObject.SetActive(true);
                        CareerUiKit.SetProgress(profileAttrFills[i], Mathf.Clamp01(attrs[i].value / 99f));
                    }
                }
                else
                {
                    if (profileAttrLabels[i] != null) profileAttrLabels[i].gameObject.SetActive(false);
                    if (profileAttrFills[i] != null) profileAttrFills[i].gameObject.SetActive(false);
                }
            }
        }

        static (string label, int value)[] AttrRowsFor(PlayerPosition pos, PlayerStats s)
        {
            if (s == null)
                return System.Array.Empty<(string, int)>();

            return pos switch
            {
                PlayerPosition.Quarterback => new[]
                {
                    ("THROW ACCURACY", s.throwing),
                    ("ARM STRENGTH", Mathf.Clamp(s.throwing - 5 + s.strength / 10, 1, 99)),
                    ("SPEED", s.speed),
                    ("STAMINA", s.StaminaStat),
                    ("AWARENESS", s.awareness)
                },
                PlayerPosition.RunningBack => new[]
                {
                    ("SPEED", s.speed),
                    ("STRENGTH", s.strength),
                    ("CATCHING", s.catching),
                    ("STAMINA", s.StaminaStat),
                    ("AWARENESS", s.awareness)
                },
                PlayerPosition.WideReceiver or PlayerPosition.TightEnd => new[]
                {
                    ("CATCHING", s.catching),
                    ("SPEED", s.speed),
                    ("STRENGTH", s.strength),
                    ("STAMINA", s.StaminaStat),
                    ("AWARENESS", s.awareness)
                },
                PlayerPosition.OffensiveLine => new[]
                {
                    ("STRENGTH", s.strength),
                    ("BLOCKING", s.KeySkill(pos)),
                    ("STAMINA", s.StaminaStat),
                    ("AWARENESS", s.awareness),
                    ("SPEED", s.speed)
                },
                _ => new[]
                {
                    ("TACKLING", s.KeySkill(pos)),
                    ("SPEED", s.speed),
                    ("STRENGTH", s.strength),
                    ("STAMINA", s.StaminaStat),
                    ("AWARENESS", s.awareness)
                }
            };
        }

        static string FullPosName(PlayerPosition pos) => pos switch
        {
            PlayerPosition.Quarterback => "QUARTERBACK",
            PlayerPosition.RunningBack => "RUNNING BACK",
            PlayerPosition.WideReceiver => "WIDE RECEIVER",
            PlayerPosition.TightEnd => "TIGHT END",
            PlayerPosition.OffensiveLine => "OFFENSIVE LINE",
            PlayerPosition.DefensiveLine => "DEFENSIVE LINE",
            PlayerPosition.Linebacker => "LINEBACKER",
            PlayerPosition.Cornerback => "CORNERBACK",
            PlayerPosition.Safety => "SAFETY",
            _ => "PLAYER"
        };

        void RefreshLeague()
        {
            if (leagueContent == null) return;
            ClearChildren(leagueContent);
            var s = SeasonManager.Instance;
            if (s == null) return;
            for (int i = 0; i < s.schedule.Count; i++)
            {
                var g = s.schedule[i];
                string line = g.isPlayed
                    ? $"Week {g.week}:  {g.playerScore}-{g.opponentScore} {(g.playerScore > g.opponentScore ? "W" : g.playerScore < g.opponentScore ? "L" : "T")}"
                    : $"Week {g.week}:  —";
                var label = CareerUiKit.Label(leagueContent, "W" + i, new Vector2(0.5f, 0.5f), new Vector2(0f, 32f), 20f);
                label.text = line;
                label.gameObject.AddComponent<LayoutElement>().minHeight = 32f;
            }
        }

        void RefreshPostMatch()
        {
            var flow = SceneFlow.Instance;
            int p = flow != null ? flow.LastPlayerScore : 0;
            int o = flow != null ? flow.LastOpponentScore : 0;
            string result = p > o ? "YOU WIN" : p < o ? "YOU LOSE" : "TIE";
            if (flow != null && flow.LastWasPractice)
                result = "PRACTICE COMPLETE";
            if (postMatchBody != null)
                postMatchBody.text = $"{result}\n{p}  -  {o}\n\nRecord  {SeasonManager.Instance?.GetRecord()}";
        }

        void RefreshStats()
        {
            if (statsContent == null) return;
            ClearChildren(statsContent);
            var sm = ScoreManager.Instance;
            string body = sm != null
                ? $"Season snapshot\nPlayer TDs {sm.playerStats.touchdowns}  FGs {sm.playerStats.fieldGoals}\n" +
                  $"Opp TDs {sm.opponentStats.touchdowns}  FGs {sm.opponentStats.fieldGoals}"
                : "No stats yet.";
            var label = CareerUiKit.Label(statsContent, "StatsBody", new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), 22f);
            label.text = body;
            label.gameObject.AddComponent<LayoutElement>().minHeight = 120f;
        }

        void SimWeek()
        {
            if (SeasonManager.Instance == null || TeamManager.Instance == null) return;
            TeamManager.Instance.GenerateNewOpponent();
            int p = Random.Range(10, 38);
            int o = Random.Range(7, 35);
            SeasonManager.Instance.CompleteGame(p, o);
            SaveService.Instance?.SaveCareer();
            SceneFlow.Instance?.RecordResult(p, o, practice: false);
            PlayBanner.Show($"SIM  {p}-{o}", 1.5f);
            Show(CareerScreen.Home);
        }

        void FillStubList(Transform content, List<string> names, string prefix)
        {
            if (content == null) return;
            ClearChildren(content);
            foreach (var n in names)
            {
                string label = $"{prefix}: {n}";
                var btn = CareerUiKit.Button(content, n, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), label,
                    () => PlayBanner.Show(label, 0.8f, BannerTone.Neutral));
                var rt = btn.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.sizeDelta = new Vector2(0f, 40f);
                }
                btn.gameObject.AddComponent<LayoutElement>().minHeight = 40f;
            }
        }

        static List<string> GenerateProspectNames()
        {
            var list = new List<string>();
            for (int i = 0; i < 12; i++)
                list.Add($"Prospect {i + 1}  OVR {Random.Range(55, 92)}");
            return list;
        }

        static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Destroy(t.GetChild(i).gameObject);
        }

        static float OffenseStars(TeamData team)
        {
            if (team?.roster == null || team.roster.Count == 0) return 1f;
            int sum = 0, n = 0;
            foreach (var p in team.roster)
            {
                if (p.position == PlayerPosition.Quarterback || p.position == PlayerPosition.RunningBack
                    || p.position == PlayerPosition.WideReceiver || p.position == PlayerPosition.TightEnd
                    || p.position == PlayerPosition.OffensiveLine)
                {
                    sum += p.stats.GetOverallRating();
                    n++;
                }
            }
            if (n == 0) return CareerUiKit.StarsFromOvr(team.GetTeamRating());
            return CareerUiKit.StarsFromOvr(sum / n);
        }

        static float DefenseStars(TeamData team)
        {
            if (team?.roster == null || team.roster.Count == 0) return 1f;
            int sum = 0, n = 0;
            foreach (var p in team.roster)
            {
                if (p.position == PlayerPosition.DefensiveLine || p.position == PlayerPosition.Linebacker
                    || p.position == PlayerPosition.Cornerback || p.position == PlayerPosition.Safety)
                {
                    sum += p.stats.GetOverallRating();
                    n++;
                }
            }
            if (n == 0) return CareerUiKit.StarsFromOvr(team.GetTeamRating());
            return CareerUiKit.StarsFromOvr(sum / n);
        }

        static Color CardColor(PlayerPosition pos) => pos switch
        {
            PlayerPosition.DefensiveLine or PlayerPosition.Linebacker or PlayerPosition.Cornerback or PlayerPosition.Safety
                => new Color(0.65f, 0.2f, 0.22f),
            PlayerPosition.TightEnd => new Color(0.75f, 0.45f, 0.15f),
            _ => new Color(0.25f, 0.45f, 0.85f)
        };

        static string ShortPos(PlayerPosition pos) => pos switch
        {
            PlayerPosition.Quarterback => "QB",
            PlayerPosition.RunningBack => "RB",
            PlayerPosition.WideReceiver => "WR",
            PlayerPosition.TightEnd => "TE",
            PlayerPosition.DefensiveLine => "DL",
            PlayerPosition.Linebacker => "LB",
            PlayerPosition.Cornerback => "CB",
            PlayerPosition.Safety => "S",
            PlayerPosition.OffensiveLine => "OL",
            _ => "P"
        };

        static string LastName(string full)
        {
            if (string.IsNullOrEmpty(full)) return "PLAYER";
            int sp = full.LastIndexOf(' ');
            return sp >= 0 ? full.Substring(sp + 1) : full;
        }

        static string Bar(int v)
        {
            int filled = Mathf.Clamp(v / 10, 0, 10);
            return new string('█', filled) + new string('░', 10 - filled) + $"  {v}";
        }

        static string RandomFirst()
        {
            string[] n = { "Cole", "Jamie", "Alex", "Riley", "Morgan", "Casey", "Quinn", "Avery" };
            return n[Random.Range(0, n.Length)];
        }

        static string RandomLast()
        {
            string[] n = { "Gedeon", "Park", "Nguyen", "Brooks", "Carter", "Diaz", "Kim", "Walsh" };
            return n[Random.Range(0, n.Length)];
        }
    }
}
