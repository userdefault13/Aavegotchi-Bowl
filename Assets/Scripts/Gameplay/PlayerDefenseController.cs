using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Human defender / kickoff-gunner control (Tecmo Super Bowl style).
    ///
    /// Post-snap: WASD · J / click tap-run (→ battle mash in contact) · Z dive · K battle.
    /// Pre-snap: J = prev · K / click / tap = next (Q/E alts).
    /// </summary>
    public class PlayerDefenseController : MonoBehaviour
    {
        public static PlayerDefenseController Instance { get; private set; }

        [Header("Movement")]
        public float moveSpeed = 2.9f;
        public float sprintSpeed = 4.0f;
        public float acceleration = 12f;

        [Header("Dive")]
        public KeyCode diveKey = KeyCode.F;
        public float diveSpeed = 10.5f;
        public float diveCommitSeconds = 0.32f;
        public float diveMissSlideSeconds = 0.42f;
        public float diveMissStunSeconds = 1.15f;
        public float diveContactRadius = 1.05f;
        /// <summary>Extra feet of slide after a miss (~1 yard ≈ 1 world unit).</summary>
        public float diveMissSlideYards = 2.6f;
        public float diveMaxTotalSeconds = 0.85f;
        public float diveMashExtendPerTap = 0.12f;
        public float diveMashExtendHeldPerSecond = 0.28f;

        [Header("Jump INT")]
        public float jumpIntRadius = 2.4f;
        public float jumpIntCooldown = 0.55f;
        float nextJumpAt;

        [Header("Tackle")]
        public float wrapRadius = 0.9f;
        [Tooltip("Slightly larger wrap window while covering a kickoff return.")]
        public float kickoffWrapRadius = 1.15f;
        public float wrapCooldown = 0.35f;
        float nextWrapAt;

        /// <summary>Full defense cycle roster (Tecmo: any teammate).</summary>
        static readonly string[] CycleRoster =
        {
            "CB_Top", "CB_Bot", "S", "LB_1", "LB_2", "DL_1", "DL_2", "DL_3", "DL_4"
        };

        /// <summary>Cover gunners when the player kicks off (offense-tagged units).</summary>
        static readonly string[] KickoffGunnerRoster =
        {
            "WR_Top", "WR_Bot", "RB", "TE", "Quarterback"
        };

        public bool IsActive { get; private set; }
        public bool IsLocked { get; private set; }
        /// <summary>True while controlling a gunner on a player kickoff.</summary>
        public bool IsKickoffCoverage { get; private set; }
        /// <summary>DefenderAI when on normal defense; may be null for kickoff gunners.</summary>
        public DefenderAI Controlled { get; private set; }
        /// <summary>Unit currently steered (defender or kickoff gunner).</summary>
        public Transform ControlledUnit { get; private set; }

        /// <summary>True while this controller's unit is in a dive or miss-slide.</summary>
        public bool IsDiving => diving || diveMissSliding;

        public bool IsDivingUnit(Transform t)
            => IsDiving && ControlledUnit != null && t == ControlledUnit;

        int selectIndex;
        Vector3 velocity;
        Vector3 faceDir = Vector3.left;
        bool diving;
        bool diveMissSliding;
        float divePhaseEndsAt;
        float diveStartedAt;
        Vector3 diveDir;
        bool diveHitResolved;
        Rigidbody rb;
        SpriteRenderer spriteRenderer;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>True when the human should play defense this down.</summary>
        public static bool PlayerIsOnDefense()
        {
            if (!GameRules.EnablePlayerDefense) return false;
            return FieldManager.Instance != null && !FieldManager.Instance.isPlayerPossession;
        }

        public void BeginPreSnapSelection()
        {
            if (!GameRules.EnablePlayerDefense)
            {
                Cancel();
                return;
            }

            IsKickoffCoverage = false;
            IsActive = true;
            IsLocked = false;
            ClearDiveGhost();
            diveHitResolved = false;
            velocity = Vector3.zero;
            faceDir = Vector3.left;

            // Keep prior Q/E pick when re-armed (guess → cadence); only default once.
            if (ControlledUnit == null
                || System.Array.IndexOf(CycleRoster, ControlledUnit.name) < 0)
            {
                selectIndex = 0;
                ApplySelection(CycleRoster[selectIndex], followCamera: true);
            }
            else
            {
                selectIndex = System.Array.IndexOf(CycleRoster, ControlledUnit.name);
                ApplySelection(CycleRoster[selectIndex], followCamera: true);
            }
            PlayBanner.Show("CLICK / K NEXT  ·  J PREV  ·  WASD · Z DIVE", 1.7f, BannerTone.Neutral);
            Debug.Log("Defense: click/tap cycles next · pre-snap J/K · post-snap click=run/battle");
        }

        /// <summary>
        /// Player kicked — steer a cover gunner and tackle the AI returner.
        /// Active through approach, flight, and live return (ignores EnablePlayerDefense).
        /// </summary>
        public void BeginKickoffCoverage()
        {
            IsKickoffCoverage = true;
            IsActive = true;
            IsLocked = true;
            ClearDiveGhost();
            diveHitResolved = false;
            velocity = Vector3.zero;
            // Chase toward the return (opposite kick flight).
            float ret = FieldManager.Instance != null ? FieldManager.Instance.ReturnDirX : -1f;
            faceDir = Vector3.right * ret;

            selectIndex = 0;
            if (!ApplyKickoffGunner(KickoffGunnerRoster[selectIndex], followCamera: true))
            {
                // Fall through roster if WR_Top missing.
                for (int i = 1; i < KickoffGunnerRoster.Length; i++)
                {
                    if (ApplyKickoffGunner(KickoffGunnerRoster[i], followCamera: true))
                    {
                        selectIndex = i;
                        break;
                    }
                }
            }

            PlayBanner.Show("COVER!  CLICK / Q/E CYCLE  ·  Z DIVE  ·  J RUN", 1.2f, BannerTone.Neutral);
            Debug.Log("Kickoff cover: WASD · J/K cycle · Z dive · J run→battle");
        }

        public void Cancel()
        {
            ClearDiveGhost();
            ClearControlFlags();
            IsActive = false;
            IsLocked = false;
            IsKickoffCoverage = false;
            Controlled = null;
            ControlledUnit = null;
            rb = null;
        }

        public void LockControlOnSnap()
        {
            if (IsKickoffCoverage) return;

            if (!GameRules.EnablePlayerDefense)
            {
                Cancel();
                return;
            }

            if (!IsActive) BeginPreSnapSelection();
            IsLocked = true;
            if (Controlled != null)
                Controlled.IsPlayerControlled = true;
            FollowCameraAndRing(ControlledUnit);
        }

        void ClearControlFlags()
        {
            foreach (var name in CycleRoster)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                var ai = go.GetComponent<DefenderAI>();
                if (ai != null) ai.IsPlayerControlled = false;
            }

            foreach (var name in KickoffGunnerRoster)
            {
                var go = GameObject.Find(name);
                if (go == null) continue;
                var rc = go.GetComponent<ReceiverController>();
                if (rc != null) rc.SetKickoffGunnerControl(false);
                var ai = go.GetComponent<DefenderAI>();
                if (ai != null) ai.IsPlayerControlled = false;
                var pc = go.GetComponent<PlayerController>();
                if (pc != null) pc.SetControlled(false);
            }
        }

        void Update()
        {
            if (!IsActive) return;
            if (GameManager.Instance == null
                || GameManager.Instance.currentState != GameState.Playing
                || GameManager.Instance.waitingForNextPlay
                || GameManager.Instance.pendingQuarterEnd
                || (FieldManager.Instance != null
                    && (FieldManager.Instance.JustScoredTouchdown
                        || FieldManager.Instance.JustScoredSafety)))
            {
                velocity = Vector3.zero;
                if (IsKickoffCoverage && GameManager.Instance != null
                    && GameManager.Instance.waitingForNextPlay)
                    Cancel();
                return;
            }

            if (IsKickoffCoverage)
            {
                if (!StillKickoffCoverage())
                {
                    Cancel();
                    return;
                }

                // Kickoff live cover: Q/E cycle only — J is run, K is battle.
                HandleCycleInput(includeTecmoJk: false);
                TickLiveControl();
                return;
            }

            if (!PlayerIsOnDefense())
            {
                Cancel();
                return;
            }

            // Pre-snap: J = prev · K = next (Q/E alts).
            if (GameManager.Instance.isPreSnap
                || (SnapCadence.Instance != null && SnapCadence.Instance.IsActive && !IsLocked))
            {
                HandleCycleInput(includeTecmoJk: true);
                velocity = Vector3.zero;
                return;
            }

            TickLiveControl();
        }

        void TickLiveControl()
        {
            if (!IsLocked || ControlledUnit == null) return;
            if (ContactBattle.IsCombatant(ControlledUnit)
                || TecmoContact.IsInvolved(ControlledUnit))
            {
                velocity = Vector3.zero;
                return;
            }
            if (PlayerStun.IsUnitStunned(ControlledUnit))
            {
                velocity = Vector3.zero;
                return;
            }

            if (diving || diveMissSliding)
            {
                UpdateDive();
                return;
            }

            // Normal defense: no cycle post-snap (J/K / Q/E pre-snap only).
            // Kickoff gunner cycling is handled in Update before TickLiveControl.

            HandleMoveInput();
            if (WasDivePressed())
            {
                // CB / S: A near a live ball in the catch window → jump INT attempt.
                if (!IsKickoffCoverage && TryJumpIntercept())
                    return;
                StartDive();
            }
            else
            {
                TryWrapTackle();
                TryOpposingContactBattle();
            }
        }

        void FixedUpdate()
        {
            if (rb == null || ControlledUnit == null || !IsLocked) return;
            if (GameManager.Instance == null
                || GameManager.Instance.waitingForNextPlay
                || ContactBattle.IsCombatant(ControlledUnit)
                || TecmoContact.IsInvolved(ControlledUnit)
                || PlayerStun.IsUnitStunned(ControlledUnit))
            {
                velocity = Vector3.zero;
                ArcadeMove.Apply(rb, Vector3.zero);
                return;
            }

            // Normal defense stays frozen pre-snap; kickoff coverage runs during isKicking.
            if (!IsKickoffCoverage && GameManager.Instance.isPreSnap)
            {
                velocity = Vector3.zero;
                ArcadeMove.Apply(rb, Vector3.zero);
                return;
            }

            ArcadeMove.Apply(rb, velocity);
        }

        static bool StillKickoffCoverage()
        {
            if (FieldManager.Instance == null || FieldManager.Instance.KickoffReceiverIsPlayer)
                return false;
            if (GameManager.Instance == null) return false;
            return GameManager.Instance.isKicking || GameManager.Instance.isKickoffReturn;
        }

        /// <param name="includeTecmoJk">
        /// Pre-snap / kickoff: also cycle with J (prev) / K (next).
        /// Always: Q = prev, E / Tab / left-click / tap = next.
        /// </param>
        void HandleCycleInput(bool includeTecmoJk)
        {
            string[] roster = IsKickoffCoverage ? KickoffGunnerRoster : CycleRoster;

            bool next = Input.GetKeyDown(KeyCode.E)
                        || Input.GetKeyDown(KeyCode.Tab)
                        || WasPointerCycleTap()
                        || (includeTecmoJk && TecmoInput.CycleNextDown());
            bool prev = Input.GetKeyDown(KeyCode.Q)
                        || (includeTecmoJk && TecmoInput.CyclePrevDown());

            if (next)
            {
                Cycle(1, roster);
                return;
            }

            if (prev)
            {
                Cycle(-1, roster);
                return;
            }

            for (int i = 0; i < roster.Length && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    selectIndex = i;
                    TrySelectRosterIndex(roster, followCamera: true);
                    return;
                }
            }
        }

        /// <summary>Left-click / touch → cycle next (pre-snap only; live play uses click for run/battle).</summary>
        static bool WasPointerCycleTap()
        {
            // Live defense: click is run / battle mash — don't cycle.
            if (TecmoInput.IsDefenseLivePlay()) return false;
            return TecmoInput.PointerTapDown();
        }

        void Cycle(int delta, string[] roster)
        {
            if (roster == null || roster.Length == 0) return;

            int start = selectIndex;
            for (int n = 0; n < roster.Length; n++)
            {
                selectIndex = (selectIndex + delta + roster.Length) % roster.Length;
                if (TrySelectRosterIndex(roster, followCamera: true))
                {
                    string who = roster[selectIndex];
                    PlayBanner.Show(FormatUnitLabel(who), 0.55f, BannerTone.Neutral);
                    return;
                }
            }

            selectIndex = start;
        }

        static string FormatUnitLabel(string unitName)
        {
            return unitName switch
            {
                "CB_Top" => "CB TOP",
                "CB_Bot" => "CB BOT",
                "LB_1" => "LB 1",
                "LB_2" => "LB 2",
                "DL_1" => "DL 1",
                "DL_2" => "DL 2",
                "DL_3" => "DL 3",
                "DL_4" => "DL 4",
                "WR_Top" => "WR TOP",
                "WR_Bot" => "WR BOT",
                "Quarterback" => "QB",
                _ => unitName
            };
        }

        bool TrySelectRosterIndex(string[] roster, bool followCamera)
        {
            if (roster == null || selectIndex < 0 || selectIndex >= roster.Length)
                return false;
            if (IsKickoffCoverage)
                return ApplyKickoffGunner(roster[selectIndex], followCamera);
            return ApplySelection(roster[selectIndex], followCamera);
        }

        bool ApplySelection(string unitName, bool followCamera)
        {
            ClearDiveGhost();
            ClearControlFlags();
            var go = GameObject.Find(unitName);
            if (go == null)
                go = FindUnitIncludingInactive(unitName);
            if (go == null || !go.activeInHierarchy)
            {
                Controlled = null;
                ControlledUnit = null;
                return false;
            }

            ControlledUnit = go.transform;
            Controlled = go.GetComponent<DefenderAI>();
            if (Controlled == null)
                Controlled = go.AddComponent<DefenderAI>();

            if (IsLocked)
                Controlled.IsPlayerControlled = true;

            if (go.GetComponent<StaminaSprint>() == null)
                go.AddComponent<StaminaSprint>();
            go.GetComponent<StaminaSprint>().ResetStamina();

            rb = go.GetComponent<Rigidbody>();
            ArcadeMove.ConfigureKinematicBody(rb);
            spriteRenderer = go.GetComponentInChildren<SpriteRenderer>();
            if (followCamera)
                FollowCameraAndRing(go.transform);
            return true;
        }

        static GameObject FindUnitIncludingInactive(string unitName)
        {
            if (string.IsNullOrEmpty(unitName)) return null;
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t != null && t.name == unitName)
                    return t.gameObject;
            }
            return null;
        }

        bool ApplyKickoffGunner(string unitName, bool followCamera)
        {
            ClearDiveGhost();
            ClearControlFlags();
            var go = GameObject.Find(unitName);
            if (go == null)
                go = FindUnitIncludingInactive(unitName);
            if (go == null || !go.activeInHierarchy)
            {
                Controlled = null;
                ControlledUnit = null;
                return false;
            }

            ControlledUnit = go.transform;
            Controlled = go.GetComponent<DefenderAI>(); // usually null on offense gunners
            if (Controlled != null)
                Controlled.IsPlayerControlled = true;

            var rc = go.GetComponent<ReceiverController>();
            if (rc != null)
                rc.SetKickoffGunnerControl(true);

            if (go.GetComponent<StaminaSprint>() == null)
                go.AddComponent<StaminaSprint>();
            go.GetComponent<StaminaSprint>().ResetStamina();

            // QB gunner — keep PlayerController from fighting kickoff coverage.
            var pc = go.GetComponent<PlayerController>();
            if (pc != null)
                pc.SetControlled(false);

            var qbc = go.GetComponent<QuarterbackController>();
            if (qbc != null)
                qbc.FreezeForKickoff();

            rb = go.GetComponent<Rigidbody>();
            if (rb == null)
                rb = go.AddComponent<Rigidbody>();
            ArcadeMove.ConfigureKinematicBody(rb);
            spriteRenderer = go.GetComponentInChildren<SpriteRenderer>();
            PlayerStun.GetOrAdd(go);

            if (followCamera)
                FollowCameraAndRing(go.transform);
            return true;
        }

        static void FollowCameraAndRing(Transform t)
        {
            if (t == null) return;

            var cam = Camera.main;
            if (cam != null)
            {
                var cc = cam.GetComponent<CameraController>();
                if (cc != null) cc.SetTarget(t);
            }

            var ring = GameObject.Find("SelectionRing");
            if (ring != null)
            {
                var sr = ring.GetComponent<SelectionRing>();
                if (sr != null) sr.SetFollow(t);
            }
        }

        void HandleMoveInput()
        {
            var stamina = ControlledUnit != null
                ? ControlledUnit.GetComponent<StaminaSprint>()
                : null;
            bool boosting = stamina != null && stamina.IsBoosting;
            float baseMove = StatBridge.MoveSpeedOf(ControlledUnit, moveSpeed);
            float baseSprint = StatBridge.SprintSpeedOf(ControlledUnit, sprintSpeed);
            float speed = boosting ? baseSprint : baseMove;

            Vector2 input = ArcadeMove.ReadDigitalPlanar();
            if (input.sqrMagnitude < 0.01f)
            {
                velocity = Vector3.zero;
            }
            else
            {
                velocity = new Vector3(input.x, input.y, 0f).normalized * speed;
                faceDir = velocity.normalized;
            }

            if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
                spriteRenderer.flipX = velocity.x < 0f;
        }

        bool WasDivePressed()
        {
            // Tecmo A (Z/F/RMB) = dive / jump — never WASD.
            return TecmoInput.ADown();
        }

        bool TryJumpIntercept()
        {
            if (ControlledUnit == null) return false;
            if (Time.time < nextJumpAt) return false;

            string n = ControlledUnit.name;
            bool secondary = n.StartsWith("CB_") || n == "S" || n.Contains("Safety");
            if (!secondary) return false;

            if (!FootballBehavior.TryPlayerJumpIntercept(ControlledUnit, jumpIntRadius))
                return false;

            nextJumpAt = Time.time + jumpIntCooldown;
            var sr = ControlledUnit.GetComponentInChildren<SpriteRenderer>();
            if (sr != null)
                sr.transform.localPosition += Vector3.up * 0.35f;
            PlayBanner.Show("JUMP!", 0.55f, BannerTone.Tip);
            return true;
        }

        void StartDive()
        {
            diveDir = faceDir.sqrMagnitude > 0.01f ? faceDir.normalized : Vector3.left;
            if (velocity.sqrMagnitude > 0.25f)
                diveDir = velocity.normalized;

            diving = true;
            diveMissSliding = false;
            diveHitResolved = false;
            diveStartedAt = Time.time;
            divePhaseEndsAt = diveStartedAt + diveCommitSeconds;
            velocity = diveDir * diveSpeed;
            TecmoDive.SetColliderGhost(ControlledUnit, true);
            string who = ControlledUnit != null ? ControlledUnit.name : "GUNNER";
            Debug.Log($"{who} diving! (mash A to extend)");
        }

        void UpdateDive()
        {
            if (diving)
                TryDefenseMashExtend();

            velocity = diveDir * (diveMissSliding ? diveSpeed * 0.55f : diveSpeed);

            // NES: dive only cares about the ball carrier — ghost through OL/others.
            if (!diveHitResolved && TryGetBallCarrier(out Transform carrier, out float dist)
                && dist <= diveContactRadius)
            {
                diveHitResolved = true;
                diving = false;
                diveMissSliding = false;
                velocity = Vector3.zero;
                TecmoDive.SetColliderGhost(ControlledUnit, false);
                BeginHardTackle(carrier);
                return;
            }

            if (Time.time >= divePhaseEndsAt)
            {
                if (diving && !diveMissSliding)
                {
                    diving = false;
                    diveMissSliding = true;
                    divePhaseEndsAt = Time.time + diveMissSlideSeconds;
                    return;
                }

                if (diveMissSliding)
                {
                    diveMissSliding = false;
                    velocity = Vector3.zero;
                    TecmoDive.SetColliderGhost(ControlledUnit, false);
                    if (ControlledUnit != null)
                    {
                        ControlledUnit.position += diveDir * diveMissSlideYards * 0.35f;
                        var stun = PlayerStun.GetOrAdd(ControlledUnit.gameObject);
                        stun.Stun(diveMissStunSeconds, diveDir * 1.2f);
                        Debug.Log($"{ControlledUnit.name} missed the dive — getting up");
                    }
                }
            }

            if (spriteRenderer != null && Mathf.Abs(diveDir.x) > 0.05f)
                spriteRenderer.flipX = diveDir.x < 0f;
        }

        void TryDefenseMashExtend()
        {
            if (!GameRules.EnableTecmoDiveMashExtend) return;
            float cap = diveStartedAt + diveMaxTotalSeconds;
            if (WasDivePressed())
                divePhaseEndsAt = Mathf.Min(divePhaseEndsAt + diveMashExtendPerTap, cap);
            else if (TecmoDive.AExtendPressedOrHeld())
                divePhaseEndsAt = Mathf.Min(
                    divePhaseEndsAt + diveMashExtendHeldPerSecond * Time.deltaTime, cap);
        }

        void ClearDiveGhost()
        {
            TecmoDive.SetColliderGhost(ControlledUnit, false);
            diving = false;
            diveMissSliding = false;
        }

        void TryWrapTackle()
        {
            if (Time.time < nextWrapAt) return;
            if (!TryGetBallCarrier(out Transform carrier, out float dist)) return;
            float radius = IsKickoffCoverage ? kickoffWrapRadius : wrapRadius;
            if (dist > radius) return;
            BeginWrapTackle(carrier);
        }

        /// <summary>Player defender bumps OL / WR / TE → battle (carrier wraps stay above).</summary>
        void TryOpposingContactBattle()
        {
            if (ControlledUnit == null) return;
            if (!GameRules.EnableContactBattle && !GameRules.EnableLineEngageBattles) return;
            if (Time.time < nextWrapAt) return;

            float radius = IsKickoffCoverage ? kickoffWrapRadius : wrapRadius;
            Transform best = null;
            float bestDist = radius;
            FindNearestOffense(ControlledUnit.position, "Lineman", ref best, ref bestDist);
            FindNearestOffense(ControlledUnit.position, "Receiver", ref best, ref bestDist);
            FindNearestOffense(ControlledUnit.position, "Player", ref best, ref bestDist);
            if (best == null) return;

            if (ContactBattle.TryOpposingTouch(best, ControlledUnit))
            {
                nextWrapAt = Time.time + wrapCooldown;
                velocity = Vector3.zero;
            }
        }

        static void FindNearestOffense(Vector3 origin, string tag, ref Transform best, ref float bestDist)
        {
            GameObject[] units;
            try { units = GameObject.FindGameObjectsWithTag(tag); }
            catch { return; }

            Vector2 pos = new Vector2(origin.x, origin.y);
            foreach (var u in units)
            {
                if (u == null || !u.activeInHierarchy) continue;
                if (PlayerStun.IsUnitStunned(u)) continue;
                if (ContactBattle.IsBallCarrier(u.transform)) continue;
                float d = Vector2.Distance(pos, new Vector2(u.transform.position.x, u.transform.position.y));
                if (d >= bestDist) continue;
                bestDist = d;
                best = u.transform;
            }
        }

        void BeginHardTackle(Transform carrier)
        {
            // Tecmo dive connect → instant tackle (fumble is a SoftTackle roll inside).
            RequestTackle(carrier, hardHit: true);
        }

        void BeginWrapTackle(Transform carrier)
        {
            RequestTackle(carrier, hardHit: false);
        }

        void ForceDiveFumble(Transform carrier)
        {
            if (carrier == null) return;
            string tackler = ControlledUnit != null ? ControlledUnit.name : "GUNNER";
            Vector3 knock = diveDir.sqrMagnitude > 0.01f ? diveDir : Vector3.left;

            var rc = carrier.GetComponent<ReceiverController>();
            if (rc != null && rc.hasBall)
            {
                rc.ForceFumbleFromDive(tackler, knock);
                return;
            }

            var qbc = carrier.GetComponent<QuarterbackController>();
            if (qbc != null)
                qbc.ForceFumbleFromDive(tackler, knock);
        }

        void RequestTackle(Transform carrier, bool hardHit)
        {
            if (carrier == null || ControlledUnit == null) return;
            if (PlayerStun.IsUnitStunned(ControlledUnit)) return;

            // AI has the ball on defense downs and on player-kick returns.
            bool aiBall = PlayerIsOnDefense() || IsKickoffCoverage;
            string tackler = ControlledUnit.name;
            Transform tacklerTf = ControlledUnit;

            var qbc = carrier.GetComponent<QuarterbackController>();
            if (qbc != null)
            {
                nextWrapAt = Time.time + wrapCooldown;
                qbc.ApplySackOrTackle(tackler, aiCarrier: aiBall, hardHit: hardHit, tackler: tacklerTf);
                return;
            }

            var rc = carrier.GetComponent<ReceiverController>();
            if (rc != null && rc.hasBall)
            {
                nextWrapAt = Time.time + wrapCooldown;
                rc.ForceTackleFromDefender(tackler, hardHit, tacklerTf);
            }
        }

        static bool TryGetBallCarrier(out Transform carrier, out float dist)
        {
            carrier = null;
            dist = float.MaxValue;

            GameObject go = null;
            try { go = GameObject.FindGameObjectWithTag("BallCarrier"); }
            catch { /* tag missing */ }

            // Kickoff: AI returner may still be S before BallCarrier tag sticks.
            if (go == null && Instance != null && Instance.IsKickoffCoverage)
            {
                var s = GameObject.Find("S");
                if (s != null && s.activeInHierarchy)
                {
                    var rc = s.GetComponent<ReceiverController>();
                    if (rc != null && rc.hasBall)
                        go = s;
                }
            }

            if (go == null)
            {
                var qb = GameObject.Find("Quarterback");
                if (qb != null)
                {
                    var pc = qb.GetComponent<PlayerController>();
                    var qbc = qb.GetComponent<QuarterbackController>();
                    bool holding = pc != null && pc.hasBall
                                   && (qbc == null || !qbc.hasThrown || qbc.isScrambling);
                    if (holding) go = qb;
                }
            }

            if (go == null || Instance == null || Instance.ControlledUnit == null)
                return false;

            carrier = go.transform;
            dist = Vector2.Distance(
                new Vector2(Instance.ControlledUnit.position.x, Instance.ControlledUnit.position.y),
                new Vector2(carrier.position.x, carrier.position.y));
            return true;
        }
    }
}
