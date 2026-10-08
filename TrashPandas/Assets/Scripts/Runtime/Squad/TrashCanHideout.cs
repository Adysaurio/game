using System.Collections;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>A trash can: the lid flips open, you hop in, it closes.</summary>
    public sealed class TrashCanHideout : Hideout
    {
        public Transform Lid;
        Quaternion _lidClosed;
        public override string Prompt => "E: hide in the can";

        void Awake() { if (Lid) _lidClosed = Lid.localRotation; }

        IEnumerator Lid01(float from, float to, float seconds)
        {
            if (!Lid) yield break;
            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                Lid.localRotation = _lidClosed * Quaternion.Euler(0f, 0f, 110f * Mathf.SmoothStep(from, to, t / seconds));
                yield return null;
            }
            Lid.localRotation = _lidClosed * Quaternion.Euler(0f, 0f, 110f * to);
        }

        protected override IEnumerator OpenFx(bool fast) => Lid01(0f, 1f, fast ? 0.08f : 0.18f);
        protected override IEnumerator CloseFx() => Lid01(1f, 0f, 0.18f);

        public override void Wiggle()
        {
            if (Lid && !_wiggling) StartCoroutine(WiggleLid());
        }

        bool _wiggling;
        IEnumerator WiggleLid()
        {
            _wiggling = true;
            yield return Lid01(0f, 0.12f, 0.08f);
            yield return Lid01(0.12f, 0f, 0.1f);
            _wiggling = false;
        }
    }
}
