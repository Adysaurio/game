using System.Collections;
using TrashPandas.Runtime.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Runtime.Grabbing
{
    /// <summary>
    /// Lets the coat's hands pick up Grabbables they touch while reaching, carry them, and throw them on
    /// release with the hand's velocity. Big items need both hands at once.
    /// </summary>
    [RequireComponent(typeof(TrenchcoatBody))]
    public sealed class HandGrabber : MonoBehaviour
    {
        public float GrabRadius = 0.25f;
        public float TwoHandGrabRadius = 0.45f;
        public float ThrowMultiplier = 1.6f;
        public float IgnoreCoatAfterThrow = 0.35f;
        public float MaxThrowSpeed = 8f;

        TrenchcoatBody _body;
        Collider _coatCollider;
        Transform _twoHandAnchor;
        Grabbable _left, _right, _both;
        Vector3 _lastLeft, _lastRight;
        Vector3 _leftVelocity, _rightVelocity;
        readonly Collider[] _hits = new Collider[16];
        readonly System.Collections.Generic.Dictionary<Grabbable, Transform> _holders = new System.Collections.Generic.Dictionary<Grabbable, Transform>();
        readonly System.Collections.Generic.Dictionary<Grabbable, Quaternion> _grabRotationOffset = new System.Collections.Generic.Dictionary<Grabbable, Quaternion>();

        public Grabbable HeldLeft => _left;
        public Grabbable HeldRight => _right;
        public Grabbable HeldBoth => _both;

        void Awake()
        {
            _body = GetComponent<TrenchcoatBody>();
            _coatCollider = GetComponent<Collider>();
            _twoHandAnchor = new GameObject("TwoHandAnchor").transform;
            _twoHandAnchor.SetParent(transform, false);
        }

        void LateUpdate()
        {
            bool leftReach = _body.LeftReachActive, rightReach = _body.RightReachActive;

            // Releases first, using the hand velocity from *before* this frame: once released, the hand
            // starts returning to rest, and that motion must not fling the item backwards.
            if (_both && !(leftReach && rightReach)) Release(ref _both, (_leftVelocity + _rightVelocity) * 0.5f);
            if (_left && !leftReach) Release(ref _left, _leftVelocity);
            if (_right && !rightReach) Release(ref _right, _rightVelocity);

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Vector3 l = _body.LeftHand.position, r = _body.RightHand.position;
            _leftVelocity = Vector3.Lerp(_leftVelocity, (l - _lastLeft) / dt, 0.5f);
            _rightVelocity = Vector3.Lerp(_rightVelocity, (r - _lastRight) / dt, 0.5f);
            _lastLeft = l;
            _lastRight = r;
            _twoHandAnchor.position = (l + r) * 0.5f;
            _twoHandAnchor.rotation = transform.rotation;

            if (!_both && leftReach && rightReach && !_left && !_right)
                TryGrab(ref _both, _twoHandAnchor, TwoHandGrabRadius, bigOnly: true);
            if (!_both && leftReach && !_left) TryGrab(ref _left, _body.LeftHand, GrabRadius, bigOnly: false);
            if (!_both && rightReach && !_right) TryGrab(ref _right, _body.RightHand, GrabRadius, bigOnly: false);

            // Carried items follow their holder (hand or two-hand anchor), keeping their own scale.
            foreach (var pair in _holders)
            {
                if (!pair.Key || !pair.Value) continue;
                pair.Key.transform.SetPositionAndRotation(pair.Value.position, transform.rotation * _grabRotationOffset[pair.Key]);
            }
        }

        void TryGrab(ref Grabbable slot, Transform holder, float radius, bool bigOnly)
        {
            int count = Physics.OverlapSphereNonAlloc(holder.position, radius, _hits, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var g = _hits[i].GetComponentInParent<Grabbable>();
                if (!g || g.IsHeld || g.RequiresBothHands != bigOnly) continue;
                Attach(g, holder);
                slot = g;
                return;
            }
        }

        void Attach(Grabbable g, Transform holder)
        {
            g.IsHeld = true;
            g.Body.isKinematic = true;
            foreach (var c in g.Colliders) c.enabled = false;
            // Never parent to the hand: re-parenting rescales the item through the hierarchy. We follow instead.
            _holders[g] = holder;
            _grabRotationOffset[g] = Quaternion.Inverse(transform.rotation) * g.transform.rotation;
        }

        void Release(ref Grabbable slot, Vector3 handVelocity)
        {
            var g = slot;
            slot = null;
            _holders.Remove(g);
            _grabRotationOffset.Remove(g);
            if (!g) return;
            g.Body.isKinematic = false;
            foreach (var c in g.Colliders) c.enabled = true;
            g.Body.linearVelocity = Vector3.ClampMagnitude(handVelocity * ThrowMultiplier, MaxThrowSpeed);
            g.IsHeld = false;
            if (_coatCollider) StartCoroutine(IgnoreCoatBriefly(g));
        }

        IEnumerator IgnoreCoatBriefly(Grabbable g)
        {
            foreach (var c in g.Colliders) Physics.IgnoreCollision(c, _coatCollider, true);
            yield return new WaitForSeconds(IgnoreCoatAfterThrow);
            if (!g) yield break;
            foreach (var c in g.Colliders) if (c) Physics.IgnoreCollision(c, _coatCollider, false);
        }
    }
}
