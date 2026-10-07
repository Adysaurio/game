using System;
using NUnit.Framework;
using TrashPandas.Core.Session;
using TrashPandas.Core.Trenchcoat;

namespace TrashPandas.Tests
{
    public class SessionRosterTests
    {
        [Test]
        public void Roster_JoinUpToFive()
        {
            var roster = new SessionRoster();
            for (ulong id = 10; id < 15; id++) Assert.IsTrue(roster.Join(id));
            Assert.IsFalse(roster.Join(99), "a sixth player is rejected");
            Assert.IsFalse(roster.Join(10), "joining twice is rejected");
            Assert.AreEqual(5, roster.Clients.Count);
        }

        [Test]
        public void Roster_RejectsJoinDuringRound()
        {
            var roster = new SessionRoster();
            roster.Join(1); roster.Join(2);
            roster.StartRound();
            Assert.IsFalse(roster.Join(3));
        }

        [Test]
        public void Roster_StartNeedsTwo()
        {
            var roster = new SessionRoster();
            roster.Join(1);
            Assert.Throws<InvalidOperationException>(() => roster.StartRound());
            roster.Join(2);
            Assert.DoesNotThrow(() => roster.StartRound());
            Assert.Throws<InvalidOperationException>(() => roster.StartRound(), "already started");
        }

        [Test]
        public void Roster_AssignsSlotsInJoinOrder()
        {
            var roster = new SessionRoster();
            roster.Join(42); roster.Join(7); roster.Join(13);
            var slots = roster.StartRound();

            Assert.AreEqual(3, slots.SlotCount);
            Assert.AreEqual(0, roster.PlayerIdOf(42));
            Assert.AreEqual(1, roster.PlayerIdOf(7));
            Assert.AreEqual(2, roster.PlayerIdOf(13));
            Assert.AreEqual(0, slots.SlotOf(0), "player 0 (first to join) takes the legs");
            Assert.AreEqual(BodyPart.Legs, slots.PartsOf(slots.SlotOf(roster.PlayerIdOf(42).Value).Value));
            Assert.AreEqual(7UL, roster.ClientOf(1));
            Assert.AreEqual(BodyPart.None, slots.MissingParts);
        }

        [Test]
        public void Roster_LeaveInLobby_RemovesPlayer()
        {
            var roster = new SessionRoster();
            roster.Join(1); roster.Join(2);
            Assert.IsTrue(roster.Leave(1));
            Assert.AreEqual(1, roster.Clients.Count);
            Assert.IsFalse(roster.Leave(1));
        }

        [Test]
        public void Roster_Disconnect_FreesSlot()
        {
            var roster = new SessionRoster();
            roster.Join(1); roster.Join(2); roster.Join(3);
            var slots = roster.StartRound();

            Assert.IsTrue(roster.Leave(2));
            Assert.AreEqual(BodyPart.Arms, slots.MissingParts, "player 1 (arms) is gone");
            Assert.IsNull(roster.PlayerIdOf(2));
            Assert.IsNull(roster.ClientOf(1));
            Assert.IsFalse(roster.Leave(2), "leaving twice is a no-op");
        }

        [Test]
        public void Roster_ReturnRace_OnlyOneEnters()
        {
            var roster = new SessionRoster();
            roster.Join(1); roster.Join(2); roster.Join(3);
            var slots = roster.StartRound();
            slots.Leave(0); slots.Leave(1); // both hop out as raccoons
            slots.TryEnter(0, 0);           // player 0 takes the legs back; only the arms slot is free

            int free = slots.FirstFreeSlot().Value;
            Assert.IsTrue(slots.TryEnter(1, free));
            Assert.IsFalse(slots.TryEnter(0, free), "already inside");
            Assert.IsNull(slots.FirstFreeSlot());
        }

        [Test]
        public void Roster_EndRound_BackToLobby_KeepsConnectedPlayers()
        {
            var roster = new SessionRoster();
            roster.Join(1); roster.Join(2); roster.Join(3);
            roster.StartRound();
            roster.Leave(3);
            roster.EndRound();

            Assert.IsFalse(roster.RoundStarted);
            Assert.IsNull(roster.Slots);
            CollectionAssert.AreEqual(new ulong[] { 1, 2 }, roster.Clients);
            Assert.IsTrue(roster.Join(4), "lobby is open again");
        }
    
        [Test]
        public void CanStart_NeedsTwoConnectedPlayers()
        {
            var roster = new SessionRoster();
            roster.Join(1);
            Assert.IsFalse(roster.CanStart);
            roster.Join(2);
            Assert.IsTrue(roster.CanStart);
            roster.StartRound();
            roster.Leave(2);
            Assert.IsFalse(roster.CanStart, "a restart needs two players still connected");
        }
    }
}
