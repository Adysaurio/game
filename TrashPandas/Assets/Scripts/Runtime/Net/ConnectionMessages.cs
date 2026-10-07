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
                    return "We couldn't find that room. Check the code, or ask the host for a new one.";
                case SessionError.RateLimitExceeded:
                    return "Too many attempts. Please wait a few seconds and try again.";
                case SessionError.NotAuthorized:
                case SessionError.Forbidden:
                    return "This room is closed or full.";
                default:
                    return Generic;
            }
        }

        public static string ForException(Exception e)
        {
            if (e is SessionException se) return ForSessionError(se.Error);
            string text = e.Message ?? "";
            if (text.IndexOf("project", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("id", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Online play isn't set up yet: link this Unity project to Unity Cloud (Edit > Project Settings > Services).";
            return Generic;
        }
    }
}
