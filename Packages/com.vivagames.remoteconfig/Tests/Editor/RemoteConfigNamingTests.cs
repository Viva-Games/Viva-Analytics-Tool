using NUnit.Framework;
using Viva.Services.RemoteConfig.Editor;

namespace Viva.Services.RemoteConfig.Tests
{
    public class RemoteConfigNamingTests
    {
        [TestCase("lives", "Lives")]
        [TestCase("startInterstitialsLevel", "StartInterstitialsLevel")]
        [TestCase("idfa_list", "IdfaList")]
        [TestCase("MAX_LIVES", "MaxLives")]
        [TestCase("HTTPTimeout", "HttpTimeout")]
        [TestCase("level10Bonus", "Level10Bonus")]
        [TestCase("nickname_blocklist_exact", "NicknameBlocklistExact")]
        [TestCase("_private", "Private")]
        [TestCase("dailyChallengeSlots", "DailyChallengeSlots")]
        [TestCase("a", "A")]
        [TestCase("ab_", "Ab")]
        public void ToIdentifier_ProducesPascalCase(string key, string expected)
        {
            Assert.AreEqual(expected, RemoteConfigNaming.ToIdentifier(key));
        }

        [Test]
        public void ToIdentifier_HandlesEmptyAndDigitStart()
        {
            Assert.AreEqual("_", RemoteConfigNaming.ToIdentifier(""));
            Assert.AreEqual("_", RemoteConfigNaming.ToIdentifier(null));
            Assert.AreEqual("_", RemoteConfigNaming.ToIdentifier("_"));
            Assert.AreEqual("_1abc", RemoteConfigNaming.ToIdentifier("_1abc"));
        }

        [Test]
        public void IsValidIdentifier()
        {
            Assert.IsTrue(RemoteConfigNaming.IsValidIdentifier("Lives"));
            Assert.IsTrue(RemoteConfigNaming.IsValidIdentifier("_1abc"));
            Assert.IsFalse(RemoteConfigNaming.IsValidIdentifier(""));
            Assert.IsFalse(RemoteConfigNaming.IsValidIdentifier("1abc"));
            Assert.IsFalse(RemoteConfigNaming.IsValidIdentifier("a-b"));
        }

        [Test]
        public void ReservedIdentifiers_AreTheGeneratedMembers()
        {
            Assert.IsTrue(RemoteConfigNaming.IsReserved("Keys"));
            Assert.IsTrue(RemoteConfigNaming.IsReserved("Defaults"));
            Assert.IsFalse(RemoteConfigNaming.IsReserved("Lives"));
            Assert.AreEqual("Keys", RemoteConfigNaming.ToIdentifier("keys"), "so a parameter named keys is rejected by the validator");
        }
    }
}
