using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.Core;
using RetroBowl.UI;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// NES Tecmo-style contact: non-dive touch → brief mash lock.
    /// Carrier mashes J/K to break; defender mashes J/K to finish the wrap.
    /// Extra defenders join the pile (stacked Strength / HP) and bias wrap + popcorn.
    /// Dive contact stays outside this (instant tackle / strip).
    /// </summary>
    public class TecmoContact : MonoBehaviour
    {
        public static TecmoContact Instance { get; private set; }

        [Header("Mash")]
        public float tapPower = 0.22f;
        public float aiFinishPerSecond = 0.55f;
        public float aiBreakPerSecond = 0.42f;
        public float passiveFinishPerSecond = 0.18f;
        public float maxLockSeconds = 1.85f;
        public float breakStunSeconds = 0.55f;
        public float breakKnockback = 0.85f;

        public bool IsActive { get; private set; }
        public Transform Carrier { get; private set; }
        public Transform Tackler { get; private set; }

        readonly List<Transform> assistTacklers = new List<Transform>(4);

        float break01;
        float finish01;
        float startedAt;
        bool playerCarrier;
        bool playerTackler;
        bool pocketSack;
        Action onBroken;
        Action onTackled;

        GameObject uiRoot;
        TextMeshProUGUI promptLabel;
        Image breakFill;
        Image finishFill;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var host = GameManager.Instance != null
                ? GameManager.Instance.gameObject
                : new GameObject("TecmoContact");
            if (host.GetComponent<TecmoContact>() == null)
                host.AddComponent<TecmoContact>();
        }

        public static bool IsInvolved(Transform t)
        {
            if (Instance == null || !Instance.IsActive || t == null) return false;
            if (t == Instance.Carrier || t == Instance.Tackler) return true;
            for (int i = 0; i < Instance.assistTacklers.Count; i++)
            {
                if (Instance.assistTacklers[i] == t) return true;
            }
            return false;
        }

        /// <summary>Active mash lock for this ball carrier (joiners use <see cref="TryJoinTackler"/>).</summary>
        public static bool IsActiveForCarrier(Transform carrier)
        {
            return Instance != null && Instance.IsActive && Instance.Carrier == carrier;
        }

        int StackedTacklerHp()
        {
            int sum = SoftTackle.HittingPowerOf(Tackler);
            for (int i = 0; i < assistTacklers.Count; i++)
                sum += SoftTackle.HittingPowerOf(assistTacklers[i]);
            return Mathf.Max(1, sum);
        }

        /// <summary>0–1 wrap skill from pile HP (soft-caps past 99).</summary>
        float StackedTacklerSkill01()
        {
            int pile = StackedTacklerHp();
            // Primary 1–99 → ~0–1; each assist adds diminishing returns.
            return Mathf.Clamp01(pile / 99f * 0.72f + 0.18f);
        }

        int PileCount()
            => (Tackler != null ? 1 : 0) + assistTacklers.Count;

        /// <summary>
        /// Start a Tecmo wrap fight. hardHit / dive paths should call
        /// <see cref="ResolveInstantTackle"/> instead.
        /// If already locked on this carrier, joins the pile instead.
        /// </summary>
        public static bool Begin(
            Transform carrier,
            Transform tackler,
            Action onBroken,
            Action onTackled,
            bool pocketSack = false)
        {
            if (!GameRules.EnableTecmoContact) return false;
            EnsureExists();
            if (Instance.IsActive && Instance.Carrier == carrier)
                return Instance.TryJoinTacklerInternal(tackler);
            return Instance.BeginInternal(carrier, tackler, onBroken, onTackled, pocketSack);
        }

        /// <summary>
        /// Second+ defender touches the carrier during an active mash lock — stack HP.
        /// </summary>
        public static bool TryJoinTackler(Transform extra)
        {
            if (!GameRules.EnableTecmoContact || !GameRules.EnableTecmoHpStack) return false;
            EnsureExists();
            return Instance.TryJoinTacklerInternal(extra);
        }

        /// <summary>
        /// Offense teammate bumps into the wrapping defender — peel them off the carrier.
        /// </summary>
        public static bool TryTeammatePopAssist(Transform helper, float reach = 1.05f)
        {
            if (!GameRules.EnableTecmoContact) return false;
            EnsureExists();
            return Instance.TryTeammatePopAssistInternal(helper, reach);
        }

        bool TryTeammatePopAssistInternal(Transform helper, float reach)
        {
            if (!IsActive || Carrier == null || helper == null) return false;
            if (helper == Carrier) return false;
            if (!helper.gameObject.activeInHierarchy) return false;
            if (PlayerStun.IsUnitStunned(helper)) return false;
            if (ContactBattle.IsDefenseSide(helper)) return false;
            if (!ContactBattle.IsOffenseSide(helper) && helper.GetComponent<OffensiveBlocker>() == null)
                return false;

            // Prefer peeling the nearest defender in the pile.
            Transform target = null;
            float best = reach;
            void Consider(Transform d)
            {
                if (d == null || !d.gameObject.activeInHierarchy) return;
                float dist = Vector2.Distance(
                    new Vector2(helper.position.x, helper.position.y),
                    new Vector2(d.position.x, d.position.y));
                if (dist < best)
                {
                    best = dist;
                    target = d;
                }
            }

            Consider(Tackler);
            for (int i = 0; i < assistTacklers.Count; i++)
                Consider(assistTacklers[i]);

            if (target == null) return false;

            Vector3 away = target.position - helper.position;
            away.z = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.right;
            PlayerStun.GetOrAdd(target.gameObject)
                ?.Stun(breakStunSeconds * 0.9f, away.normalized * breakKnockback);

            // Assist only — remove from pile, keep mash vs primary.
            if (target != Tackler)
            {
                assistTacklers.Remove(target);
                PlayBanner.Show("HELP BLOCK!", 0.55f, BannerTone.Positive);
                RefreshUi();
                return true;
            }

            // Primary tackler peeled — carrier breaks free (skip duplicate banner).
            FinishBreak(showBanner: false);
            PlayBanner.Show("HELP BLOCK!", 0.7f, BannerTone.Positive);
            return true;
        }

        bool TryJoinTacklerInternal(Transform extra)
        {
            if (!GameRules.EnableTecmoHpStack) return false;
            if (!IsActive || Carrier == null || extra == null) return false;
            if (extra == Carrier || extra == Tackler) return false;
            for (int i = 0; i < assistTacklers.Count; i++)
            {
                if (assistTacklers[i] == extra) return false;
            }
            if (assistTacklers.Count >= GameRules.TecmoMaxAssistTacklers) return false;
            if (PlayerStun.IsUnitStunned(extra)) return false;
            if (TecmoDive.IsGhostDiving(extra) || TecmoDive.IsGhostDiving(Carrier)) return false;
            if (!extra.gameObject.activeInHierarchy) return false;

            assistTacklers.Add(extra);
            StopUnit(extra);

            // Player-controlled joiner can also mash wrap.
            if (IsPlayerTacklerUnit(extra))
                playerTackler = true;

            int pileHp = StackedTacklerHp();
            PlayBanner.Show($"PILE ON! +{SoftTackle.HittingPowerOf(extra)} HP", 0.55f, BannerTone.Neutral);
            RefreshUi();

            var popcorn = SoftTackle.EvaluatePopcorn(Carrier, pileHp);
            if (popcorn == SoftTackle.PopcornResult.TacklerPopcornsCarrier)
            {
                SoftTackle.ApplyTacklerPopcorn(Carrier, Tackler);
                FinishTackle();
                return true;
            }

            // Carrier can only popcorn the whole pile if still ≥50 stronger — rare once stack builds.
            if (popcorn == SoftTackle.PopcornResult.CarrierPopcornsTackler)
            {
                SoftTackle.ApplyCarrierPopcorn(Carrier, extra);
                // Stun only the weak joiner; keep mash vs primary if pile still soft.
                return true;
            }

            // Instant bias toward wrap when help arrives.
            finish01 = Mathf.Clamp01(finish01 + 0.12f + SoftTackle.HittingPowerOf(extra) / 400f);
            return true;
        }

        /// <summary>Dive / hard hit — no mash; tackle (or fumble roll) immediately.</summary>
        public static void ResolveInstantTackle(
            Transform carrier,
            Transform tackler,
            Action onTackled,
            Action onBroken = null,
            bool hardHit = true,
            bool pocketSack = false)
        {
            if (carrier == null)
            {
                onTackled?.Invoke();
                return;
            }

            // NES dive connect: popcorn still applies (HP≥50 auto-flatten).
            if (hardHit)
            {
                var popcorn = SoftTackle.EvaluatePopcorn(carrier, tackler);
                if (popcorn == SoftTackle.PopcornResult.CarrierPopcornsTackler)
                {
                    SoftTackle.ApplyCarrierPopcorn(carrier, tackler);
                    onBroken?.Invoke();
                    return;
                }

                if (popcorn == SoftTackle.PopcornResult.TacklerPopcornsCarrier)
                {
                    SoftTackle.ApplyTacklerPopcorn(carrier, tackler);
                    if (SoftTackle.RollFumble(carrier, tackler, hardHit: true, pocketSack))
                    {
                        string whoPop = tackler != null ? tackler.name : "DEFENSE";
                        var rcPop = carrier.GetComponent<ReceiverController>();
                        if (rcPop != null && rcPop.hasBall)
                        {
                            rcPop.ForceFumbleFromDive(whoPop, SoftTackle.FumbleKnockDir(carrier, tackler));
                            return;
                        }

                        var qbcPop = carrier.GetComponent<QuarterbackController>();
                        if (qbcPop != null)
                        {
                            qbcPop.ForceFumbleFromDive(whoPop, SoftTackle.FumbleKnockDir(carrier, tackler));
                            return;
                        }
                    }

                    onTackled?.Invoke();
                    return;
                }
            }

            // Still allow a rare shed on non-dive hard wraps if SoftTackle says so.
            if (!hardHit)
            {
                bool playerCarrier = IsPlayerCarrierUnit(carrier);
                bool tackled = SoftTackle.ResolveWrap(
                    carrier, tackler, pocketSack, playerCarrier, hardHit: false);
                if (!tackled)
                {
                    onBroken?.Invoke();
                    return;
                }
            }

            if (SoftTackle.RollFumble(carrier, tackler, hardHit, pocketSack))
            {
                string who = tackler != null ? tackler.name : "DEFENSE";
                var rc = carrier.GetComponent<ReceiverController>();
                if (rc != null && rc.hasBall)
                {
                    rc.ForceFumbleFromDive(who, SoftTackle.FumbleKnockDir(carrier, tackler));
                    return;
                }

                var qbc = carrier.GetComponent<QuarterbackController>();
                if (qbc != null)
                {
                    qbc.ForceFumbleFromDive(who, SoftTackle.FumbleKnockDir(carrier, tackler));
                    return;
                }
            }

            MatchPresentation.Tackle();
            onTackled?.Invoke();
        }

        bool BeginInternal(
            Transform carrier,
            Transform tackler,
            Action onBroken,
            Action onTackled,
            bool pocketSack)
        {
            if (carrier == null || tackler == null || carrier == tackler) return false;
            if (IsActive) return false;
            if (ContactBattle.Instance != null && ContactBattle.Instance.ShowingPlayerUi) return false;
            if (TackleBattle.Instance != null && TackleBattle.Instance.IsActive) return false;
            if (GameManager.Instance != null
                && (GameManager.Instance.waitingForNextPlay
                    || GameManager.Instance.isPreSnap
                    || GameManager.Instance.inTackleBattle))
                return false;
            if (PassCollisionGate.ShouldBlockBattle(carrier, tackler)) return false;
            if (PlayerStun.IsUnitStunned(carrier) || PlayerStun.IsUnitStunned(tackler))
                return false;

            // NES dive: no mash lock while either unit is diving (dive uses instant resolve).
            if (TecmoDive.IsGhostDiving(carrier) || TecmoDive.IsGhostDiving(tackler))
                return false;

            // Tecmo popcorn: |Strength| ≥ 50 → auto flatten (no mash lock).
            var popcorn = SoftTackle.EvaluatePopcorn(carrier, tackler);
            if (popcorn == SoftTackle.PopcornResult.CarrierPopcornsTackler)
            {
                SoftTackle.ApplyCarrierPopcorn(carrier, tackler);
                onBroken?.Invoke();
                return true;
            }

            if (popcorn == SoftTackle.PopcornResult.TacklerPopcornsCarrier)
            {
                SoftTackle.ApplyTacklerPopcorn(carrier, tackler);
                if (SoftTackle.RollFumble(carrier, tackler, hardHit: false, pocketSack))
                {
                    string who = tackler != null ? tackler.name : "DEFENSE";
                    var rcPop = carrier.GetComponent<ReceiverController>();
                    if (rcPop != null && rcPop.hasBall)
                    {
                        rcPop.ForceFumbleFromDive(who, SoftTackle.FumbleKnockDir(carrier, tackler));
                        return true;
                    }

                    var qbcPop = carrier.GetComponent<QuarterbackController>();
                    if (qbcPop != null)
                    {
                        qbcPop.ForceFumbleFromDive(who, SoftTackle.FumbleKnockDir(carrier, tackler));
                        return true;
                    }
                }

                onTackled?.Invoke();
                return true;
            }

            playerCarrier = IsPlayerCarrierUnit(carrier);
            playerTackler = IsPlayerTacklerUnit(tackler);

            // AI↔AI: quiet SoftTackle roll — no mash UI.
            if (!playerCarrier && !playerTackler)
            {
                bool tackled = SoftTackle.ResolveWrap(
                    carrier, tackler, pocketSack, playerCarrier: false, hardHit: false);
                if (!tackled)
                {
                    onBroken?.Invoke();
                    return true;
                }

                if (SoftTackle.RollFumble(carrier, tackler, hardHit: false, pocketSack))
                {
                    string who = tackler != null ? tackler.name : "DEFENSE";
                    var rc = carrier.GetComponent<ReceiverController>();
                    if (rc != null && rc.hasBall)
                    {
                        rc.ForceFumbleFromDive(who, SoftTackle.FumbleKnockDir(carrier, tackler));
                        return true;
                    }

                    var qbc = carrier.GetComponent<QuarterbackController>();
                    if (qbc != null)
                    {
                        qbc.ForceFumbleFromDive(who, SoftTackle.FumbleKnockDir(carrier, tackler));
                        return true;
                    }
                }

                MatchPresentation.Tackle();
                onTackled?.Invoke();
                return true;
            }

            Carrier = carrier;
            Tackler = tackler;
            assistTacklers.Clear();
            this.onBroken = onBroken;
            this.onTackled = onTackled;
            this.pocketSack = pocketSack;
            break01 = 0f;
            finish01 = 0f;
            startedAt = Time.time;

            IsActive = true;
            if (GameManager.Instance != null)
                GameManager.Instance.inTackleBattle = true;

            StopUnit(carrier);
            StopUnit(tackler);
            EnsureUi();
            ShowUi();
            PlayBanner.Show("CONTACT!", 0.45f, BannerTone.Neutral);
            return true;
        }

        void Update()
        {
            if (!IsActive) return;

            if (GameManager.Instance == null
                || GameManager.Instance.currentState != GameState.Playing
                || GameManager.Instance.waitingForNextPlay
                || GameManager.Instance.isPreSnap)
            {
                CancelSilent();
                return;
            }

            if (Carrier == null || Tackler == null
                || !Carrier.gameObject.activeInHierarchy
                || !Tackler.gameObject.activeInHierarchy)
            {
                CancelSilent();
                return;
            }

            StopUnit(Carrier);
            StopUnit(Tackler);
            for (int i = assistTacklers.Count - 1; i >= 0; i--)
            {
                var a = assistTacklers[i];
                if (a == null || !a.gameObject.activeInHierarchy)
                {
                    assistTacklers.RemoveAt(i);
                    continue;
                }
                StopUnit(a);
            }

            float dt = Time.unscaledDeltaTime;

            // Mash J/K — free-run J auto-switches to battle once locked.
            float carrierStr = UnitRuntimeStats.Of(Carrier)?.Strength01 ?? 0.55f;
            float tacklerStr = StackedTacklerSkill01();
            float tapBreak = tapPower * Mathf.Lerp(0.85f, 1.25f, carrierStr);
            float tapFinish = tapPower * Mathf.Lerp(0.85f, 1.25f, tacklerStr);

            if (TecmoInput.BattleDown())
            {
                if (playerCarrier && !playerTackler)
                    break01 += tapBreak;
                else if (playerTackler && !playerCarrier)
                    finish01 += tapFinish;
                else if (playerCarrier && playerTackler)
                {
                    break01 += tapBreak * 0.55f;
                    finish01 += tapFinish * 0.45f;
                }
                else
                {
                    finish01 += tapFinish * 0.5f;
                }
            }

            if (!playerTackler)
                finish01 += (aiFinishPerSecond * Mathf.Lerp(0.8f, 1.25f, tacklerStr)
                             + passiveFinishPerSecond) * dt
                            * (pocketSack ? 1.25f : 1f);
            else
                finish01 += passiveFinishPerSecond * 0.35f * dt;

            if (!playerCarrier)
                break01 += aiBreakPerSecond * Mathf.Lerp(0.75f, 1.3f, carrierStr) * dt
                           * (pocketSack ? 0.65f : 1f);

            // Skill bias from stacked tackler HP.
            finish01 += tacklerStr * 0.08f * dt;
            // Each assist adds a little continuous wrap pressure.
            if (assistTacklers.Count > 0)
                finish01 += 0.06f * assistTacklers.Count * dt;

            break01 = Mathf.Clamp01(break01);
            finish01 = Mathf.Clamp01(finish01);
            RefreshUi();

            if (break01 >= 1f)
            {
                FinishBreak();
                return;
            }

            if (finish01 >= 1f || Time.time - startedAt >= maxLockSeconds)
            {
                FinishTackle();
                return;
            }
        }

        void FixedUpdate()
        {
            if (!IsActive) return;
            StopUnit(Carrier);
            StopUnit(Tackler);
            for (int i = 0; i < assistTacklers.Count; i++)
                StopUnit(assistTacklers[i]);
        }

        void FinishBreak(bool showBanner = true)
        {
            Vector3 away = Tackler != null
                ? Tackler.position - Carrier.position
                : Vector3.left;
            away.z = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.left;
            var knock = away.normalized * breakKnockback;
            if (Tackler != null)
                PlayerStun.GetOrAdd(Tackler.gameObject)?.Stun(breakStunSeconds, knock);
            for (int i = 0; i < assistTacklers.Count; i++)
            {
                if (assistTacklers[i] != null)
                    PlayerStun.GetOrAdd(assistTacklers[i].gameObject)
                        ?.Stun(breakStunSeconds * 0.85f, knock);
            }

            if (showBanner)
                PlayBanner.Show("BROKE TACKLE!", 0.7f, BannerTone.Positive);
            MatchPresentation.Shake(0.12f, 0.1f);

            var broken = onBroken;
            ClearState();
            broken?.Invoke();
        }

        void FinishTackle()
        {
            if (SoftTackle.RollFumble(Carrier, Tackler, hardHit: false, pocketSack))
            {
                string who = Tackler != null ? Tackler.name : "DEFENSE";
                var rc = Carrier.GetComponent<ReceiverController>();
                if (rc != null && rc.hasBall)
                {
                    var cb = onTackled;
                    ClearState();
                    rc.ForceFumbleFromDive(who, SoftTackle.FumbleKnockDir(Carrier, Tackler));
                    return;
                }

                var qbc = Carrier.GetComponent<QuarterbackController>();
                if (qbc != null)
                {
                    ClearState();
                    qbc.ForceFumbleFromDive(who, SoftTackle.FumbleKnockDir(Carrier, Tackler));
                    return;
                }
            }

            MatchPresentation.Tackle();
            PlayBanner.Show("TACKLED!", 0.55f, BannerTone.Neutral);
            var tackled = onTackled;
            ClearState();
            tackled?.Invoke();
        }

        void CancelSilent()
        {
            ClearState();
        }

        public void ForceCancel()
        {
            if (!IsActive) return;
            ClearState();
        }

        void ClearState()
        {
            IsActive = false;
            Carrier = null;
            Tackler = null;
            assistTacklers.Clear();
            onBroken = null;
            onTackled = null;
            break01 = 0f;
            finish01 = 0f;
            if (GameManager.Instance != null)
                GameManager.Instance.inTackleBattle = false;
            HideUi();
        }

        static bool IsPlayerCarrierUnit(Transform t)
        {
            if (t == null) return false;
            var rc = t.GetComponent<ReceiverController>();
            if (rc != null && rc.IsPlayerControlled && rc.hasBall) return true;
            var pc = t.GetComponent<PlayerController>();
            if (pc != null && pc.IsControlled && pc.hasBall) return true;
            var qbc = t.GetComponent<QuarterbackController>();
            if (qbc != null && pc != null && pc.IsControlled) return true;
            return false;
        }

        static bool IsPlayerTacklerUnit(Transform t)
        {
            if (t == null) return false;
            if (PlayerDefenseController.Instance != null
                && PlayerDefenseController.Instance.IsLocked
                && PlayerDefenseController.Instance.ControlledUnit == t)
                return true;
            var ai = t.GetComponent<DefenderAI>();
            return ai != null && ai.IsPlayerControlled;
        }

        static void StopUnit(Transform t)
        {
            if (t == null) return;
            var rb = t.GetComponent<Rigidbody>();
            if (rb != null)
                ArcadeMove.Apply(rb, Vector3.zero);
        }

        void EnsureUi()
        {
            if (uiRoot != null) return;

            var canvasGo = new GameObject("TecmoContactCanvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 62;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.AddComponent<GraphicRaycaster>();

            uiRoot = new GameObject("TecmoContactUi");
            uiRoot.transform.SetParent(canvasGo.transform, false);
            var rootRt = uiRoot.AddComponent<RectTransform>();
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            promptLabel = MakeLabel(uiRoot.transform, "Prompt", new Vector2(0.5f, 0.22f), 34f);

            breakFill = MakeBar(uiRoot.transform, "BreakBar", new Vector2(0.5f, 0.14f),
                new Color(0.35f, 0.9f, 0.45f, 0.9f));
            finishFill = MakeBar(uiRoot.transform, "FinishBar", new Vector2(0.5f, 0.10f),
                new Color(0.95f, 0.35f, 0.3f, 0.9f));
            uiRoot.SetActive(false);
        }

        static Image MakeBar(Transform parent, string name, Vector2 anchor, Color color)
        {
            var bg = new GameObject(name + "Bg");
            bg.transform.SetParent(parent, false);
            var bgRt = bg.AddComponent<RectTransform>();
            bgRt.anchorMin = anchor;
            bgRt.anchorMax = anchor;
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.sizeDelta = new Vector2(320f, 16f);
            var bgImg = bg.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.12f, 0.75f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(bg.transform, false);
            var fillRt = fill.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0.1f);
            fillRt.anchorMax = new Vector2(0f, 0.9f);
            fillRt.pivot = new Vector2(0f, 0.5f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            var img = fill.AddComponent<Image>();
            img.color = color;
            return img;
        }

        static TextMeshProUGUI MakeLabel(Transform parent, string name, Vector2 anchor, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(800f, 60f);
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

        void ShowUi()
        {
            EnsureUi();
            if (uiRoot != null) uiRoot.SetActive(true);
            RefreshUi();
        }

        void HideUi()
        {
            if (uiRoot != null) uiRoot.SetActive(false);
        }

        void RefreshUi()
        {
            if (promptLabel != null)
            {
                string pile = PileCount() > 1 ? $"  ·  PILE x{PileCount()}" : "";
                if (playerCarrier && !playerTackler)
                    promptLabel.text = "MASH J/K / CLICK  ·  BREAK!" + pile;
                else if (playerTackler && !playerCarrier)
                    promptLabel.text = "MASH J/K / CLICK  ·  WRAP!" + pile;
                else
                    promptLabel.text = "CONTACT!" + pile;
            }

            if (breakFill != null)
                breakFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(break01), 0.9f);
            if (finishFill != null)
                finishFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(finish01), 0.9f);
        }
    }
}
