using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Raccoon;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>
    /// Hiding switches to a low first-person view from inside your hiding spot: watch the shoes go by and
    /// pick the moment to come out. Moving (or leaving the spot) brings the normal camera back.
    /// </summary>
    public static class HidePeek
    {
        static RaccoonController s_peeking;
        static float s_stillFor;

        public static bool Active => s_peeking;

        public static void Tick(PlayerCameraRig rig, RaccoonController r, float normalRadius, float normalLook)
        {
            if (!rig || !r) { Exit(rig, normalRadius, normalLook); return; }
            if (s_peeking && s_peeking != r) Exit(rig, normalRadius, normalLook);
            bool hidden = !r.Crawling && !r.Frozen && HidingSpot.SpotOf(r).HasValue && HidingSpot.Hides(r);
            float speed = r.PlanarSpeed;
            s_stillFor = hidden && speed < 0.25f ? s_stillFor + Time.deltaTime : 0f;

            if (!s_peeking && hidden && s_stillFor > 0.35f)
            {
                s_peeking = r;
                rig.SetTarget(r.transform, 0.12f, 0.24f);
                rig.Orbit.VerticalAxis.Value = 4f;
                SetVisible(r, false); // we're looking out of its eyes
                Ui.Sfx.Play2D(Ui.Sound.Grab, 0.35f);
            }
            else if (s_peeking && (!hidden || speed > 0.7f)) Exit(rig, normalRadius, normalLook);
        }

        public static void Exit(PlayerCameraRig rig, float normalRadius, float normalLook)
        {
            if (!s_peeking) { s_peeking = null; return; }
            var r = s_peeking;
            s_peeking = null;
            SetVisible(r, true);
            if (rig && r) rig.SetTarget(r.transform, normalRadius, normalLook);
        }

        static void SetVisible(RaccoonController r, bool on)
        {
            if (!r) return;
            foreach (var rend in r.GetComponentsInChildren<Renderer>())
                if (!(rend is ParticleSystemRenderer)) rend.enabled = on;
        }
    }
}
