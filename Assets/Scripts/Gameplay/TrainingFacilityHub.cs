using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.App;
using RetroBowl.Core;
using RetroBowl.UI;
using RetroBowl.UI.Career;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Training Facility on the practice field:
    /// offices → active-8 play modal → playbook editor → receiver pick → drill matchup.
    /// </summary>
    public class TrainingFacilityHub : MonoBehaviour
    {
        public static TrainingFacilityHub Instance { get; private set; }

        TrainingOffice office = TrainingOffice.None;
        OffensivePlay selectedPlay;

        Canvas canvas;
        GameObject officesRoot;
        GameObject playsRoot;
        GameObject playbookRoot;
        GameObject receiverRoot;
        GameObject drillHud;
        TextMeshProUGUI drillLabel;
        readonly List<TextMeshProUGUI> playSlotLabels = new List<TextMeshProUGUI>();
        readonly List<TextMeshProUGUI> pbRunLabels = new List<TextMeshProUGUI>();
        readonly List<TextMeshProUGUI> pbPassLabels = new List<TextMeshProUGUI>();

        public static void EnsureExists(TrainingOffice startOffice = TrainingOffice.None)
        {
            if (Instance != null)
            {
                Instance.Begin(startOffice);
                return;
            }
            var go = new GameObject("TrainingFacilityHub");
            Instance = go.AddComponent<TrainingFacilityHub>();
            Instance.Begin(startOffice);
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

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Begin(TrainingOffice startOffice)
        {
            // Rebuild UI so chrome picks up transparent blue styling after script reloads.
            if (canvas != null)
            {
                Destroy(canvas.gameObject);
                canvas = null;
                officesRoot = null;
                playsRoot = null;
                playbookRoot = null;
                receiverRoot = null;
                drillHud = null;
                playSlotLabels.Clear();
            }

            EnsureUi();
            office = startOffice;
            selectedPlay = null;

            // Hide play-call modal — do NOT deactivate the Canvas GameObject (HUD lives there).
            PlayCallingUI.DestroyOrphanPlayCallCanvases();
            var playUi = Object.FindAnyObjectByType<PlayCallingUI>(FindObjectsInactive.Include);
            if (playUi != null)
                playUi.SuspendForTraining();

            if (office == TrainingOffice.None)
                ShowOffices();
            else
                OpenOffice(office);
        }

        public void Teardown()
        {
            if (canvas != null)
                Destroy(canvas.gameObject);
            canvas = null;
            Destroy(gameObject);
        }

        void EnsureUi()
        {
            if (canvas != null) return;

            var go = new GameObject("TrainingFacilityCanvas");
            go.transform.SetParent(transform, false);
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 75;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            go.AddComponent<GraphicRaycaster>();

            BuildOfficesPanel();
            BuildPlaysPanel();
            BuildPlaybookPanel();
            BuildReceiverPanel();
            BuildDrillHud();

            GameFonts.ApplyAllUnder(canvas.transform);
        }

        void HideAllPanels()
        {
            if (officesRoot != null) officesRoot.SetActive(false);
            if (playsRoot != null) playsRoot.SetActive(false);
            if (playbookRoot != null) playbookRoot.SetActive(false);
            if (receiverRoot != null) receiverRoot.SetActive(false);
            if (drillHud != null) drillHud.SetActive(false);
        }

        // ───────── Offices ─────────

        // Retro Bowl Training Facility modal chrome (teal field tutorial).
        static readonly Color TrainModalFill = CareerUiKit.TrainModalFill;
        static readonly Color TrainModalDeep = new Color(0.08f, 0.14f, 0.38f, 0.32f);
        static readonly Color TrainBtnFill = CareerUiKit.TrainBtnFill;

        void BuildOfficesPanel()
        {
            officesRoot = CareerUiKit.SolidPanel(canvas.transform, "Offices", TrainModalDeep);
            CareerUiKit.StyleTrainModal(officesRoot, TrainModalDeep);
            CareerUiKit.Label(officesRoot.transform, "Title", new Vector2(0.5f, 0.88f),
                new Vector2(900f, 50f), 36f).text = "* * *  TRAINING FACILITY  * * *";
            CareerUiKit.Label(officesRoot.transform, "Sub", new Vector2(0.5f, 0.78f),
                new Vector2(800f, 36f), 20f).text = "Choose an office to open the practice field";

            CareerUiKit.OutlinedButton(officesRoot.transform, "HC", new Vector2(0.5f, 0.58f),
                new Vector2(420f, 64f), "HEAD COACH OFFICE",
                () => OpenOffice(TrainingOffice.HeadCoach), TrainBtnFill);
            CareerUiKit.OutlinedButton(officesRoot.transform, "OC", new Vector2(0.5f, 0.44f),
                new Vector2(420f, 64f), "OC OFFICE",
                () => OpenOffice(TrainingOffice.OffenseCoordinator), TrainBtnFill);
            CareerUiKit.OutlinedButton(officesRoot.transform, "DC", new Vector2(0.5f, 0.3f),
                new Vector2(420f, 64f), "DC OFFICE",
                () => OpenOffice(TrainingOffice.DefenseCoordinator), TrainBtnFill);
            CareerUiKit.OutlinedButton(officesRoot.transform, "Exit", new Vector2(0.12f, 0.1f),
                new Vector2(180f, 48f), "EXIT", () => PracticeMode.ExitToMenu(), TrainBtnFill);
        }

        void ShowOffices()
        {
            HideAllPanels();
            if (officesRoot != null)
            {
                CareerUiKit.StyleTrainModal(officesRoot, TrainModalDeep);
                officesRoot.SetActive(true);
            }
        }

        void OpenOffice(TrainingOffice o)
        {
            office = o;
            ShowActivePlays();
        }

        static string OfficeTitle(TrainingOffice o) => o switch
        {
            TrainingOffice.OffenseCoordinator => "OC OFFICE",
            TrainingOffice.DefenseCoordinator => "DC OFFICE",
            _ => "HEAD COACH OFFICE"
        };

        // ───────── Active 8 plays ─────────

        void BuildPlaysPanel()
        {
            playsRoot = CareerUiKit.SolidPanel(canvas.transform, "ActivePlays", TrainModalFill);
            CareerUiKit.StyleTrainModal(playsRoot);
            CareerUiKit.Label(playsRoot.transform, "Title", new Vector2(0.5f, 0.92f),
                new Vector2(900f, 44f), 32f).text = "ACTIVE PLAYS";
            CareerUiKit.Label(playsRoot.transform, "Hint", new Vector2(0.5f, 0.86f),
                new Vector2(900f, 28f), 18f).text =
                "Tap a play to drill · PLAYBOOK to activate / deactivate slots";

            playSlotLabels.Clear();
            for (int i = 0; i < 8; i++)
            {
                int idx = i;
                bool isRun = i < 4;
                int slot = i % 4;
                float x = isRun ? 0.28f : 0.72f;
                float y = 0.72f - slot * 0.12f;
                var btn = CareerUiKit.OutlinedButton(playsRoot.transform, "Play" + i,
                    new Vector2(x, y), new Vector2(340f, 52f), "—",
                    () => OnPickActivePlay(isRun, slot));
                playSlotLabels.Add(btn.GetComponentInChildren<TextMeshProUGUI>());
            }

            CareerUiKit.Label(playsRoot.transform, "RunHdr", new Vector2(0.28f, 0.8f),
                new Vector2(200f, 28f), 20f).text = "RUN";
            CareerUiKit.Label(playsRoot.transform, "PassHdr", new Vector2(0.72f, 0.8f),
                new Vector2(200f, 28f), 20f).text = "PASS";

            CareerUiKit.OutlinedButton(playsRoot.transform, "Playbook", new Vector2(0.5f, 0.18f),
                new Vector2(280f, 52f), "OPEN PLAYBOOK", ShowPlaybookEdit, CareerUiKit.RbYellow);
            CareerUiKit.OutlinedButton(playsRoot.transform, "Offices", new Vector2(0.12f, 0.08f),
                new Vector2(200f, 48f), "OFFICES", ShowOffices);
            CareerUiKit.OutlinedButton(playsRoot.transform, "Exit", new Vector2(0.88f, 0.08f),
                new Vector2(160f, 48f), "EXIT", () => PracticeMode.ExitToMenu());
        }

        void ShowActivePlays()
        {
            HideAllPanels();
            if (playsRoot != null)
            {
                CareerUiKit.StyleTrainModal(playsRoot, TrainModalFill);
                playsRoot.SetActive(true);
            }
            var title = playsRoot.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
            if (title != null)
                title.text = $"{OfficeTitle(office)}  ·  ACTIVE PLAYS";
            RefreshActivePlayLabels();
        }

        void RefreshActivePlayLabels()
        {
            for (int i = 0; i < playSlotLabels.Count && i < 8; i++)
            {
                bool isRun = i < 4;
                int slot = i % 4;
                var label = playSlotLabels[i];
                if (label == null) continue;
                if (isRun)
                {
                    bool on = Playbook.IsRunSlotActive(slot);
                    var play = on ? Playbook.FindById(Playbook.GetActiveRunId(slot)) : null;
                    label.text = on && play != null
                        ? $"{slot + 1}. {play.DisplayName}"
                        : $"{slot + 1}. (OFF)";
                    label.color = on ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                }
                else
                {
                    bool on = Playbook.IsPassSlotActive(slot);
                    var play = on ? Playbook.FindById(Playbook.GetActivePassId(slot)) : null;
                    label.text = on && play != null
                        ? $"{slot + 1}. {play.DisplayName}"
                        : $"{slot + 1}. (OFF)";
                    label.color = on ? Color.white : new Color(1f, 1f, 1f, 0.4f);
                }
            }
        }

        void OnPickActivePlay(bool isRun, int slot)
        {
            if (isRun && !Playbook.IsRunSlotActive(slot)) return;
            if (!isRun && !Playbook.IsPassSlotActive(slot)) return;
            selectedPlay = isRun ? Playbook.GetRunSlotPlay(slot) : Playbook.GetPassSlotPlay(slot);
            if (selectedPlay == null) return;
            ShowReceiverPick();
        }

        // ───────── Playbook edit ─────────

        void BuildPlaybookPanel()
        {
            playbookRoot = CareerUiKit.SolidPanel(canvas.transform, "PlaybookEdit", TrainModalFill);
            CareerUiKit.StyleTrainModal(playbookRoot);
            CareerUiKit.Label(playbookRoot.transform, "Title", new Vector2(0.5f, 0.92f),
                new Vector2(900f, 40f), 32f).text = "* * *  PLAYBOOK  * * *";
            CareerUiKit.Label(playbookRoot.transform, "Hint", new Vector2(0.5f, 0.84f),
                new Vector2(960f, 36f), 17f).text =
                "CYCLE = next concept · TOGGLE = activate / deactivate for the active 8";

            CareerUiKit.Label(playbookRoot.transform, "RunHdr", new Vector2(0.28f, 0.76f),
                new Vector2(280f, 28f), 22f).text = "RUN";
            CareerUiKit.Label(playbookRoot.transform, "PassHdr", new Vector2(0.72f, 0.76f),
                new Vector2(280f, 28f), 22f).text = "PASS";

            pbRunLabels.Clear();
            pbPassLabels.Clear();
            for (int i = 0; i < Playbook.ModalSlotCount; i++)
            {
                int slot = i;
                float y = 0.66f - i * 0.12f;

                var runCycle = CareerUiKit.OutlinedButton(playbookRoot.transform, "RunC" + i,
                    new Vector2(0.22f, y), new Vector2(260f, 44f), "RUN",
                    () => { Playbook.CycleActiveRunSlot(slot); RefreshPlaybookEdit(); });
                pbRunLabels.Add(runCycle.GetComponentInChildren<TextMeshProUGUI>());

                CareerUiKit.OutlinedButton(playbookRoot.transform, "RunT" + i,
                    new Vector2(0.4f, y), new Vector2(100f, 44f), "ON/OFF",
                    () => { Playbook.ToggleActiveRunSlot(slot); RefreshPlaybookEdit(); });

                var passCycle = CareerUiKit.OutlinedButton(playbookRoot.transform, "PassC" + i,
                    new Vector2(0.66f, y), new Vector2(260f, 44f), "PASS",
                    () => { Playbook.CycleActivePassSlot(slot); RefreshPlaybookEdit(); });
                pbPassLabels.Add(passCycle.GetComponentInChildren<TextMeshProUGUI>());

                CareerUiKit.OutlinedButton(playbookRoot.transform, "PassT" + i,
                    new Vector2(0.84f, y), new Vector2(100f, 44f), "ON/OFF",
                    () => { Playbook.ToggleActivePassSlot(slot); RefreshPlaybookEdit(); });
            }

            CareerUiKit.OutlinedButton(playbookRoot.transform, "Back", new Vector2(0.5f, 0.1f),
                new Vector2(220f, 48f), "DONE", ShowActivePlays, CareerUiKit.RbYellow);
        }

        void ShowPlaybookEdit()
        {
            HideAllPanels();
            if (playbookRoot != null)
            {
                CareerUiKit.StyleTrainModal(playbookRoot, TrainModalFill);
                playbookRoot.SetActive(true);
            }
            RefreshPlaybookEdit();
        }

        void RefreshPlaybookEdit()
        {
            for (int i = 0; i < Playbook.ModalSlotCount; i++)
            {
                if (i < pbRunLabels.Count && pbRunLabels[i] != null)
                {
                    bool on = Playbook.IsRunSlotActive(i);
                    var play = on ? Playbook.FindById(Playbook.GetActiveRunId(i)) : null;
                    pbRunLabels[i].text = on && play != null
                        ? $"{i + 1}. {play.DisplayName}"
                        : $"{i + 1}. (OFF)";
                }
                if (i < pbPassLabels.Count && pbPassLabels[i] != null)
                {
                    bool on = Playbook.IsPassSlotActive(i);
                    var play = on ? Playbook.FindById(Playbook.GetActivePassId(i)) : null;
                    pbPassLabels[i].text = on && play != null
                        ? $"{i + 1}. {play.DisplayName}"
                        : $"{i + 1}. (OFF)";
                }
            }
        }

        // ───────── Receiver pick ─────────

        void BuildReceiverPanel()
        {
            receiverRoot = CareerUiKit.SolidPanel(canvas.transform, "Receivers", TrainModalFill);
            CareerUiKit.StyleTrainModal(receiverRoot);
            CareerUiKit.Label(receiverRoot.transform, "Title", new Vector2(0.5f, 0.88f),
                new Vector2(900f, 44f), 30f).text = "SELECT TARGET";
            CareerUiKit.Label(receiverRoot.transform, "Sub", new Vector2(0.5f, 0.8f),
                new Vector2(800f, 32f), 18f).text = "";
            CareerUiKit.OutlinedButton(receiverRoot.transform, "Back", new Vector2(0.12f, 0.1f),
                new Vector2(180f, 48f), "BACK", ShowActivePlays);
        }

        void ShowReceiverPick()
        {
            HideAllPanels();
            if (receiverRoot == null) return;
            CareerUiKit.StyleTrainModal(receiverRoot, TrainModalFill);
            receiverRoot.SetActive(true);

            // Clear prior target buttons (keep Title / Sub / Back).
            for (int i = receiverRoot.transform.childCount - 1; i >= 0; i--)
            {
                var child = receiverRoot.transform.GetChild(i);
                if (child.name == "Title" || child.name == "Sub" || child.name == "Back")
                    continue;
                Destroy(child.gameObject);
            }

            var title = receiverRoot.transform.Find("Title")?.GetComponent<TextMeshProUGUI>();
            var sub = receiverRoot.transform.Find("Sub")?.GetComponent<TextMeshProUGUI>();
            if (title != null)
                title.text = selectedPlay != null
                    ? $"DRILL: {selectedPlay.DisplayName}"
                    : "SELECT TARGET";

            bool isRun = selectedPlay != null && selectedPlay.Type == OffensivePlayType.Run;
            if (sub != null)
            {
                sub.text = isRun
                    ? "Run play — work the RB vs the LB covering him"
                    : office == TrainingOffice.DefenseCoordinator
                        ? "Defense drill — cover the selected receiver"
                        : "Pass play — pick a receiver (QB + coverage shown)";
            }

            var options = new List<(string unit, string label)>();
            if (isRun)
            {
                options.Add(("RB", "RB  ·  vs LB_1"));
            }
            else
            {
                options.Add(("WR_Top", "WR TOP  ·  vs CB_TOP"));
                options.Add(("WR_Bot", "WR BOT  ·  vs CB_BOT"));
                options.Add(("TE", "TE  ·  vs LB_2"));
            }

            for (int i = 0; i < options.Count; i++)
            {
                var opt = options[i];
                float y = 0.62f - i * 0.14f;
                CareerUiKit.OutlinedButton(receiverRoot.transform, "T_" + opt.unit,
                    new Vector2(0.5f, y), new Vector2(420f, 56f), opt.label,
                    () => StartDrill(opt.unit), CareerUiKit.RbYellow);
            }
        }

        // ───────── Drill ─────────

        void BuildDrillHud()
        {
            drillHud = new GameObject("DrillHud");
            drillHud.transform.SetParent(canvas.transform, false);
            var rt = drillHud.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.92f);
            rt.anchorMax = new Vector2(0.5f, 0.92f);
            rt.sizeDelta = new Vector2(980f, 70f);
            var bg = drillHud.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.14f, 0.9f);
            drillLabel = CareerUiKit.Label(drillHud.transform, "Lbl", new Vector2(0.5f, 0.55f),
                new Vector2(940f, 36f), 20f);
            CareerUiKit.OutlinedButton(drillHud.transform, "Menu", new Vector2(0.12f, 0.15f),
                new Vector2(140f, 36f), "PLAYS", ShowActivePlays);
            CareerUiKit.OutlinedButton(drillHud.transform, "Exit", new Vector2(0.88f, 0.15f),
                new Vector2(120f, 36f), "EXIT", () => PracticeMode.ExitToMenu());
            drillHud.SetActive(false);
        }

        void StartDrill(string skillUnit)
        {
            if (selectedPlay == null) return;

            bool isRun = selectedPlay.Type == OffensivePlayType.Run;
            string coverage = FormationRoster.CoverageForSkill(skillUnit, isRun);
            FormationRoster.ApplyTrainingMatchup(skillUnit, coverage, selectedPlay);

            // Drop train modal blue fill — only the small drill HUD stays.
            HideAllPanels();
            if (drillHud != null) drillHud.SetActive(true);

            string posLine = isRun
                ? $"QB · RB · {coverage}"
                : $"QB · {skillUnit} · {coverage}";
            if (drillLabel != null)
            {
                drillLabel.text =
                    $"{OfficeTitle(office)}  ·  {selectedPlay.DisplayName}  ·  {posLine}\n" +
                    (isRun ? "Handoff / run the drill" : "Snap · aim · throw to the highlighted target");
            }

            PlayBanner.Show(
                isRun ? $"RUN DRILL\n{skillUnit} vs {coverage}" : $"PASS DRILL\n{skillUnit} vs {coverage}",
                1.6f, BannerTone.Neutral);

            // Arm a live practice rep — hike with D-pad / WASD after cadence.
            if (GameManager.Instance != null)
            {
                GameManager.Instance.isPreSnap = true;
                GameManager.Instance.SkipPostScoreFlowOnce();
            }
            Playbook.Select(selectedPlay);
            Playbook.ApplyRoutesToFormation();

            PlayCallingUI.DestroyOrphanPlayCallCanvases();
            var playUi = Object.FindAnyObjectByType<PlayCallingUI>(FindObjectsInactive.Include);
            if (playUi != null)
                playUi.SuspendForTraining();

            SnapCadence.EnsureExists();
            if (SnapCadence.Instance != null)
                SnapCadence.Instance.Begin();
        }
    }
}
