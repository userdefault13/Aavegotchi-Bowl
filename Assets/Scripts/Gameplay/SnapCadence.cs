using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.Core;
using RetroBowl.UI;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// After a pass is called: READY → SET → HUT → HUT… until click.
    /// Click snaps (releases AI) and starts QB aim.
    /// </summary>
    public class SnapCadence : MonoBehaviour
    {
        public static SnapCadence Instance { get; private set; }

        [Header("Timing")]
        public float readyHold = 0.55f;
        public float setHold = 0.45f;
        public float hutInterval = 0.55f;

        [Header("UI")]
        public TextMeshProUGUI cadenceText;

        public bool IsActive { get; private set; }
        /// <summary>True while AI offense runs a fixed three-hut cadence (player on defense).</summary>
        public bool IsDefenseCadence { get; private set; }

        float phaseTimer;
        int phase; // 0 ready, 1 set, 2+ hut
        bool waitingForClick;
        int defenseHutTarget = 3;
        int defenseHutShown;

        void Awake()
        {
            Instance = this;
            EnsureUi();
            Hide();
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var host = GameManager.Instance != null
                ? GameManager.Instance.gameObject
                : new GameObject("SnapCadence");
            if (host.GetComponent<SnapCadence>() == null)
                host.AddComponent<SnapCadence>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void EnsureUi()
        {
            if (cadenceText != null) return;

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("CadenceCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            var go = new GameObject("CadenceText");
            go.transform.SetParent(canvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.62f);
            rt.anchorMax = new Vector2(0.5f, 0.62f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(900f, 120f);
            rt.anchoredPosition = Vector2.zero;

            cadenceText = go.AddComponent<TextMeshProUGUI>();
            GameFonts.Apply(cadenceText);
            cadenceText.alignment = TextAlignmentOptions.Center;
            cadenceText.fontSize = 72f;
            cadenceText.fontStyle = FontStyles.Bold;
            cadenceText.color = Color.white;
            cadenceText.outlineWidth = 0.2f;
            cadenceText.outlineColor = Color.black;
            cadenceText.text = "";
            cadenceText.raycastTarget = false;
        }

        public void Begin()
        {
            EnsureUi();
            IsActive = true;
            IsDefenseCadence = false;
            waitingForClick = false;
            defenseHutShown = 0;
            phase = 0;
            phaseTimer = 0f;
            // Boost only arms on ReleaseSnap evaluate — never carry over from last down.
            DefenseGuessBlitz.Clear();
            ShowWord("READY");
            Debug.Log("Cadence: READY");
        }

        /// <summary>
        /// Defense mode: READY → SET → HUT × hutCount, then auto-snap so the
        /// player has time to cycle defenders (J prev / K next). No cycle required to snap.
        /// </summary>
        public void BeginDefenseAutoSnap(int hutCount = 3)
        {
            EnsureUi();
            IsActive = true;
            IsDefenseCadence = true;
            waitingForClick = false;
            defenseHutTarget = Mathf.Max(1, hutCount);
            defenseHutShown = 0;
            phase = 0;
            phaseTimer = 0f;
            DefenseGuessBlitz.Clear();
            ShowWord("READY");
            Debug.Log($"Defense cadence: READY (auto-snap after {defenseHutTarget} huts)");
        }

        public void Cancel()
        {
            IsActive = false;
            IsDefenseCadence = false;
            waitingForClick = false;
            defenseHutShown = 0;
            Hide();
        }

        void Update()
        {
            if (!IsActive) return;
            if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Playing)
            {
                Cancel();
                return;
            }

            // Offense: D-pad / WASD / arrows hike anytime during cadence.
            // Start / click / Space still snap after HUT. Right stick is aim-only post-snap.
            if (!IsDefenseCadence)
            {
                bool isRun = Playbook.Selected != null
                             && Playbook.Selected.Type == OffensivePlayType.Run;

                bool dpadHike = TecmoInput.HikeDown();
                bool confirmHike = waitingForClick && TecmoInput.ConfirmDown();

                if (dpadHike || confirmHike)
                {
                    if (confirmHike && !dpadHike
                        && GameManager.Instance != null
                        && GameManager.Instance.SuppressPlayClick)
                        return;

                    if (GameManager.Instance != null && !GameManager.Instance.CanStartPlay())
                    {
                        IsActive = false;
                        waitingForClick = false;
                        Hide();
                        return;
                    }

                    FinishOffenseSnap(isRun);
                    return;
                }
            }

            phaseTimer += Time.deltaTime;

            if (phase == 0)
            {
                if (phaseTimer >= readyHold)
                {
                    phase = 1;
                    phaseTimer = 0f;
                    ShowWord("SET");
                }
                return;
            }

            if (phase == 1)
            {
                if (phaseTimer >= setHold)
                {
                    phase = 2;
                    phaseTimer = 0f;
                    if (IsDefenseCadence)
                    {
                        defenseHutShown = 1;
                        ShowWord("HUT");
                    }
                    else
                    {
                        waitingForClick = true;
                        ShowWord("HUT");
                    }
                }
                return;
            }

            // phase >= 2 — hut loop
            if (phaseTimer < hutInterval) return;
            phaseTimer = 0f;

            if (IsDefenseCadence)
            {
                // After the Nth hut has been on screen for one interval → snap.
                if (defenseHutShown >= defenseHutTarget)
                {
                    AutoSnapDefense();
                    return;
                }

                defenseHutShown++;
                ShowWord("HUT");
                return;
            }

            ShowWord("HUT");
        }

        void AutoSnapDefense()
        {
            if (GameManager.Instance != null && !GameManager.Instance.CanStartPlay())
            {
                Cancel();
                return;
            }

            bool isRun = Playbook.Selected != null
                         && Playbook.Selected.Type == OffensivePlayType.Run;
            ReleaseSnap(isRun);

            IsActive = false;
            IsDefenseCadence = false;
            waitingForClick = false;
            Hide();
            if (PlayRoutePreview.Instance != null)
                PlayRoutePreview.Instance.Hide();

            if (PlayerDefenseController.Instance != null)
                PlayerDefenseController.Instance.LockControlOnSnap();

            if (DefensePlayDirector.Instance != null)
                DefensePlayDirector.Instance.OnDefenseSnap(isRun);

            Debug.Log(isRun
                ? "Defense cadence: snapped — AI handoff"
                : "Defense cadence: snapped — AI pass");
        }

        void FinishOffenseSnap(bool isRun)
        {
            ReleaseSnap(isRun);
            IsActive = false;
            waitingForClick = false;
            Hide();
            if (PlayRoutePreview.Instance != null)
                PlayRoutePreview.Instance.Hide();

            if (isRun)
                Debug.Log("Cadence: HUT — snapped, handoff");
            else
                Debug.Log("Cadence: HUT — snapped. Right stick aim, release to throw. D-pad to move.");
        }

        void ReleaseSnap(bool isRun)
        {
            MatchPresentation.Snap();
            if (GameManager.Instance != null)
                GameManager.Instance.SnapBall();

            // Tecmo guess: if D locked the same play id, max boost + force blitz this play.
            DefenseGuessBlitz.EvaluateOnSnap();

            // Ensure playbook routes are live even if preview was skipped.
            if (Playbook.Selected != null)
                Playbook.ApplyRoutesToFormation();

            foreach (var go in GameObject.FindGameObjectsWithTag("Receiver"))
            {
                if (go == null || !go.activeInHierarchy) continue;

                // RB takes the handoff on run plays — no route sprint.
                if (isRun && go.name.StartsWith("RB"))
                    continue;

                var blocker = go.GetComponent<OffensiveBlocker>();
                if (blocker != null && blocker.BlocksBeforeRoute)
                {
                    if (isRun)
                        blocker.BeginRunBlock();
                    else
                        blocker.BeginPassPro(); // TE chips first, then releases into route.
                    continue;
                }

                var rc = go.GetComponent<ReceiverController>();
                if (rc != null) rc.StartRoute();
            }

            // OL: run block seals lanes; pass pro protects the pocket.
            GameObject[] line;
            try { line = GameObject.FindGameObjectsWithTag("Lineman"); }
            catch { line = System.Array.Empty<GameObject>(); }

            foreach (var go in line)
            {
                if (go == null || !go.activeInHierarchy) continue;
                var blocker = go.GetComponent<OffensiveBlocker>();
                if (blocker == null) continue;
                if (isRun)
                    blocker.BeginRunBlock();
                else
                    blocker.BeginPassPro();
            }

            if (isRun)
                HandoffToRunningBack();
            else
                AttachBallToQuarterback();
        }

        static void AttachBallToQuarterback()
        {
            var qb = GameObject.Find("Quarterback");
            if (qb == null)
            {
                try { qb = GameObject.FindGameObjectWithTag("Player"); }
                catch { /* tag missing */ }
            }
            if (qb == null) return;

            FootballBehavior.AttachHeldTo(qb.transform);
            var pc = qb.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.hasBall = true;
                pc.SetControlled(
                    FieldManager.Instance == null || FieldManager.Instance.isPlayerPossession);
            }
        }

        static void HandoffToRunningBack()
        {
            // Ball leaves the turf into the RB hands (hike → handoff).
            var rbGo = GameObject.Find("RB");
            if (rbGo == null) return;
            var rc = rbGo.GetComponent<ReceiverController>();
            if (rc != null)
                rc.ReceiveHandoff();
            else
                FootballBehavior.AttachHeldTo(rbGo.transform);
        }

        static void ArmQuarterbackAim()
        {
            var qb = GameObject.FindGameObjectWithTag("Player");
            if (qb == null) return;
            var qbc = qb.GetComponent<QuarterbackController>();
            if (qbc != null) qbc.BeginAimFromCadence();
        }

        void ShowWord(string word)
        {
            if (cadenceText == null) return;
            cadenceText.gameObject.SetActive(true);
            cadenceText.text = word;
        }

        void Hide()
        {
            if (cadenceText == null) return;
            cadenceText.text = "";
            cadenceText.gameObject.SetActive(false);
        }
    }
}
