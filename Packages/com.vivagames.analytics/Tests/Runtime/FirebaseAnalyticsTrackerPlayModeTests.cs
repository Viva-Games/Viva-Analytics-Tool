using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Viva.Core;

namespace Viva.Services.Analytics.Tests
{
    /// <summary>
    /// Integración real en Play Mode: el tracker de Firebase espera al gate compartido (VivaFirebase),
    /// no lanza una segunda comprobación de dependencias, vacía la cola al estar listo y dispara
    /// FirebaseReady una sola vez. Solo se compila con VIVA_FIREBASE_ANALYTICS y VIVA_FIREBASE.
    /// </summary>
    public class FirebaseAnalyticsTrackerPlayModeTests
    {
        private const float TIMEOUT_SECONDS = 60f;
        private const string CHECK_STARTED_MARK = "Checking Firebase dependencies";

        private class ProbeEvent : IAnalyticsEvent
        {
            public string GetEventKey() => "viva_tracker_probe";
            public Dictionary<string, object> GetTrackingFields() => new Dictionary<string, object> { { "source", "playmode_test" } };
        }

        [UnityTest]
        public IEnumerator Tracker_WaitsForSharedGate_FlushesQueueAndRaisesFirebaseReadyOnce()
        {
            bool gateWasReady = VivaFirebase.IsReady;
            int checksStarted = 0;
            Application.LogCallback counter = (message, stackTrace, type) =>
            {
                if (message.Contains(CHECK_STARTED_MARK)) checksStarted++;
            };
            Application.logMessageReceived += counter;

            var tracker = new FirebaseAnalyticsTracker { LogToConsole = false };
            int readyRaised = 0;
            tracker.FirebaseReady += () => readyRaised++;

            LogAssert.Expect(LogType.Log, new Regex(@"\[Analytics\] Firebase ready: sending \d+ queued event\(s\)"));

            AnalyticsService.AddTracker(tracker);
            try
            {
                if (!gateWasReady)
                {
                    Assert.IsFalse(tracker.IsFirebaseReady, "the tracker is not ready until the shared check finishes");
                    Assert.AreEqual(ReadyState.Running, VivaFirebase.State, "the tracker started the shared check");
                    tracker.TrackEvent(new ProbeEvent()); // se queda en cola hasta que Firebase esté listo
                }

                float deadline = Time.realtimeSinceStartup + TIMEOUT_SECONDS;
                while (!tracker.IsFirebaseReady && !VivaFirebase.HasFailed && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.IsTrue(tracker.IsFirebaseReady, "Firebase did not become ready. Reason: " + VivaFirebase.FailureReason);
                Assert.AreEqual(ReadyState.Ready, VivaFirebase.State);
                Assert.AreEqual(1, readyRaised, "FirebaseReady is raised exactly once");
                Assert.AreEqual(gateWasReady ? 0 : 1, checksStarted,
                    "the tracker never starts a second dependency check; it starts the shared one only if nobody did");

                tracker.TrackEvent(new ProbeEvent()); // ya listo: se envía en el acto, sin excepciones
            }
            finally
            {
                AnalyticsService.RemoveTracker(tracker);
                Application.logMessageReceived -= counter;
            }
        }
    }
}
