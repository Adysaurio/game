using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Raccoon;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>
    /// The camera while hiding: in a bush or under a table it rises a little to see over the cover; in a trash
    /// can it orbits the can (look around freely, like Fortnite's dumpsters); in a drain pipe it follows you
    /// down the tunnel, looking where you crawl.
    /// </summary>
    public static class HidePeek
    {
        enum Mode { None, Raised, Can, Tunnel }
        static Mode s_mode;
        static RaccoonController s_who;
        static float s_stillFor, s_lastYaw, s_nextWiggle;

        public static bool Active => s_mode != Mode.None;

        public static void Tick(PlayerCameraRig rig, RaccoonController r, float normalRadius, float normalLook)
        {
            if (!rig || !r) { Exit(rig, normalRadius, normalLook); return; }
            if (s_who && s_who != r) Exit(rig, normalRadius, normalLook);

            Mode want = Mode.None;
            if (r.Crawling) want = Mode.Tunnel;
            else if (r.InCan) want = Mode.Can;
            else
            {
                bool hidden = !r.Frozen && HidingSpot.SpotOf(r).HasValue && HidingSpot.Hides(r);
                s_stillFor = hidden ? s_stillFor + Time.deltaTime : 0f;
                if (hidden && s_stillFor > 0.25f) want = Mode.Raised;
            }

            if (want != s_mode)
            {
                Exit(rig, normalRadius, normalLook);
                s_mode = want;
                s_who = r;
                switch (want)
                {
                    case Mode.Raised:
                        rig.SetTarget(r.transform, normalRadius + 0.9f, normalLook + 0.35f);
                        rig.Orbit.VerticalAxis.Value = Mathf.Max(rig.Orbit.VerticalAxis.Value, 28f);
                        break;
                    case Mode.Can:
                        rig.SetTarget(r.InCan.transform, 3.2f, 0.9f);
                        rig.Orbit.VerticalAxis.Value = Mathf.Max(rig.Orbit.VerticalAxis.Value, 22f);
                        break;
                    case Mode.Tunnel:
                        rig.SetTarget(r.transform, 0.95f, 0.22f);
                        PlayerCameraRig.CinematicLock = true;
                        break;
                }
            }
            if (s_mode == Mode.Can && r.InCan)
            {
                float yaw = rig.Orbit.HorizontalAxis.Value;
                float swing = Mathf.Abs(Mathf.DeltaAngle(yaw, s_lastYaw)) / Mathf.Max(Time.deltaTime, 1e-4f);
                s_lastYaw = yaw;
                if (swing > 90f && Time.time >= s_nextWiggle) { s_nextWiggle = Time.time + 0.9f; r.InCan.Wiggle(); }
            }
            if (s_mode == Mode.Tunnel)
            {
                // Look down the tunnel the way you're crawling.
                Vector3 d = r.transform.forward;
                rig.Orbit.HorizontalAxis.Value = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                rig.Orbit.VerticalAxis.Value = 6f;
            }
        }

        public static void Exit(PlayerCameraRig rig, float normalRadius, float normalLook)
        {
            var who = s_who;
            var mode = s_mode;
            s_mode = Mode.None;
            s_who = null;
            if (mode == Mode.None) return;
            if (mode == Mode.Tunnel) PlayerCameraRig.CinematicLock = false;
            if (rig && who) rig.SetTarget(who.transform, normalRadius, normalLook);
        }
    }
}
