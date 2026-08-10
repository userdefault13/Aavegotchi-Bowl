using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// Stun / knockback for anyone losing a contact battle.
    /// Stunned players cannot move, block, or rush until it expires.
    /// </summary>
    public class PlayerStun : MonoBehaviour
    {
        public float knockbackDamping = 6f;

        float stunnedUntil;
        Vector3 knockVel;
        Rigidbody rb;

        public bool IsStunned => Time.time < stunnedUntil;
        public float Remaining => Mathf.Max(0f, stunnedUntil - Time.time);

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();
            ArcadeMove.ConfigureKinematicBody(rb);
        }

        public void Stun(float seconds, Vector3 knockback)
        {
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + Mathf.Max(0.05f, seconds));
            knockVel = knockback;
            knockVel.z = 0f;
            ArcadeMove.Apply(rb, knockVel);
        }

        public void Clear()
        {
            stunnedUntil = 0f;
            knockVel = Vector3.zero;
            ArcadeMove.Apply(rb, Vector3.zero);
        }

        void Update()
        {
            if (GameManager.Instance != null
                && (GameManager.Instance.isPreSnap || GameManager.Instance.waitingForNextPlay))
            {
                Clear();
                return;
            }

            if (!IsStunned)
            {
                knockVel = Vector3.zero;
                return;
            }

            knockVel = Vector3.Lerp(knockVel, Vector3.zero, knockbackDamping * Time.deltaTime);
            ArcadeMove.Apply(rb, knockVel);
        }

        public static PlayerStun GetOrAdd(GameObject go)
        {
            if (go == null) return null;
            var s = go.GetComponent<PlayerStun>();
            if (s == null) s = go.AddComponent<PlayerStun>();
            return s;
        }

        public static bool IsUnitStunned(Component c)
        {
            if (c == null) return false;
            var s = c.GetComponent<PlayerStun>();
            return s != null && s.IsStunned;
        }

        public static bool IsUnitStunned(GameObject go)
        {
            if (go == null) return false;
            var s = go.GetComponent<PlayerStun>();
            return s != null && s.IsStunned;
        }
    }
}
