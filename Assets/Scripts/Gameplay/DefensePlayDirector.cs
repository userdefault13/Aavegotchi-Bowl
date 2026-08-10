using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// When the human is on defense (!isPlayerPossession), pick an AI offensive
    /// play and run a three-hut cadence so the player can cycle defenders before
    /// the auto-snap. After snap, AI throws / hands off while the human controls
    /// one defender.
    /// </summary>
    public class DefensePlayDirector : MonoBehaviour
    {
        public static DefensePlayDirector Instance { get; private set; }

        [Header("Timing")]
        public float playCallDelay = 1.15f;
        public float aiPassHoldMin = 0.85f;
        public float aiPassHoldMax = 1.65f;

        float callTimer;
        float guessWaitTimer;
        bool playArmed;
        bool aiPassPending;
        float aiThrowAt;
        QuarterbackController aiQb;

        [Header("Defense guess")]
        [Tooltip("If the player never locks a guess, AI picks one so the down is not soft-locked.")]
        public float guessFailsafeSeconds = 18f;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (GameManager.Instance == null
                || GameManager.Instance.currentState != GameState.Playing)
                return;

            if (GameManager.Instance.waitingForNextPlay
                || GameManager.Instance.isKicking
                || GameManager.Instance.isKickoffReturn
                || GameManager.Instance.isInterceptionReturn)
            {
                ResetDirector();
                return;
            }

            // Player has the ball — PlayCallingUI owns the down. Never auto-pick for the human.
            if (!GameRules.EnableAiOffenseWhenDefending) return;
            if (FieldManager.Instance == null || FieldManager.Instance.isPlayerPossession)
            {
                if (playArmed || aiPassPending)
                {
                    ResetDirector();
                    SetAiOffensePossession(false);
                }
                return;
            }

            // Live AI throw after snap.
            if (aiPassPending)
            {
                TickAiPass();
                return;
            }

            if (!GameManager.Instance.isPreSnap) return;
            if (SnapCadence.Instance != null && SnapCadence.Instance.IsActive) return;
            if (playArmed) return;

            // Wait for Tecmo-style defense guess (same 8 plays) before AI arms the call.
            if (!Playbook.HasDefenseGuess)
            {
                guessWaitTimer += Time.deltaTime;
                if (guessWaitTimer >= guessFailsafeSeconds)
                    Playbook.PickAiDefenseGuess();
                return;
            }

            callTimer += Time.deltaTime;
            if (callTimer < playCallDelay) return;

            ArmDefensePlay();
        }

        void ArmDefensePlay()
        {
            if (GameManager.Instance != null && !GameManager.Instance.CanStartPlay())
                return;

            playArmed = true;
            callTimer = 0f;

            var play = ChooseAiPlay();
            if (play == null)
            {
                // 4th-down special already handled inside ChooseAiPlay via FG/punt.
                playArmed = false;
                return;
            }

            Playbook.Select(play);
            PassCollisionGate.ResetForPlay();

            if (GameRules.EnablePlayerDefense && PlayerDefenseController.Instance != null)
                PlayerDefenseController.Instance.BeginPreSnapSelection();
            else if (PlayerDefenseController.Instance != null)
                PlayerDefenseController.Instance.Cancel();

            // Disable player QB control — AI has the ball.
            SetAiOffensePossession(true);

            var cadence = SnapCadence.Instance;
            if (cadence == null && GameManager.Instance != null)
                cadence = GameManager.Instance.gameObject.AddComponent<SnapCadence>();

            // Original Retro Bowl: no defender cycling — short/auto snap for AI offense.
            int huts = GameRules.EnablePlayerDefense ? 3 : 1;
            if (cadence != null)
                cadence.BeginDefenseAutoSnap(hutCount: huts);

            var preview = PlayRoutePreview.Instance;
            if (preview != null)
                preview.PrepareSelectedPlayRoutes();

            Debug.Log(GameRules.EnablePlayerDefense
                ? $"Defense: AI called {play.DisplayName} — three huts, cycle defenders"
                : $"AI offense: {play.DisplayName}");
        }

        OffensivePlay ChooseAiPlay()
        {
            if (FieldManager.Instance == null) return Playbook.PassPlays[0];

            int down = FieldManager.Instance.down;
            int ytg = FieldManager.Instance.yardsToGo;
            int toEz = FieldManager.Instance.GetYardsToEndzone();

            if (down == 4)
            {
                if (toEz < 35 && ytg > 4)
                {
                    ExecuteSpecialTeams(isFieldGoal: true);
                    return null;
                }

                if (ytg >= 3 || toEz > 40)
                {
                    ExecuteSpecialTeams(isFieldGoal: false);
                    return null;
                }
            }

            // Pick from the same 8 modal plays the defense can guess.
            bool preferPass = ytg > 5 || Random.value > 0.42f;
            return Playbook.PickAiOffenseFromModal(preferPass);
        }

        void ExecuteSpecialTeams(bool isFieldGoal)
        {
            playArmed = false;
            if (GameManager.Instance != null)
                GameManager.Instance.SnapBall();

            if (isFieldGoal)
            {
                FieldManager.Instance.FieldGoalAttempt();
                PlayBanner.ShowPlayOver("FIELD GOAL");
            }
            else
            {
                FieldManager.Instance.Punt();
                PlayBanner.ShowPlayOver("PUNT");
            }
        }

        /// <summary>Called from SnapCadence after the auto three-hut snap.</summary>
        public void OnDefenseSnap(bool isRun)
        {
            playArmed = false;
            PassCollisionGate.ResetForPlay();

            if (PlayerDefenseController.Instance != null)
                PlayerDefenseController.Instance.LockControlOnSnap();

            SetAiOffensePossession(true);

            if (isRun)
            {
                // Handoff already started by SnapCadence — AI RB runs.
                aiPassPending = false;
                return;
            }

            aiQb = FindQb();
            if (aiQb == null)
            {
                aiPassPending = false;
                return;
            }

            aiQb.SetAiControlled(true);
            aiPassPending = true;
            aiThrowAt = Time.time + Random.Range(aiPassHoldMin, aiPassHoldMax);
        }

        void TickAiPass()
        {
            if (aiQb == null)
            {
                aiPassPending = false;
                return;
            }

            if (aiQb.hasThrown || aiQb.isScrambling)
            {
                aiPassPending = false;
                return;
            }

            // Sack / scramble may interrupt.
            if (GameManager.Instance != null && GameManager.Instance.waitingForNextPlay)
            {
                aiPassPending = false;
                return;
            }

            if (Time.time < aiThrowAt) return;

            Transform target = PickAiThrowTarget();
            Vector3 aim = target != null
                ? target.position + Vector3.right * (1.2f * (FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f))
                : aiQb.transform.position + Vector3.right * (10f * (FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f));
            aim.z = 0f;
            aiQb.AiThrowTo(aim);
            aiPassPending = false;
        }

        static Transform PickAiThrowTarget()
        {
            Transform best = null;
            float bestScore = float.MinValue;

            foreach (var name in FormationRoster.SkillOffense)
            {
                if (name == "RB") continue; // prefer WRs / TE on AI passes
                var go = GameObject.Find(name);
                if (go == null || !go.activeInHierarchy) continue;
                var rc = go.GetComponent<ReceiverController>();
                if (rc == null || rc.hasBall) continue;

                // Prefer deeper + slightly open (farther from nearest CB).
                float depth = go.transform.position.x;
                float sep = NearestDefenderSeparation(go.transform);
                float score = depth + sep * 0.65f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = go.transform;
                }
            }

            return best;
        }

        static float NearestDefenderSeparation(Transform receiver)
        {
            float min = 8f;
            GameObject[] defs;
            try { defs = GameObject.FindGameObjectsWithTag("Defender"); }
            catch { return min; }

            Vector2 rp = new Vector2(receiver.position.x, receiver.position.y);
            foreach (var d in defs)
            {
                if (d == null || !d.activeInHierarchy) continue;
                float dist = Vector2.Distance(rp, new Vector2(d.transform.position.x, d.transform.position.y));
                if (dist < min) min = dist;
            }
            return min;
        }

        static QuarterbackController FindQb()
        {
            var qb = GameObject.Find("Quarterback");
            return qb != null ? qb.GetComponent<QuarterbackController>() : null;
        }

        public static void SetAiOffensePossession(bool aiHasBall)
        {
            var qb = GameObject.Find("Quarterback");
            if (qb == null) return;

            var qbc = qb.GetComponent<QuarterbackController>();
            var pc = qb.GetComponent<PlayerController>();

            if (qbc != null)
                qbc.SetAiControlled(aiHasBall);

            if (pc != null)
            {
                // Human never steers the QB while defending.
                pc.SetControlled(!aiHasBall && PlayerHasOffense());
                if (aiHasBall)
                    pc.hasBall = true;
            }
        }

        static bool PlayerHasOffense()
            => FieldManager.Instance == null || FieldManager.Instance.isPlayerPossession;

        void ResetDirector()
        {
            playArmed = false;
            aiPassPending = false;
            callTimer = 0f;
            guessWaitTimer = 0f;
            aiQb = null;
        }

        /// <summary>Drop any armed AI throw / cadence bookkeeping (quarter break, etc.).</summary>
        public void ResetDirectorState() => ResetDirector();

        /// <summary>Call when resetting for the next play from roster / banner.</summary>
        public void PrepareForNextPlay()
        {
            ResetDirector();
            callTimer = 0f;

            if (PlayerIsDefending())
            {
                SetAiOffensePossession(true);
                if (GameRules.EnablePlayerDefense && PlayerDefenseController.Instance != null)
                    PlayerDefenseController.Instance.BeginPreSnapSelection();
                else if (PlayerDefenseController.Instance != null)
                    PlayerDefenseController.Instance.Cancel();

                // Do not ArmDefensePlay here — Update waits for Playbook.DefenseGuess
                // (PlayCallingUI defense-guess modal), then arms after playCallDelay.
            }
            else
            {
                SetAiOffensePossession(false);
                if (PlayerDefenseController.Instance != null)
                    PlayerDefenseController.Instance.Cancel();
            }
        }

        public static bool PlayerIsDefending()
            => FieldManager.Instance != null && !FieldManager.Instance.isPlayerPossession;
    }
}
