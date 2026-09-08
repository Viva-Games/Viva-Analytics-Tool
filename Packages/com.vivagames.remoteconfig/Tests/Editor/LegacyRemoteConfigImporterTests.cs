using System.Collections.Generic;
using NUnit.Framework;
using Viva.Services.RemoteConfig.Editor;

namespace Viva.Services.RemoteConfig.Tests
{
    /// <summary>Formas de llamada reales de Arrows: literales, constantes, defaults no literales, GetJson.</summary>
    public class LegacyRemoteConfigImporterTests
    {
        private const string AdManager = @"
using Viva.Services.Analytics;
public class AdManager
{
    void Configure()
    {
        int minLevel = RemoteConfigManager.GetInt(""startInterstitialsLevel"", 15);
        int timerSeconds = RemoteConfigManager.GetInt(""interstitialsTimer"", 125);
    }
}";

        private const string DailyChallenge = @"
public class DailyChallengeController
{
    private const string RC_SLOTS = ""dailyChallengeSlots"";
    private const string RC_UNLOCK_LEVEL = ""dailyChallengeUnlockLevel"";
    public int BaseSlots => RemoteConfigManager.GetInt(RC_SLOTS, config.baseSlots);
    bool Unlocked => level >= RemoteConfigManager.GetInt(RC_UNLOCK_LEVEL, Mathf.Max(1, config.unlockDisplayLevel));
}";

        private const string Leaderboard = @"
public class LeaderboardService
{
    private const string RC_ALLOWLIST = ""nicknameAllowlist"";
    void Apply()
    {
        var a = RemoteConfigManager.GetString(RC_ALLOWLIST, """");
        var b = RemoteConfigManager.GetString(""nicknameBlocklistExact"", string.Empty);
        var c = RemoteConfigManager.GetString(""verbatim"", @""a""""b"");
    }
}";

        private const string Idfa = @"
public class IdfaDebugController
{
    private const string RemoteConfigKey = ""idfa_list"";
    void Handle()
    {
        string rawJson = RemoteConfigManager.GetString(RemoteConfigKey, string.Empty);
        var parsed = RemoteConfigManager.GetJson<IdfaList>(""idfa_json"", default);
        bool flag = RemoteConfigManager.GetBool(""debugFlag"", true);
        bool other = RemoteConfigManager.GetBool(""debugFlag"");
    }
}";

        private const string LegacyManager = @"
namespace Viva.Services.Analytics
{
    public static class RemoteConfigManager
    {
        public static int GetInt(string key, int defaultValue = 0) { return RemoteConfigManager.GetInt(""ignored"", 1); }
    }
}";

        private static LegacyRemoteConfigImporter.ImportResult Scan(params (string path, string content)[] sources)
        {
            var list = new List<KeyValuePair<string, string>>();
            foreach (var source in sources) list.Add(new KeyValuePair<string, string>(source.path, source.content));
            return LegacyRemoteConfigImporter.ScanSources(list);
        }

        private static LegacyRemoteConfigImporter.ImportedParameter Find(LegacyRemoteConfigImporter.ImportResult result, string key)
        {
            foreach (var parameter in result.Parameters) if (parameter.Key == key) return parameter;
            Assert.Fail("key not imported: " + key);
            return null;
        }

        [Test]
        public void LiteralKeysAndDefaults()
        {
            var result = Scan(("Assets/Scripts/AdManager.cs", AdManager));

            Assert.AreEqual(1, result.ScannedFiles);
            Assert.AreEqual(2, result.CallSites);
            Assert.AreEqual(2, result.Parameters.Count);

            var minLevel = Find(result, "startInterstitialsLevel");
            Assert.AreEqual("int", minLevel.Type);
            Assert.IsTrue(minLevel.HasLiteralDefault);
            Assert.AreEqual("15", minLevel.DefaultValue);
            CollectionAssert.AreEqual(new[] { "AdManager.cs:7" }, minLevel.CallSites);
            Assert.AreEqual("125", Find(result, "interstitialsTimer").DefaultValue);
        }

        [Test]
        public void KeysThroughConstants_AndNonLiteralDefaults()
        {
            var result = Scan(("Assets/Scripts/DailyChallengeController.cs", DailyChallenge));

            var slots = Find(result, "dailyChallengeSlots");
            Assert.IsFalse(slots.HasLiteralDefault);
            Assert.AreEqual(string.Empty, slots.DefaultValue);
            Assert.AreEqual("config.baseSlots", slots.DefaultExpression);
            StringAssert.Contains("Needs a default value (the code used config.baseSlots)", slots.Note);

            var unlock = Find(result, "dailyChallengeUnlockLevel");
            Assert.AreEqual("Mathf.Max(1, config.unlockDisplayLevel)", unlock.DefaultExpression, "nested parentheses and commas are kept together");
            Assert.AreEqual(2, result.WithoutLiteralDefault);
        }

