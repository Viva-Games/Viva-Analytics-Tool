using System.Collections.Generic;
using NUnit.Framework;
using Viva.Core;
using Viva.Services.Analytics.Consent;

namespace Viva.Services.Analytics.Tests
{
    /// <summary>
    /// El consentimiento publicado en VivaConsent (por Viva Ads tras el CMP de MAX) llega a los trackers
    /// traducido a Consent Mode sin código del proyecto, se publique antes o después de crear el tracker.
    /// </summary>
    public class SharedConsentTests
    {
        private sealed class CapturingTracker : AAnalyticsTracker
        {
            public Dictionary<ConsentSignal, bool> LastConsent;
            public int ConsentCalls;
            private bool _initialized;

            protected override void Initialize() => _initialized = true;

            protected override bool IsInitialized() => _initialized;

            public override void TrackEvent(IAnalyticsEvent eventToTrack)
            {
            }

            public override void SetConsent(IReadOnlyDictionary<ConsentSignal, bool> signals)
            {
                LastConsent = new Dictionary<ConsentSignal, bool>();
                foreach (var pair in signals) LastConsent[pair.Key] = pair.Value;
                ConsentCalls++;
            }
        }

        private readonly List<CapturingTracker> _trackers = new List<CapturingTracker>();

        [SetUp]
        public void SetUp()
        {
            VivaConsent.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var tracker in _trackers) AnalyticsService.RemoveTracker(tracker);
            _trackers.Clear();
            VivaConsent.Reset();
        }

        private CapturingTracker AddTracker()
        {
            var tracker = new CapturingTracker();
            _trackers.Add(tracker);
            AnalyticsService.AddTracker(tracker);
            return tracker;
        }

        private static ConsentState State(bool isGdpr, bool? p1, bool? p3, bool? p4, bool? p7, bool? google) => new ConsentState
        {
            IsGdpr = isGdpr, Purpose1 = p1, Purpose3 = p3, Purpose4 = p4, Purpose7 = p7, GoogleVendor = google, Source = "test"
        };

        [Test]
        public void ConsentPublishedBeforeTheTracker_IsAppliedWhenTheTrackerIsAdded()
        {
            VivaConsent.Set(State(true, true, true, true, true, true));

            var tracker = AddTracker();

            Assert.IsNotNull(tracker.LastConsent);
            Assert.IsTrue(tracker.LastConsent[ConsentSignal.AnalyticsStorage]);
            Assert.IsTrue(tracker.LastConsent[ConsentSignal.AdStorage]);
            Assert.IsTrue(tracker.LastConsent[ConsentSignal.AdUserData]);
            Assert.IsTrue(tracker.LastConsent[ConsentSignal.AdPersonalization]);
        }

        [Test]
        public void ConsentPublishedAfterTheTracker_IsAppliedThroughTheMapper()
        {
            var tracker = AddTracker();

            VivaConsent.Set(State(true, true, false, false, true, true));

            Assert.IsTrue(tracker.LastConsent[ConsentSignal.AnalyticsStorage]);
            Assert.IsTrue(tracker.LastConsent[ConsentSignal.AdStorage]);
            Assert.IsTrue(tracker.LastConsent[ConsentSignal.AdUserData], "purpose 1 and 7 and Google vendor");
            Assert.IsFalse(tracker.LastConsent[ConsentSignal.AdPersonalization], "purposes 3 and 4 denied");
        }

        [Test]
        public void OutsideGdpr_EverythingIsGranted()
        {
            var tracker = AddTracker();

            VivaConsent.Set(State(false, null, null, null, null, null));

            foreach (var pair in tracker.LastConsent) Assert.IsTrue(pair.Value, pair.Key.ToString());
        }

        [Test]
        public void ReConsent_AppliesTheLastState()
        {
            var tracker = AddTracker();
            VivaConsent.Set(State(true, true, true, true, true, true));
            int callsAfterFirst = tracker.ConsentCalls;

            VivaConsent.Set(State(true, false, false, false, false, false));

            Assert.AreEqual(callsAfterFirst + 1, tracker.ConsentCalls);
            Assert.IsFalse(tracker.LastConsent[ConsentSignal.AnalyticsStorage]);
            Assert.IsFalse(tracker.LastConsent[ConsentSignal.AdPersonalization]);
        }

        [Test]
        public void SeveralTrackers_AllReceiveTheSharedConsent()
        {
            var first = AddTracker();
            var second = AddTracker();

            VivaConsent.Set(State(true, true, true, true, true, true));

            Assert.IsTrue(first.LastConsent[ConsentSignal.AdPersonalization]);
            Assert.IsTrue(second.LastConsent[ConsentSignal.AdPersonalization]);
        }
    }
}
