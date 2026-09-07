using NUnit.Framework;
using Viva.Services.Analytics.Consent;

namespace Viva.Services.Analytics.Tests
{
    public class ConsentModeMapperTests
    {
        [Test]
        public void NonGdpr_AllGranted_EvenWithNullPurposes()
        {
            var tcf = new TcfConsent(isGdpr: false, null, null, null, null, null);
            var r = ConsentModeMapper.Map(tcf);

            Assert.IsTrue(r[ConsentSignal.AnalyticsStorage]);
            Assert.IsTrue(r[ConsentSignal.AdStorage]);
            Assert.IsTrue(r[ConsentSignal.AdUserData]);
            Assert.IsTrue(r[ConsentSignal.AdPersonalization]);
        }

        [Test]
        public void Gdpr_AcceptsAll_AllGranted()
        {
            var tcf = new TcfConsent(isGdpr: true, true, true, true, true, true);
            var r = ConsentModeMapper.Map(tcf);

            Assert.IsTrue(r[ConsentSignal.AnalyticsStorage]);
            Assert.IsTrue(r[ConsentSignal.AdStorage]);
            Assert.IsTrue(r[ConsentSignal.AdUserData]);
            Assert.IsTrue(r[ConsentSignal.AdPersonalization]);
        }

        [Test]
        public void Gdpr_RejectsAll_AllDenied()
        {
            var tcf = new TcfConsent(isGdpr: true, false, false, false, false, false);
            var r = ConsentModeMapper.Map(tcf);

            Assert.IsFalse(r[ConsentSignal.AnalyticsStorage]);
            Assert.IsFalse(r[ConsentSignal.AdStorage]);
            Assert.IsFalse(r[ConsentSignal.AdUserData]);
            Assert.IsFalse(r[ConsentSignal.AdPersonalization]);
        }

        [Test]
        public void Gdpr_NullPurposes_TreatedAsDenied()
        {
            var tcf = new TcfConsent(isGdpr: true, null, null, null, null, null);
            var r = ConsentModeMapper.Map(tcf);

            Assert.IsFalse(r[ConsentSignal.AnalyticsStorage]);
            Assert.IsFalse(r[ConsentSignal.AdStorage]);
            Assert.IsFalse(r[ConsentSignal.AdUserData]);
            Assert.IsFalse(r[ConsentSignal.AdPersonalization]);
        }

        [Test]
        public void Gdpr_OnlyPurpose1_StorageGranted_AdSignalsDenied()
        {
            var tcf = new TcfConsent(isGdpr: true, true, null, null, null, null);
            var r = ConsentModeMapper.Map(tcf);

            Assert.IsTrue(r[ConsentSignal.AnalyticsStorage]);
            Assert.IsTrue(r[ConsentSignal.AdStorage]);
            Assert.IsFalse(r[ConsentSignal.AdUserData]);
            Assert.IsFalse(r[ConsentSignal.AdPersonalization]);
        }

        [Test]
        public void Gdpr_AdUserData_RequiresPurpose1And7AndGoogleVendor()
        {
            var tcf = new TcfConsent(isGdpr: true, true, false, false, true, true);
            var r = ConsentModeMapper.Map(tcf);

            Assert.IsTrue(r[ConsentSignal.AdUserData]);
            Assert.IsFalse(r[ConsentSignal.AdPersonalization]);
        }

        [Test]
        public void Gdpr_AdPersonalization_RequiresPurpose3And4AndGoogleVendor()
        {
            var tcf = new TcfConsent(isGdpr: true, true, true, true, false, true);
            var r = ConsentModeMapper.Map(tcf);

            Assert.IsTrue(r[ConsentSignal.AdPersonalization]);
            Assert.IsFalse(r[ConsentSignal.AdUserData]);
        }

        [Test]
        public void Gdpr_AdUserData_DeniedWithoutGoogleVendor()
        {
            var tcf = new TcfConsent(isGdpr: true, true, true, true, true, false);
            var r = ConsentModeMapper.Map(tcf);

            Assert.IsFalse(r[ConsentSignal.AdUserData]);
            Assert.IsFalse(r[ConsentSignal.AdPersonalization]);
        }
    }
}
