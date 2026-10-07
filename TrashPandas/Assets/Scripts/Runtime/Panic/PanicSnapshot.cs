using System;
using TrashPandas.Core.Panic;
using Unity.Netcode;

namespace TrashPandas.Runtime.Panic
{
    /// <summary>Per-player panic status for the HUD: who each player is (client), escaped/caught, hits taken.</summary>
    public struct PanicSnapshot : INetworkSerializable, IEquatable<PanicSnapshot>
    {
        public const int Max = 5;
        byte _count;
        ulong _c0, _c1, _c2, _c3, _c4;      // client id + 1 (0 = offline / unknown)
        byte _o0, _o1, _o2, _o3, _o4;       // PlayerOutcome
        byte _h0, _h1, _h2, _h3, _h4;       // hits taken
        public float SecondsLeft;

        public int Count => _count;

        public void Set(int player, ulong? client, PlayerOutcome outcome, int hits)
        {
            if (player < 0 || player >= Max) return;
            if (player + 1 > _count) _count = (byte)(player + 1);
            ulong c = client.HasValue ? client.Value + 1 : 0;
            switch (player)
            {
                case 0: _c0 = c; _o0 = (byte)outcome; _h0 = (byte)hits; break;
                case 1: _c1 = c; _o1 = (byte)outcome; _h1 = (byte)hits; break;
                case 2: _c2 = c; _o2 = (byte)outcome; _h2 = (byte)hits; break;
                case 3: _c3 = c; _o3 = (byte)outcome; _h3 = (byte)hits; break;
                case 4: _c4 = c; _o4 = (byte)outcome; _h4 = (byte)hits; break;
            }
        }

        public PlayerOutcome OutcomeOf(int p) => (PlayerOutcome)(p switch { 0 => _o0, 1 => _o1, 2 => _o2, 3 => _o3, 4 => _o4, _ => 0 });
        public int HitsOf(int p) => p switch { 0 => _h0, 1 => _h1, 2 => _h2, 3 => _h3, 4 => _h4, _ => 0 };

        public int? PlayerOfClient(ulong clientId)
        {
            ulong c = clientId + 1;
            if (_c0 == c) return 0; if (_c1 == c) return 1; if (_c2 == c) return 2; if (_c3 == c) return 3; if (_c4 == c) return 4;
            return null;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref _count);
            s.SerializeValue(ref _c0); s.SerializeValue(ref _c1); s.SerializeValue(ref _c2); s.SerializeValue(ref _c3); s.SerializeValue(ref _c4);
            s.SerializeValue(ref _o0); s.SerializeValue(ref _o1); s.SerializeValue(ref _o2); s.SerializeValue(ref _o3); s.SerializeValue(ref _o4);
            s.SerializeValue(ref _h0); s.SerializeValue(ref _h1); s.SerializeValue(ref _h2); s.SerializeValue(ref _h3); s.SerializeValue(ref _h4);
            s.SerializeValue(ref SecondsLeft);
        }

        public bool Equals(PanicSnapshot o) =>
            _count == o._count && _c0 == o._c0 && _c1 == o._c1 && _c2 == o._c2 && _c3 == o._c3 && _c4 == o._c4 &&
            _o0 == o._o0 && _o1 == o._o1 && _o2 == o._o2 && _o3 == o._o3 && _o4 == o._o4 &&
            _h0 == o._h0 && _h1 == o._h1 && _h2 == o._h2 && _h3 == o._h3 && _h4 == o._h4 &&
            (int)SecondsLeft == (int)o.SecondsLeft;
    }
}
