using System.Collections.Generic;
using NUnit.Framework;

namespace Viva.Services.Analytics.Tests
{
    /// <summary>Reparto de eventos por destinos: cada tracker recibe solo los eventos que comparten destino con él.</summary>
    public class RoutingTests
    {
        private sealed class RecordingTracker : AAnalyticsTracker
        {
            private readonly AnalyticsTargets _targets;
            public readonly List<string> Received = new List<string>();
            private bool _initialized;

            public RecordingTracker(AnalyticsTargets targets) => _targets = targets;

            public override AnalyticsTargets Targets => _targets;

            protected override void Initialize() => _initialized = true;

            protected override bool IsInitialized() => _initialized;

            public override void TrackEvent(IAnalyticsEvent eventToTrack) => Received.Add(eventToTrack.GetEventKey());
        }

        private sealed class PlainEvent : IAnalyticsEvent
        {
            public string GetEventKey() => "plain";
            public Dictionary<string, object> GetTrackingFields() => new Dictionary<string, object>();
        }

        private sealed class RoutedEvent : IRoutedAnalyticsEvent
        {
            private readonly AnalyticsTargets _targets;
            public RoutedEvent(AnalyticsTargets targets) => _targets = targets;
            public AnalyticsTargets Targets => _targets;
            public string GetEventKey() => "routed_" + _targets;
            public Dictionary<string, object> GetTrackingFields() => new Dictionary<string, object> { { "a", 1 } };
        }

        private readonly List<RecordingTracker> _trackers = new List<RecordingTracker>();

        private RecordingTracker Add(AnalyticsTargets targets)
        {
            var tracker = new RecordingTracker(targets);
            _trackers.Add(tracker);
            AnalyticsService.AddTracker(tracker);
            return tracker;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var tracker in _trackers) AnalyticsService.RemoveTracker(tracker);
            _trackers.Clear();
        }

        [Test]
        public void PlainEvent_GoesToFirebaseAndConsoleOnly()
        {
            var firebase = Add(AnalyticsTargets.Firebase);
            var facebook = Add(AnalyticsTargets.Facebook);
            var singular = Add(AnalyticsTargets.Singular);
            var console = Add(AnalyticsTargets.All);

            AnalyticsService.TrackEvent(new PlainEvent());

            CollectionAssert.AreEqual(new[] { "plain" }, firebase.Received);
            CollectionAssert.IsEmpty(facebook.Received);
            CollectionAssert.IsEmpty(singular.Received);
            CollectionAssert.AreEqual(new[] { "plain" }, console.Received);
        }

        [Test]
        public void RoutedEvent_ReachesEveryMarkedTarget()
        {
            var firebase = Add(AnalyticsTargets.Firebase);
            var facebook = Add(AnalyticsTargets.Facebook);
            var singular = Add(AnalyticsTargets.Singular);

            AnalyticsService.TrackEvent(new RoutedEvent(AnalyticsTargets.Firebase | AnalyticsTargets.Facebook));
            AnalyticsService.TrackEvent(new RoutedEvent(AnalyticsTargets.Firebase | AnalyticsTargets.Singular));

            Assert.AreEqual(2, firebase.Received.Count);
            CollectionAssert.AreEqual(new[] { "routed_Firebase, Facebook" }, facebook.Received);
            CollectionAssert.AreEqual(new[] { "routed_Firebase, Singular" }, singular.Received);
        }

        [Test]
        public void CommonParameters_DoNotChangeTheRouting()
        {
            var facebook = Add(AnalyticsTargets.Facebook);
            AnalyticsService.RegisterCommonParameter("session", () => 3);
            try
            {
                AnalyticsService.TrackEvent(new PlainEvent());
                AnalyticsService.TrackEvent(new RoutedEvent(AnalyticsTargets.All));
            }
            finally
            {
                AnalyticsService.RemoveCommonParameter("session");
            }

            CollectionAssert.AreEqual(new[] { "routed_All" }, facebook.Received);
        }

        [Test]
        public void TargetsOf_DefaultsToFirebase()
        {
            Assert.AreEqual(AnalyticsTargets.Firebase, new PlainEvent().TargetsOf());
            Assert.AreEqual(AnalyticsTargets.Firebase | AnalyticsTargets.Singular, new RoutedEvent(AnalyticsTargets.Firebase | AnalyticsTargets.Singular).TargetsOf());
            Assert.IsTrue(AnalyticsTargets.All.Includes(AnalyticsTargets.Facebook));
            Assert.IsFalse(AnalyticsTargets.Facebook.Includes(AnalyticsTargets.Firebase));
        }
    }
}
