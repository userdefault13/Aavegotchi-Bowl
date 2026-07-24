using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 5f;
        public float sprintSpeed = 8f;
        public float acceleration = 10f;
        private Vector3 velocity;
        private bool isSprinting = false;

        [Header("Ball Handling")]
        public bool hasBall = false;
        public GameObject ballObject;

        [Header("References")]
        private Rigidbody rb;
        private bool isControlled = true;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }
        }

        void Update()
        {
            if (!isControlled || GameManager.Instance.currentState != GameState.Playing)
            {
                return;
            }

            HandleInput();
        }

        void FixedUpdate()
        {
            if (!isControlled) return;

            rb.velocity = new Vector3(velocity.x, rb.velocity.y, velocity.z);
        }

        void HandleInput()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            isSprinting = Input.GetKey(KeyCode.LeftShift);
            float currentSpeed = isSprinting ? sprintSpeed : moveSpeed;

            Vector3 targetVelocity = new Vector3(horizontal, 0, vertical).normalized * currentSpeed;
            velocity = Vector3.Lerp(velocity, targetVelocity, acceleration * Time.deltaTime);

            if (velocity.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(velocity);
            }
        }

        public void GiveBall()
        {
            hasBall = true;
            if (ballObject != null)
            {
                ballObject.transform.SetParent(transform);
                ballObject.transform.localPosition = new Vector3(0.5f, 1f, 0.5f);
            }
        }

        public void RemoveBall()
        {
            hasBall = false;
            if (ballObject != null)
            {
                ballObject.transform.SetParent(null);
            }
        }

        public void SetControlled(bool controlled)
        {
            isControlled = controlled;
        }

        void OnCollisionEnter(Collision collision)
        {
            if (hasBall && collision.gameObject.CompareTag("Defender"))
            {
                Tackle();
            }
        }

        void Tackle()
        {
            Debug.Log("Tackled!");
            RemoveBall();
        }
    }
}
