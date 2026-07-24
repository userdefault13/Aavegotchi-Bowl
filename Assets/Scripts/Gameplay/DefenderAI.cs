using UnityEngine;

namespace RetroBowl.Gameplay
{
    public class DefenderAI : MonoBehaviour
    {
        [Header("AI Behavior")]
        public float moveSpeed = 5f;
        public float detectionRadius = 15f;
        public float tackleRadius = 1.5f;
        
        [Header("Target Priority")]
        public Transform ballCarrier;
        public Transform ballTarget;
        private Transform currentTarget;

        [Header("AI State")]
        public DefenderState currentState = DefenderState.Patrolling;
        private Vector3 patrolPosition;
        private Rigidbody rb;

        [Header("Stats")]
        public int speed = 70;
        public int awareness = 60;
        public int tackling = 65;

        void Start()
        {
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotation;
            }

            SetRandomPatrolPosition();
        }

        void Update()
        {
            UpdateAI();
        }

        void UpdateAI()
        {
            FindTarget();

            switch (currentState)
            {
                case DefenderState.Patrolling:
                    Patrol();
                    break;
                case DefenderState.Pursuing:
                    Pursue();
                    break;
                case DefenderState.Covering:
                    CoverReceiver();
                    break;
            }
        }

        void FindTarget()
        {
            GameObject ballCarrierObj = GameObject.FindGameObjectWithTag("BallCarrier");
            if (ballCarrierObj != null)
            {
                ballCarrier = ballCarrierObj.transform;
                currentTarget = ballCarrier;
                currentState = DefenderState.Pursuing;
                return;
            }

            GameObject ballObj = GameObject.FindGameObjectWithTag("Football");
            if (ballObj != null)
            {
                ballTarget = ballObj.transform;
                FootballBehavior ball = ballObj.GetComponent<FootballBehavior>();
                
                if (ball != null && ball.isInAir && !ball.isCaught)
                {
                    float distanceToBall = Vector3.Distance(transform.position, ballTarget.position);
                    if (distanceToBall < detectionRadius)
                    {
                        currentTarget = ballTarget;
                        currentState = DefenderState.Pursuing;
                        return;
                    }
                }
            }

            GameObject[] receivers = GameObject.FindGameObjectsWithTag("Receiver");
            if (receivers.Length > 0)
            {
                Transform nearestReceiver = FindNearestReceiver(receivers);
                if (nearestReceiver != null)
                {
                    currentTarget = nearestReceiver;
                    currentState = DefenderState.Covering;
                    return;
                }
            }

            currentState = DefenderState.Patrolling;
        }

        Transform FindNearestReceiver(GameObject[] receivers)
        {
            Transform nearest = null;
            float minDistance = float.MaxValue;

            foreach (GameObject receiver in receivers)
            {
                float distance = Vector3.Distance(transform.position, receiver.transform.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = receiver.transform;
                }
            }

            return nearest;
        }

        void Patrol()
        {
            MoveTowards(patrolPosition);

            if (Vector3.Distance(transform.position, patrolPosition) < 2f)
            {
                SetRandomPatrolPosition();
            }
        }

        void Pursue()
        {
            if (currentTarget != null)
            {
                MoveTowards(currentTarget.position);

                float distance = Vector3.Distance(transform.position, currentTarget.position);
                if (distance < tackleRadius)
                {
                    AttemptTackle();
                }
            }
            else
            {
                currentState = DefenderState.Patrolling;
            }
        }

        void CoverReceiver()
        {
            if (currentTarget != null)
            {
                Vector3 coverPosition = currentTarget.position - (currentTarget.forward * 2f);
                MoveTowards(coverPosition);
            }
            else
            {
                currentState = DefenderState.Patrolling;
            }
        }

        void MoveTowards(Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0;

            float adjustedSpeed = moveSpeed * (speed / 70f);
            Vector3 velocity = direction * adjustedSpeed;

            if (rb != null)
            {
                rb.velocity = new Vector3(velocity.x, rb.velocity.y, velocity.z);
            }

            if (velocity.magnitude > 0.1f)
            {
                transform.rotation = Quaternion.LookRotation(velocity);
            }
        }

        void AttemptTackle()
        {
            float tackleSuccess = Random.value * (tackling / 100f);
            
            if (tackleSuccess > 0.4f)
            {
                Debug.Log($"{gameObject.name} made a tackle!");
            }
        }

        void SetRandomPatrolPosition()
        {
            float randomX = Random.Range(-20f, 20f);
            float randomZ = Random.Range(-20f, 20f);
            patrolPosition = new Vector3(randomX, 0, randomZ);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, detectionRadius);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, tackleRadius);
        }
    }

    public enum DefenderState
    {
        Patrolling,
        Pursuing,
        Covering
    }
}
