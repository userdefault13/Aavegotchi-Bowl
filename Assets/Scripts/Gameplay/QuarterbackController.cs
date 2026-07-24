using UnityEngine;
using RetroBowl.Core;

namespace RetroBowl.Gameplay
{
    public class QuarterbackController : MonoBehaviour
    {
        [Header("Throwing")]
        public float throwPower = 20f;
        public float maxThrowDistance = 50f;
        public float throwArc = 0.5f;
        public LineRenderer trajectoryLine;
        
        [Header("Aiming")]
        public Transform aimTarget;
        public float aimSpeed = 5f;
        private Vector3 targetPosition;
        
        [Header("State")]
        public bool isAiming = false;
        public bool hasThrown = false;
        
        [Header("References")]
        public GameObject ballPrefab;
        private GameObject currentBall;
        private Camera mainCamera;
        public Transform[] receivers;

        private PlayerController playerController;

        void Start()
        {
            mainCamera = Camera.main;
            playerController = GetComponent<PlayerController>();
            
            if (trajectoryLine == null)
            {
                trajectoryLine = gameObject.AddComponent<LineRenderer>();
                trajectoryLine.startWidth = 0.1f;
                trajectoryLine.endWidth = 0.1f;
                trajectoryLine.positionCount = 20;
                trajectoryLine.enabled = false;
            }

            FindReceivers();
        }

        void Update()
        {
            if (GameManager.Instance.currentState != GameState.Playing)
            {
                return;
            }

            if (Input.GetMouseButtonDown(0) && !isAiming && !hasThrown)
            {
                StartAiming();
            }

            if (isAiming)
            {
                UpdateAim();

                if (Input.GetMouseButtonUp(0))
                {
                    ThrowBall();
                }
            }
        }

        void FindReceivers()
        {
            GameObject[] receiverObjects = GameObject.FindGameObjectsWithTag("Receiver");
            receivers = new Transform[receiverObjects.Length];
            for (int i = 0; i < receiverObjects.Length; i++)
            {
                receivers[i] = receiverObjects[i].transform;
            }
        }

        void StartAiming()
        {
            isAiming = true;
            trajectoryLine.enabled = true;
            
            if (playerController != null)
            {
                playerController.SetControlled(false);
            }
        }

        void UpdateAim()
        {
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            Plane groundPlane = new Plane(Vector3.up, transform.position);
            
            if (groundPlane.Raycast(ray, out float distance))
            {
                targetPosition = ray.GetPoint(distance);
                
                float distanceToTarget = Vector3.Distance(transform.position, targetPosition);
                if (distanceToTarget > maxThrowDistance)
                {
                    Vector3 direction = (targetPosition - transform.position).normalized;
                    targetPosition = transform.position + direction * maxThrowDistance;
                }
            }

            if (aimTarget != null)
            {
                aimTarget.position = targetPosition;
            }

            DrawTrajectory();
        }

        void DrawTrajectory()
        {
            Vector3 startPos = transform.position + Vector3.up * 2f;
            Vector3 endPos = targetPosition + Vector3.up * 0.5f;

            for (int i = 0; i < trajectoryLine.positionCount; i++)
            {
                float t = i / (float)(trajectoryLine.positionCount - 1);
                Vector3 point = Vector3.Lerp(startPos, endPos, t);
                
                float arc = throwArc * Mathf.Sin(t * Mathf.PI);
                point.y += arc * Vector3.Distance(startPos, endPos);
                
                trajectoryLine.SetPosition(i, point);
            }
        }

        void ThrowBall()
        {
            isAiming = false;
            hasThrown = true;
            trajectoryLine.enabled = false;

            if (playerController != null)
            {
                playerController.RemoveBall();
                playerController.SetControlled(true);
            }

            GameObject ball = Instantiate(ballPrefab, transform.position + Vector3.up * 2f, Quaternion.identity);
            
            FootballBehavior ballBehavior = ball.GetComponent<FootballBehavior>();
            if (ballBehavior == null)
            {
                ballBehavior = ball.AddComponent<FootballBehavior>();
            }
            
            ballBehavior.ThrowToTarget(targetPosition);

            Invoke(nameof(ResetThrow), 3f);
        }

        void ResetThrow()
        {
            hasThrown = false;
        }

        public Transform GetNearestReceiver(Vector3 position)
        {
            Transform nearest = null;
            float minDistance = float.MaxValue;

            foreach (Transform receiver in receivers)
            {
                if (receiver == null) continue;
                
                float distance = Vector3.Distance(position, receiver.position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearest = receiver;
                }
            }

            return nearest;
        }
    }
}
