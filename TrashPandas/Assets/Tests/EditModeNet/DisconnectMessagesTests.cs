using System;
using NUnit.Framework;
using TrashPandas.Runtime.Net;

namespace TrashPandas.Tests.Net
{
    public class DisconnectMessagesTests
    {
        [Test]
        public void LostHostAfterPlaying_SaysHostLeft()
        {
            StringAssert.Contains("host left", ConnectionMessages.ForClientStopped(wasConnected: true, leftByChoice: false, reason: null));
        }

        [Test]
        public void NeverConnected_SaysCouldNotReach()
        {
            StringAssert.Contains("Couldn't reach", ConnectionMessages.ForClientStopped(wasConnected: false, leftByChoice: false, reason: null));
        }

        [Test]
        public void LeftByChoice_NoMessage()
        {
            Assert.IsNull(ConnectionMessages.ForClientStopped(wasConnected: true, leftByChoice: true, reason: null));
        }

        [Test]
        public void ServerGaveAReason_ShowsIt()
        {
            Assert.AreEqual("The game already started.", ConnectionMessages.ForClientStopped(true, false, "The game already started."));
        }

        [Test]
        public void FullRoom_FromInnerServiceError()
        {
            var e = new InvalidOperationException("Request failed", new Exception("Lobby is full"));
            StringAssert.Contains("full", ConnectionMessages.ForException(e));
        }

        [Test]
        public void InvalidCode_FromInnerServiceError()
        {
            var e = new InvalidOperationException("Request failed", new Exception("Bad Request: validation error on join code"));
            StringAssert.Contains("couldn't find that room", ConnectionMessages.ForException(e));
        }
    }
}
