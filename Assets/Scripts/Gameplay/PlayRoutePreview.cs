using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Pre-snap route preview (Retro Bowl style).
    /// Hold/toggle with Space or Right Mouse to show WR/TE/RB routes.
    /// </summary>
    public class PlayRoutePreview : MonoBehaviour
    {
        public static PlayRoutePreview Instance { get; private set; }

        [Header("Look")]
        public Color routeColor = new Color(0.35f, 0.95f, 0.45f, 0.95f);
        public float lineWidth = 0.12f;

        bool visible;
        bool routesPrepared;
        readonly System.Collections.Generic.List<LineRenderer> lines = new();

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ClearLines();
        }

        void Update()
        {
            if (GameManager.Instance == null || GameManager.Instance.currentState != GameState.Playing)
            {
                Hide();
                return;
            }

            // Only on pre-snap (play call / cadence).
            if (!GameManager.Instance.isPreSnap)
            {
                Hide();
                return;
            }

            // Don't steal keys while the Tecmo play-call modal is open.
            if (RetroBowl.UI.PlayCallingUI.Instance != null
                && RetroBowl.UI.PlayCallingUI.Instance.IsModalOpen)
            {
                Hide();
                return;
            }

            bool pressed = Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(1);
            if (pressed)
            {
                if (visible) Hide();
                else Show();
            }

            if (visible)
                RefreshLinePositions();
        }

        /// <summary>Call when a play is selected so preview matches the playbook package.</summary>
        public void PrepareSelectedPlayRoutes()
        {
            if (Playbook.Selected != null)
                Playbook.ApplyRoutesToFormation();
            else
            {
                foreach (var go in FindReceivers())
                {
                    var rc = go.GetComponent<ReceiverController>();
                    if (rc != null) rc.GenerateRoute();
                }
            }

            routesPrepared = true;
            RebuildLines();
            // Don't auto-show — wait for Space / RMB.
            SetLinesVisible(false);
            visible = false;
        }

        /// <summary>Legacy alias — prefer <see cref="PrepareSelectedPlayRoutes"/>.</summary>
        public void PreparePassRoutes() => PrepareSelectedPlayRoutes();

        public void Show()
        {
            if (!routesPrepared)
                PrepareSelectedPlayRoutes();
            else
                RebuildLines();

            visible = true;
            SetLinesVisible(true);
        }

        public void Invalidate()
        {
            routesPrepared = false;
            Hide();
            ClearLines();
        }

        public void Hide()
        {
            visible = false;
            SetLinesVisible(false);
        }

        void RebuildLines()
        {
            ClearLines();

            foreach (var go in FindReceivers())
            {
                var rc = go.GetComponent<ReceiverController>();
                if (rc == null || rc.routePoints == null || rc.routePoints.Length == 0)
                    continue;

                var lr = CreateLine(go.name + "_Route");
                // Start at player, then waypoints.
                int count = rc.routePoints.Length + 1;
                lr.positionCount = count;
                lr.SetPosition(0, Flat(go.transform.position));
                for (int i = 0; i < rc.routePoints.Length; i++)
                    lr.SetPosition(i + 1, Flat(rc.routePoints[i]));

                lines.Add(lr);
            }
        }

        void RefreshLinePositions()
        {
            // Keep the start point glued to each receiver while formation is locked.
            int idx = 0;
            foreach (var go in FindReceivers())
            {
                if (idx >= lines.Count) break;
                var rc = go.GetComponent<ReceiverController>();
                if (rc == null || rc.routePoints == null || rc.routePoints.Length == 0)
                    continue;

                var lr = lines[idx];
                if (lr == null) { idx++; continue; }

                lr.SetPosition(0, Flat(go.transform.position));
                for (int i = 0; i < rc.routePoints.Length && i + 1 < lr.positionCount; i++)
                    lr.SetPosition(i + 1, Flat(rc.routePoints[i]));
                idx++;
            }
        }

        LineRenderer CreateLine(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = routeColor;
            lr.endColor = new Color(routeColor.r, routeColor.g, routeColor.b, 0.55f);
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth * 0.65f;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.sortingOrder = 15;
            lr.useWorldSpace = true;
            lr.enabled = visible;
            return lr;
        }

        void SetLinesVisible(bool on)
        {
            foreach (var lr in lines)
            {
                if (lr != null) lr.enabled = on;
            }
        }

        void ClearLines()
        {
            foreach (var lr in lines)
            {
                if (lr != null) Destroy(lr.gameObject);
            }
            lines.Clear();
        }

        static Vector3 Flat(Vector3 p) => new Vector3(p.x, p.y, 0f);

        static System.Collections.Generic.List<GameObject> FindReceivers()
        {
            var list = new System.Collections.Generic.List<GameObject>();
            try
            {
                foreach (var go in GameObject.FindGameObjectsWithTag("Receiver"))
                {
                    if (go != null && go.activeInHierarchy)
                        list.Add(go);
                }
            }
            catch { /* tag missing */ }

            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
        }
    }
}
