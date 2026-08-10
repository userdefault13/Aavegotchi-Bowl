using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.App;
using RetroBowl.Core;
using RetroBowl.Gameplay;
using RetroBowl.Managers;

namespace RetroBowl.UI
{
    /// <summary>
    /// Legacy same-scene career overlay. Prefer CareerNav in CareerScene.
    /// Kept for fallback when SceneFlow is unavailable.
    /// </summary>
    public class CareerHub : MonoBehaviour
    {
        public static CareerHub Instance { get; private set; }

        GameObject root;
        TextMeshProUGUI titleLabel;
        TextMeshProUGUI infoLabel;
        TextMeshProUGUI dilemmaLabel;
        GameObject dilemmaRow;
        bool dilemmaActive;
        string dilemmaId;

        public static void EnsureExists()
        {
            if (Instance != null) return;
            // Hybrid architecture: do not spawn Match overlays — use CareerNav.
            if (SceneFlow.Instance != null) return;
            var host = GameManager.Instance != null
                ? GameManager.Instance.gameObject
                : new GameObject("CareerHub");
            if (host.GetComponent<CareerHub>() == null)
                host.AddComponent<CareerHub>();
        }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Show()
        {
            if (SceneFlow.Instance != null)
            {
                Hide();
                SceneFlow.Instance.GoToCareer(CareerScreen.Home);
                return;
            }

            EnsureUi();
            Refresh();
            root.SetActive(true);
            MaybeRollDilemma();
        }

        public void Hide()
        {
            if (root != null)
                root.SetActive(false);
            dilemmaActive = false;
        }

        void Refresh()
        {
            var season = SeasonManager.Instance;
            var team = TeamManager.Instance;

            string teamName = team != null && team.playerTeam != null
                ? $"{team.playerTeam.cityName} {team.playerTeam.teamName}"
                : "YOUR TEAM";

            if (titleLabel != null)
                titleLabel.text = "AAVEGOTCHI BOWL";

            string week = season != null ? $"WEEK {season.currentWeek}/{season.totalWeeks}" : "WEEK —";
            string record = season != null ? $"RECORD  {season.GetRecord()}" : "";
            string morale = season != null ? $"MORALE  {Mathf.RoundToInt(season.teamMorale)}" : "";
            string opp = team != null && team.opponentTeam != null
                ? $"NEXT  vs {team.opponentTeam.cityName} {team.opponentTeam.teamName}"
                : "";
            string weather = "WEATHER  ?";
            WeatherSystem.EnsureExists();
            if (WeatherSystem.Instance != null)
                weather = $"FORECAST  {WeatherSystem.Instance.Label}";

            if (infoLabel != null)
                infoLabel.text = $"{teamName}\n{week}   ·   {record}\n{morale}\n{opp}\n{weather}";

            if (dilemmaRow != null)
                dilemmaRow.SetActive(dilemmaActive);
            if (dilemmaLabel != null && dilemmaActive)
                dilemmaLabel.text = DilemmaText(dilemmaId);
        }

        void MaybeRollDilemma()
        {
            var season = SeasonManager.Instance;
            if (season == null) return;
            if (season.currentWeek <= 1) return;
            if (season.currentWeek % 3 != 0) return;
            if (dilemmaActive) return;
            if (Random.value > 0.55f) return;

            dilemmaActive = true;
            dilemmaId = Random.value < 0.5f ? "rest" : "press";
            Refresh();
        }

        static string DilemmaText(string id) => id switch
        {
            "rest" => "STAR WR WANTS REST\n1 KEEP HIM IN   ·   2 REST (+MORALE, -RATING)",
            "press" => "PRESS ASKS ABOUT LOSSES\n1 HONEST   ·   2 SPIN (+FANS RISK)",
            _ => "TEAM DILEMMA\n1 OR 2"
        };

