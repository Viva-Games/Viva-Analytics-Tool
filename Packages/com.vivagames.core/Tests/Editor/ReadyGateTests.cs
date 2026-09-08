using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Viva.Core.Tests
{
    /// <summary>
    /// ReadyGate es la base de VivaFirebase: estas pruebas cubren el contrato del que dependen todos los
    /// módulos que esperan a Firebase (arranque único, orden de callbacks, ejecución inmediata, fallo).
    /// </summary>
    public class ReadyGateTests
    {
        [Test]
        public void StartsOnlyOnce()
        {
            var gate = new ReadyGate("test");

            Assert.AreEqual(ReadyState.NotStarted, gate.State);
            Assert.IsTrue(gate.TryStart(), "the first caller starts the initialization");
            Assert.AreEqual(ReadyState.Running, gate.State);
            Assert.IsFalse(gate.TryStart(), "the second caller must only wait");
            Assert.AreEqual(ReadyState.Running, gate.State);
        }

        [Test]
        public void TryStart_AfterFinished_ReturnsFalse()
        {
            var gate = new ReadyGate("test");
            gate.SetReady();

            Assert.IsFalse(gate.TryStart());
            Assert.AreEqual(ReadyState.Ready, gate.State);
        }

        [Test]
        public void WhenReady_BeforeReady_QueuesAndRunsInSubscriptionOrder()
        {
            var gate = new ReadyGate("test");
            var calls = new List<string>();
            gate.TryStart();
            gate.WhenReady(() => calls.Add("first"));
            gate.WhenReady(() => calls.Add("second"));

            Assert.AreEqual(0, calls.Count, "nothing runs before SetReady");
            Assert.AreEqual(2, gate.PendingCount);

            gate.SetReady();

            CollectionAssert.AreEqual(new[] { "first", "second" }, calls);
            Assert.AreEqual(0, gate.PendingCount);
        }

        [Test]
        public void WhenReady_AfterReady_RunsImmediately()
        {
            var gate = new ReadyGate("test");
            gate.TryStart();
            gate.SetReady();

            bool ran = false;
            gate.WhenReady(() => ran = true);

            Assert.IsTrue(ran);
            Assert.AreEqual(0, gate.PendingCount);
        }

        [Test]
        public void SetFailed_RunsOnlyFailureCallbacksWithReason()
        {
            var gate = new ReadyGate("test");
            gate.TryStart();
            bool readyRan = false;
            string reason = null;
            gate.WhenReady(() => readyRan = true, r => reason = r);

            gate.SetFailed("no play services");

            Assert.IsFalse(readyRan);
            Assert.AreEqual("no play services", reason);
            Assert.AreEqual(ReadyState.Failed, gate.State);
            Assert.IsTrue(gate.HasFailed);
            Assert.AreEqual("no play services", gate.FailureReason);
        }

        [Test]
        public void WhenReady_AfterFailed_RunsFailureCallbackImmediately()
        {
            var gate = new ReadyGate("test");
            gate.TryStart();
            gate.SetFailed("boom");

            bool readyRan = false;
            string reason = null;
            gate.WhenReady(() => readyRan = true, r => reason = r);

            Assert.IsFalse(readyRan);
            Assert.AreEqual("boom", reason);
        }

        [Test]
        public void WhenReady_WithoutFailureCallback_DoesNothingOnFailure()
        {
            var gate = new ReadyGate("test");
            gate.TryStart();
            bool readyRan = false;
            gate.WhenReady(() => readyRan = true);

            Assert.DoesNotThrow(() => gate.SetFailed("boom"));
            Assert.IsFalse(readyRan);
        }

        [Test]
        public void SetReady_Twice_IsIgnoredAndCallbacksRunOnce()
        {
            var gate = new ReadyGate("test");
            gate.TryStart();
            int calls = 0;
            gate.WhenReady(() => calls++);

            gate.SetReady();
            LogAssert.Expect(LogType.Warning, new Regex("already Ready"));
            gate.SetReady();

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void SetFailed_AfterReady_IsIgnored()
        {
            var gate = new ReadyGate("test");
            gate.TryStart();
            gate.SetReady();

            LogAssert.Expect(LogType.Warning, new Regex("already Ready"));
            gate.SetFailed("late failure");

            Assert.AreEqual(ReadyState.Ready, gate.State);
            Assert.IsNull(gate.FailureReason);
        }

        [Test]
        public void CallbackException_DoesNotStopTheOthers()
        {
            var gate = new ReadyGate("test");
            gate.TryStart();
            var calls = new List<string>();
            gate.WhenReady(() => throw new InvalidOperationException("handler broke"));
            gate.WhenReady(() => calls.Add("second"));

            LogAssert.Expect(LogType.Error, new Regex("ready callback failed"));
            gate.SetReady();

            CollectionAssert.AreEqual(new[] { "second" }, calls);
            Assert.AreEqual(ReadyState.Ready, gate.State);
        }

        [Test]
        public void SubscribingFromInsideACallback_RunsImmediately()
        {
            var gate = new ReadyGate("test");
            gate.TryStart();
            var calls = new List<string>();
            gate.WhenReady(() =>
            {
                calls.Add("outer");
                gate.WhenReady(() => calls.Add("inner"));
            });

            gate.SetReady();

            CollectionAssert.AreEqual(new[] { "outer", "inner" }, calls);
        }

        /// <summary>
        /// Escenario real: dos módulos independientes (analíticas y remote config) se inicializan en el
        /// mismo Awake sin saber el uno del otro. Solo uno arranca la comprobación y los dos reciben el aviso.
        /// </summary>
        [Test]
        public void TwoIndependentModules_ShareOneInitialization()
        {
            var gate = new ReadyGate("Firebase");
            var log = new List<string>();

            // Módulo A
            if (gate.TryStart()) log.Add("A starts the check");
            gate.WhenReady(() => log.Add("A ready"));

            // Módulo B
            if (gate.TryStart()) log.Add("B starts the check");
            gate.WhenReady(() => log.Add("B ready"));

            gate.SetReady();

            // Módulo C llega tarde, cuando ya está todo listo.
            gate.WhenReady(() => log.Add("C ready"));

            CollectionAssert.AreEqual(new[] { "A starts the check", "A ready", "B ready", "C ready" }, log);
        }
    }
}
