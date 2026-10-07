using System.Collections.Generic;
using NUnit.Framework;
using TrashPandas.Core.Panic;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class ChaserMindTests
    {
        static List<ChaseTarget> T(params (int id, Vector3 pos)[] items)
        {
            var list = new List<ChaseTarget>();
            foreach (var (id, pos) in items) list.Add(new ChaseTarget { Id = id, Position = pos });
            return list;
        }

        [Test]
        public void FetchesWeaponFirst()
        {
            var m = new ChaserMind();
            var o = m.Update(0.1f, Vector3.zero, hasWeapon: false, weapon: new Vector3(3, 0, 0), T((1, new Vector3(0, 0, 5))));
            Assert.AreEqual(ChaserState.FetchWeapon, o.State);
            Assert.AreEqual(new Vector3(3, 0, 0), o.Destination);
            Assert.IsFalse(o.Swing);
        }

        [Test]
        public void NoWeaponAvailable_ChasesBareHanded()
        {
            var m = new ChaserMind();
            var o = m.Update(0.1f, Vector3.zero, false, null, T((1, new Vector3(0, 0, 5))));
            Assert.AreEqual(ChaserState.Chase, o.State);
            Assert.AreEqual(1, o.TargetId);
        }

        [Test]
        public void ChasesNearestTarget_AndSticksToIt()
        {
            var m = new ChaserMind();
            var o = m.Update(0.1f, Vector3.zero, true, null, T((1, new Vector3(0, 0, 8)), (2, new Vector3(0, 0, 3))));
            Assert.AreEqual(2, o.TargetId);
            Assert.AreEqual(new Vector3(0, 0, 3), o.Destination);
            o = m.Update(0.1f, Vector3.zero, true, null, T((1, new Vector3(0, 0, 3.5f)), (2, new Vector3(0, 0, 3.2f))));
            Assert.AreEqual(2, o.TargetId, "doesn't flip-flop between similar targets");
        }

        [Test]
        public void SwingsInRange_RespectsCooldown()
        {
            var m = new ChaserMind();
            var near = T((1, new Vector3(0, 0, 1f)));
            Assert.IsTrue(m.Update(0.1f, Vector3.zero, true, null, near).Swing);
            Assert.IsFalse(m.Update(0.5f, Vector3.zero, true, null, near).Swing, "cooling down");
            Assert.IsFalse(m.Update(0.5f, Vector3.zero, true, null, near).Swing);
            Assert.IsTrue(m.Update(0.3f, Vector3.zero, true, null, near).Swing, "1.2 s later");
        }

        [Test]
        public void OutOfRange_NoSwing()
        {
            var m = new ChaserMind();
            Assert.IsFalse(m.Update(0.1f, Vector3.zero, true, null, T((1, new Vector3(0, 0, 3f)))).Swing);
        }

        [Test]
        public void RetargetsWhenTargetGone()
        {
            var m = new ChaserMind();
            m.Update(0.1f, Vector3.zero, true, null, T((1, new Vector3(0, 0, 2)), (2, new Vector3(0, 0, 6))));
            var o = m.Update(0.1f, Vector3.zero, true, null, T((2, new Vector3(0, 0, 6))));
            Assert.AreEqual(2, o.TargetId);
        }

        [Test]
        public void NoTargets_Idle()
        {
            var m = new ChaserMind();
            var o = m.Update(0.1f, new Vector3(1, 0, 1), true, null, new List<ChaseTarget>());
            Assert.AreEqual(ChaserState.Idle, o.State);
            Assert.AreEqual(-1, o.TargetId);
        }
    }

    public class WeaponAssignerTests
    {
        [Test]
        public void EachHumanGetsTheNearestFreeWeapon()
        {
            var humans = new[] { new Vector3(0, 0, 0), new Vector3(10, 0, 0) };
            var weapons = new[] { new Vector3(9, 0, 0), new Vector3(1, 0, 0) };
            CollectionAssert.AreEqual(new[] { 1, 0 }, WeaponAssigner.Assign(humans, weapons));
        }

        [Test]
        public void TwoHumansSameBroom_OnlyOneGetsIt()
        {
            var humans = new[] { new Vector3(0, 0, 0), new Vector3(0.5f, 0, 0) };
            var weapons = new[] { new Vector3(1, 0, 0) };
            var result = WeaponAssigner.Assign(humans, weapons);
            Assert.AreEqual(1, System.Array.FindAll(result, w => w == 0).Length);
            Assert.Contains(-1, result);
            Assert.AreEqual(0, result[1], "the closer human gets it");
        }
    }

    public class ExitZonesTests
    {
        [Test]
        public void Contains_UsesGroundDistance()
        {
            var exits = new[] { new Vector3(0, 0, -9), new Vector3(15, 0, 8) };
            Assert.AreEqual(0, ExitZones.Contains(exits, new Vector3(0.5f, 1f, -9.3f), 1.2f));
            Assert.AreEqual(1, ExitZones.Contains(exits, new Vector3(15, 0, 8), 1.2f));
            Assert.AreEqual(-1, ExitZones.Contains(exits, new Vector3(5, 0, 0), 1.2f));
        }
    }
}
