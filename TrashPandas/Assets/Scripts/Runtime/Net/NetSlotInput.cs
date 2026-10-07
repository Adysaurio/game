using TrashPandas.Core.Trenchcoat;
using Unity.Netcode;

namespace TrashPandas.Runtime.Net
{
    /// <summary>One player's slot input as sent to the host, with a sequence number to drop stale packets.</summary>
    public struct NetSlotInput : INetworkSerializable
    {
        public uint Sequence;
        public SlotInput Input;

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Sequence);
            s.SerializeValue(ref Input.Move);
            s.SerializeValue(ref Input.Crouch);
            s.SerializeValue(ref Input.JumpPressedAt);
            s.SerializeValue(ref Input.Aim);
            s.SerializeValue(ref Input.AimPoint);
            s.SerializeValue(ref Input.HasAimPoint);
            s.SerializeValue(ref Input.GrabOne);
            s.SerializeValue(ref Input.GrabBoth);
            s.SerializeValue(ref Input.PreferLeftHand);
        }
    }
}
