using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    public class ReceiverController : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 6f;
        public float acceleration = 8f;
        private Vector3 velocity;

        [Header("Route")]
        public bool isRunningRoute = false;
        public Vector3[] routePoints;
        private int currentRoutePoint = 0;
        public float waypointThreshold = 1f;

        [Header("Catching")]
        public float catchRadius = 2f;
        public float catchChance = 0.7f;
        public bool hasBall = false;

        private Rigidbody rb;
        private bool isPlayerControlled = false;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }

            GenerateRoute();
        }

        void Update()
        {
            if (GameManager.Instance.currentState != GameState.Playing)
            {
                return;
            }

            if (isPlayerControlled)
            {
                HandlePlayerInput();
            }
            else if (isRunningRoute)
            {
                RunRoute();
            }
        }

        void FixedUpdate()
        {
            if (rb != null)
            {
                rb.velocity = new Vector3(velocity.x, rb.velocity.y, velocity.z);
            }
        }

        void HandlePlayerInput()
        {
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");

            Vector3 targetVelocity = new Vector3(horizontal, 0, vertical).normalized * moveSpeed;
            velocity = Vector3.Lerp(velocity, targetVelocity, acceleration * Time.deltaTime);

            if (velocity.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(velocity);
            }
        }

        void GenerateRoute()
        {
            int routeType = Random.Range(0, 3);
            Vector3 startPos = transform.position;

            switch (routeType)
            {
                case 0:
                    routePoints = new Vector3[]
                    {
                        startPos + Vector3.forward * 10f,
                        startPos + Vector3.forward * 10f + Vector3.right * 5f
                    };
                    break;
                case 1:
                    routePoints = new Vector3[]
                    {
                        startPos + Vector3.forward * 15f
                    };
                    break;
                case 2:
                    routePoints = new Vector3[]
                    {
                        startPos + Vector3.forward * 8f,
                        startPos + Vector3.forward * 8f + Vector3.left * 5f
                    };
                    break;
            }

            isRunningRoute = true;
            currentRoutePoint = 0;
        }

        void RunRoute()
        {
            if (routePoints == null || currentRoutePoint >= routePoints.Length)
            {
                velocity = Vector3.zero;
                return;
            }

            Vector3 targetPoint = routePoints[currentRoutePoint];
            Vector3 direction = (targetPoint - transform.position).normalized;
            
            velocity = direction * moveSpeed;

            if (velocity.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(velocity);
            }

            if (Vector3.Distance(transform.position, targetPoint) < waypointThreshold)
            {
                currentRoutePoint++;
                
                if (currentRoutePoint >= routePoints.Length)
                {
                    velocity = Vector3.zero;
                }
            }
        }

        public void AttemptCatch(FootballBehavior ball)
        {
            float distance = Vector3.Distance(transform.position, ball.transform.position);
            
            if (distance <= catchRadius)
            {
                float successRoll = Random.value;
                
                if (successRoll < catchChance)
                {
                    ball.Catch(transform);
                    hasBall = true;
                    isPlayerControlled = true;
                    isRunningRoute = false;
                    
                    Debug.Log($"{gameObject.name} caught the ball!");
                }
                else
                {
                    Debug.Log($"{gameObject.name} dropped the pass!");
                }
            }
        }

        public void SetPlayerControlled(bool controlled)
        {
            isPlayerControlled = controlled;
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
            Debug.Log($"{gameObject.name} was tackled!");
            hasBall = false;
            isPlayerControlled = false;
            
            int yardsGained = Random.Range(3, 15);
            FieldManager.Instance.AdvanceBall(yardsGained);
        }

        void OnDrawGizmosSelected()
        {
            if (routePoints != null)
            {
                Gizmos.color = Color.yellow;
                for (int i = 0; i < routePoints.Length; i++)
                {
                    Gizmos.DrawSphere(routePoints[i], 0.3f);
                    if (i > 0)
                    {
                        Gizmos.DrawLine(routePoints[i - 1], routePoints[i]);
                    }
                }
            }

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, catchRadius);
        }
    }
}
