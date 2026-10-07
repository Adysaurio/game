using System;
using System.Threading.Tasks;
using TrashPandas.Core.Session;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace TrashPandas.Runtime.Net
{
    /// <summary>
    /// Internet play through Unity Relay: the host gets a short room code; friends type it to join.
    /// The Multiplayer Services session starts Netcode's host/client for us.
    /// </summary>
    public sealed class RelayConnector : ISessionConnector
    {
        ISession _session;

        static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                // A per-launch profile keeps several local instances (Multiplayer Play Mode) from sharing one identity.
                var options = new InitializationOptions().SetProfile("p" + Guid.NewGuid().ToString("N").Substring(0, 8));
                await UnityServices.InitializeAsync(options);
            }
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        public async Task<string> HostAsync(int maxPlayers)
        {
            try
            {
                await EnsureSignedInAsync();
                var options = new SessionOptions { MaxPlayers = maxPlayers }.WithRelayNetwork();
                _session = await MultiplayerService.Instance.CreateSessionAsync(options);
                return _session.Code;
            }
            catch (Exception e) { throw new InvalidOperationException(ConnectionMessages.ForException(e), e); }
        }

        public async Task JoinAsync(string typed)
        {
            string code = JoinCode.Normalize(typed);
            if (!JoinCode.IsPlausible(code))
                throw new InvalidOperationException("That doesn't look like a room code. Codes are letters and numbers, like XK429P.");
            try
            {
                await EnsureSignedInAsync();
                _session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            }
            catch (Exception e) { throw new InvalidOperationException(ConnectionMessages.ForException(e), e); }
        }

        public async Task LeaveAsync()
        {
            try { if (_session != null) await _session.LeaveAsync(); }
            catch (Exception) { /* leaving a session that already closed is fine */ }
            finally
            {
                _session = null;
                if (NetworkManager.Singleton && NetworkManager.Singleton.IsListening) NetworkManager.Singleton.Shutdown();
            }
        }
    }
}
