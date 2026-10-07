using System.Threading.Tasks;

namespace TrashPandas.Runtime.Net
{
    /// <summary>How players find each other. The game never talks to Relay or Steam directly.</summary>
    public interface ISessionConnector
    {
        /// <summary>Starts hosting and returns what other players type to join (a room code or an address).</summary>
        Task<string> HostAsync(int maxPlayers);

        /// <summary>Joins a host. Throws with a message from <see cref="ConnectionMessages"/> on failure.</summary>
        Task JoinAsync(string code);

        Task LeaveAsync();
    }
}
