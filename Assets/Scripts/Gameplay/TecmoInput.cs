using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Face buttons — kept off WASD so WASD stays a pure D-pad.
    /// Keyboard + Bluetooth / USB gamepad (New Input System <see cref="Gamepad"/>).
    ///
    /// Pad map (Xbox labels):
    ///   D-pad = move · hike on snap cadence
    ///   Right stick = throw aim only (and kick aim Y)
    ///   South (A) = dive · East (B) = run / kick / cycle prev
    ///   West (X) = battle / cycle next · Start = confirm
    ///   LB / RB = cycle prev / next
    /// </summary>
    public static class TecmoInput
    {
        public const float StickThrowStartDeadzone = 0.42f;
        public const float StickThrowHoldDeadzone = 0.22f;
        public const float StickAimDeadzone = 0.25f;
        public const float StickSensitivityMin = 0.25f;
        public const float StickSensitivityMax = 2f;
        public const float StickSensitivityDefault = 1f;

        const string StickSensitivityPref = "TecmoStickSensitivity";

        static Gamepad Pad => Gamepad.current;

        static float stickMagPrev;
        static float stickMagCur;
        static int stickSampleFrame = -1;
        static float cachedStickSensitivity = -1f;

        public static bool MoveLeftDown()
        {
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))
                return true;
            var kb = Keyboard.current;
            if (kb != null && (kb.aKey.wasPressedThisFrame || kb.leftArrowKey.wasPressedThisFrame))
                return true;
            var pad = Pad;
            return pad != null && pad.dpad.left.wasPressedThisFrame;
        }

        public static bool MoveRightDown()
        {
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))
                return true;
            var kb = Keyboard.current;
            if (kb != null && (kb.dKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame))
                return true;
            var pad = Pad;
            return pad != null && pad.dpad.right.wasPressedThisFrame;
        }

        public static bool MoveUpDown()
        {
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow))
                return true;
            var kb = Keyboard.current;
            if (kb != null && (kb.wKey.wasPressedThisFrame || kb.upArrowKey.wasPressedThisFrame))
                return true;
            var pad = Pad;
            return pad != null && pad.dpad.up.wasPressedThisFrame;
        }

        public static bool MoveDownDown()
        {
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow))
                return true;
            var kb = Keyboard.current;
            if (kb != null && (kb.sKey.wasPressedThisFrame || kb.downArrowKey.wasPressedThisFrame))
                return true;
            var pad = Pad;
            return pad != null && pad.dpad.down.wasPressedThisFrame;
        }

        /// <summary>Controller right-stick aim sensitivity (0.25–2). Persisted in PlayerPrefs.</summary>
        public static float StickSensitivity
        {
            get
            {
                if (cachedStickSensitivity < 0f)
                {
                    cachedStickSensitivity = Mathf.Clamp(
                        PlayerPrefs.GetFloat(StickSensitivityPref, StickSensitivityDefault),
                        StickSensitivityMin,
                        StickSensitivityMax);
                }

                return cachedStickSensitivity;
            }
            set
            {
                cachedStickSensitivity = Mathf.Clamp(value, StickSensitivityMin, StickSensitivityMax);
                PlayerPrefs.SetFloat(StickSensitivityPref, cachedStickSensitivity);
                PlayerPrefs.Save();
            }
        }

        static void SampleStick()
        {
            int f = Time.frameCount;
            if (stickSampleFrame == f) return;
            stickSampleFrame = f;
            stickMagPrev = stickMagCur;
            stickMagCur = ReadThrowStick().magnitude;
        }

        /// <summary>A-button down (dive, jump INT). Never KeyCode.A / J / K.</summary>
        public static bool ADown()
        {
            if (Input.GetKeyDown(KeyCode.Z)
                || Input.GetKeyDown(KeyCode.F)
                || Input.GetKeyDown(KeyCode.JoystickButton0)
                || Input.GetMouseButtonDown(1))
                return true;

            var pad = Pad;
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        /// <summary>A held — extend NES dive / slide while mashed.</summary>
        public static bool AHeld()
        {
            if (Input.GetKey(KeyCode.Z)
                || Input.GetKey(KeyCode.F)
                || Input.GetKey(KeyCode.JoystickButton0)
                || Input.GetMouseButton(1))
                return true;

            var pad = Pad;
            return pad != null && pad.buttonSouth.isPressed;
        }

        public static bool MashIsBattle()
        {
            if (TecmoContact.Instance != null && TecmoContact.Instance.IsActive)
                return true;
            if (ContactBattle.Instance != null && ContactBattle.Instance.ShowingPlayerUi)
                return true;
            if (TackleBattle.Instance != null && TackleBattle.Instance.IsActive)
                return true;
            if (GameManager.Instance != null && GameManager.Instance.inTackleBattle)
                return true;
            return false;
        }

        public static bool IsDefenseLivePlay()
        {
            var pdc = PlayerDefenseController.Instance;
            if (pdc == null || !pdc.IsActive || !pdc.IsLocked)
                return false;
            var gm = GameManager.Instance;
            if (gm == null || gm.currentState != GameState.Playing)
                return false;
            if (gm.waitingForNextPlay || gm.pendingQuarterEnd)
                return false;
            if (!pdc.IsKickoffCoverage && gm.isPreSnap)
                return false;
            return true;
        }

        public static bool IsKickMeterActive()
        {
            var kc = KickingController.Instance;
            return kc != null
                   && kc.IsActive
                   && !kc.IsBallInFlight
                   && !kc.IsKickerApproaching;
        }

        /// <summary>
        /// How many pointers are active for aim (mouse buttons + touches).
        /// Two fingers / LMB+RMB → bullet pass.
        /// </summary>
        public static int AimPointerCount()
        {
            // Prefer real touch contacts when present (trackpad / touchscreen).
            if (Input.touchCount > 0)
                return Input.touchCount;

            var ts = Touchscreen.current;
            if (ts != null)
            {
                int n = 0;
                for (int i = 0; i < ts.touches.Count; i++)
                {
                    if (ts.touches[i].isInProgress)
                        n++;
                }
                if (n > 0) return n;
            }

            int buttons = 0;
            if (Input.GetMouseButton(0)) buttons++;
            if (Input.GetMouseButton(1)) buttons++;
            if (Input.GetMouseButton(2)) buttons++;
            return buttons;
        }

        public static bool PointerTapDown()
        {
            bool pressed = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            if (!pressed && Input.touchCount > 0)
            {
                // Cast to int — avoids CS0104 between UnityEngine.TouchPhase and
                // UnityEngine.InputSystem.TouchPhase (Began == 0).
                pressed = (int)Input.GetTouch(0).phase == 0;
            }
            if (!pressed) return false;

            if (GameManager.Instance != null && GameManager.Instance.SuppressPlayClick)
                return false;
            // Mash UI sits under a GraphicRaycaster — don't eat battle taps.
            if (!MashIsBattle()
                && EventSystem.current != null
                && EventSystem.current.IsPointerOverGameObject())
                return false;
            return true;
        }

        /// <summary>Menu / banner / snap confirm — Space · Enter · click · Start.</summary>
        public static bool ConfirmDown()
        {
            if (Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || PointerTapDown())
                return true;

            var pad = Pad;
            return pad != null && pad.startButton.wasPressedThisFrame;
        }

        /// <summary>
        /// Menu cursor confirm — keyboard / Start / South (A).
        /// Excludes mouse click so Button.onClick still owns pointer.
        /// </summary>
        public static bool MenuConfirmDown()
        {
            if (Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter))
                return true;

            var pad = Pad;
            if (pad == null) return false;
            return pad.startButton.wasPressedThisFrame
                   || pad.buttonSouth.wasPressedThisFrame;
        }

        /// <summary>J / East (B) = kick while the meter is up.</summary>
        public static bool KickDown()
        {
            if (Input.GetKeyDown(KeyCode.J)
                || Input.GetKeyDown(KeyCode.X)
                || Input.GetKeyDown(KeyCode.JoystickButton1))
                return true;

            var pad = Pad;
            return pad != null && pad.buttonEast.wasPressedThisFrame;
        }

        public static bool CyclePrevDown()
        {
            if (Input.GetKeyDown(KeyCode.J)
                || Input.GetKeyDown(KeyCode.X)
                || Input.GetKeyDown(KeyCode.JoystickButton1)
                || Input.GetKeyDown(KeyCode.LeftShift))
                return true;

            var pad = Pad;
            if (pad == null) return false;
            return pad.buttonEast.wasPressedThisFrame
                   || pad.leftShoulder.wasPressedThisFrame;
        }

        public static bool CycleNextDown()
        {
            if (Input.GetKeyDown(KeyCode.K)
                || Input.GetKeyDown(KeyCode.JoystickButton2))
                return true;

            var pad = Pad;
            if (pad == null) return false;
            return pad.buttonWest.wasPressedThisFrame
                   || pad.rightShoulder.wasPressedThisFrame;
        }

        static bool RunKeysDown()
        {
            if (Input.GetKeyDown(KeyCode.J)
                || Input.GetKeyDown(KeyCode.X)
                || Input.GetKeyDown(KeyCode.JoystickButton1)
                || Input.GetKeyDown(KeyCode.LeftShift)
                || Input.GetKeyDown(KeyCode.RightShift))
                return true;

            var pad = Pad;
            return pad != null && pad.buttonEast.wasPressedThisFrame;
        }

        static bool RunKeysHeld()
        {
            if (Input.GetKey(KeyCode.J)
                || Input.GetKey(KeyCode.X)
                || Input.GetKey(KeyCode.JoystickButton1)
                || Input.GetKey(KeyCode.LeftShift)
                || Input.GetKey(KeyCode.RightShift))
                return true;

            var pad = Pad;
            return pad != null && pad.buttonEast.isPressed;
        }

        public static bool BDown()
        {
            if (RunKeysDown()) return true;
            if (IsDefenseLivePlay() && !MashIsBattle() && PointerTapDown())
                return true;
            return false;
        }

        public static bool BHeld() => RunKeysHeld();

        /// <summary>Mash tap for ContactBattle / TecmoContact / TackleBattle.</summary>
        public static bool BattleDown()
        {
            // K / E / West always count as battle (outside mash K is also cycle-next pre-snap).
            if (Input.GetKeyDown(KeyCode.K)
                || Input.GetKeyDown(KeyCode.JoystickButton2)
                || Input.GetKeyDown(KeyCode.E))
                return true;

            var pad = Pad;
            if (pad != null && pad.buttonWest.wasPressedThisFrame)
                return true;

            // J / click / Space only while a mash UI is up (J is run otherwise).
            if (!MashIsBattle()) return false;

            if (Input.GetKeyDown(KeyCode.J)
                || Input.GetKeyDown(KeyCode.X)
                || Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.KeypadEnter)
                || Input.GetKeyDown(KeyCode.LeftShift)
                || Input.GetKeyDown(KeyCode.RightShift)
                || Input.GetKeyDown(KeyCode.JoystickButton1))
                return true;

            if (pad != null
                && (pad.buttonEast.wasPressedThisFrame || pad.buttonSouth.wasPressedThisFrame))
                return true;

            if (PointerTapDown()) return true;
            return false;
        }

        /// <summary>
        /// Hike / snap: any D-pad (or WASD / arrow) press.
        /// Right stick is aim-only after the ball is snapped.
        /// </summary>
        public static bool HikeDown()
        {
            return MoveLeftDown()
                   || MoveRightDown()
                   || MoveUpDown()
                   || MoveDownDown();
        }

        /// <summary>Digital move from gamepad D-pad only (stick is for throwing).</summary>
        public static Vector2 ReadGamepadDpadDigital()
        {
            var pad = Pad;
            if (pad == null) return Vector2.zero;

            float x = 0f;
            float y = 0f;
            if (pad.dpad.left.isPressed) x = -1f;
            if (pad.dpad.right.isPressed) x = 1f;
            if (pad.dpad.up.isPressed) y = 1f;
            if (pad.dpad.down.isPressed) y = -1f;
            return new Vector2(x, y);
        }

        /// <summary>
        /// Right stick for throw / hike aim. Y inverted, scaled by
        /// <see cref="StickSensitivity"/>, clamped to unit circle.
        /// </summary>
        public static Vector2 ReadThrowStick()
        {
            var pad = Pad;
            if (pad == null) return Vector2.zero;
            Vector2 v = pad.rightStick.ReadValue();
            v.y = -v.y;
            v *= StickSensitivity;
            return Vector2.ClampMagnitude(v, 1f);
        }

        /// <summary>True the frame right stick crosses the throw-start deadzone.</summary>
        public static bool ThrowStickPressedThisFrame()
        {
            SampleStick();
            return stickMagPrev < StickThrowStartDeadzone
                   && stickMagCur >= StickThrowStartDeadzone;
        }

        /// <summary>True while right stick is past the hold deadzone (aiming a pass).</summary>
        public static bool ThrowStickHeld()
        {
            SampleStick();
            return stickMagCur >= StickThrowHoldDeadzone;
        }

        /// <summary>Kick aim Y (-1..+1): keys, D-pad, or right stick Y (sensitivity applied).</summary>
        public static float AimY()
        {
            float y = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) y -= 1f;

            var pad = Pad;
            if (pad != null)
            {
                if (pad.dpad.up.isPressed) y = 1f;
                else if (pad.dpad.down.isPressed) y = -1f;
                else
                {
                    float sy = ReadThrowStick().y;
                    if (Mathf.Abs(sy) >= StickAimDeadzone)
                        y = Mathf.Sign(sy);
                }
            }

            return Mathf.Clamp(y, -1f, 1f);
        }

        public static bool RunDown() => BDown();
        public static bool RunHeld() => BHeld();
    }
}