        void Update()
        {
            if (root == null || !root.activeSelf) return;
            if (GameManager.Instance != null
                && GameManager.Instance.currentState != GameState.Menu
                && GameManager.Instance.currentState != GameState.GameOver)
            {
                // Hub only on menu / post-game.
                if (GameManager.Instance.currentState == GameState.Playing)
                    Hide();
                return;
            }

            if (dilemmaActive)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1))
                    ResolveDilemma(1);
                else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2))
                    ResolveDilemma(2);
            }

            if (Input.GetKeyDown(KeyCode.P))
                OnPractice();
            else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                if (!dilemmaActive)
                    OnPlayWeek();
            }
        }

        void ResolveDilemma(int choice)
        {
            var season = SeasonManager.Instance;
            if (season == null) { dilemmaActive = false; Refresh(); return; }

            if (dilemmaId == "rest")
            {
                if (choice == 2)
                    season.AdjustMorale(8f);
                else
                    season.AdjustMorale(-3f);
            }
            else
            {
                if (choice == 1)
                    season.AdjustMorale(4f);
                else
                    season.AdjustMorale(Random.value < 0.5f ? 6f : -6f);
            }

            dilemmaActive = false;
            PlayBanner.Show("DILEMMA RESOLVED", 1.2f, BannerTone.Neutral);
            Refresh();
        }

        public void OnPlayWeek()
        {
            Hide();
            if (SceneFlow.Instance != null)
            {
                SceneFlow.Instance.GoToCareer(CareerScreen.PreMatch);
                return;
            }

            if (TeamManager.Instance != null)
                TeamManager.Instance.GenerateNewOpponent();

            WeatherSystem.EnsureExists();
            WeatherSystem.Instance?.RollForMatch();

            if (GameManager.Instance != null)
            {
                GameManager.Instance.isPracticeMode = false;
                GameManager.Instance.StartNewGame();
                PostScoreFlow.BeginOpeningKickoff();
            }
        }

        public void OnPractice()
        {
            Hide();
            if (SceneFlow.Instance != null)
            {
                SceneFlow.Instance.StartMatch(MatchLaunchArgs.Practice());
                return;
            }
            PracticeMode.Start();
        }

        public void OnSimWeek()
        {
            if (SeasonManager.Instance == null || TeamManager.Instance == null) return;

            TeamManager.Instance.GenerateNewOpponent();
            int p = Random.Range(10, 38);
            int o = Random.Range(7, 35);
            // Morale / rating slight bias.
            float moraleBias = (SeasonManager.Instance.teamMorale - 50f) / 100f;
            if (Random.value < 0.5f + moraleBias * 0.2f)
                p += Random.Range(0, 7);
            else
                o += Random.Range(0, 7);

            SeasonManager.Instance.CompleteGame(p, o);
            PlayBanner.Show($"SIM RESULT\n{p} - {o}", 2f, BannerTone.Neutral);
            Refresh();
        }

        void EnsureUi()
        {
            if (root != null) return;

            var canvas = Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("CareerCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 70;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            root = new GameObject("CareerHubRoot");
            root.transform.SetParent(canvas.transform, false);
            var rt = root.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.08f, 0.12f, 0.92f);

            titleLabel = MakeLabel(root.transform, "Title", new Vector2(0.5f, 0.88f), 48f);
            infoLabel = MakeLabel(root.transform, "Info", new Vector2(0.5f, 0.62f), 26f);

            MakeButton(root.transform, "Play", new Vector2(0.5f, 0.40f), "PLAY WEEK  (ENTER)", OnPlayWeek);
            MakeButton(root.transform, "Practice", new Vector2(0.5f, 0.30f), "PRACTICE  (P)", OnPractice);
            MakeButton(root.transform, "Sim", new Vector2(0.5f, 0.20f), "SIM WEEK", OnSimWeek);

            dilemmaRow = new GameObject("Dilemma");
            dilemmaRow.transform.SetParent(root.transform, false);
            var drt = dilemmaRow.AddComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.5f, 0.08f);
            drt.anchorMax = new Vector2(0.5f, 0.08f);
            drt.sizeDelta = new Vector2(900f, 80f);
            dilemmaLabel = dilemmaRow.AddComponent<TextMeshProUGUI>();
            GameFonts.Apply(dilemmaLabel);
            dilemmaLabel.alignment = TextAlignmentOptions.Center;
            dilemmaLabel.fontSize = 22f;
            dilemmaLabel.fontStyle = FontStyles.Bold;
            dilemmaLabel.color = new Color(1f, 0.9f, 0.45f);
            dilemmaRow.SetActive(false);
        }

        static TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 anchor, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1000f, 160f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            GameFonts.Apply(tmp);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = size;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.outlineWidth = 0.2f;
            tmp.outlineColor = Color.black;
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
            rt.sizeDelta = new Vector2(420f, 56f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.18f, 0.28f, 0.95f);
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
            tmp.fontSize = 24f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.color = Color.white;
            tmp.text = label;
            tmp.raycastTarget = false;
        }
    }
}
