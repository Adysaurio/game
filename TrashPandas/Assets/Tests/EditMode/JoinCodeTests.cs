using NUnit.Framework;
using TrashPandas.Core.Session;

namespace TrashPandas.Tests
{
    public class JoinCodeTests
    {
        [TestCase(" xk4 29p ", "XK429P")]
        [TestCase("ab-cd-ef", "ABCDEF")]
        [TestCase("QWERTY", "QWERTY")]
        public void Normalize_UppercasesAndStripsSeparators(string typed, string expected)
        {
            Assert.AreEqual(expected, JoinCode.Normalize(typed));
        }

        [TestCase("XK429P", true)]
        [TestCase("", false)]
        [TestCase("ABC", false)]
        [TestCase("AB?CDE", false)]
        [TestCase("ABCDEFGHIJKLMNOP", false)]
        public void IsPlausible_ChecksLengthAndCharacters(string code, bool expected)
        {
            Assert.AreEqual(expected, JoinCode.IsPlausible(code));
        }

        [Test]
        public void Normalize_Null_IsEmpty()
        {
            Assert.AreEqual("", JoinCode.Normalize(null));
        }
    }
}
