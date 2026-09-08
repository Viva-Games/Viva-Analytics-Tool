using System.Collections.Generic;
using NUnit.Framework;
using Viva.Services.RemoteConfig.Editor;

namespace Viva.Services.RemoteConfig.Tests
{
    public class RemoteConfigValidatorTests
    {
        private static RemoteConfigParameterDefinition P(string key, string type, string value) =>
            new RemoteConfigParameterDefinition(key, type, value);

        [Test]
        public void ValidList_HasNoErrors()
        {
            var result = RemoteConfigValidator.Validate(new List<RemoteConfigParameterDefinition>
            {
                P("lives", "int", "5"),
                P("startInterstitialsLevel", "int", "15"),
                P("bigNumber", "long", "3000000000"),
                P("ratio", "float", "2.5"),
                P("precise", "double", "-0.001"),
                P("enabled", "bool", "true"),
                P("nicknameAllowlist", "string", ""),
                P("idfa_list", "json", "{\"entries\":[]}"),
                P("_underscore", "string", "x"),
            });

            Assert.IsTrue(result.IsValid, string.Join("\n", result.Errors));
            Assert.AreEqual(0, result.Warnings.Count);
        }

        [TestCase("", "empty")]
        [TestCase("1lives", "must start with a letter")]
        [TestCase("my-key", "only letters, digits and underscores")]
        [TestCase("my key", "only letters, digits and underscores")]
        [TestCase("ñandu", "only letters, digits and underscores")]
        public void InvalidKeys(string key, string expectedFragment)
        {
            StringAssert.Contains(expectedFragment, RemoteConfigValidator.ValidateKey(key));
        }

        [Test]
        public void KeyLongerThan256_IsRejected()
        {
            Assert.IsNull(RemoteConfigValidator.ValidateKey(new string('a', 256)));
            StringAssert.Contains("longer than 256", RemoteConfigValidator.ValidateKey(new string('a', 257)));
        }

        [Test]
        public void Keys_AreCaseSensitiveAndNeverNormalized()
        {
            Assert.IsNull(RemoteConfigValidator.ValidateKey("startInterstitialsLevel"));
            Assert.IsNull(RemoteConfigValidator.ValidateKey("START_INTERSTITIALS_LEVEL"));
            Assert.IsNull(RemoteConfigValidator.ValidateKey("start_interstitials_level"));
        }

        [Test]
        public void DuplicatedKey_IsAnError()
        {
            var result = RemoteConfigValidator.Validate(new List<RemoteConfigParameterDefinition> { P("lives", "int", "1"), P("lives", "int", "2") });
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.HasErrorAt(1));
            StringAssert.Contains("Duplicated key", result.ErrorsAt(1));
        }

        [Test]
        public void KeysProducingTheSamePropertyName_AreAnError()
        {
            var result = RemoteConfigValidator.Validate(new List<RemoteConfigParameterDefinition> { P("lives", "int", "1"), P("Lives", "int", "2") });
            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("would both generate the property Lives", result.ErrorsAt(1));
        }

        [Test]
        public void ReservedPropertyNames_AreAnError()
        {
            var result = RemoteConfigValidator.Validate(new List<RemoteConfigParameterDefinition> { P("keys", "int", "1"), P("defaults", "int", "2") });
            Assert.AreEqual(2, result.Errors.Count);
            StringAssert.Contains("reserved", result.ErrorsAt(0));
        }

        [TestCase("int", "5", null)]
        [TestCase("int", "2.5", "not a valid int")]
        [TestCase("int", "3000000000", "not a valid int")]
        [TestCase("long", "3000000000", null)]
        [TestCase("long", "abc", "not a valid long")]
        [TestCase("float", "2.5", null)]
        [TestCase("float", "2,5", "not a valid float")]
        [TestCase("float", "1e40", "not a valid float")]
        [TestCase("double", "1e300", null)]
        [TestCase("double", "NaN", "NaN and infinity")]
        [TestCase("double", "Infinity", "NaN and infinity")]
        [TestCase("bool", "true", null)]
        [TestCase("bool", "false", null)]
        [TestCase("bool", "yes", "must be true or false")]
        [TestCase("bool", "True", "must be true or false")]
        [TestCase("string", "", null)]
        [TestCase("string", "anything, really", null)]
        [TestCase("json", "{\"a\":[1,2]}", null)]
        [TestCase("json", "{a:1}", "Invalid JSON")]
        [TestCase("json", "", "Invalid JSON")]
        [TestCase("color", "red", "Unknown type")]
        public void Values(string type, string value, string expectedErrorFragment)
        {
            string error = RemoteConfigValidator.ValidateValue(type, value);
            if (expectedErrorFragment == null) Assert.IsNull(error, error);
            else StringAssert.Contains(expectedErrorFragment, error);
        }

        [Test]
        public void UnknownType_IsAnErrorInTheList()
        {
            var result = RemoteConfigValidator.Validate(new List<RemoteConfigParameterDefinition> { P("lives", "color", "red") });
            Assert.IsFalse(result.IsValid);
            StringAssert.Contains("Unknown type", result.ErrorsAt(0));
        }

        [Test]
        public void FirebaseLimits_OnlyWarn()
        {
            var many = new List<RemoteConfigParameterDefinition>();
            for (int i = 0; i < RemoteConfigValidator.MAX_PARAMETERS + 1; i++) many.Add(P("k" + i, "int", "1"));
            var result = RemoteConfigValidator.Validate(many);
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(1, result.Warnings.Count);
            StringAssert.Contains("3000 parameters", result.Warnings[0].Message);

            var huge = RemoteConfigValidator.Validate(new List<RemoteConfigParameterDefinition>
            {
                P("big", "string", new string('x', RemoteConfigValidator.MAX_TOTAL_VALUE_LENGTH + 1))
            });
            Assert.IsTrue(huge.IsValid);
            StringAssert.Contains("total length", huge.Warnings[0].Message);
        }

        [Test]
        public void NormalizeValue_UsesInvariantCanonicalForms()
        {
            Assert.AreEqual("5", RemoteConfigValidator.NormalizeValue("int", " 5 "));
            Assert.AreEqual("2.5", RemoteConfigValidator.NormalizeValue("float", "2.50"));
            Assert.AreEqual("true", RemoteConfigValidator.NormalizeValue("bool", " TRUE "));
            Assert.AreEqual("abc", RemoteConfigValidator.NormalizeValue("int", "abc"), "invalid values are left as typed so the error stays visible");
            Assert.AreEqual("  keep  ", RemoteConfigValidator.NormalizeValue("string", "  keep  "));
        }
    }
}
