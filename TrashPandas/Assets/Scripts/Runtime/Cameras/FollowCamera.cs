using UnityEngine;

namespace TrashPandas.Runtime.Cameras
{
    /// <summary>Third-person follow camera. Its yaw lags behind the target's so turns don't feel dizzying.</summary>
    public sealed class FollowCamera : MonoBehaviour
    {
        public Transform Target;
        public float Distance = 5f;
        public float Height = 3f;
        public float PositionSmooth = 8f;
        public float YawSmooth = 3f;

        float _yaw;

        public void Follow(Transform target, float distance, float height)
        {
            Target = target;
            Distance = distance;
            Height = height;
            if (target) _yaw = target.eulerAngles.y;
        }

        void LateUpdate()
        {
            if (!Target) return;
            _yaw = Mathf.LerpAngle(_yaw, Target.eulerAngles.y, Time.deltaTime * YawSmooth);
            Vector3 back = Quaternion.Euler(0f, _yaw, 0f) * Vector3.back;
            Vector3 desired = Target.position + back * Distance + Vector3.up * Height;
            transform.position = Vector3.Lerp(transform.position, desired, Time.deltaTime * PositionSmooth);
            transform.rotation = Quaternion.LookRotation(Target.position + Vector3.up * (Height * 0.35f) - transform.position);
        }
    }
}
