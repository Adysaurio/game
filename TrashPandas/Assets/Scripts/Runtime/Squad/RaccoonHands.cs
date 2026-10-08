using TrashPandas.Core.Raccoons;
using TrashPandas.Runtime.Cameras;
using TrashPandas.Runtime.Input;
using TrashPandas.Runtime.Raccoon;
using UnityEngine;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>The owner's side of the click: quick click = grab/drop, hold and release = throw.</summary>
    public sealed class RaccoonHands
    {
        readonly ClickIntent _click = new ClickIntent();
        public float Charge(float now) => _click.Charge(now);

        /// <param name="tap">(highlighted item index, where I see myself)</param>
        /// <param name="throwIt">(camera direction, strength 0..1)</param>
        public void Tick(DebugInputReader reader, PlayerCameraRig rig, RaccoonController raccoon,
                         System.Action<int, Vector3> tap, System.Action<Vector3, float> throwIt, bool botTap = false)
        {
            var carry = CarryDirector.Instance;
            if (!raccoon || !carry) return;
            bool carrying = carry.IsCarrying(raccoon.PlayerId);
            var highlight = GrabHighlight.Instance ? GrabHighlight.Instance.Refresh(rig, raccoon, carrying) : null;
            float now = Time.time;
            if (reader.ClickPressed(rig)) _click.Press(now);
            if (reader.ClickReleased(rig) || botTap)
            {
                var outcome = botTap ? new ClickOutcome { Kind = ClickResult.Tap } : _click.Release(now);
                if (outcome.Kind == ClickResult.Tap) tap(carry.IndexOf(highlight), raccoon.transform.position);
                else if (outcome.Kind == ClickResult.Throw && carrying) throwIt(rig.OutputCamera.transform.forward, outcome.Strength);
            }
        }
    }
}