        [Test]
        public void StringDefaults_EmptyLiteral_StringEmpty_AndVerbatim()
        {
            var result = Scan(("Assets/Scripts/LeaderboardService.cs", Leaderboard));

            Assert.AreEqual("string", Find(result, "nicknameAllowlist").Type);
            Assert.IsTrue(Find(result, "nicknameAllowlist").HasLiteralDefault);
            Assert.AreEqual(string.Empty, Find(result, "nicknameAllowlist").DefaultValue);
            Assert.IsTrue(Find(result, "nicknameBlocklistExact").HasLiteralDefault);
            Assert.AreEqual(string.Empty, Find(result, "nicknameBlocklistExact").DefaultValue);
            Assert.AreEqual("a\"b", Find(result, "verbatim").DefaultValue);
        }

        [Test]
        public void GetJson_GetBool_AndCallsWithoutDefault()
        {
            var result = Scan(("Assets/Scripts/IdfaDebugController.cs", Idfa));

            Assert.AreEqual("string", Find(result, "idfa_list").Type);
            Assert.AreEqual("json", Find(result, "idfa_json").Type);
            Assert.IsFalse(Find(result, "idfa_json").HasLiteralDefault);

            var flag = Find(result, "debugFlag");
            Assert.AreEqual("bool", flag.Type);
            Assert.IsTrue(flag.HasLiteralDefault);
            Assert.AreEqual("true", flag.DefaultValue);
            Assert.AreEqual(2, flag.CallSites.Count, "the call without a default still counts as a call site");
        }

        [Test]
        public void SameKeyInSeveralFiles_MergesCallSites_AndFlagsConflicts()
        {
            const string other = @"
class Other
{
    void A() { var x = RemoteConfigManager.GetInt(""startInterstitialsLevel"", 20); }
    void B() { var y = RemoteConfigManager.GetString(""startInterstitialsLevel"", ""x""); }
}";
            var result = Scan(("Assets/A/AdManager.cs", AdManager), ("Assets/B/Other.cs", other));

            var parameter = Find(result, "startInterstitialsLevel");
            Assert.AreEqual(3, parameter.CallSites.Count);
            Assert.AreEqual("15", parameter.DefaultValue, "the first literal wins");
            Assert.AreEqual(2, parameter.Conflicts.Count);
            StringAssert.Contains("Default 20 in Other.cs:4 differs from 15", parameter.Conflicts[0]);
            StringAssert.Contains("Read as string in Other.cs:5 but as int elsewhere", parameter.Conflicts[1]);
        }

        [Test]
        public void TheLegacyManagerItself_IsSkipped()
        {
            var result = Scan(("Assets/VivaAnalytics/Scripts/RemoteConfigManager.cs", LegacyManager), ("Assets/Scripts/AdManager.cs", AdManager));

            Assert.AreEqual(1, result.ScannedFiles);
            Assert.AreEqual(2, result.Parameters.Count);
            Assert.IsNull(result.Parameters.Find(p => p.Key == "ignored"));
        }

        [Test]
        public void UnresolvableKey_IsReported()
        {
            const string source = @"
class X { void A() { var v = RemoteConfigManager.GetInt(SomeOtherClass.KEY, 1); } }";
            var result = Scan(("Assets/X.cs", source));

            Assert.AreEqual(0, result.Parameters.Count);
            Assert.AreEqual(1, result.Unresolved.Count);
            StringAssert.Contains("SomeOtherClass.KEY", result.Unresolved[0]);
        }

        [Test]
        public void BackupPath_UsesTheLegacyFolderNextToScripts()
        {
            Assert.AreEqual("Assets/VivaAnalytics/Legacy/RemoteConfigManager.legacy.txt",
                LegacyRemoteConfigImporter.BackupPathFor("Assets/VivaAnalytics/Scripts/RemoteConfigManager.cs"));
            Assert.AreEqual("Assets/Game/Legacy/RemoteConfigManager.legacy.txt",
                LegacyRemoteConfigImporter.BackupPathFor("Assets/Game/RemoteConfigManager.cs"));
        }
    }
}
