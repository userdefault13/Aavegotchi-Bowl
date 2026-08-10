using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// NES Tecmo offensive dive: locked burst (~1–3 yd), mash A to extend slide,
    /// ghost through bodies, then get-up. Defender touch during dive/get-up → tackle
    /// (unless carrier popcorns).
    /// </summary>
    public class OffenseDiveSlide
    {
        public enum Phase
        {
            None,
            Diving,
            GettingUp
        }

        public KeyCode diveKeyAlt = KeyCode.LeftControl;
        public float diveSpeed = 8.5f;
        public float minDiveSeconds = 0.18f;
        public float maxDiveSeconds = 0.38f;
        /// <summary>Hard cap on total dive time including mash extends.</summary>
        public float maxDiveTotalSeconds = 0.72f;
        public float mashExtendPerTap = 0.10f;
        public float mashExtendWhileHeldPerSecond = 0.22f;
        public float getUpSeconds = 2f;
        public float flickMinDownfield = 1.6f;
        public float flickMaxSeconds = 0.24f;
        public float minForwardDot = 0.35f;

        public Phase Current { get; private set; } = Phase.None;
        public bool IsDiving => Current == Phase.Diving;
        public bool IsGettingUp => Current == Phase.GettingUp;
        public bool IsBusy => Current != Phase.None;

        float endsAt;
        float diveStartedAt;
        Vector3 diveVelocity;
        bool flickTracking;
        float flickStartedAt;
        Vector3 flickStartPlay;
        Transform ghostHost;

        public void BindHost(Transform host) => ghostHost = host;

        public void Reset()
        {
            Current = Phase.None;
            diveVelocity = Vector3.zero;
            flickTracking = false;
            SetGhost(false);
        }

        /// <summary>
        /// Poll dive / get-up. Returns true the frame get-up finishes (caller resumes run).
        /// While diving/getting up, <paramref name="velocity"/> is the forced motion (0 when up).
        /// </summary>
        public bool Tick(
            bool canStart,
            Vector3 currentVelocity,
            Camera camera,
            out Vector3 velocity)
        {
            velocity = Vector3.zero;

            if (Current == Phase.Diving)
            {
                TryMashExtend();
                velocity = diveVelocity;
                if (Time.time >= endsAt)
                {
                    Current = Phase.GettingUp;
                    endsAt = Time.time + getUpSeconds;
                    diveVelocity = Vector3.zero;
                    velocity = Vector3.zero;
                    // Stay ghosted on the ground so OL doesn't shove; tackle uses range checks.
                    PlayBanner.Show("GET UP!", 0.7f, BannerTone.Neutral);
                }
                return false;
            }

            if (Current == Phase.GettingUp)
            {
                velocity = Vector3.zero;
                if (Time.time >= endsAt)
                {
                    Current = Phase.None;
                    SetGhost(false);
                    return true; // back on feet
                }
                return false;
            }

            if (!canStart)
            {
                flickTracking = false;
                return false;
            }

            if (TryConsumeStart(currentVelocity, camera, out Vector3 dir))
            {
                float secs = Random.Range(minDiveSeconds, maxDiveSeconds);
                Current = Phase.Diving;
                diveStartedAt = Time.time;
                endsAt = diveStartedAt + secs;
                diveVelocity = dir * diveSpeed;
                velocity = diveVelocity;
                SetGhost(true);
                float yards = diveSpeed * secs;
                Debug.Log($"Offense dive — ~{yards:0.0} yd burst (mash A to extend)");
            }

            return false;
        }

        void TryMashExtend()
        {
            if (!GameRules.EnableTecmoDiveMashExtend) return;
            float cap = diveStartedAt + maxDiveTotalSeconds;
            if (TecmoInput.ADown()
                || Input.GetKeyDown(diveKeyAlt)
                || Input.GetKeyDown(KeyCode.RightControl))
            {
                endsAt = Mathf.Min(endsAt + mashExtendPerTap, cap);
            }
            else if (TecmoDive.AExtendPressedOrHeld())
            {
                endsAt = Mathf.Min(endsAt + mashExtendWhileHeldPerSecond * Time.deltaTime, cap);
            }
        }

        void SetGhost(bool on)
        {
            if (ghostHost != null)
                TecmoDive.SetColliderGhost(ghostHost, on);
        }

        bool TryConsumeStart(Vector3 currentVelocity, Camera camera, out Vector3 dir)
        {
            dir = PreferDownfield(currentVelocity);

            // Tecmo A = dive (Z/F/RMB). J = tap-run free / battle in contact. K = battle. WASD = D-pad.
            if (TecmoInput.ADown()
                || Input.GetKeyDown(diveKeyAlt)
                || Input.GetKeyDown(KeyCode.RightControl))
                return true;

            return TryMouseFlickDive(camera, ref dir);
        }

        static Vector3 PreferDownfield(Vector3 currentVelocity)
        {
            var v = currentVelocity;
            v.z = 0f;
            float dir = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
            if (v.sqrMagnitude > 0.04f && v.normalized.x * dir >= 0.2f)
                return v.normalized;
            return Vector3.right * dir;
        }

        bool TryMouseFlickDive(Camera camera, ref Vector3 dir)
        {
            if (!TryPlayPlanePoint(camera, out Vector3 point))
            {
                flickTracking = false;
                return false;
            }

            var qb = GameObject.FindGameObjectWithTag("Player");
            if (qb != null)
            {
                var qbc = qb.GetComponent<QuarterbackController>();
                if (qbc != null && qbc.isAiming)
                {
                    flickTracking = false;
                    return false;
                }
            }

            if (Input.GetMouseButtonDown(0))
            {
                flickTracking = true;
                flickStartedAt = Time.time;
                flickStartPlay = point;
                return false;
            }

            if (!flickTracking)
                return false;

            if (Input.GetMouseButton(0))
            {
                if (Time.time - flickStartedAt > flickMaxSeconds)
                    flickTracking = false;
                return false;
            }

            if (!Input.GetMouseButtonUp(0))
                return false;

            flickTracking = false;
            float elapsed = Time.time - flickStartedAt;
            if (elapsed > flickMaxSeconds)
                return false;

            Vector3 delta = point - flickStartPlay;
            delta.z = 0f;
            float drive = FieldManager.Instance != null ? FieldManager.Instance.DriveDirX : 1f;
            if (delta.x * drive < flickMinDownfield)
                return false;

            Vector3 n = delta.normalized;
            if (n.x * drive < minForwardDot)
                return false;

            dir = n;
            return true;
        }

        static bool TryPlayPlanePoint(Camera camera, out Vector3 point)
        {
            point = Vector3.zero;
            if (camera == null)
                camera = Camera.main;
            if (camera == null) return false;

            Ray ray = camera.ScreenPointToRay(Input.mousePosition);
            var playPlane = new Plane(Vector3.forward, Vector3.zero);
            if (!playPlane.Raycast(ray, out float distance))
                return false;

            point = ray.GetPoint(distance);
            point.z = 0f;
            return true;
        }
    }
}
