using NUnit.Framework;

namespace Viva.Services.RemoteConfig.Tests
{
    public class RemoteConfigConverterTests
    {
        [TestCase("1")]
        [TestCase("true")]
        [TestCase("TRUE")]
        [TestCase("t")]
        [TestCase("yes")]
        [TestCase("Y")]
        [TestCase("on")]
        [TestCase(" on ")]
        public void Bool_TrueStringsOfTheSdk(string raw)
        {
            Assert.IsTrue(RemoteConfigConverter.TryToBool(raw, out bool value));
            Assert.IsTrue(value);
        }

        [TestCase("0")]
        [TestCase("false")]
        [TestCase("False")]
        [TestCase("f")]
        [TestCase("no")]
        [TestCase("N")]
        [TestCase("off")]
        [TestCase("")]
        public void Bool_FalseStringsOfTheSdk(string raw)
        {
            Assert.IsTrue(RemoteConfigConverter.TryToBool(raw, out bool value));
            Assert.IsFalse(value);
        }

        [TestCase("maybe")]
        [TestCase("2")]
        [TestCase("yes please")]
        [TestCase(null)]
        public void Bool_InvalidStrings(string raw)
        {
            Assert.IsFalse(RemoteConfigConverter.TryToBool(raw, out _));
        }

        [TestCase("42", 42L)]
        [TestCase(" 42 ", 42L)]
        [TestCase("-7", -7L)]
        [TestCase("9223372036854775807", long.MaxValue)]
        public void Long_Valid(string raw, long expected)
        {
            Assert.IsTrue(RemoteConfigConverter.TryToLong(raw, out long value));
            Assert.AreEqual(expected, value);
        }

        [TestCase("2.5")]
        [TestCase("abc")]
        [TestCase("1e3")]
        [TestCase("")]
        [TestCase(null)]
        public void Long_Invalid(string raw)
        {
            Assert.IsFalse(RemoteConfigConverter.TryToLong(raw, out _));
        }

        [Test]
        public void Int_RejectsValuesOutOfRange()
        {
            Assert.IsTrue(RemoteConfigConverter.TryToInt("2147483647", out int max));
            Assert.AreEqual(int.MaxValue, max);
            Assert.IsFalse(RemoteConfigConverter.TryToInt("2147483648", out _));
            Assert.IsFalse(RemoteConfigConverter.TryToInt("-2147483649", out _));
        }

        [TestCase("2.5", 2.5)]
        [TestCase("3", 3.0)]
        [TestCase("-0.5", -0.5)]
        [TestCase("1e3", 1000.0)]
        [TestCase(" 7.25 ", 7.25)]
        public void Double_ValidWithInvariantCulture(string raw, double expected)
        {
            Assert.IsTrue(RemoteConfigConverter.TryToDouble(raw, out double value));
            Assert.AreEqual(expected, value, 1e-9);
        }

        [TestCase("2,5")]
        [TestCase("abc")]
        [TestCase("")]
        [TestCase(null)]
        public void Double_Invalid(string raw)
        {
            Assert.IsFalse(RemoteConfigConverter.TryToDouble(raw, out _));
        }

        [Test]
        public void Float_ValidAndOutOfRange()
        {
            Assert.IsTrue(RemoteConfigConverter.TryToFloat("2.5", out float value));
            Assert.AreEqual(2.5f, value);
            Assert.IsFalse(RemoteConfigConverter.TryToFloat("1e40", out _), "beyond float range");
        }

        [Test]
        public void ToRaw_FormatsLikeTheFirebaseConsole()
        {
            Assert.AreEqual(string.Empty, RemoteConfigConverter.ToRaw(null));
            Assert.AreEqual("text", RemoteConfigConverter.ToRaw("text"));
            Assert.AreEqual("true", RemoteConfigConverter.ToRaw(true));
            Assert.AreEqual("false", RemoteConfigConverter.ToRaw(false));
            Assert.AreEqual("5", RemoteConfigConverter.ToRaw(5));
            Assert.AreEqual("5", RemoteConfigConverter.ToRaw(5L));
            Assert.AreEqual("2.5", RemoteConfigConverter.ToRaw(2.5));
            Assert.AreEqual("2.5", RemoteConfigConverter.ToRaw(2.5f));
            Assert.AreEqual("1", RemoteConfigConverter.ToRaw(1.0));
        }

        [Test]
        public void ToRaw_UsesInvariantCultureWhateverTheCurrentCultureIs()
        {
            var previous = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("es-ES");
                Assert.AreEqual("2.5", RemoteConfigConverter.ToRaw(2.5));
                Assert.IsTrue(RemoteConfigConverter.TryToDouble("2.5", out double value));
                Assert.AreEqual(2.5, value);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = previous;
            }
        }
    }
}
