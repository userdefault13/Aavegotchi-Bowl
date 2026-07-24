using UnityEngine;

namespace RetroBowl.Gameplay
{
    public class FootballBehavior : MonoBehaviour
    {
        [Header("Physics")]
        public float throwSpeed = 15f;
        public float gravity = 9.81f;
        private Rigidbody rb;
        
        [Header("State")]
        public bool isInAir = true;
        public bool isCaught = false;
        private Vector3 targetPosition;
        private float flightTime;
        
        void Start()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.useGravity = true;
        }

        public void ThrowToTarget(Vector3 target)
        {
            targetPosition = target;
            isInAir = true;

            Vector3 startPos = transform.position;
            Vector3 endPos = new Vector3(target.x, startPos.y, target.z);
            
            float distance = Vector3.Distance(startPos, endPos);
            flightTime = distance / throwSpeed;

            Vector3 direction = (endPos - startPos).normalized;
            float verticalVelocity = (gravity * flightTime) / 2f;
            
            Vector3 velocity = direction * throwSpeed + Vector3.up * verticalVelocity;
            rb.velocity = velocity;

            Vector3 angularVelocity = new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), Random.Range(10f, 20f));
            rb.angularVelocity = angularVelocity;
        }

        void Update()
        {
            if (isInAir && transform.position.y < 0.5f)
            {
                Land();
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (isInAir && !isCaught)
            {
                if (other.CompareTag("Receiver") || other.CompareTag("Player"))
                {
                    ReceiverController receiver = other.GetComponent<ReceiverController>();
                    if (receiver != null)
                    {
                        receiver.AttemptCatch(this);
                    }
                }
                else if (other.CompareTag("Defender"))
                {
                    Intercept();
                }
            }
        }

        public void Catch(Transform catcher)
        {
            isCaught = true;
            isInAir = false;
            
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            transform.SetParent(catcher);
            transform.localPosition = new Vector3(0.3f, 1f, 0.3f);
            transform.localRotation = Quaternion.Euler(-45f, 0f, 0f);

            Debug.Log("Ball Caught!");
        }

        void Land()
        {
            isInAir = false;
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            Debug.Log("Ball hit the ground - Incomplete Pass");
            Destroy(gameObject, 2f);
        }

        void Intercept()
        {
            Debug.Log("Intercepted!");
            isInAir = false;
            isCaught = true;
            
            Destroy(gameObject, 0.5f);
        }
    }
}
