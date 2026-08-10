using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using RetroBowl.Core;
using RetroBowl.UI;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Post-contact tackle battle: mash / tap to fill a break meter.
    /// Base chance is percentage-driven; taps raise the odds for extra yards.
    /// </summary>
    public class TackleBattle : MonoBehaviour
    {
        public static TackleBattle Instance { get; private set; }

        [Header("Timing")]
        public float battleDuration = 1.15f;
        public float breakImmunity = 0.85f;

        [Header("Odds")]
        [Range(0.05f, 0.8f)] public float baseBreakChance = 0.32f;
        [Range(0.02f, 0.25f)] public float chancePerTap = 0.09f;
        [Range(0.4f, 1f)] public float maxBreakChance = 0.92f;
        /// <summary>Sack / pocket pressure — harder to shed.</summary>
        [Range(0.05f, 0.5f)] public float sackBaseBreakChance = 0.14f;

        [Header("Input")]
        public KeyCode breakKey = KeyCode.Space;
        public KeyCode breakKeyAlt = KeyCode.E;

        public bool IsActive { get; private set; }

        TextMeshProUGUI titleLabel;
        TextMeshProUGUI promptLabel;
        TextMeshProUGUI meterLabel;
        Image meterFill;
        GameObject root;

        float endsAt;
        float currentChance;
        int tapCount;
        bool resolveOnNextFrame;
        Action onBroken;
        Action onTackled;
        Transform carrier;
        string tacklerName;
        bool aiCarrier;
        bool hardHit;

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
            var host = GameManager.Instance != null ? GameManager.Instance.gameObject : new GameObject("TackleBattle");
            if (host.GetComponent<TackleBattle>() == null)
                host.AddComponent<TackleBattle>();
        }

        /// <summary>Start a break-tackle QTE. Returns false if a battle is already running.</summary>
        public static bool Begin(
            Transform carrier,
            string tacklerName,
            Action onBroken,
            Action onTackled,
            bool isSack = false,
            bool aiCarrier = false,
            bool hardHit = false)
        {
            if (!GameRules.EnableTackleBattleQte)
                return false;
            EnsureExists();
            return Instance.BeginInternal(carrier, tacklerName, onBroken, onTackled, isSack, aiCarrier, hardHit);
        }

        bool BeginInternal(
            Transform carrier,
            string tacklerName,
            Action onBroken,
            Action onTackled,
            bool isSack,
            bool aiCarrier,
            bool hardHit)
        {
            if (IsActive) return false;
            if (GameManager.Instance != null && GameManager.Instance.waitingForNextPlay)
                return false;

            EnsureUi();
            IsActive = true;
            this.carrier = carrier;
            this.tacklerName = tacklerName;
            this.onBroken = onBroken;
            this.onTackled = onTackled;
            this.aiCarrier = aiCarrier;
            this.hardHit = hardHit;
            tapCount = 0;
            resolveOnNextFrame = false;

            // Dive / AI-carrier: much harder (or impossible) for offense to break.
            if (hardHit)
                currentChance = aiCarrier ? 0.04f : Mathf.Min(baseBreakChance * 0.35f, 0.12f);
            else if (aiCarrier)
                currentChance = isSack ? 0.1f : 0.18f;
            else
                currentChance = isSack ? sackBaseBreakChance : baseBreakChance;

            // Hard dive vs AI — resolve almost immediately as a tackle.
            float duration = hardHit && aiCarrier ? 0.35f : battleDuration;
            endsAt = Time.unscaledTime + duration;

            if (GameManager.Instance != null)
                GameManager.Instance.inTackleBattle = true;

            root.SetActive(true);
            titleLabel.text = isSack ? "SACK BATTLE!" : (hardHit ? "BIG HIT!" : "TACKLE!");
            if (aiCarrier)
                promptLabel.text = hardHit ? "DIVING TACKLE!" : "WRAPPING UP...";
            else
                promptLabel.text = "TAP SPACE / CLICK  ·  BREAK TACKLE";
            RefreshMeter();

            // Freeze carrier motion for the battle window.
            if (carrier != null)
            {
                var rb = carrier.GetComponent<Rigidbody>();
                ArcadeMove.Apply(rb, Vector3.zero);
            }

            return true;
        }

        void Update()
        {
            if (!IsActive) return;

            if (GameManager.Instance != null && GameManager.Instance.currentState != GameState.Playing)
            {
                CancelSilent();
                return;
            }

            // Only the human ball-carrier mashes to break — AI carriers cannot.
            if (!aiCarrier && WasBreakPressed())
                RegisterTap();

            float left = endsAt - Time.unscaledTime;
            if (left <= 0f || resolveOnNextFrame)
            {
                Resolve();
                return;
            }

            // Pulse prompt.
            if (promptLabel != null)
            {
                float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
                var c = promptLabel.color;
                c.a = a;
                promptLabel.color = c;
            }
        }

        bool WasBreakPressed()
        {
            return Input.GetKeyDown(breakKey)
                   || Input.GetKeyDown(breakKeyAlt)
                   || Input.GetMouseButtonDown(0)
                   || Input.GetKeyDown(KeyCode.Return);
        }

        void RegisterTap()
        {
            tapCount++;
            currentChance = Mathf.Min(maxBreakChance, currentChance + chancePerTap);
            RefreshMeter();

            // Early resolve if they max the meter.
            if (currentChance >= maxBreakChance - 0.001f)
                resolveOnNextFrame = true;
        }

        void RefreshMeter()
        {
            float pct = Mathf.Clamp01(currentChance);
            if (meterFill != null)
                meterFill.fillAmount = pct;
            if (meterLabel != null)
                meterLabel.text = $"{Mathf.RoundToInt(pct * 100f)}%  ·  {tapCount} taps";
        }

        void Resolve()
        {
            if (!IsActive) return;
            IsActive = false;

            bool broken = UnityEngine.Random.value < currentChance;
            Hide();

            if (GameManager.Instance != null)
                GameManager.Instance.inTackleBattle = false;

            var brokenCb = onBroken;
            var tackledCb = onTackled;
            onBroken = null;
            onTackled = null;

            if (broken)
            {
                PlayBanner.Show("BROKEN!", 0.9f);
                Debug.Log($"Tackle broken ({tapCount} taps, {Mathf.RoundToInt(currentChance * 100f)}% vs {tacklerName})");
                brokenCb?.Invoke();
            }
            else
            {
                PlayBanner.Show("TACKLED!", 0.8f);
                Debug.Log($"Tackle succeeded ({tapCount} taps, {Mathf.RoundToInt(currentChance * 100f)}% failed vs {tacklerName})");
                tackledCb?.Invoke();
            }

            carrier = null;
        }

        /// <summary>Abort the QTE without tackle/break callbacks (e.g. ball-carrier scored).</summary>
        public void CancelForScore()
        {
            if (!IsActive) return;
            CancelSilent();
            carrier = null;
        }

        void CancelSilent()
        {
            IsActive = false;
            Hide();
            onBroken = null;
            onTackled = null;
            carrier = null;
            if (GameManager.Instance != null)
                GameManager.Instance.inTackleBattle = false;
        }

        void EnsureUi()
        {
            if (root != null) return;

            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("TackleBattleCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 60;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            root = new GameObject("TackleBattleUI");
            root.transform.SetParent(canvas.transform, false);
            var rootRt = root.AddComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(0.5f, 0.22f);
            rootRt.anchorMax = new Vector2(0.5f, 0.22f);
            rootRt.pivot = new Vector2(0.5f, 0.5f);
            rootRt.sizeDelta = new Vector2(720f, 160f);

            titleLabel = CreateText(root.transform, "Title", new Vector2(0f, 52f), 44f, FontStyles.Bold);
            promptLabel = CreateText(root.transform, "Prompt", new Vector2(0f, 12f), 26f, FontStyles.Bold);
            meterLabel = CreateText(root.transform, "MeterLabel", new Vector2(0f, -52f), 22f, FontStyles.Normal);

            // Meter bar background.
            var barGo = new GameObject("MeterBar");
            barGo.transform.SetParent(root.transform, false);
            var barRt = barGo.AddComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0.5f, 0.5f);
            barRt.anchorMax = new Vector2(0.5f, 0.5f);
            barRt.pivot = new Vector2(0.5f, 0.5f);
            barRt.anchoredPosition = new Vector2(0f, -22f);
            barRt.sizeDelta = new Vector2(520f, 22f);
            var barBg = barGo.AddComponent<Image>();
            barBg.color = new Color(0.08f, 0.08f, 0.08f, 0.85f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(barGo.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero;
            fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(3f, 3f);
            fillRt.offsetMax = new Vector2(-3f, -3f);
            meterFill = fillGo.AddComponent<Image>();
            meterFill.color = new Color(0.95f, 0.75f, 0.15f, 1f);
            meterFill.type = Image.Type.Filled;
            meterFill.fillMethod = Image.FillMethod.Horizontal;
            meterFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            meterFill.fillAmount = 0.3f;
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
            rt.sizeDelta = new Vector2(700f, 48f);

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
