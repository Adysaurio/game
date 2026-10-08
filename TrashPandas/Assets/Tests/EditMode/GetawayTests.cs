using NUnit.Framework;
using TrashPandas.Core.Round;

namespace TrashPandas.Tests
{
    public class GetawayTests
    {
        const byte Picked = 0b0111;

        [Test]
        public void ExitsStayShut_UntilEveryObjectiveIsDelivered()
        {
            Assert.IsFalse(Getaway.ExitsOpen(Picked, 0, 300f));
            Assert.IsFalse(Getaway.ExitsOpen(Picked, 0b0011, 300f), "two of three");
            Assert.IsTrue(Getaway.ExitsOpen(Picked, 0b0111, 300f), "all three: GETAWAY");
        }

        [Test]
        public void ExtraLootDoesNotMatter_OnlyTheObjectives()
        {
            Assert.IsTrue(Getaway.ExitsOpen(Picked, 0b1111, 300f));
        }

        [Test]
        public void NoObjectivesYet_ExitsShut()
        {
            Assert.IsFalse(Getaway.ExitsOpen(0, 0, 300f), "objectives not rolled yet");
        }

        [Test]
        public void TimeUp_ThePartyIsOver_ExitsOpenAnyway()
        {
            Assert.IsTrue(Getaway.PartyOver(0f));
            Assert.IsFalse(Getaway.PartyOver(0.5f));
            Assert.IsTrue(Getaway.ExitsOpen(Picked, 0, 0f));
        }
    }
}
