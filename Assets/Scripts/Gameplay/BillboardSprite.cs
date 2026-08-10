using UnityEngine;

namespace RetroBowl.Gameplay
{
    /// <summary>2D sprites face the camera; no billboard tilt needed.</summary>
    public class BillboardSprite : MonoBehaviour
    {
        void LateUpdate()
        {
            transform.localRotation = Quaternion.identity;
        }
    }
}
