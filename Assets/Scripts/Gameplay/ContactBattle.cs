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
    /// Universal offense↔defense contact battle.
    /// Player-controlled unit in the pair → mash meter UI.
    /// AI↔AI → silent timed lock (both units freeze until a winner).
    /// Only one player-facing battle at a time; many AI locks can run.
    /// </summary>
    public class ContactBattle : MonoBehaviour
    {
        public static ContactBattle Instance { get; private set; }

        [Header("Stamina")]
        public float maxStamina = 100f;
        public float tapDamage = 18f;
        public float selfCostPerTap = 1.2f;
        public float passiveDrainPerSecond = 5f;
        public float pressurePerSecond = 9f;

        [Header("Knockback")]
        public float winStunSeconds = 0.85f;
        public float winKnockback = 1.8f;
        public float loseStunSeconds = 0.7f;
        public float loseKnockback = 1.25f;

        [Header("AI")]
        [Range(0.15f, 0.85f)] public float aiOffenseWinChance = 0.48f;
        [Range(0.15f, 0.85f)] public float aiCarrierBreakChance = 0.38f;
        [Tooltip("Silent AI↔AI lock duration before a winner is rolled.")]
        public float aiLockSeconds = 0.75f;

        [Header("Input")]
        public KeyCode battleKey = KeyCode.K;
        public KeyCode battleKeyAlt = KeyCode.E;

        public bool IsActive { get; private set; }
        public bool ShowingPlayerUi { get; private set; }
        public Transform Offense { get; private set; }
        public Transform Defense { get; private set; }

        float offenseStamina;
        float defenseStamina;
        int tapCount;
        bool playerOnOffense;
        bool involvesBallCarrier;
        /// <summary>True when this battle set <see cref="GameManager.inTackleBattle"/>.</summary>
        bool ownsTackleLock;
        Action onOffenseWon;
        Action onDefenseWon;

        /// <summary>AI↔AI fights that freeze both units until resolve.</summary>
        struct SilentLock
        {
            public Transform Offense;
            public Transform Defense;
            public float EndsAt;
            public bool CarrierFight;
            public Action OnOffenseWon;
            public Action OnDefenseWon;
        }

        readonly List<SilentLock> silentLocks = new List<SilentLock>(8);

        GameObject root;
        TextMeshProUGUI titleLabel;
        TextMeshProUGUI promptLabel;
        TextMeshProUGUI meterLabel;
        Image offenseFill;
        Image defenseFill;

        void Awake()
        {
            Instance = this;
            EnsureUi();
            Hide();
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
                : new GameObject("ContactBattle");
            if (host.GetComponent<ContactBattle>() == null)
                host.AddComponent<ContactBattle>();
        }

        public static bool IsCombatant(Transform t)
        {
            if (Instance == null || t == null) return false;
            if (Instance.IsActive && (t == Instance.Offense || t == Instance.Defense))
                return true;
            for (int i = 0; i < Instance.silentLocks.Count; i++)
            {
                var lockPair = Instance.silentLocks[i];
                if (t == lockPair.Offense || t == lockPair.Defense)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Any opposing O↔D touch — classifies sides and starts a battle.
        /// Ball-carrier wraps stay on TecmoContact / carrier controllers.
        /// </summary>
        public static bool TryOpposingTouch(
            Transform a,
            Transform b,
            Action onOffenseWon = null,
            Action onDefenseWon = null)
        {
            if (!GameRules.EnableContactBattle && !GameRules.EnableLineEngageBattles)
                return false;
            if (a == null || b == null || a == b) return false;
            if (IsBallCarrier(a) || IsBallCarrier(b)) return false;
            if (TecmoContact.IsInvolved(a) || TecmoContact.IsInvolved(b)) return false;
            // NES dive ghosts through blockers — no ContactBattle during dive.
            if (TecmoDive.IsGhostDiving(a) || TecmoDive.IsGhostDiving(b)) return false;
            if (!TryClassifySides(a, b, out Transform offense, out Transform defense))
                return false;
            return Begin(offense, defense, onOffenseWon, onDefenseWon);
        }

        /// <summary>
        /// Start a contact battle. Returns false if a player UI battle is already
        /// active, invalid pair, or PassCollisionGate blocks the matchup.
        /// AI↔AI still resolves while a player mash is up (other units keep battling).
        /// </summary>
        public static bool Begin(
            Transform offense,
            Transform defense,
            Action onOffenseWon = null,
            Action onDefenseWon = null)
        {
            if (!GameRules.EnableContactBattle && !GameRules.EnableLineEngageBattles)
                return false;
            EnsureExists();
            return Instance.BeginInternal(offense, defense, onOffenseWon, onDefenseWon);
        }

        bool BeginInternal(
            Transform offense,
            Transform defense,
            Action onOffenseWon,
            Action onDefenseWon)
        {
            if (offense == null || defense == null) return false;
            if (offense == defense) return false;

            if (GameManager.Instance != null
                && (GameManager.Instance.waitingForNextPlay || GameManager.Instance.isPreSnap))
                return false;

            if (PassCollisionGate.ShouldBlockBattle(offense, defense))
                return false;

            if (PlayerStun.IsUnitStunned(offense) || PlayerStun.IsUnitStunned(defense))
                return false;

            if (TecmoContact.IsInvolved(offense) || TecmoContact.IsInvolved(defense))
                return false;

            bool playerOff = IsPlayerUnit(offense);
            bool playerDef = IsPlayerUnit(defense);
            bool showUi = playerOff || playerDef;
            // Only one human — if somehow both, favor the ball carrier / offense.
            bool tapsForOffense = playerOff || !playerDef;

            // Player mash: only one UI battle. AI↔AI may still resolve around it.
            if (showUi)
            {
                if (ShowingPlayerUi || IsActive) return false;
                if (IsCombatant(offense) || IsCombatant(defense)) return false;
                if (TackleBattle.Instance != null && TackleBattle.Instance.IsActive) return false;
                if (GameManager.Instance != null && GameManager.Instance.inTackleBattle) return false;
            }
            else if (ShowingPlayerUi && IsActive
                     && (offense == Offense || offense == Defense
                         || defense == Offense || defense == Defense))
            {
                return false;
            }

            bool carrierFight = IsBallCarrier(offense) || IsBallCarrier(defense);
            // Carrier should be the offense side for wrap outcomes.
            if (IsBallCarrier(defense) && !IsBallCarrier(offense))
            {
                var swap = offense;
                offense = defense;
                defense = swap;
                var cbSwap = onOffenseWon;
                onOffenseWon = onDefenseWon;
                onDefenseWon = cbSwap;
                tapsForOffense = IsPlayerUnit(offense) || !IsPlayerUnit(defense);
                carrierFight = IsBallCarrier(offense) || IsBallCarrier(defense);
            }

            // AI↔AI: timed freeze lock — no UI, no instant slide-through.
            if (!showUi)
            {
                if (IsCombatant(offense) || IsCombatant(defense))
                    return false;
                if (TecmoContact.IsInvolved(offense) || TecmoContact.IsInvolved(defense))
                    return false;

                silentLocks.Add(new SilentLock
                {
                    Offense = offense,
                    Defense = defense,
                    EndsAt = Time.time + Mathf.Max(0.25f, aiLockSeconds),
                    CarrierFight = carrierFight,
                    OnOffenseWon = onOffenseWon,
                    OnDefenseWon = onDefenseWon
                });
                StopUnit(offense);
                StopUnit(defense);
                return true;
            }

            this.onOffenseWon = onOffenseWon;
            this.onDefenseWon = onDefenseWon;
            Offense = offense;
            Defense = defense;
            playerOnOffense = tapsForOffense;
            tapCount = 0;
            ownsTackleLock = false;
            involvesBallCarrier = carrierFight;

            EnsureUi();
            IsActive = true;
            ShowingPlayerUi = true;
            offenseStamina = maxStamina;
            defenseStamina = maxStamina;

            StopUnit(offense);
            StopUnit(defense);

            if (involvesBallCarrier && GameManager.Instance != null)
            {
                GameManager.Instance.inTackleBattle = true;
                ownsTackleLock = true;
            }

            root.SetActive(true);
            titleLabel.text = "BATTLE!";
            promptLabel.text = playerOnOffense
                ? "MASH J/K / CLICK  ·  BREAK FREE"
                : "MASH J/K / CLICK  ·  WRAP UP";
            RefreshMeters();
            return true;
        }

        void Update()
        {
            TickSilentLocks();

            if (!IsActive || !ShowingPlayerUi) return;

            if (GameManager.Instance == null
                || GameManager.Instance.currentState != GameState.Playing
                || GameManager.Instance.isPreSnap
                || GameManager.Instance.waitingForNextPlay)
            {
                CancelSilent();
                return;
            }

            if (Offense == null || Defense == null
                || !Offense.gameObject.activeInHierarchy
                || !Defense.gameObject.activeInHierarchy)
            {
                CancelSilent();
                return;
            }

            StopUnit(Offense);
            StopUnit(Defense);

            if (WasTap())
                RegisterTap();

            float dt = Time.unscaledDeltaTime;
            if (playerOnOffense)
            {
                // Defense grinds; offense tires without taps.
                offenseStamina -= (passiveDrainPerSecond + pressurePerSecond) * dt;
                defenseStamina -= passiveDrainPerSecond * 0.35f * dt;
            }
            else
            {
                // WRAP UP: taps hurt offense (blue). Keep your red bar alive longer
                // so mashing reads clearly instead of auto-losing to pressure.
                offenseStamina -= passiveDrainPerSecond * 0.25f * dt;
                defenseStamina -= (passiveDrainPerSecond * 0.55f + pressurePerSecond * 0.3f) * dt;
            }

            RefreshMeters();

            if (offenseStamina <= 0f)
            {
                Finish(offenseWon: false, showBanner: true);
                return;
            }

            if (defenseStamina <= 0f)
            {
                Finish(offenseWon: true, showBanner: true);
                return;
            }

            if (promptLabel != null)
            {
                float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
                var c = promptLabel.color;
                c.a = a;
                promptLabel.color = c;
            }
        }

        void FixedUpdate()
        {
            if (IsActive && ShowingPlayerUi)
            {
                StopUnit(Offense);
                StopUnit(Defense);
            }

            for (int i = 0; i < silentLocks.Count; i++)
            {
                StopUnit(silentLocks[i].Offense);
                StopUnit(silentLocks[i].Defense);
            }
        }

        void TickSilentLocks()
        {
            if (silentLocks.Count == 0) return;

            if (GameManager.Instance == null
                || GameManager.Instance.currentState != GameState.Playing
                || GameManager.Instance.isPreSnap
                || GameManager.Instance.waitingForNextPlay)
            {
                silentLocks.Clear();
                return;
            }

            for (int i = silentLocks.Count - 1; i >= 0; i--)
            {
                var lockPair = silentLocks[i];
                var off = lockPair.Offense;
                var def = lockPair.Defense;

                if (off == null || def == null
                    || !off.gameObject.activeInHierarchy
                    || !def.gameObject.activeInHierarchy
                    || PlayerStun.IsUnitStunned(off)
                    || PlayerStun.IsUnitStunned(def))
                {
                    silentLocks.RemoveAt(i);
                    continue;
                }

                StopUnit(off);
                StopUnit(def);

                if (Time.time < lockPair.EndsAt)
                    continue;

                silentLocks.RemoveAt(i);
                float winChance = lockPair.CarrierFight ? aiCarrierBreakChance : aiOffenseWinChance;
                bool offenseWon = UnityEngine.Random.value < winChance;
                ApplyBattleOutcome(
                    off, def, offenseWon,
                    lockPair.OnOffenseWon, lockPair.OnDefenseWon,
                    lockPair.CarrierFight, showBanner: false, taps: 0);
            }
        }

        bool WasTap()
        {
            return TecmoInput.BattleDown();
        }

        void RegisterTap()
        {
            tapCount++;
            if (playerOnOffense)
            {
                defenseStamina -= tapDamage;
                offenseStamina -= selfCostPerTap;
            }
            else
            {
                offenseStamina -= tapDamage;
                defenseStamina -= selfCostPerTap;
            }

            RefreshMeters();
        }

        bool ResolveAiWinner()
        {
            float chance = involvesBallCarrier ? aiCarrierBreakChance : aiOffenseWinChance;
            return UnityEngine.Random.value < chance;
        }

        void Finish(bool offenseWon, bool showBanner)
        {
            var off = Offense;
            var def = Defense;
            var wonCb = onOffenseWon;
            var lostCb = onDefenseWon;
            bool carrierFight = involvesBallCarrier;
            int taps = tapCount;
            bool clearTackle = ownsTackleLock;

            IsActive = false;
            ShowingPlayerUi = false;
            ownsTackleLock = false;
            Hide();
            ClearPair();

            if (clearTackle && GameManager.Instance != null)
                GameManager.Instance.inTackleBattle = false;

            ApplyBattleOutcome(off, def, offenseWon, wonCb, lostCb, carrierFight, showBanner, taps);
        }

        /// <summary>
        /// Shared win/lose push + callbacks. Safe for AI resolves that never
        /// owned the singleton pair / tackle lock.
        /// </summary>
        static void ApplyBattleOutcome(
            Transform off,
            Transform def,
            bool offenseWon,
            Action wonCb,
            Action lostCb,
            bool carrierFight,
            bool showBanner,
            int taps)
        {
            var winner = offenseWon ? off : def;
            var loser = offenseWon ? def : off;

            bool loserIsCarrier = IsBallCarrier(loser);

            if (offenseWon)
            {
                if (showBanner)
                    PlayBanner.Show("BROKE FREE!", 0.85f);

                ApplyStunPush(winner, loser, Instance != null ? Instance.winStunSeconds : 0.85f,
                    Instance != null ? Instance.winKnockback : 1.8f);

                var blocker = winner != null ? winner.GetComponent<OffensiveBlocker>() : null;
                if (blocker != null)
                    blocker.NotifyBattleWon(loser);

                var loserBlocker = loser != null ? loser.GetComponent<OffensiveBlocker>() : null;
                if (loserBlocker != null)
                    loserBlocker.NotifyBattleLost();

                wonCb?.Invoke();
            }
            else if (loserIsCarrier || carrierFight)
            {
                if (showBanner)
                    PlayBanner.Show("WRAPPED!", 0.85f);
                if (loser != null)
                    StopUnit(loser);
                lostCb?.Invoke();
            }
            else
            {
                if (showBanner)
                    PlayBanner.Show("WRAPPED!", 0.85f);

                ApplyStunPush(winner, loser, Instance != null ? Instance.loseStunSeconds : 0.7f,
                    Instance != null ? Instance.loseKnockback : 1.25f);

                var loserBlocker = loser != null ? loser.GetComponent<OffensiveBlocker>() : null;
                if (loserBlocker != null)
                    loserBlocker.NotifyBattleLost();

                var winBlocker = winner != null ? winner.GetComponent<OffensiveBlocker>() : null;
                if (winBlocker != null)
                    winBlocker.NotifyBattleWon(loser);

                lostCb?.Invoke();
            }

            Debug.Log(
                $"Contact battle: {(offenseWon ? "offense" : "defense")} wins" +
                $" ({taps} taps, ui={showBanner})");
        }

        static void ApplyStunPush(Transform winner, Transform loser, float stunSec, float knock)
        {
            if (loser == null) return;

            Vector3 away = loser.position - (winner != null ? winner.position : loser.position);
            away.z = 0f;
            if (away.sqrMagnitude < 0.01f)
            {
                bool loserIsDef = loser.GetComponent<DefenderAI>() != null;
                float drive = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
                // Defense knocked toward attack endzone; offense knocked backward.
                away = loserIsDef ? Vector3.right * drive : Vector3.right * (-drive);
            }

            away.Normalize();
            var stun = PlayerStun.GetOrAdd(loser.gameObject);
            stun.Stun(stunSec, away * knock);
        }

        void ClearPair()
        {
            Offense = null;
            Defense = null;
            onOffenseWon = null;
            onDefenseWon = null;
            tapCount = 0;
            involvesBallCarrier = false;
        }

        public void ForceCancel() => CancelSilent();

        void CancelSilent()
        {
            bool clearTackle = ownsTackleLock;
            IsActive = false;
            ShowingPlayerUi = false;
            ownsTackleLock = false;
            Hide();
            ClearPair();
            silentLocks.Clear();
            if (clearTackle && GameManager.Instance != null)
                GameManager.Instance.inTackleBattle = false;
        }

        static void StopUnit(Transform t)
        {
            if (t == null) return;
            var rb = t.GetComponent<Rigidbody>();
            ArcadeMove.Apply(rb, Vector3.zero);
        }

        public static bool IsPlayerUnit(Transform t)
        {
            if (t == null) return false;

            var pc = t.GetComponent<PlayerController>();
            if (pc != null && pc.IsControlled) return true;
            // Human offense QB with the ball (control may be cleared for the QTE).
            if (pc != null && pc.hasBall
                && FieldManager.Instance != null && FieldManager.Instance.isPlayerPossession)
                return true;

            var rc = t.GetComponent<ReceiverController>();
            if (rc != null && rc.IsPlayerControlled) return true;

            var def = t.GetComponent<DefenderAI>();
            if (def != null && def.IsPlayerControlled) return true;

            return false;
        }

        public static bool IsBallCarrier(Transform t)
        {
            if (t == null) return false;

            var rc = t.GetComponent<ReceiverController>();
            if (rc != null && rc.hasBall) return true;

            var pc = t.GetComponent<PlayerController>();
            if (pc != null && pc.hasBall) return true;

            try
            {
                if (t.CompareTag("BallCarrier")) return true;
            }
            catch (UnityException) { /* tag missing */ }

            return false;
        }

        public static bool IsDefenseSide(Transform t)
        {
            if (t == null) return false;
            try
            {
                if (t.CompareTag("Defender")) return true;
            }
            catch (UnityException) { /* tag missing */ }

            return t.GetComponent<DefenderAI>() != null;
        }

        public static bool IsOffenseSide(Transform t)
        {
            if (t == null) return false;
            if (IsDefenseSide(t)) return false;

            try
            {
                if (t.CompareTag("Receiver")
                    || t.CompareTag("Lineman")
                    || t.CompareTag("Player")
                    || t.CompareTag("BallCarrier"))
                    return true;
            }
            catch (UnityException) { /* tag missing */ }

            return t.GetComponent<ReceiverController>() != null
                   || t.GetComponent<OffensiveBlocker>() != null
                   || t.GetComponent<QuarterbackController>() != null
                   || t.GetComponent<PlayerController>() != null;
        }

        public static bool TryClassifySides(Transform a, Transform b, out Transform offense, out Transform defense)
        {
            offense = null;
            defense = null;
            if (a == null || b == null) return false;

            bool aOff = IsOffenseSide(a);
            bool bOff = IsOffenseSide(b);
            bool aDef = IsDefenseSide(a);
            bool bDef = IsDefenseSide(b);

            if (aOff && bDef)
            {
                offense = a;
                defense = b;
                return true;
            }

            if (bOff && aDef)
            {
                offense = b;
                defense = a;
                return true;
            }

            return false;
        }

        void RefreshMeters()
        {
            if (offenseFill != null)
                offenseFill.fillAmount = Mathf.Clamp01(offenseStamina / maxStamina);
            if (defenseFill != null)
                defenseFill.fillAmount = Mathf.Clamp01(defenseStamina / maxStamina);
            if (meterLabel != null)
            {
                string oName = Offense != null ? Offense.name : "OFF";
                string dName = Defense != null ? Defense.name : "DEF";
                meterLabel.text =
                    $"{oName} {Mathf.CeilToInt(Mathf.Max(0, offenseStamina))}  ·  " +
                    $"{dName} {Mathf.CeilToInt(Mathf.Max(0, defenseStamina))}  ·  {tapCount} taps";
            }
        }

        void EnsureUi()
        {
            if (root != null) return;

            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("ContactBattleCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 55;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            root = new GameObject("ContactBattleUI");
            root.transform.SetParent(canvas.transform, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0.18f);
            rootRt.anchorMax = new Vector2(0.5f, 0.18f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(760f, 170f);

            titleLabel = CreateText(root.transform, "Title", new Vector2(0f, 58f), 40f, FontStyles.Bold);
            promptLabel = CreateText(root.transform, "Prompt", new Vector2(0f, 20f), 24f, FontStyles.Bold);
            meterLabel = CreateText(root.transform, "MeterLabel", new Vector2(0f, -58f), 20f, FontStyles.Normal);

            offenseFill = CreateBar(root.transform, "OffenseBar", new Vector2(-150f, -22f),
                new Color(0.2f, 0.5f, 1f, 1f));
            defenseFill = CreateBar(root.transform, "DefenseBar", new Vector2(150f, -22f),
                new Color(0.95f, 0.25f, 0.2f, 1f));
        }

        static Image CreateBar(Transform parent, string name, Vector2 anchored, Color fillColor)
        {
            var barGo = new GameObject(name);
            barGo.transform.SetParent(parent, false);
            var barRt = barGo.AddComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.5f, 0.5f);
            barRt.anchorMax = new Vector2(0.5f, 0.5f);
            barRt.pivot = new Vector2(0.5f, 0.5f);
            barRt.anchoredPosition = anchored;
            barRt.sizeDelta = new Vector2(260f, 18f);
            var bg = barGo.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);
            bg.raycastTarget = false;

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barGo.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2f, 2f);
            fillRt.offsetMax = new Vector2(-2f, -2f);
            var fill = fillGo.AddComponent<Image>();
            fill.color = fillColor;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            return fill;
        }

        static TextMeshProUGUI CreateText(Transform parent, string name, Vector2 anchored, float size, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchored;
            rt.sizeDelta = new Vector2(720f, 44f);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            GameFonts.Apply(tmp);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.color = Color.white;
            tmp.outlineWidth = 0.22f;
            tmp.outlineColor = new Color(0f, 0f, 0f, 0.9f);
            tmp.raycastTarget = false;
            return tmp;
        }

        void Hide()
        {
            if (root != null)
                root.SetActive(false);
        }
    }
}
