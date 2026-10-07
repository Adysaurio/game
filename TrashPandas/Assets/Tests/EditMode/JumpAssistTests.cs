using NUnit.Framework;
using TrashPandas.Core.Movement;

namespace TrashPandas.Tests
{
    public class JumpAssistTests
    {
        [Test]
        public void GroundedPress_Jumps_OnlyOnce()
        {
            var jump = new JumpAssist();
            jump.SetGrounded(true, 1f);
            jump.Press(1f);
            Assert.IsTrue(jump.TryConsume(1f));
            Assert.IsFalse(jump.TryConsume(1.01f), "a press is consumed once");
        }

        [Test]
        public void PressJustBeforeLanding_IsBuffered()
        {
            var jump = new JumpAssist();
            jump.SetGrounded(false, 1f);
            jump.Press(1f);
            Assert.IsFalse(jump.TryConsume(1f), "still airborne");
            jump.SetGrounded(true, 1.1f);
            Assert.IsTrue(jump.TryConsume(1.1f), "buffered 0.1 s");
        }

        [Test]
        public void PressTooEarlyBeforeLanding_IsDropped()
        {
            var jump = new JumpAssist();
            jump.Press(1f);
            jump.SetGrounded(true, 1.3f);
            Assert.IsFalse(jump.TryConsume(1.3f));
        }

        [Test]
        public void PressJustAfterLeavingLedge_UsesCoyoteTime()
        {
            var jump = new JumpAssist();
            jump.SetGrounded(true, 1f);
            jump.SetGrounded(false, 1.05f);
            jump.Press(1.1f);
            Assert.IsTrue(jump.TryConsume(1.1f));
        }

        [Test]
        public void PressLongAfterLeavingLedge_NoJump()
        {
            var jump = new JumpAssist();
            jump.SetGrounded(true, 1f);
            jump.Press(1.25f);
            Assert.IsFalse(jump.TryConsume(1.25f));
        }
    }
}
