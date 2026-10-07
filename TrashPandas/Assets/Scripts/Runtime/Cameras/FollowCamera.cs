using UnityEngine;

namespace TrashPandas.Runtime.Cameras
{
    /// <summary>Third-person follow camera. Distance scales with the target's size.</summary>
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 5f;
        public float Height = 3f;
        public float Smooth = 6f;

        public void Follow(Transform target, float distance, float height)
        {
            Target = target;
            Distance = distance;
            Height = height;
        }

        void LateUpdate()
        {
            if (!Target) return;
            Vector3 back = -Vector3.ProjectOnPlane(Target.forward, Vector3.up).normalized;
            Vector3 desired = Target.position + back * Distance + Vector3.up * Height;
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * Smooth);
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(Target.position + Vector3.up * (Height * 0.3f) - transform.position), Time.deltaTime * Smooth);
        }
    }
}
