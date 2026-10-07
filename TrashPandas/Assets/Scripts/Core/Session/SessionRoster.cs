using System;
using System.Collections.Generic;
using TrashPandas.Core.Trenchcoat;

namespace TrashPandas.Core.Session
{
    /// <summary>
    /// Who is in the session (host side). In the lobby players join and leave freely; starting the round
    /// fixes the player list, gives each player an id in join order and seats them in a SlotSystem.
    /// A player who disconnects mid-round just leaves an empty slot.
    /// </summary>
    public sealed class SessionRoster
    {
        public const int MaxPlayers = SlotLayout.MaxPlayers;

        readonly List<ulong> _clients = new List<ulong>();
        readonly Dictionary<ulong, int> _playerIds = new Dictionary<ulong, int>();

        public IReadOnlyList<ulong> Clients => _clients;
        public bool RoundStarted => Slots != null;
        public SlotSystem Slots { get; private set; }

        public bool Join(ulong clientId)
        {
            if (RoundStarted || _clients.Count >= MaxPlayers || _clients.Contains(clientId)) return false;
            _clients.Add(clientId);
            return true;
        }

        public bool Leave(ulong clientId)
        {
            if (!_clients.Remove(clientId)) return false;
            if (_playerIds.TryGetValue(clientId, out int playerId))
            {
                Slots?.Leave(playerId);
                _playerIds.Remove(clientId);
            }
            return true;
        }

        public SlotSystem StartRound()
        {
            if (RoundStarted) throw new InvalidOperationException("The round already started.");
            if (_clients.Count < SlotLayout.MinPlayers)
                throw new InvalidOperationException($"At least {SlotLayout.MinPlayers} players are needed to start.");

            var slots = new SlotSystem(_clients.Count);
            _playerIds.Clear();
            for (int i = 0; i < _clients.Count; i++)
            {
                _playerIds[_clients[i]] = i;
                slots.TryEnter(i, i);
            }
            Slots = slots;
            return slots;
        }

        public void EndRound()
        {
            Slots = null;
            _playerIds.Clear();
        }

        public int? PlayerIdOf(ulong clientId) =>
            _playerIds.TryGetValue(clientId, out int id) ? id : (int?)null;

        public ulong? ClientOf(int playerId)
        {
            foreach (var pair in _playerIds)
                if (pair.Value == playerId) return pair.Key;
            return null;
        }
    }
}
