using System.Collections.Generic;

namespace TrashPandas.Core.Panic
{
    public enum PlayerOutcome : byte { None, Running, Escaped, Caught }

    /// <summary>Who escaped and who got caught during the panic, and when the round is over.</summary>
    public sealed class RoundOutcome
    {
        readonly Dictionary<int, PlayerOutcome> _status = new Dictionary<int, PlayerOutcome>();
        float _endsAt;

        public bool IsOver { get; private set; }
        public int Escaped { get; private set; }
        public int Caught { get; private set; }

        public void Begin(IEnumerable<int> players, float now, float timeLimit = 90f)
        {
            _status.Clear();
            Escaped = Caught = 0;
            IsOver = false;
            _endsAt = now + timeLimit;
            foreach (int p in players) _status[p] = PlayerOutcome.Running;
            CheckOver();
        }

        public PlayerOutcome StatusOf(int player) => _status.TryGetValue(player, out var s) ? s : PlayerOutcome.None;

        public float SecondsLeft(float now) => System.Math.Max(0f, _endsAt - now);

        public bool MarkEscaped(int player) => Resolve(player, PlayerOutcome.Escaped);
        public bool MarkCaught(int player) => Resolve(player, PlayerOutcome.Caught);

        public void Tick(float now)
        {
            if (IsOver || now < _endsAt) return;
            foreach (int p in new List<int>(_status.Keys))
                if (_status[p] == PlayerOutcome.Running) Resolve(p, PlayerOutcome.Caught);
            IsOver = true;
        }

        bool Resolve(int player, PlayerOutcome outcome)
        {
            if (!_status.TryGetValue(player, out var s) || s != PlayerOutcome.Running) return false;
            _status[player] = outcome;
            if (outcome == PlayerOutcome.Escaped) Escaped++; else Caught++;
            CheckOver();
            return true;
        }

        void CheckOver()
        {
            foreach (var s in _status.Values) if (s == PlayerOutcome.Running) return;
            IsOver = _status.Count > 0;
        }
    }
}
