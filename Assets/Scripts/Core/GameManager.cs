using UnityEngine;
using UnityEngine.SceneManagement;
using RetroBowl.App;
using RetroBowl.Gameplay;
using RetroBowl.Managers;
using RetroBowl.UI;

namespace RetroBowl.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Game State")]
        public GameState currentState = GameState.Menu;
        public int currentQuarter = 1;
        public float quarterTimeRemaining = 120f;
        public const float QUARTER_DURATION = 120f;
        /// <summary>True until a play is called — formation stays locked.</summary>
        public bool isPreSnap = true;
        /// <summary>Play result banner up — freeze until click for next play.</summary>
        public bool waitingForNextPlay;
        /// <summary>Tackle break QTE in progress — freeze pursuit briefly.</summary>
        public bool inTackleBattle;
        /// <summary>Clock hit 0 during a live play — end quarter only after the play finishes.</summary>
        public bool pendingQuarterEnd;
        /// <summary>FG / punt kick mini-game active.</summary>
        public bool isKicking;
        /// <summary>Live kickoff return — returner is the ball-carrier; no play-calling.</summary>
        public bool isKickoffReturn;
        /// <summary>Live interception return — interceptor running it back (pick-six possible).</summary>
        public bool isInterceptionReturn;
        /// <summary>Kickoff or INT return — ball is live, no play-calling.</summary>
        public bool IsLiveReturn => isKickoffReturn || isInterceptionReturn;
        /// <summary>Sudden-death overtime after a regulation tie.</summary>
        public bool isOvertime;
        /// <summary>Sandbox — no clock, no season record.</summary>
        public bool isPracticeMode;
        /// <summary>Absolute LOS yard when the current play snapped (0–100 field).</summary>
        public int playLosYard = 20;

        /// <summary>True while a snapped play is still resolving (not pre-snap / not banner).</summary>
        public bool IsPlayInProgress => !isPreSnap && !waitingForNextPlay;

        [Header("Score")]
        public int playerScore = 0;
        public int opponentScore = 0;

        [Header("Game Settings")]
        public int quartersPerGame = 4;
        public bool isPlayoffs = false;

        private bool isPaused = false;
        /// <summary>Re-entrancy guard — EndQuarter must not run twice in one unwind.</summary>
        bool endingQuarter;
        /// <summary>Ignore snap / next-play mouse clicks for this many frames after continue.</summary>
        int suppressPlayClickFrames;
        /// <summary>Skip one PostScoreFlow intercept (e.g. lining up for 2PT).</summary>
        bool skipPostScoreFlowOnce;

        /// <summary>True while the continue-button click should not also snap / advance plays.</summary>
        public bool SuppressPlayClick
            => suppressPlayClickFrames > 0;

        /// <summary>Ignore snap / kick / next-play clicks for a few frames (UI handoff).</summary>
        public void SuppressNextPlayClicks(int frames = 3)
        {
            suppressPlayClickFrames = Mathf.Max(suppressPlayClickFrames, Mathf.Max(0, frames));
        }

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // Upgrade old 3D capsule scene into Retro Bowl look on Play.
            if (GetComponent<RetroLookApplier>() == null)
                gameObject.AddComponent<RetroLookApplier>();
            if (GetComponent<SnapCadence>() == null)
                gameObject.AddComponent<SnapCadence>();
            if (GetComponent<PlayBanner>() == null)
                gameObject.AddComponent<PlayBanner>();
            if (GetComponent<PlayRoutePreview>() == null)
                gameObject.AddComponent<PlayRoutePreview>();
            if (GetComponent<TackleBattle>() == null)
                gameObject.AddComponent<TackleBattle>();
            if (GetComponent<ContactBattle>() == null)
                gameObject.AddComponent<ContactBattle>();
            if (GetComponent<TecmoContact>() == null)
                gameObject.AddComponent<TecmoContact>();
            // Legacy shim — harmless if present on old scenes.
            if (GetComponent<LineBattle>() == null)
                gameObject.AddComponent<LineBattle>();
            if (GetComponent<StaminaSprintHud>() == null)
                gameObject.AddComponent<StaminaSprintHud>();
            if (GetComponent<PassCollisionGate>() == null)
                gameObject.AddComponent<PassCollisionGate>();
            if (GetComponent<PlayerDefenseController>() == null)
                gameObject.AddComponent<PlayerDefenseController>();
            if (GetComponent<DefensePlayDirector>() == null)
                gameObject.AddComponent<DefensePlayDirector>();
            if (GetComponent<KickingController>() == null)
                gameObject.AddComponent<KickingController>();
            if (GetComponent<PostScoreFlow>() == null)
                gameObject.AddComponent<PostScoreFlow>();
            if (GetComponent<AudioManager>() == null)
                gameObject.AddComponent<AudioManager>();
            if (GetComponent<ScoreManager>() == null)
                gameObject.AddComponent<ScoreManager>();
            if (GetComponent<WeatherSystem>() == null)
                gameObject.AddComponent<WeatherSystem>();
            if (GetComponent<SeasonManager>() == null)
                gameObject.AddComponent<SeasonManager>();
            if (GetComponent<TeamManager>() == null)
                gameObject.AddComponent<TeamManager>();
            if (GetComponent<SceneFlow>() == null)
                gameObject.AddComponent<SceneFlow>();
            if (GetComponent<SaveService>() == null)
                gameObject.AddComponent<SaveService>();
            if (GetComponent<CareerNav>() == null)
                gameObject.AddComponent<CareerNav>();
        }

        void Start()
        {
            // Hybrid flow: BootLoader / SceneFlow owns Menu → Career transitions.
            if (SceneFlow.Instance != null && SceneFlow.Instance.OwnsLifecycle)
                return;
            SetGameState(GameState.Menu);
        }

        void Update()
        {
            if (suppressPlayClickFrames > 0)
                suppressPlayClickFrames--;

            if (currentState == GameState.Playing)
            {
                UpdateGameTime();
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                // Career UI owns ESC for BACK / dismiss modal.
                if (CareerNav.Instance != null && CareerNav.Instance.IsUiOpen)
                    return;
                TogglePause();
            }
        }

        void UpdateGameTime()
        {
            if (isPaused || currentState != GameState.Playing)
                return;

            // Practice: clock frozen for drills.
            if (isPracticeMode)
                return;

            // Deferred end — re-check once the TD/banner/play resolution clears.
            if (pendingQuarterEnd)
            {
                if (!ShouldDeferQuarterEnd())
                    EndQuarter();
                return;
            }

            if (quarterTimeRemaining <= 0f)
            {
                HandleQuarterExpired();
                return;
            }

            quarterTimeRemaining -= Time.deltaTime;

            if (quarterTimeRemaining <= 0f)
            {
                quarterTimeRemaining = 0f;
                HandleQuarterExpired();
            }
        }

        void HandleQuarterExpired()
        {
            quarterTimeRemaining = 0f;

            // Live play, tackle QTE, or result banner still up:
            // defer until the play is fully consumed (banner Continue → NotifyPlayCompleted).
            if (ShouldDeferQuarterEnd())
            {
                pendingQuarterEnd = true;
                return;
            }

            EndQuarter();
        }

        /// <summary>
        /// True while ending the quarter mid-resolution would race ReadyNextPlay /
        /// InitializeDrive / defense AI and leave destroyed ball / tackle state dangling.
        /// </summary>
        bool ShouldDeferQuarterEnd()
        {
            // Live play, tackle QTE, or result/TD banner still up.
            // JustScoredTouchdown alone must NOT defer forever after ReadyNextPlay —
            // that stuck the clock at 0 until a play-call click side-effected EndQuarter.
            return IsPlayInProgress || waitingForNextPlay || inTackleBattle || isKicking
                   || isKickoffReturn || isInterceptionReturn
                   || (FieldManager.Instance != null && FieldManager.Instance.PendingTryAfterTd);
        }

        /// <summary>
        /// Call when a play fully ends (tackle / incomplete / OOB / TD / etc.).
        /// Returns true if the quarter (or game) ended because the clock had expired.
        /// </summary>
        public bool NotifyPlayCompleted()
        {
            if (endingQuarter
                || currentState == GameState.QuarterBreak
                || currentState == GameState.GameOver)
                return currentState == GameState.QuarterBreak || currentState == GameState.GameOver;

            if (!pendingQuarterEnd && quarterTimeRemaining > 0f)
                return false;

            EndQuarter();
            return true;
        }

        /// <summary>
        /// False if the quarter must end before a new play can start.
        /// Pure query — never ends the quarter as a side effect of play-call clicks.
        /// </summary>
        public bool CanStartPlay()
        {
            if (endingQuarter
                || currentState == GameState.QuarterBreak
                || currentState == GameState.GameOver)
                return false;

            if (pendingQuarterEnd || quarterTimeRemaining <= 0f)
                return false;

            if (isKicking || isKickoffReturn || isInterceptionReturn || waitingForNextPlay)
                return false;

            return true;
        }

        void EndQuarter()
        {
            if (endingQuarter) return;
            if (currentState == GameState.QuarterBreak || currentState == GameState.GameOver)
                return;

            endingQuarter = true;
            try
            {
                pendingQuarterEnd = false;
                waitingForNextPlay = false;
                inTackleBattle = false;
                isKicking = false;
                isKickoffReturn = false;
                isInterceptionReturn = false;
                isPreSnap = true;
                Playbook.Clear();

                HaltLivePlaySystems();

                var playUi = Object.FindAnyObjectByType<PlayCallingUI>();
                if (playUi != null)
                    playUi.ForceResetSelection();

                currentQuarter++;

                if (currentQuarter > quartersPerGame && !isOvertime)
                {
                    if (playerScore == opponentScore)
                    {
                        // Sudden-death OT — next score wins.
                        isOvertime = true;
                        currentQuarter = quartersPerGame + 1;
                        quarterTimeRemaining = QUARTER_DURATION;
                        SetGameState(GameState.QuarterBreak);
                        Debug.Log("OVERTIME — next score wins");
                    }
                    else
                    {
                        EndGame();
                    }
                }
                else if (currentQuarter > quartersPerGame && isOvertime)
                {
                    // OT period expired still tied.
                    EndGame();
                }
                else
                {
                    quarterTimeRemaining = QUARTER_DURATION;
                    SetGameState(GameState.QuarterBreak);
                }
            }
            finally
            {
                endingQuarter = false;
            }
        }

        /// <summary>Cancel cadence / battles / directors so ContinueFromQuarterBreak starts clean.</summary>
        void HaltLivePlaySystems()
        {
            if (SnapCadence.Instance != null)
                SnapCadence.Instance.Cancel();
            PlayBanner.ForceHide();

            if (TackleBattle.Instance != null)
                TackleBattle.Instance.CancelForScore();
            if (ContactBattle.Instance != null)
                ContactBattle.Instance.ForceCancel();
            if (TecmoContact.Instance != null)
                TecmoContact.Instance.ForceCancel();
            if (LineBattle.Instance != null)
                LineBattle.Instance.ForceCancel();
            if (KickingController.Instance != null)
                KickingController.Instance.Cancel();
            if (PostScoreFlow.Instance != null)
                PostScoreFlow.Instance.Cancel();
            if (CoinTossFlow.Instance != null)
                CoinTossFlow.Instance.Cancel();

            isKicking = false;
            isKickoffReturn = false;
            isInterceptionReturn = false;

            if (DefensePlayDirector.Instance != null)
                DefensePlayDirector.Instance.ResetDirectorState();
            if (PlayerDefenseController.Instance != null)
                PlayerDefenseController.Instance.Cancel();
        }

        void EndGame()
        {
            SetGameState(GameState.GameOver);
            HaltLivePlaySystems();

            if (playerScore > opponentScore)
                Debug.Log(isOvertime ? "Player Wins in OT!" : "Player Wins!");
            else if (opponentScore > playerScore)
                Debug.Log(isOvertime ? "Opponent Wins in OT!" : "Opponent Wins!");
            else
                Debug.Log("It's a Tie!");

            if (!isPracticeMode && SeasonManager.Instance != null)
                SeasonManager.Instance.CompleteGame(playerScore, opponentScore);

            bool practice = isPracticeMode;
            isPracticeMode = false;
            SaveService.Instance?.SaveCareer();

            if (SceneFlow.Instance != null)
            {
                SceneFlow.Instance.RecordResult(playerScore, opponentScore, practice);
                SceneFlow.Instance.GoToCareer(practice ? CareerScreen.Home : CareerScreen.PostMatch);
            }
            else
            {
                CareerHub.Instance?.Hide();
                CareerNav.EnsureExists();
                CareerNav.Instance?.Show(practice ? CareerScreen.Home : CareerScreen.PostMatch);
            }
        }

        /// <summary>Call after any OT score is fully applied (FG / XP / 2PT / safety).</summary>
        public void CheckSuddenDeathWin()
        {
            if (!isOvertime) return;
            if (currentState == GameState.GameOver) return;
            if (playerScore == opponentScore) return;

            // Finish kickoff flow first if still pending — otherwise end now.
            if (FieldManager.Instance != null && FieldManager.Instance.PendingKickoff)
                return;

            EndGame();
        }

        public void SetGameState(GameState newState)
        {
            currentState = newState;
            Debug.Log($"Game State: {newState}");

            switch (newState)
            {
                case GameState.Menu:
                    Time.timeScale = 1f;
                    break;
                case GameState.Playing:
                    Time.timeScale = 1f;
                    isPaused = false;
                    break;
                case GameState.Paused:
                    Time.timeScale = 0f;
                    isPaused = true;
                    break;
                case GameState.QuarterBreak:
                    Time.timeScale = 0f;
                    break;
                case GameState.GameOver:
                    Time.timeScale = 0f;
                    break;
            }
        }

        public void StartNewGame()
        {
            playerScore = 0;
            opponentScore = 0;
            currentQuarter = 1;
            quarterTimeRemaining = QUARTER_DURATION;
            isPreSnap = true;
            waitingForNextPlay = false;
            inTackleBattle = false;
            pendingQuarterEnd = false;
            isKicking = false;
            isKickoffReturn = false;
            isInterceptionReturn = false;
            isOvertime = false;
            endingQuarter = false;
            skipPostScoreFlowOnce = false;
            suppressPlayClickFrames = 0;
            // isPracticeMode set by caller (SceneFlow / PracticeMode)
            Playbook.Clear();
            CareerHub.Instance?.Hide();
            CareerNav.Instance?.HideAll();

            // Drop leftover cadence / AI offense / kick UI from a prior Match on DDOL AppRoot.
            HaltLivePlaySystems();

            if (ScoreManager.Instance != null)
                ScoreManager.Instance.ResetStats();

            // Week games open with coin toss → kickoff (SceneFlow → BeginOpeningKickoff).
            // Spot a neutral receive tee so DefensePlayDirector does not arm AI offense
            // during the toss (isPlayerPossession = false until ResolveKickoff).
            if (FieldManager.Instance != null && !isPracticeMode)
                FieldManager.Instance.PrepareOpeningReceive();

            var playUi = Object.FindAnyObjectByType<PlayCallingUI>();
            if (playUi != null)
                playUi.ForceResetSelection();

            WeatherSystem.EnsureExists();
            if (WeatherSystem.Instance != null && !isPracticeMode)
            {
                if (WeatherSystem.Instance.kind == WeatherKind.Clear
                    && Mathf.Abs(WeatherSystem.Instance.windY) < 0.01f)
                    WeatherSystem.Instance.RollForMatch();
            }

            // Bind career franchise + week opponent onto the match HUD (DDOL TeamManager).
            var hud = Object.FindAnyObjectByType<GameHUD>();
            if (hud != null && TeamManager.Instance != null)
            {
                hud.SetTeamNames(
                    TeamManager.Instance.PlayerTeamAbbrev(),
                    TeamManager.Instance.OpponentTeamAbbrev());
            }

            SetGameState(GameState.Playing);
        }

        public void SnapBall()
        {
            if (!CanStartPlay())
                return;

            isPreSnap = false;
            if (FieldManager.Instance != null)
            {
                FieldManager.Instance.ClearPlayResultFlags();
                playLosYard = FieldManager.Instance.currentYardLine;
            }
        }

        public float GetPlayLosWorldX()
            => playLosYard - 50f;

        /// <summary>Next ReadyNextPlay will set formations without running PAT/kickoff flow.</summary>
        public void SkipPostScoreFlowOnce() => skipPostScoreFlowOnce = true;

        public void ReadyNextPlay()
        {
            // Post-TD try / kickoff takes priority over a normal next-play reset.
            if (skipPostScoreFlowOnce)
            {
                skipPostScoreFlowOnce = false;
            }
            else if (PostScoreFlow.TryHandleAfterPlayBanner())
            {
                return;
            }

            isPreSnap = true;
            inTackleBattle = false;
            isKicking = false;
            isKickoffReturn = false;
            isInterceptionReturn = false;

            if (TackleBattle.Instance != null)
                TackleBattle.Instance.CancelForScore();
            if (ContactBattle.Instance != null)
                ContactBattle.Instance.ForceCancel();
            if (TecmoContact.Instance != null)
                TecmoContact.Instance.ForceCancel();
            if (LineBattle.Instance != null)
                LineBattle.Instance.ForceCancel();
            // Always dismiss kick meter / aim before lining up a normal down.
            KickingController.EnsureExists();
            KickingController.Instance?.Cancel();

            if (FieldManager.Instance != null)
            {
                // TD / first-down flags belong to the prior play — clear so a
                // zeroed clock can EndQuarter without needing another play call.
                FieldManager.Instance.ClearPlayResultFlags();
                playLosYard = FieldManager.Instance.currentYardLine;
                try
                {
                    FormationRoster.PlaceOnly(FormationRoster.CurrentLosYard());
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"FormationRoster.PlaceOnly failed: {ex}");
                    FormationRoster.EnsureAndPlace(FormationRoster.CurrentLosYard());
                }
            }

            // Hide player play-call UI before arming AI offense (after kickoff / TOD).
            bool playerDefending = FieldManager.Instance != null
                                   && !FieldManager.Instance.isPlayerPossession;
            if (playerDefending)
            {
                var playUi = Object.FindAnyObjectByType<PlayCallingUI>();
                if (playUi != null)
                    playUi.ForceResetSelection();
            }

            if (GetComponent<DefensePlayDirector>() == null)
                gameObject.AddComponent<DefensePlayDirector>();

            if (DefensePlayDirector.Instance != null)
                DefensePlayDirector.Instance.PrepareForNextPlay();
            else if (PlayerDefenseController.PlayerIsOnDefense()
                     && PlayerDefenseController.Instance != null)
            {
                PlayerDefenseController.Instance.BeginPreSnapSelection();
            }
            else if (PlayerDefenseController.Instance != null)
            {
                PlayerDefenseController.Instance.Cancel();
            }

            PassCollisionGate.ResetForPlay();

            // OT win after kickoff spot is applied.
            CheckSuddenDeathWin();
        }

        public void AddScore(bool isPlayer, int points)
        {
            if (isPlayer)
            {
                playerScore += points;
            }
            else
            {
                opponentScore += points;
            }

            Debug.Log($"Score - Player: {playerScore} | Opponent: {opponentScore}");
        }

        public void TogglePause()
        {
            if (currentState == GameState.Playing)
            {
                SetGameState(GameState.Paused);
            }
            else if (currentState == GameState.Paused)
            {
                SetGameState(GameState.Playing);
            }
        }

        public void ContinueFromQuarterBreak()
        {
            // Invoked from the Continue button — always recover even if state desynced.
            // Same mouse click must not also snap / advance plays.
            suppressPlayClickFrames = 3;

            pendingQuarterEnd = false;
            waitingForNextPlay = false;
            inTackleBattle = false;
            isKicking = false;
            isKickoffReturn = false;
            isInterceptionReturn = false;
            isPreSnap = true;
            endingQuarter = false;
            Playbook.Clear();

            try
            {
                HaltLivePlaySystems();

                if (FieldManager.Instance != null)
                    FieldManager.Instance.ClearPlayResultFlags();

                var playUi = Object.FindAnyObjectByType<PlayCallingUI>();
                if (playUi != null)
                    playUi.ForceResetSelection();

                // Halftime (Q2→Q3): coin-toss deferred receive / kick.
                if (currentQuarter == 3
                    && FieldManager.Instance != null
                    && FieldManager.Instance.HasSecondHalfKickoffPending
                    && CoinTossFlow.TryBeginSecondHalfKickoff())
                {
                    // Kickoff started — skip normal next-play lineup.
                }
                else
                {
                    // EndQuarter may have interrupted a TD / play-over banner before
                    // ContinueToNextPlay ran — formations / football / AI still mid-play.
                    ReadyNextPlay();
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"ContinueFromQuarterBreak failed during reset: {ex}");
            }
            finally
            {
                // Always leave QuarterBreak even if a formation/AI reset threw —
                // otherwise the overlay sticks and a second Continue can double-init.
                if (currentState != GameState.GameOver)
                    SetGameState(GameState.Playing);
            }
        }

        public void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            StartNewGame();
        }

        public void QuitToMenu()
        {
            isPreSnap = true;
            isPracticeMode = false;
            isKicking = false;
            isKickoffReturn = false;
            isInterceptionReturn = false;
            Playbook.Clear();
            HaltLivePlaySystems();
            SaveService.Instance?.SaveCareer();

            if (SceneFlow.Instance != null)
            {
                SceneFlow.Instance.GoToCareer(CareerScreen.Home);
                return;
            }

            SetGameState(GameState.Menu);
            CareerHub.Instance?.Hide();
            CareerNav.EnsureExists();
            CareerNav.Instance?.Show(CareerScreen.Home);
        }

        public string GetFormattedTime()
        {
            if (isPracticeMode)
                return "PRACTICE";

            float t = Mathf.Max(0f, quarterTimeRemaining);
            int minutes = Mathf.FloorToInt(t / 60);
            int seconds = Mathf.FloorToInt(t % 60);
            // Retro Bowl style: "2:00" (no leading zero on minutes).
            return $"{minutes}:{seconds:00}";
        }
    }

    public enum GameState
    {
        Menu,
        Playing,
        Paused,
        QuarterBreak,
        GameOver
    }
}
