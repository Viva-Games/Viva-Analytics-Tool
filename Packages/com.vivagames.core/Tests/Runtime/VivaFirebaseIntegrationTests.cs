using System.Collections;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Viva.Core.Tests
{
    /// <summary>
    /// Integración real con el SDK de Firebase en el editor (Play Mode): dos módulos independientes piden
    /// la inicialización, la comprobación de dependencias se hace una sola vez y los dos reciben el aviso
    /// en el hilo principal. Solo se compila con VIVA_FIREBASE (Firebase.App.dll en el proyecto).
    /// </summary>
    public class VivaFirebaseIntegrationTests
    {
        private const float TIMEOUT_SECONDS = 60f;
        private const string CHECK_STARTED_MARK = "Checking Firebase dependencies";

        [UnityTest]
        public IEnumerator TwoModules_OneDependencyCheck_BothNotifiedOnMainThread()
        {
            if (VivaFirebase.State != ReadyState.NotStarted)
            {
                Assert.Ignore("VivaFirebase was already started in this domain by another test or play session.");
            }

            int mainThread = Thread.CurrentThread.ManagedThreadId;
            var notifications = new List<string>();
            int checksStarted = 0;
            Application.LogCallback counter = (message, stackTrace, type) =>
            {
                if (message.Contains(CHECK_STARTED_MARK)) checksStarted++;
            };
            Application.logMessageReceived += counter;

            try
            {
                // Módulo A: arranca la comprobación. Módulo B: llega después y solo espera.
                VivaFirebase.EnsureInitialized("Module A");
                VivaFirebase.WhenReady(
                    () => notifications.Add("A on main thread: " + (Thread.CurrentThread.ManagedThreadId == mainThread)),
                    reason => notifications.Add("A failed: " + reason));

                VivaFirebase.EnsureInitialized("Module B");
                VivaFirebase.WhenReady(
                    () => notifications.Add("B on main thread: " + (Thread.CurrentThread.ManagedThreadId == mainThread)),
                    reason => notifications.Add("B failed: " + reason));

                Assert.AreEqual(ReadyState.Running, VivaFirebase.State, "the check should be running after the first EnsureInitialized");
                Assert.AreEqual(0, notifications.Count, "nobody is notified before the check finishes");

                float deadline = Time.realtimeSinceStartup + TIMEOUT_SECONDS;
                while (VivaFirebase.State == ReadyState.Running && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }

                Assert.AreEqual(ReadyState.Ready, VivaFirebase.State,
                    "Firebase did not become ready. Failure reason: " + VivaFirebase.FailureReason);
                Assert.AreEqual(1, checksStarted, "the dependency check must start exactly once");
                CollectionAssert.AreEqual(
                    new[] { "A on main thread: True", "B on main thread: True" },
                    notifications,
                    "both modules are notified once, in subscription order, on the main thread");

                // Módulo C llega cuando ya está todo listo: se ejecuta en el acto.
                bool lateCallbackRan = false;
                VivaFirebase.WhenReady(() => lateCallbackRan = true);
                Assert.IsTrue(lateCallbackRan, "a late subscriber runs immediately");
            }
            finally
            {
                Application.logMessageReceived -= counter;
            }
        }
    }
}
