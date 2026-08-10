using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// While the QB still has the ball (or the pass is live before a catch
    /// attempt resolves), CB/S ↔ WR/TE pairs ignore collider contact so they
    /// do not contest / block mid-route. After a catch attempt (complete, drop,
    /// or incomplete), collisions are restored for contact battles.
    /// OL/DL engagements are untouched — only secondary vs skill receivers.
    /// </summary>
    public class PassCollisionGate : MonoBehaviour
    {
        public static PassCollisionGate Instance { get; private set; }

        bool catchResolved;
        bool currentlyIgnoring;
        Collider[] secondaryCols = System.Array.Empty<Collider>();
        Collider[] receiverCols = System.Array.Empty<Collider>();

        static readonly string[] SecondaryNames = { "CB_Top", "CB_Bot", "S" };
        static readonly string[] ReceiverNames = { "WR_Top", "WR_Bot", "TE" };

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RestoreAll();
        }

        void LateUpdate()
        {
            if (GameManager.Instance == null
                || GameManager.Instance.currentState != GameState.Playing
                || GameManager.Instance.isPreSnap
                || GameManager.Instance.waitingForNextPlay)
            {
                if (currentlyIgnoring)
                    RestoreAll();
                catchResolved = false;
                return;
            }

            bool shouldIgnore = ShouldIgnoreContact();
            if (shouldIgnore && !currentlyIgnoring)
                ApplyIgnore(true);
            else if (!shouldIgnore && currentlyIgnoring)
                ApplyIgnore(false);
        }

        bool ShouldIgnoreContact()
        {
            if (catchResolved) return false;

            // Pre-throw pocket: free release for WRs vs CBs.
            if (OffensiveBlocker.QbStillHasBall())
                return true;

            // Pass in the air until a catch attempt resolves (or incomplete).
            FootballBehavior ball = FindLiveBall();
            if (ball != null && ball.isInAir && !ball.isCaught && !ball.isBouncing)
                return true;

            return false;
        }

        static FootballBehavior FindLiveBall()
        {
            try
            {
                var go = GameObject.FindGameObjectWithTag("Football");
                if (go != null) return go.GetComponent<FootballBehavior>();
            }
            catch { /* tag missing */ }

            var all = Object.FindObjectsByType<FootballBehavior>(FindObjectsInactive.Exclude);
            foreach (var fb in all)
            {
                if (fb == null || !fb.gameObject.scene.IsValid()) continue;
                if (fb.isInAir || fb.isBouncing) return fb;
            }
            return null;
        }

        /// <summary>Call when a receiver attempts a catch (hit or drop) or contest ends.</summary>
        public static void NotifyCatchAttemptResolved()
        {
            if (Instance == null) return;
            Instance.catchResolved = true;
            Instance.ApplyIgnore(false);
        }

        /// <summary>Reset at the start of each play / snap.</summary>
        public static void ResetForPlay()
        {
            if (Instance == null) return;
            Instance.catchResolved = false;
            Instance.ApplyIgnore(false);
        }

        /// <summary>
        /// True while CB/S ↔ WR/TE contact should not start a ContactBattle
        /// (QB still has the ball or pass is live before catch resolves).
        /// </summary>
        public static bool ShouldBlockBattle(Transform a, Transform b)
        {
            if (Instance == null || a == null || b == null) return false;
            if (!Instance.ShouldIgnoreContact()) return false;
            return (IsSecondaryUnit(a) && IsSkillReceiver(b))
                   || (IsSecondaryUnit(b) && IsSkillReceiver(a));
        }

        static bool IsSecondaryUnit(Transform t)
        {
            if (t == null) return false;
            for (int i = 0; i < SecondaryNames.Length; i++)
            {
                if (t.name == SecondaryNames[i] || t.name.StartsWith(SecondaryNames[i]))
                    return true;
            }

            return false;
        }

        static bool IsSkillReceiver(Transform t)
        {
            if (t == null) return false;
            for (int i = 0; i < ReceiverNames.Length; i++)
            {
                if (t.name == ReceiverNames[i] || t.name.StartsWith(ReceiverNames[i]))
                    return true;
            }

            // Generic receiver tag (route runners).
            try
            {
                if (t.CompareTag("Receiver")) return true;
            }
            catch (UnityException) { /* tag missing */ }

            return t.GetComponent<ReceiverController>() != null
                   && t.GetComponent<OffensiveBlocker>() == null;
        }

        void ApplyIgnore(bool ignore)
        {
            CacheColliders();
            for (int s = 0; s < secondaryCols.Length; s++)
            {
                var a = secondaryCols[s];
                if (a == null) continue;
                for (int r = 0; r < receiverCols.Length; r++)
                {
                    var b = receiverCols[r];
                    if (b == null) continue;
                    Physics.IgnoreCollision(a, b, ignore);
                }
            }
            currentlyIgnoring = ignore;
        }

        void RestoreAll() => ApplyIgnore(false);

        void CacheColliders()
        {
            secondaryCols = new Collider[SecondaryNames.Length];
            for (int i = 0; i < SecondaryNames.Length; i++)
            {
                var go = GameObject.Find(SecondaryNames[i]);
                secondaryCols[i] = go != null ? go.GetComponent<Collider>() : null;
            }

            receiverCols = new Collider[ReceiverNames.Length];
            for (int i = 0; i < ReceiverNames.Length; i++)
            {
                var go = GameObject.Find(ReceiverNames[i]);
                receiverCols[i] = go != null ? go.GetComponent<Collider>() : null;
            }
        }
    }
}
