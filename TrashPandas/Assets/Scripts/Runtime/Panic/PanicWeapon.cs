using TrashPandas.Runtime.Npc;
using UnityEngine;

namespace TrashPandas.Runtime.Panic
{
    public enum WeaponKind : byte { Broom, Pan, Chair, Tray }

    /// <summary>Something a panicked human grabs to swat raccoons with. Follows the holder's hand.</summary>
    public sealed class PanicWeapon : MonoBehaviour
    {
        public WeaponKind Kind;
        public Vector3 HeldOffset = new Vector3(0.1f, 0f, 0.15f);

        public NpcPawn Holder { get; private set; }
        float _swingT = 1f;

        public void PickUp(NpcPawn pawn)
        {
            Holder = pawn;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            var rb = GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;
        }

        public void PlaySwing() => _swingT = 0f;

        void LateUpdate()
        {
            if (!Holder) return;
            var hand = Holder.Hand ? Holder.Hand : Holder.transform;
            _swingT = Mathf.Min(1f, _swingT + Time.deltaTime * 4f);
            float swing = Mathf.Sin(_swingT * Mathf.PI) * 100f; // overhead whack
            transform.SetPositionAndRotation(hand.TransformPoint(HeldOffset), hand.rotation * Quaternion.Euler(-30f + swing, 0f, 0f));
        }
    }
}
