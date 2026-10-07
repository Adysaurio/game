using NUnit.Framework;
using TrashPandas.Core.Input;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class MouseLookFilterTests
    {
        [Test]
        public void Filter_PassesNormalMovement()
        {
            var filter = new MouseLookFilter();
            Assert.AreEqual(new Vector2(12f, -3f), filter.Filter(new Vector2(12f, -3f)));
        }

        [Test]
        public void Filter_IgnoresSettleFramesAfterLock()
        {
            var filter = new MouseLookFilter { SettleFrames = 2 };
            filter.NotifyLockChanged();
            Assert.AreEqual(Vector2.zero, filter.Filter(new Vector2(500f, 0f)));
            Assert.AreEqual(Vector2.zero, filter.Filter(new Vector2(5f, 0f)));
            Assert.AreEqual(new Vector2(5f, 0f), filter.Filter(new Vector2(5f, 0f)));
        }

        [Test]
        public void Filter_DropsSpikesAndNaN()
        {
            var filter = new MouseLookFilter { MaxDeltaPerFrame = 300f };
            Assert.AreEqual(Vector2.zero, filter.Filter(new Vector2(800f, 0f)));
            Assert.AreEqual(Vector2.zero, filter.Filter(new Vector2(float.NaN, 1f)));
            Assert.AreEqual(new Vector2(200f, 0f), filter.Filter(new Vector2(200f, 0f)));
        }
    }
}
