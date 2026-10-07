using TrashPandas.Core.Events;
using TrashPandas.Runtime.Input;
using TrashPandas.Runtime.Net;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TrashPandas.Runtime.Npc
{
    /// <summary>
    /// The local player's side of a social event: reads 1/2/3, clicks, crouch, jump and movement during the
    /// response window into an <see cref="EventTaskTracker"/>, and reports the result (offline directly,
    /// online by RPC). Used by both the debug and the online controller.
    /// </summary>
    public sealed class EventParticipation
    {
        readonly EventTaskTracker _tracker = new EventTaskTracker();
        byte _serial = 255;
        float _nextSend;
        EventResponse _lastSent;
        bool _sentOnce;

        public static bool Engaged => SocialEventDirector.Instance && SocialEventDirector.Instance.Phase == EventPhase.Engaged;

        /// <summary>Call every frame. Returns true while the response window is open (callers then skip slot switching).</summary>
        public bool Tick(DebugInputReader reader, Vector2 move, int offlinePlayer)
        {
            var d = SocialEventDirector.Instance;
            if (!d || d.Phase != EventPhase.Engaged) return false;
            var snap = d.Snapshot;
            if (snap.Serial != _serial) { _serial = snap.Serial; _tracker.Reset(); _sentOnce = false; }

            var k = Keyboard.current; var m = Mouse.current;
            int key = k == null ? 0 : k.digit1Key.wasPressedThisFrame ? 1 : k.digit2Key.wasPressedThisFrame ? 2 : k.digit3Key.wasPressedThisFrame ? 3 : 0;
            var input = new TaskInput
            {
                Primary = m != null && m.leftButton.isPressed,
                Secondary = m != null && m.rightButton.isPressed,
                Crouch = reader.CrouchHeld,
                Jump = reader.JumpPressed,
                Move = move,
            };
            DevAutomation.ApplyEventBot(d.Current, ref input, ref key);
            _tracker.Record(Time.deltaTime, input, key);
            var response = _tracker.ToResponse(d.Current);

            if (!SimulationAuthority.IsOnline) { d.SubmitResponse(offlinePlayer, _serial, response); return true; }
            bool changed = !_sentOnce || response.Answer != _lastSent.Answer || response.ArmsDone != _lastSent.ArmsDone || response.LegsDone != _lastSent.LegsDone;
            if (changed || Time.unscaledTime >= _nextSend)
            {
                _nextSend = Time.unscaledTime + 0.25f;
                _lastSent = response;
                _sentOnce = true;
                d.SubmitResponseRpc(new NetEventResponse { EventSerial = _serial, Response = response });
            }
            return true;
        }
    }
}
