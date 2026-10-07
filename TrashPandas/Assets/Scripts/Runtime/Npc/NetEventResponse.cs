using TrashPandas.Core.Events;
using Unity.Netcode;

namespace TrashPandas.Runtime.Npc
{
    public struct NetEventResponse : INetworkSerializable
    {
        public byte EventSerial;
        public EventResponse Response;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref EventSerial);
            byte answer = (byte)Response.Answer;
            s.SerializeValue(ref answer);
            Response.Answer = (HeadAnswer)answer;
            s.SerializeValue(ref Response.ArmsDone);
            s.SerializeValue(ref Response.LegsDone);
        }
    }

    public enum EventPhase : byte { Idle, Warning, Engaged, Resolved }

    /// <summary>What every client needs to draw the event: phase, which event, time left, and the result.</summary>
    public struct EventSnapshot : INetworkSerializable, System.IEquatable<EventSnapshot>
    {
        public byte Phase;
        public byte EventIndex;
        public byte Serial;        // increments per event, so stale responses are ignored
        public float SecondsLeft;
        public float ResultDelta;
        public byte HeadOutcome, ArmsOutcome, LegsOutcome; // PartOutcome + 1, 0 = not judged

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Phase); s.SerializeValue(ref EventIndex); s.SerializeValue(ref Serial);
            s.SerializeValue(ref SecondsLeft); s.SerializeValue(ref ResultDelta);
            s.SerializeValue(ref HeadOutcome); s.SerializeValue(ref ArmsOutcome); s.SerializeValue(ref LegsOutcome);
        }

        public bool Equals(EventSnapshot o) =>
            Phase == o.Phase && EventIndex == o.EventIndex && Serial == o.Serial && (int)(SecondsLeft * 10) == (int)(o.SecondsLeft * 10) &&
            ResultDelta == o.ResultDelta && HeadOutcome == o.HeadOutcome && ArmsOutcome == o.ArmsOutcome && LegsOutcome == o.LegsOutcome;
    }
}
