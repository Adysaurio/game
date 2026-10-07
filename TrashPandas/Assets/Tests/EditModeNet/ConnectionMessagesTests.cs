using System;
using NUnit.Framework;
using TrashPandas.Runtime.Net;
using Unity.Services.Multiplayer;

namespace TrashPandas.Tests.Net
{
    public class ConnectionMessagesTests
    {
        [Test]
        public void WrongCode_TellsThePlayerToCheckIt()
        {
            StringAssert.Contains("couldn't find that room", ConnectionMessages.ForSessionError(SessionError.SessionNotFound));
            StringAssert.Contains("couldn't find that room", ConnectionMessages.ForSessionError(SessionError.InvalidSessionIdentifier));
        }

        [Test]
        public void RateLimit_AsksToWait()
        {
            StringAssert.Contains("wait", ConnectionMessages.ForSessionError(SessionError.RateLimitExceeded));
        }

        [Test]
        public void UnlinkedProject_ExplainsTheFix()
        {
            StringAssert.Contains("Unity Cloud", ConnectionMessages.ForException(new InvalidOperationException("No cloud project ID was found")));
        }

        [Test]
        public void UnknownErrors_GiveAGenericButUsefulMessage()
        {
            string msg = ConnectionMessages.ForSessionError(SessionError.Unknown);
            StringAssert.Contains("internet", msg);
        }
    }
}
