using UnityEngine;

namespace RetroBowl.Gameplay
{
    public class CameraController : MonoBehaviour
    {
        [Header("Follow Settings")]
        public Transform target;
        public Vector3 offset = new Vector3(0, 25, -30);
        public float smoothSpeed = 5f;
        public bool followPlayer = true;

        [Header("Bounds")]
        public float minX = -40f;
        public float maxX = 40f;
        public float minZ = -40f;
        public float maxZ = 40f;

        [Header("Zoom")]
        public float defaultFOV = 60f;
        public float zoomedFOV = 45f;
        public float zoomSpeed = 5f;
        private Camera cam;
        private bool isZoomed = false;

        void Start()
        {
            cam = GetComponent<Camera>();
            if (cam == null)
            {
                cam = Camera.main;
            }

            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                }
            }
        }

        void LateUpdate()
        {
            if (followPlayer && target != null)
            {
                FollowTarget();
            }

            HandleZoom();
        }

        void FollowTarget()
        {
            Vector3 desiredPosition = target.position + offset;

            desiredPosition.x = Mathf.Clamp(desiredPosition.x, minX, maxX);
            desiredPosition.z = Mathf.Clamp(desiredPosition.z, minZ, maxZ);

            Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
            transform.position = smoothedPosition;

            transform.LookAt(target.position + Vector3.up * 2f);
        }

        void HandleZoom()
        {
            if (Input.GetKeyDown(KeyCode.Z))
            {
                isZoomed = !isZoomed;
            }

            float targetFOV = isZoomed ? zoomedFOV : defaultFOV;
            if (cam != null)
            {
                cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, zoomSpeed * Time.deltaTime);
            }
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }

        public void SetFollowEnabled(bool enabled)
        {
            followPlayer = enabled;
        }
    }
}
