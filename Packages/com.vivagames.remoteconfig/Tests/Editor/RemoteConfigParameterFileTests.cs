using System.Collections.Generic;
using NUnit.Framework;
using Viva.Services.RemoteConfig.Editor;

namespace Viva.Services.RemoteConfig.Tests
{
    public class RemoteConfigParameterFileTests
    {
        [Test]
        public void SerializeAndParse_RoundTrip()
        {
            var parameters = new List<RemoteConfigParameterDefinition>
            {
                new RemoteConfigParameterDefinition("lives", "int", "5", "Max lives"),
                new RemoteConfigParameterDefinition("idfa_list", "json", "{\"entries\":[{\"id\":\"x\"}]}", ""),
                new RemoteConfigParameterDefinition("text", "string", "line1\nline2 \"quoted\"", "multi\nline"),
            };

            string json = RemoteConfigParameterFile.Serialize(parameters);
            var parsed = RemoteConfigParameterFile.Parse(json);

            Assert.AreEqual(3, parsed.Count);
            for (int i = 0; i < parameters.Count; i++)
            {
                Assert.AreEqual(parameters[i].key, parsed[i].key);
                Assert.AreEqual(parameters[i].type, parsed[i].type);
                Assert.AreEqual(parameters[i].defaultValue, parsed[i].defaultValue);
                Assert.AreEqual(parameters[i].description, parsed[i].description);
            }
            Assert.IsTrue(JsonSyntaxValidator.Validate(json, out _), "the file itself is valid JSON");
            StringAssert.Contains("\"parameters\"", json);
        }

        [Test]
        public void Parse_ToleratesEmptyOrMissingContent()
        {
            Assert.AreEqual(0, RemoteConfigParameterFile.Parse("").Count);
            Assert.AreEqual(0, RemoteConfigParameterFile.Parse("{}").Count);
            Assert.AreEqual(0, RemoteConfigParameterFile.Parse("{\"parameters\":[]}").Count);
            Assert.AreEqual(0, RemoteConfigParameterFile.Load("Assets/does/not/exist.json").Count);
        }

        [Test]
        public void Parse_FillsMissingFieldsWithDefaults()
        {
            var parsed = RemoteConfigParameterFile.Parse("{\"parameters\":[{\"key\":\"lives\"}]}");
            Assert.AreEqual(1, parsed.Count);
            Assert.AreEqual("lives", parsed[0].key);
            Assert.AreEqual("int", parsed[0].type);
            Assert.AreEqual(string.Empty, parsed[0].defaultValue);
            Assert.AreEqual(string.Empty, parsed[0].description);
        }
    }
}
