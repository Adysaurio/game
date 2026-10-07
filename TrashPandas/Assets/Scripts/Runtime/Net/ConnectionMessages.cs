using System;
using Unity.Services.Multiplayer;

namespace TrashPandas.Runtime.Net
{
    /// <summary>Turns service and transport failures into messages a player can act on.</summary>
    public static class ConnectionMessages
    {
        public const string Generic = "Couldn't connect. Check your internet connection and try again.";

        public static string ForSessionError(SessionError error)
        {
            switch (error)
            {
                case SessionError.SessionNotFound:
                case SessionError.InvalidSessionIdentifier:
                case SessionError.SessionDeleted:
                    return RoomNotFound;
                case SessionError.RateLimitExceeded:
                    return "Too many attempts. Please wait a few seconds and try again.";
                case SessionError.NotAuthorized:
                case SessionError.Forbidden:
                    return "This room is closed or full.";
                default:
                    return Generic;
            }
        }

        public const string RoomNotFound = "We couldn't find that room. Check the code, or ask the host for a new one.";
        public const string RoomFull = "That room is full (5 players max).";

        /// <summary>What to tell a player whose connection stopped. Null when they left on purpose.</summary>
        public static string ForClientStopped(bool wasConnected, bool leftByChoice, string reason)
        {
            if (leftByChoice) return null;
            if (!string.IsNullOrEmpty(reason)) return reason;
            return wasConnected ? "The host left the game." : "Couldn't reach the host. Check the code and try again.";
        }

        public static string ForException(Exception e)
        {
            // Service errors often arrive wrapped and mapped to "Unknown": read the whole chain's text too.
            for (var inner = e; inner != null; inner = inner.InnerException)
            {
                string t = inner.Message ?? "";
                if (t.IndexOf("full", StringComparison.OrdinalIgnoreCase) >= 0) return RoomFull;
                if (t.IndexOf("not found", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.IndexOf("validation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    t.IndexOf("bad request", StringComparison.OrdinalIgnoreCase) >= 0) return RoomNotFound;
            }
            if (e is SessionException se && se.Error != SessionError.Unknown) return ForSessionError(se.Error);
            string text = e.Message ?? "";
            if (text.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Online play isn't set up yet: link this Unity project to Unity Cloud (Edit > Project Settings > Services).";
            return Generic;
        }
    }
}
