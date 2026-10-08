using System.Collections;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>
    /// The hot dog mascot dancing around the party: hop inside (E) and ride along while it dances past everyone.
    /// Nobody looks twice at a dancing hot dog. The host walks it along its route; the transform is synced.
    /// </summary>
    public sealed class MascotHideout : Hideout
    {
        public Vector3[] Route = new Vector3[0];
        public float Speed = 1.1f;
        public Transform Body, ArmL, ArmR;
        int _leg;
        public override string Prompt => "E: hop into the hot dog";
        protected override float ExtraReach => 0.4f;
        protected override Vector3 InsidePoint => transform.position + Vector3.up * 0.3f;

        void Update()
        {
            // Dance (every machine, from the clock: same moves everywhere).
            float t = Time.time;
            if (Body) Body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 4f) * 10f);
            if (ArmL) ArmL.localRotation = Quaternion.Euler(0f, 0f, 40f + Mathf.Sin(t * 6f) * 50f);
            if (ArmR) ArmR.localRotation = Quaternion.Euler(0f, 0f, -40f - Mathf.Sin(t * 6f + 1f) * 50f);
            if (Occupant && !Occupant.Frozen) Occupant.transform.position = InsidePoint; // riding inside
            if (!Net.SimulationAuthority.IsSimulating || Route.Length == 0) return;
            Vector3 to = Route[_leg] - transform.position;
            to.y = 0f;
            if (to.magnitude < 0.3f) { _leg = (_leg + 1) % Route.Length; return; }
            // A shuffle-step: move in little bursts with the beat.
            float step = Speed * Mathf.Max(0f, Mathf.Sin(t * 4f)) * 1.6f * Time.deltaTime;
            transform.position += to.normalized * Mathf.Min(step, to.magnitude);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized), Time.deltaTime * 2f);
        }

        public override void Wiggle() { if (Body && !_shaking) StartCoroutine(Shake()); }
        bool _shaking;
        IEnumerator Shake()
        {
            _shaking = true;
            Vector3 s = Body.localScale;
            for (float k = 0f; k < 0.3f; k += Time.deltaTime) { Body.localScale = s * (1f + Mathf.Sin(k * 40f) * 0.05f); yield return null; }
            Body.localScale = s;
            _shaking = false;
        }
    }
}
