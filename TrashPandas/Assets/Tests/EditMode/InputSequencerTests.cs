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
    }
}
