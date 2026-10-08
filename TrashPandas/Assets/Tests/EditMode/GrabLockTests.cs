using System.Collections.Generic;
using NUnit.Framework;
using TrashPandas.Core.Grabbing;
using UnityEngine;

namespace TrashPandas.Tests
{
    public class GrabLockTests
    {
        static readonly List<GrabCandidate> Table = new List<GrabCandidate>
        {
            new GrabCandidate { Id = 1, Position = new Vector3(0.5f, 0.9f, 3.6f) },   // glass
            new GrabCandidate { Id = 2, Position = new Vector3(0.75f, 0.9f, 3.6f) },  // wallet
        };

        [Test]
        public void AimingAtSomething_LocksOntoIt()
        {
            Assert.AreEqual(2, GrabTargeting.LockedTarget(new Vector3(0.76f, 0.9f, 3.6f), Table, 0.2f));
        }

        [Test]
        public void AimingAtNothingInParticular_NoLock()
        {
            Assert.IsNull(GrabTargeting.LockedTarget(new Vector3(3f, 0.9f, 3.6f), Table, 0.2f));
        }

        [Test]
        public void Locked_OnlyTheTargetMayBeGrabbed()
        {
            Assert.IsFalse(GrabTargeting.MayGrab(1, locked: 2), "passing by the glass on the way to the wallet");
            Assert.IsTrue(GrabTargeting.MayGrab(2, locked: 2));
            Assert.IsTrue(GrabTargeting.MayGrab(1, locked: null), "no lock: grab what the hand touches");
        }
    }
}
