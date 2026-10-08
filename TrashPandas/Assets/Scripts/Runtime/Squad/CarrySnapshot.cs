using System;
using Unity.Netcode;

namespace TrashPandas.Runtime.Squad
{
    /// <summary>Who carries what (item index + 1 per player; 0 = nothing) and which heavy things are lifted.</summary>
    public struct CarrySnapshot : INetworkSerializable, IEquatable<CarrySnapshot>
    {
        public const int Max = 5;
        short _i0, _i1, _i2, _i3, _i4;
        byte _lifted; // bit per player: their heavy item is off the ground

        public int ItemOf(int p) => (p switch { 0 => _i0, 1 => _i1, 2 => _i2, 3 => _i3, 4 => _i4, _ => (short)0 }) - 1;
        public bool Lifted(int p) => p >= 0 && p < Max && (_lifted & (1 << p)) != 0;

        public void Set(int p, int item, bool lifted)
        {
            short v = (short)(item + 1);
            switch (p) { case 0: _i0 = v; break; case 1: _i1 = v; break; case 2: _i2 = v; break; case 3: _i3 = v; break; case 4: _i4 = v; break; default: return; }
            if (lifted) _lifted |= (byte)(1 << p); else _lifted &= (byte)~(1 << p);
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref _i0); s.SerializeValue(ref _i1); s.SerializeValue(ref _i2); s.SerializeValue(ref _i3); s.SerializeValue(ref _i4);
            s.SerializeValue(ref _lifted);
        }

        public bool Equals(CarrySnapshot o) => _i0 == o._i0 && _i1 == o._i1 && _i2 == o._i2 && _i3 == o._i3 && _i4 == o._i4 && _lifted == o._lifted;
    }
}
