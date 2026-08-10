using UnityEngine;
using UnityEngine.UI;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Tecmo mash-run: tap J when free to burst speed. WASD stays D-pad.
    /// Defense post-snap: left-click / tap also runs. On contact lock → battle mash.
    /// </summary>
    public class StaminaSprint : MonoBehaviour
    {
        public static StaminaSprint Active { get; private set; }

        [Header("Stamina")]
        public float maxStamina = 100f;
        public float stamina = 100f;
        public float drainPerTap = 9f;
        public float drainPerSecond = 26f;
        public float refillPerSecond = 28f;
        public float emptyCooldown = 0.7f;

        [Header("Boost")]
        public float boostWindow = 0.42f;
        public float sprintMultiplier = 1.6f;
        public KeyCode boostKey = KeyCode.J;
        public KeyCode boostKeyAlt = KeyCode.LeftShift;

        float boostUntil;
        float refillBlockedUntil;
        bool wasActive;

        public float Normalized => maxStamina <= 0f ? 0f : Mathf.Clamp01(stamina / maxStamina);
        public bool IsBoosting => GameRules.EnableStaminaSprint
                                  && Time.time < boostUntil
                                  && stamina > 0.5f;
        public float SpeedMultiplier => IsBoosting ? sprintMultiplier : 1f;

        void OnDisable()
        {
            if (Active == this) Active = null;
        }

        void Update()
        {
            bool canUse = CanAcceptInput();
            if (canUse)
            {
                Active = this;
                wasActive = true;
                if (WasBoostTap())
                    RegisterTap();
            }
            else if (wasActive && Active == this)
            {
                Active = null;
                wasActive = false;
                boostUntil = 0f;
            }

            float dt = Time.deltaTime;
            if (IsBoosting)
            {
                stamina -= drainPerSecond * dt;
                if (stamina <= 0f)
                {
                    stamina = 0f;
                    boostUntil = 0f;
                    refillBlockedUntil = Time.time + emptyCooldown;
                }
            }
            else if (Time.time >= refillBlockedUntil)
            {
                stamina = Mathf.Min(maxStamina, stamina + refillPerSecond * dt);
            }

            bool showHud = GameRules.EnableStaminaSprint && (canUse || Active == this);
            StaminaSprintHud.Ensure().Refresh(this, showHud);
        }

        bool CanAcceptInput()
        {
            if (!GameRules.EnableStaminaSprint) return false;
            if (GameManager.Instance == null) return false;
            if (GameManager.Instance.currentState != GameState.Playing) return false;
            if (GameManager.Instance.isPreSnap) return false;
            if (GameManager.Instance.waitingForNextPlay) return false;
            // Contact owns J → battle mash; don't burn stamina as run.
            if (TecmoInput.MashIsBattle()) return false;
            if (TecmoContact.IsInvolved(transform)) return false;
            if (GameManager.Instance.inTackleBattle) return false;
            if (ContactBattle.Instance != null && ContactBattle.Instance.ShowingPlayerUi) return false;

            var pc = GetComponent<PlayerController>();
            if (pc != null && pc.IsControlled)
            {
                var qbc = GetComponent<QuarterbackController>();
                if (qbc != null && qbc.hasThrown && !qbc.isScrambling)
                    return false;
                return true;
            }

            var rc = GetComponent<ReceiverController>();
            if (rc != null && rc.IsPlayerControlled && rc.hasBall)
                return true;

            var def = GetComponent<DefenderAI>();
            if (def != null && def.IsPlayerControlled)
                return true;

            // Kickoff gunner / defense unit steered by PlayerDefenseController.
            if (PlayerDefenseController.Instance != null
                && PlayerDefenseController.Instance.IsLocked
                && PlayerDefenseController.Instance.ControlledUnit == transform)
                return true;

            return false;
        }

        bool WasBoostTap()
        {
            // Tecmo B = run (J / X / Shift). Never WASD.
            return TecmoInput.BDown();
        }

        void RegisterTap()
        {
            if (stamina <= 0.5f)
            {
                refillBlockedUntil = Mathf.Max(refillBlockedUntil, Time.time + emptyCooldown * 0.35f);
                return;
            }

            stamina = Mathf.Max(0f, stamina - drainPerTap);
            boostUntil = Time.time + boostWindow;

            if (stamina <= 0f)
            {
                stamina = 0f;
                boostUntil = 0f;
                refillBlockedUntil = Time.time + emptyCooldown;
            }
        }

        public void ResetStamina()
        {
            stamina = maxStamina;
            boostUntil = 0f;
            refillBlockedUntil = 0f;
        }
    }

    /// <summary>Simple bottom stamina bar for the active ball-carrier.</summary>
    public class StaminaSprintHud : MonoBehaviour
    {
        static StaminaSprintHud instance;
        Image fill;
        GameObject root;

        public static StaminaSprintHud Ensure()
        {
            if (instance != null) return instance;
            var host = GameManager.Instance != null
                ? GameManager.Instance.gameObject
                : new GameObject("StaminaSprintHud");
            instance = host.GetComponent<StaminaSprintHud>();
            if (instance == null) instance = host.AddComponent<StaminaSprintHud>();
            return instance;
        }

        void Awake()
        {
            instance = this;
            EnsureUi();
            Hide();
        }

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public void Refresh(StaminaSprint sprint, bool show)
        {
            EnsureUi();
            if (!show || sprint == null)
            {
                Hide();
                return;
            }

            root.SetActive(true);
            float n = sprint.Normalized;
            if (fill != null)
            {
                fill.fillAmount = n;
                fill.color = Color.Lerp(
                    new Color(0.9f, 0.2f, 0.15f),
                    new Color(0.25f, 0.9f, 0.35f),
                    n);
                if (sprint.IsBoosting)
                    fill.color = Color.Lerp(fill.color, Color.white, 0.35f);
            }
        }

        void EnsureUi()
        {
            if (root != null) return;

            var canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("StaminaCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 40;
                canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            root = new GameObject("StaminaBar");
            root.transform.SetParent(canvas.transform, false);
            var rt = root.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 28f);
            rt.sizeDelta = new Vector2(280f, 18f);

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.05f, 0.75f);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(root.transform, false);
            var frt = fillGo.AddComponent<RectTransform>();
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(3f, 3f);
            frt.offsetMax = new Vector2(-3f, -3f);
            fill = fillGo.AddComponent<Image>();
            fill.color = new Color(0.25f, 0.9f, 0.35f);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
        }

        void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
