using NUnit.Framework;
using Viva.Services.RemoteConfig.Editor;

namespace Viva.Services.RemoteConfig.Tests
{
    public class JsonSyntaxValidatorTests
    {
        [TestCase("{}")]
        [TestCase("[]")]
        [TestCase("{\"a\":1}")]
        [TestCase("{ \"a\" : [1, 2.5, -3e10, 1E-2, 0, 0.5], \"b\" : { \"c\" : null, \"d\" : true, \"e\" : false } }")]
        [TestCase("[\"escapes: \\\" \\\\ \\/ \\b \\f \\n \\r \\t \\u00e9\"]")]
        [TestCase("\n\t{\n\t\t\"entries\": []\n\t}\n")]
        [TestCase("\"just a string\"")]
        [TestCase("42")]
        [TestCase("true")]
        [TestCase("{\"unicode\":\"ñandú 日本\"}")]
        public void Valid(string json)
        {
            Assert.IsTrue(JsonSyntaxValidator.Validate(json, out string error), error);
            Assert.IsNull(error);
        }

        [TestCase("", "empty")]
        [TestCase("   ", "empty")]
        [TestCase(null, "empty")]
        [TestCase("{\"a\":1,}", "trailing comma")]
        [TestCase("[1,]", "trailing comma")]
        [TestCase("{'a':1}", "double-quoted")]
        [TestCase("{\"a\":'x'}", "double quotes")]
        [TestCase("{\"a\":NaN}", "unexpected character")]
        [TestCase("{\"a\":01}", "leading zeros")]
        [TestCase("{\"a\":1.}", "digits after the decimal point")]
        [TestCase("{\"a\":1e}", "digits in the exponent")]
        [TestCase("{\"a\":\"unterminated}", "unterminated string")]
        [TestCase("{\"a\":1", "unterminated object")]
        [TestCase("[1", "unterminated array")]
        [TestCase("{\"a\" 1}", "expected ':'")]
        [TestCase("{\"a\":1} x", "unexpected content after")]
        [TestCase("{\"a\":\"bad \\x escape\"}", "invalid escape")]
        [TestCase("{\"a\":\"\\u12\"}", "four hex digits")]
        [TestCase("{\"a\":tru}", "expected true")]
        [TestCase("{\"a\":\"line\nbreak\"}", "control character")]
        [TestCase("{a:1}", "double-quoted")]
        public void Invalid(string json, string expectedErrorFragment)
        {
            Assert.IsFalse(JsonSyntaxValidator.Validate(json, out string error));
            StringAssert.Contains(expectedErrorFragment, error);
        }

        [Test]
        public void Error_ReportsLineAndColumn()
        {
            Assert.IsFalse(JsonSyntaxValidator.Validate("{\n  \"a\": 1,\n  \"b\": x\n}", out string error));
            StringAssert.Contains("line 3", error);
            StringAssert.Contains("column 8", error);
        }
    }
}
