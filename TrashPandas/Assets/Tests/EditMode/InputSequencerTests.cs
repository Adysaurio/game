using NUnit.Framework;
using TrashPandas.Core.Session;

namespace TrashPandas.Tests
{
    public class InputSequencerTests
    {
        [Test]
        public void InputSequencer_DropsStaleInputs()
        {
            var seq = new InputSequencer();
            Assert.IsTrue(seq.Accept(0, 5));
            Assert.IsFalse(seq.Accept(0, 5), "duplicate");
            Assert.IsFalse(seq.Accept(0, 3), "older, arrived late");
            Assert.IsTrue(seq.Accept(0, 9));
        }

        [Test]
        public void InputSequencer_TracksPlayersIndependently()
        {
            var seq = new InputSequencer();
            Assert.IsTrue(seq.Accept(0, 100));
            Assert.IsTrue(seq.Accept(1, 1), "player 1 has its own counter");
            seq.Reset(0);
            Assert.IsTrue(seq.Accept(0, 1), "a reconnecting/reset player starts over");
        }
    
        [Test]
        public void InputSequencer_SilentPlayer_GoesStale()
        {
            var seq = new InputSequencer();
            seq.Accept(0, 1, now: 10f);
            Assert.IsTrue(seq.IsFresh(0, now: 10.4f, maxAge: 0.5f));
            Assert.IsFalse(seq.IsFresh(0, now: 10.6f, maxAge: 0.5f), "no packets for a while: stop obeying the last one");
            Assert.IsFalse(seq.IsFresh(7, now: 10f, maxAge: 0.5f), "unknown player");
        }

        [Test]
        public void InputSequencer_RejectedPacket_DoesNotRefreshTimestamp()
        {
            var seq = new InputSequencer();
            seq.Accept(0, 5, now: 10f);
            seq.Accept(0, 3, now: 11f); // stale, arrives late
            Assert.IsFalse(seq.IsFresh(0, now: 11f, maxAge: 0.5f));
        }
    }
}
