using System.Collections.Generic;
using NUnit.Framework;
using Viva.Services.Analytics.Facebook;
using Viva.Services.Analytics.Singular;

namespace Viva.Services.Analytics.Tests
{
    public class TargetsCodecAndConvertersTests
    {
        [Test]
        public void Generator_OnlyFirebase_KeepsTheClassicShape()
        {
            string code = AnalyticsEventCodeGenerator.Generate("LevelStart", new List<EventParameter> { new EventParameter("level", "int") });
            StringAssert.Contains("public class LevelStart : IAnalyticsEvent", code);
            StringAssert.DoesNotContain("Targets", code);
            Assert.AreEqual(AnalyticsTargets.Firebase, EventTargetsCodec.Parse(code));
        }

        [Test]
        public void Generator_WithFacebook_DeclaresTargets_AndTheEditorReadsThemBack()
        {
            var targets = AnalyticsTargets.Firebase | AnalyticsTargets.Facebook;
            string code = AnalyticsEventCodeGenerator.Generate("LevelReached10", new List<EventParameter>(), targets);

            StringAssert.Contains("public class LevelReached10 : IRoutedAnalyticsEvent", code);
            StringAssert.Contains("public AnalyticsTargets Targets => AnalyticsTargets.Firebase | AnalyticsTargets.Facebook;", code);
            Assert.AreEqual(targets, EventTargetsCodec.Parse(code));

            string withParameters = AnalyticsEventCodeGenerator.Generate("LevelComplete", new List<EventParameter> { new EventParameter("level", "int") }, AnalyticsTargets.All);
            StringAssert.Contains("IRoutedAnalyticsEvent", withParameters);
            Assert.AreEqual(AnalyticsTargets.All, EventTargetsCodec.Parse(withParameters));
        }

        [Test]
        public void Codec_ToCodeDescribeAndFromNames()
        {
            Assert.AreEqual("AnalyticsTargets.Firebase | AnalyticsTargets.Singular", EventTargetsCodec.ToCode(AnalyticsTargets.Singular));
            Assert.AreEqual("Firebase, Facebook, Singular", EventTargetsCodec.Describe(AnalyticsTargets.All));
            Assert.AreEqual(AnalyticsTargets.Firebase | AnalyticsTargets.Facebook, EventTargetsCodec.FromNames(new[] { "facebook" }));
            Assert.AreEqual(AnalyticsTargets.Firebase, EventTargetsCodec.FromNames(null));
            Assert.AreEqual(AnalyticsTargets.Firebase, EventTargetsCodec.FromNames(new[] { "unknown" }));
            Assert.AreEqual(AnalyticsTargets.Firebase, EventTargetsCodec.Parse("no targets line here"));
        }

        [Test]
        public void Catalog_ReadsTargets()
        {
            var parsed = UnityEngine.JsonUtility.FromJson<CatalogEvent>("{\"name\":\"level_reached10\",\"targets\":[\"facebook\"]}");
            Assert.AreEqual(AnalyticsTargets.Firebase | AnalyticsTargets.Facebook, parsed.ToTargets());
            var without = UnityEngine.JsonUtility.FromJson<CatalogEvent>("{\"name\":\"level_start\"}");
            Assert.AreEqual(AnalyticsTargets.Firebase, without.ToTargets());
        }

        [TestCase("level_reached10", null)]
        [TestCase("Level Complete-2", null)]
        [TestCase("a", "between 2 and 40")]
        [TestCase("_starts_with_underscore", "starting with a letter or digit")]
        [TestCase("has.dot", "letters, digits")]
        [TestCase("this_event_name_is_definitely_longer_than_forty_chars", "between 2 and 40")]
        public void Facebook_NameRules(string name, string expectedFragment)
        {
            string error = FacebookEventConverter.ValidateName(name);
            if (expectedFragment == null) Assert.IsNull(error, error);
            else StringAssert.Contains(expectedFragment, error);
        }

        [Test]
        public void Facebook_ParameterConversionAndLimits()
        {
            var fields = new Dictionary<string, object>();
            for (int i = 0; i < 27; i++) fields["p" + i] = i;
            fields["p0"] = true;
            fields["p1"] = new string('x', 150);
            fields["p2"] = 2.5;
            fields["p3"] = null;

            var parameters = FacebookEventConverter.ToParameters(fields, out var dropped);

            Assert.AreEqual(25, parameters.Count);
            Assert.AreEqual(2, dropped.Count);
            Assert.AreEqual(1, parameters["p0"]);
            Assert.AreEqual(100, ((string)parameters["p1"]).Length);
            Assert.AreEqual(2.5, parameters["p2"]);
            Assert.AreEqual(string.Empty, parameters["p3"]);

            var issues = FacebookEventConverter.Validate("ok_event", new List<string>(fields.Keys));
            Assert.AreEqual(1, issues.Count);
            StringAssert.Contains("up to 25 parameters", issues[0]);
        }

        [TestCase("level_reached10", null)]
        [TestCase("exactly_thirty_two_characters_ab", null)]
        [TestCase("this_name_is_thirty_three_charsss", "longer than 32")]
        [TestCase("ñandu", "must be ASCII")]
        public void Singular_NameRules(string name, string expectedFragment)
        {
            string error = SingularEventConverter.ValidateName(name);
            if (expectedFragment == null) Assert.IsNull(error, error);
            else StringAssert.Contains(expectedFragment, error);
        }

        [Test]
        public void Singular_AttributeConversion()
        {
            var attributes = SingularEventConverter.ToAttributes(new Dictionary<string, object>
            {
                { "flag", true }, { "count", 7L }, { "ratio", 0.5f }, { "text", new string('y', 600) }, { "none", null }
            });

            Assert.AreEqual("true", attributes["flag"]);
            Assert.AreEqual(7L, attributes["count"]);
            Assert.AreEqual(0.5f, attributes["ratio"]);
            Assert.AreEqual(500, ((string)attributes["text"]).Length);
            Assert.AreEqual(string.Empty, attributes["none"]);
        }

        [Test]
        public void AnalyticsSettings_AlwaysHasAnInstance()
        {
            AnalyticsSettings.ClearCache();
            Assert.IsNotNull(AnalyticsSettings.Instance, "without an asset the runtime uses the defaults");
            Assert.AreEqual(AnalyticsSettings.Instance.attributeAdRevenueInSingular, AnalyticsSettings.AttributeAdRevenueInSingular);
            Assert.IsTrue(UnityEngine.ScriptableObject.CreateInstance<AnalyticsSettings>().attributeAdRevenueInSingular, "attribution is on by default");
        }

        [Test]
        public void VivaUserId_IsStableAndResettable()
        {
            VivaUserId.Clear();
            Assert.IsFalse(VivaUserId.Exists);

            string first = VivaUserId.GetOrCreate();
            Assert.AreEqual(32, first.Length);
            Assert.IsTrue(VivaUserId.Exists);
            Assert.AreEqual(first, VivaUserId.GetOrCreate(), "stable between calls");

            string second = VivaUserId.Reset();
            Assert.AreNotEqual(first, second);
            Assert.AreEqual(second, VivaUserId.GetOrCreate());
            VivaUserId.Clear();
        }
    }
}
