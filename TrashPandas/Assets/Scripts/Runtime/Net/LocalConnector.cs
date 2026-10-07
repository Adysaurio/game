using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace TrashPandas.Runtime.Net
{
    /// <summary>Direct connection on this machine (Multiplayer Play Mode) or LAN. No accounts, no services.</summary>
    public sealed class LocalConnector : ISessionConnector
    {
        public const string DefaultAddress = "127.0.0.1";
        public const ushort Port = 7777;

        public Task<string> HostAsync(int maxPlayers)
        {
            var nm = NetworkManager.Singleton;
            nm.GetComponent<UnityTransport>().SetConnectionData(DefaultAddress, Port, "0.0.0.0");
            if (!nm.StartHost()) throw new InvalidOperationException("Couldn't start hosting. Is another game already running on this machine?");
            return Task.FromResult($"{DefaultAddress}:{Port}");
        }

        public Task JoinAsync(string address)
        {
            var nm = NetworkManager.Singleton;
            string host = string.IsNullOrWhiteSpace(address) ? DefaultAddress : address.Split(':')[0].Trim();
            nm.GetComponent<UnityTransport>().SetConnectionData(host, Port);
            if (!nm.StartClient()) throw new InvalidOperationException(ConnectionMessages.Generic);
            return Task.CompletedTask;
        }

        public Task LeaveAsync()
        {
            NetworkManager.Singleton?.Shutdown();
            return Task.CompletedTask;
        }
    }
}
