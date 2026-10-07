using NUnit.Framework;
using TrashPandas.Core.Session;
using TrashPandas.Core.Trenchcoat;
using TrashPandas.Runtime.Net;
using Unity.Collections;
using Unity.Netcode;

namespace TrashPandas.Tests.Net
{
    public class SlotsSnapshotTests
    {
        static SessionRoster Started(params ulong[] clients)
        {
            var roster = new SessionRoster();
            foreach (var c in clients) roster.Join(c);
            roster.StartRound();
            return roster;
        }

        [Test]
        public void From_DescribesEverySeat()
        {
            var roster = Started(42, 7, 13);
            var snap = SlotsSnapshot.From(roster);

            Assert.AreEqual(3, snap.SlotCount);
            Assert.AreEqual(42UL, snap.OccupantClient(0));
            Assert.AreEqual(7UL, snap.OccupantClient(1));
            Assert.AreEqual(BodyPart.Arms, snap.PartsOf(1));
            Assert.AreEqual(2, snap.SlotOfClient(13));
            Assert.AreEqual(BodyPart.None, snap.MissingParts);
        }

        [Test]
        public void EmptySeat_AfterLeavingCoatOrDisconnecting()
        {
            var roster = Started(1, 2, 3);
            roster.Slots.Leave(roster.PlayerIdOf(2).Value); // hopped out as a raccoon
            roster.Leave(3);                                // disconnected
            var snap = SlotsSnapshot.From(roster);

            Assert.IsNull(snap.OccupantClient(1));
            Assert.IsNull(snap.OccupantClient(2));
            Assert.IsNull(snap.SlotOfClient(2));
            Assert.AreEqual(BodyPart.Arms | BodyPart.Head, snap.MissingParts);
        }

        [Test]
        public void RoundTrips_OverTheNetwork()
        {
            var snap = SlotsSnapshot.From(Started(5, 6, 7, 8));
            using var writer = new FastBufferWriter(256, Allocator.Temp);
            writer.WriteNetworkSerializable(snap);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out SlotsSnapshot back);

            Assert.AreEqual(4, back.SlotCount);
            Assert.AreEqual(8UL, back.OccupantClient(3));
            Assert.AreEqual(BodyPart.LegRight, back.PartsOf(1));
            Assert.IsTrue(snap.Equals(back));
        }

        [Test]
        public void Default_IsEmpty()
        {
            var snap = default(SlotsSnapshot);
            Assert.AreEqual(0, snap.SlotCount);
            Assert.IsNull(snap.SlotOfClient(1));
        }
    }
}
