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
        public bool CleanExit;
        public byte RescuePercent;
        /// <summary>The RUN's alert (100 = hunting; drains while everyone hides; 0 = calmed down).</summary>
        public byte AlertPercent;
        int _l0, _l1, _l2, _l3, _l4;        // money taken home
        byte _t0, _t1, _t2, _t3, _t4;       // total hits taken (rescues don't reset this)
        byte _r0, _r1, _r2, _r3, _r4;       // friends freed
        sbyte _f0, _f1, _f2, _f3, _f4;      // order caught (-1 never)

        public int TotalHitsOf(int p) => p switch { 0 => _t0, 1 => _t1, 2 => _t2, 3 => _t3, 4 => _t4, _ => 0 };
        public int RescuesOf(int p) => p switch { 0 => _r0, 1 => _r1, 2 => _r2, 3 => _r3, 4 => _r4, _ => 0 };
        public int CaughtOrderOf(int p) => p switch { 0 => _f0, 1 => _f1, 2 => _f2, 3 => _f3, 4 => _f4, _ => -1 };

        public void SetStats(int p, int totalHits, int rescues, int caughtOrder)
        {
            byte t = (byte)System.Math.Min(255, totalHits), r = (byte)System.Math.Min(255, rescues);
            sbyte f = (sbyte)System.Math.Clamp(caughtOrder, -1, 100);
            switch (p)
            {
                case 0: _t0 = t; _r0 = r; _f0 = f; break; case 1: _t1 = t; _r1 = r; _f1 = f; break;
                case 2: _t2 = t; _r2 = r; _f2 = f; break; case 3: _t3 = t; _r3 = r; _f3 = f; break;
                case 4: _t4 = t; _r4 = r; _f4 = f; break;
            }
        }

        public int LootOf(int p) => p switch { 0 => _l0, 1 => _l1, 2 => _l2, 3 => _l3, 4 => _l4, _ => 0 };
        public void SetLoot(int p, int v) { switch (p) { case 0: _l0 = v; break; case 1: _l1 = v; break; case 2: _l2 = v; break; case 3: _l3 = v; break; case 4: _l4 = v; break; } }

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
            s.SerializeValue(ref CleanExit);
            s.SerializeValue(ref RescuePercent);
            s.SerializeValue(ref AlertPercent);
            s.SerializeValue(ref _l0); s.SerializeValue(ref _l1); s.SerializeValue(ref _l2); s.SerializeValue(ref _l3); s.SerializeValue(ref _l4);
            s.SerializeValue(ref _t0); s.SerializeValue(ref _t1); s.SerializeValue(ref _t2); s.SerializeValue(ref _t3); s.SerializeValue(ref _t4);
            s.SerializeValue(ref _r0); s.SerializeValue(ref _r1); s.SerializeValue(ref _r2); s.SerializeValue(ref _r3); s.SerializeValue(ref _r4);
            s.SerializeValue(ref _f0); s.SerializeValue(ref _f1); s.SerializeValue(ref _f2); s.SerializeValue(ref _f3); s.SerializeValue(ref _f4);
        }

        public bool Equals(PanicSnapshot o) =>
            _count == o._count && _c0 == o._c0 && _c1 == o._c1 && _c2 == o._c2 && _c3 == o._c3 && _c4 == o._c4 &&
            _o0 == o._o0 && _o1 == o._o1 && _o2 == o._o2 && _o3 == o._o3 && _o4 == o._o4 &&
            _h0 == o._h0 && _h1 == o._h1 && _h2 == o._h2 && _h3 == o._h3 && _h4 == o._h4 &&
            (int)SecondsLeft == (int)o.SecondsLeft && CleanExit == o.CleanExit && RescuePercent == o.RescuePercent && AlertPercent == o.AlertPercent &&
            _l0 == o._l0 && _l1 == o._l1 && _l2 == o._l2 && _l3 == o._l3 && _l4 == o._l4 &&
            _t0 == o._t0 && _t1 == o._t1 && _t2 == o._t2 && _t3 == o._t3 && _t4 == o._t4 &&
            _r0 == o._r0 && _r1 == o._r1 && _r2 == o._r2 && _r3 == o._r3 && _r4 == o._r4 &&
            _f0 == o._f0 && _f1 == o._f1 && _f2 == o._f2 && _f3 == o._f3 && _f4 == o._f4;
    }
}
