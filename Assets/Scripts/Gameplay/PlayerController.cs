using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    /// <summary>
    /// 2D Retro Bowl movement: X = downfield, Y = across field.
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 2.8f;
        public float sprintSpeed = 3.8f;
        public float acceleration = 10f;
        Vector3 velocity;
        bool isSprinting;

        [Header("Ball Handling")]
        public bool hasBall;
        public GameObject ballObject;

        Rigidbody rb;
        SpriteRenderer spriteRenderer;
        bool isControlled = true;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
                rb = gameObject.AddComponent<Rigidbody>();

            ArcadeMove.ConfigureKinematicBody(rb);
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        void Update()
        {
            if (!isControlled || GameManager.Instance == null
                || GameManager.Instance.currentState != GameState.Playing
                || GameManager.Instance.isPreSnap
                || GameManager.Instance.isKicking
                || GameManager.Instance.isKickoffReturn
                || GameManager.Instance.waitingForNextPlay
                || GameManager.Instance.inTackleBattle
                || ContactBattle.IsCombatant(transform)
                || TecmoContact.IsInvolved(transform))
            {
                velocity = Vector3.zero;
                return;
            }

            HandleInput();
        }

        void FixedUpdate()
        {
            if (rb == null) return;

            // Uncontrolled (AI offense / handed off) — leave Rigidbody to other systems.
            if (!isControlled)
                return;

            if (GameManager.Instance == null
                || GameManager.Instance.currentState != GameState.Playing
                || GameManager.Instance.isPreSnap
                || GameManager.Instance.isKicking
                || GameManager.Instance.isKickoffReturn
                || GameManager.Instance.waitingForNextPlay
                || GameManager.Instance.inTackleBattle
                || ContactBattle.IsCombatant(transform)
                || TecmoContact.IsInvolved(transform))
            {
                velocity = Vector3.zero;
                ArcadeMove.Apply(rb, Vector3.zero);
                return;
            }

            ArcadeMove.Apply(rb, velocity);
        }

        void HandleInput()
        {
            var stamina = GetComponent<StaminaSprint>();
            bool boosting = stamina != null && stamina.IsBoosting;
            float currentSpeed = boosting ? sprintSpeed : moveSpeed;
            isSprinting = boosting;

            // Digital WASD/arrows — hard stop the frame keys release.
            Vector2 input = ArcadeMove.ReadDigitalPlanar();
            if (input.sqrMagnitude < 0.01f)
                velocity = Vector3.zero;
            else
                velocity = new Vector3(input.x, input.y, 0f).normalized * currentSpeed;

            if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
                spriteRenderer.flipX = velocity.x < 0f;
        }

        public void GiveBall()
        {
            hasBall = true;
            if (ballObject == null)
            {
                FootballBehavior.AttachHeldTo(transform);
                return;
            }

            var fb = ballObject.GetComponent<FootballBehavior>();
            if (fb != null)
            {
                fb.Catch(transform);
                return;
            }

            ballObject.transform.SetParent(transform);
            var offset = FootballBehavior.HandCarryOffset(transform);
            ballObject.transform.position = transform.position + offset;
            ballObject.transform.localPosition = transform.InverseTransformPoint(
                transform.position + offset);
        }

        public void RemoveBall()
        {
            hasBall = false;
            if (ballObject == null) return;

            var fb = ballObject.GetComponent<FootballBehavior>();
            if (fb != null)
                fb.ReleaseFromCarrier();
            else
                ballObject.transform.SetParent(null);

            ballObject = null;
        }

        public void SetControlled(bool controlled) => isControlled = controlled;
        public bool IsControlled => isControlled;

        /// <summary>
        /// True when a football is actually attached / caught on this unit.
        /// </summary>
        public bool HasBallAttached()
        {
            if (ballObject != null)
            {
                var fb = ballObject.GetComponent<FootballBehavior>();
                if (fb != null && fb.isCaught && fb.transform.parent == transform)
                    return true;
                if (ballObject.transform.parent == transform)
                    return true;
            }

            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                if (child != null && child.GetComponent<FootballBehavior>() != null)
                    return true;
            }

            return false;
        }
    }
}
