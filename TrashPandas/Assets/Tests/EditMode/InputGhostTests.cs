using NUnit.Framework;
using TrashPandas.Core.Debugging;
using TrashPandas.Core.Trenchcoat;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class InputGhostTests
    {
        static SlotInput Walk(float x) => new SlotInput { Move = new Vector2(x, 0f), JumpPressedAt = float.NegativeInfinity };

        [Test]
        public void Ghost_ReplaysRecordingInALoop()
        {
            var ghost = new InputGhost();
            ghost.Record(10.0f, Walk(0.1f));
            ghost.Record(10.5f, Walk(0.2f));
            ghost.Record(11.0f, Walk(0.3f));
            ghost.StartPlayback(20f);

            Assert.IsTrue(ghost.HasRecording);
            Assert.AreEqual(0.1f, ghost.Sample(20.0f).Move.x, 1e-4f);
            Assert.AreEqual(0.2f, ghost.Sample(20.6f).Move.x, 1e-4f);
            Assert.AreEqual(0.3f, ghost.Sample(21.0f).Move.x, 1e-4f);
            Assert.AreEqual(0.1f, ghost.Sample(20.0f + ghost.Duration).Move.x, 1e-4f, "loops back to the start");
        }

        [Test]
        public void Ghost_ReplaysJumpOncePerLoop()
        {
            var ghost = new InputGhost();
            ghost.Record(0.0f, Walk(0f));
            var jumped = Walk(0f);
            jumped.JumpPressedAt = 0.5f;
            ghost.Record(0.5f, jumped);
            ghost.Record(1.0f, jumped); // the press time stays the same while held in the record
            ghost.StartPlayback(100f);

            Assert.IsTrue(float.IsNegativeInfinity(ghost.Sample(100.2f).JumpPressedAt), "no jump before the press");
            Assert.AreEqual(100.5f, ghost.Sample(100.5f).JumpPressedAt, 1e-4f);
            Assert.AreEqual(100.5f, ghost.Sample(100.9f).JumpPressedAt, 1e-4f, "same press, not a new one");
            float nextLoop = 100f + ghost.Duration;
            Assert.IsTrue(float.IsNegativeInfinity(ghost.Sample(nextLoop + 0.2f).JumpPressedAt), "earlier loop's press does not leak");
            Assert.AreEqual(nextLoop + 0.5f, ghost.Sample(nextLoop + 0.5f).JumpPressedAt, 1e-4f);
        }

        [Test]
        public void Ghost_Empty_ReturnsIdle()
        {
            var ghost = new InputGhost();
            ghost.StartPlayback(0f);
            Assert.IsFalse(ghost.HasRecording);
            Assert.AreEqual(Vector2.zero, ghost.Sample(1f).Move);
            Assert.IsTrue(float.IsNegativeInfinity(ghost.Sample(1f).JumpPressedAt));
        }
    }
}
