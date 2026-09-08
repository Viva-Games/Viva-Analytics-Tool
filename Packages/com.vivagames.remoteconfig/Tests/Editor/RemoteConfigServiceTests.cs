using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Viva.Services.RemoteConfig.Tests
{
    /// <summary>
    /// Contrato de RemoteConfigService con un proveedor falso: getters, defaults, conversiones, OnReady y WhenReady.
    /// </summary>
    public class RemoteConfigServiceTests
    {
        /// <summary>Proveedor controlado desde el test. Simula que el SDK serializa los defaults a su manera.</summary>
        private sealed class FakeProvider : IRemoteConfigProvider
        {
            public readonly Dictionary<string, (string raw, RemoteConfigSource source)> Values = new Dictionary<string, (string, RemoteConfigSource)>();
            public IDictionary<string, object> ReceivedDefaults;
            public RemoteConfigOptions ReceivedOptions;
            public int InitializeCalls;
            public bool ThrowOnInitialize;
            public bool ThrowOnRead;

            private Action<RemoteConfigSource> _onValuesAvailable;
            private Action<RemoteConfigSource> _onFetchConcluded;

            public void Initialize(IDictionary<string, object> defaults, RemoteConfigOptions options,
                Action<RemoteConfigSource> onValuesAvailable, Action<RemoteConfigSource> onFetchConcluded)
            {
                InitializeCalls++;
                ReceivedDefaults = defaults;
                ReceivedOptions = options;
                _onValuesAvailable = onValuesAvailable;
                _onFetchConcluded = onFetchConcluded;
                if (ThrowOnInitialize) throw new InvalidOperationException("provider exploded");
            }

            public void Set(string key, string raw, RemoteConfigSource source = RemoteConfigSource.Remote) => Values[key] = (raw, source);

            public void ValuesAvailable(RemoteConfigSource source) => _onValuesAvailable(source);

            public void Conclude(RemoteConfigSource source) => _onFetchConcluded(source);

            public bool TryGetRaw(string key, out string raw, out RemoteConfigSource source)
            {
                if (ThrowOnRead) throw new InvalidOperationException("read exploded");
                if (Values.TryGetValue(key, out var entry))
                {
                    raw = entry.raw;
                    source = entry.source;
                    return true;
                }
                if (ReceivedDefaults != null && ReceivedDefaults.ContainsKey(key))
                {
                    raw = "SDK-FORMATTED"; // el SDK serializa los defaults a su manera; el servicio debe usar los suyos
                    source = RemoteConfigSource.Defaults;
                    return true;
                }
                raw = null;
                source = RemoteConfigSource.None;
                return false;
            }
        }

        [Serializable]
        private class Probe
        {
            public int a;
        }

        private FakeProvider _provider;
        private readonly RemoteConfigOptions _quiet = new RemoteConfigOptions { LogToConsole = false };

        [SetUp]
        public void SetUp()
        {
            RemoteConfigService.Reset();
            _provider = new FakeProvider();
        }

        [TearDown]
        public void TearDown()
        {
            RemoteConfigService.Reset();
        }

        private static Dictionary<string, object> Defaults() => new Dictionary<string, object>
        {
            { "lives", 5 },
            { "ratio", 2.5 },
            { "flag", true },
            { "name", "viva" },
        };

        [Test]
        public void BeforeInitialize_GettersReturnCallerDefaults()
        {
            Assert.IsFalse(RemoteConfigService.IsInitialized);
            Assert.IsFalse(RemoteConfigService.IsReady);
            Assert.AreEqual(RemoteConfigSource.None, RemoteConfigService.Source);
            Assert.AreEqual(7, RemoteConfigService.GetInt("lives", 7));
            Assert.AreEqual("d", RemoteConfigService.GetString("name", "d"));
            Assert.IsTrue(RemoteConfigService.GetBool("flag", true));
            Assert.IsFalse(RemoteConfigService.HasValue("lives"));
        }

        [Test]
        public void Initialize_PassesDefaultsAndOptionsToTheProvider()
        {
            var options = new RemoteConfigOptions { LogToConsole = false, MinimumFetchInterval = TimeSpan.FromMinutes(5) };
            RemoteConfigService.Initialize(_provider, Defaults(), options);

            Assert.AreSame(_provider, RemoteConfigService.Provider);
            Assert.AreEqual(1, _provider.InitializeCalls);
            Assert.AreEqual(4, _provider.ReceivedDefaults.Count);
            Assert.AreEqual(5, _provider.ReceivedDefaults["lives"]);
            Assert.AreSame(options, _provider.ReceivedOptions);
        }

        [Test]
        public void Initialize_Twice_IsIgnoredWithAWarning()
        {
            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);
            LogAssert.Expect(LogType.Warning, new Regex("Already initialized"));
            RemoteConfigService.Initialize(new FakeProvider(), Defaults(), _quiet);

            Assert.AreSame(_provider, RemoteConfigService.Provider);
            Assert.AreEqual(1, _provider.InitializeCalls);
        }

        [Test]
        public void Initialize_WithoutFactory_UsesTheLocalProvider()
        {
            LogAssert.Expect(LogType.Warning, new Regex("SDK not found"));
            RemoteConfigService.Initialize(Defaults(), new RemoteConfigOptions { LogToConsole = true });
            Assert.IsInstanceOf<LocalRemoteConfigProvider>(RemoteConfigService.Provider);
        }

        [Test]
        public void Initialize_WithFactory_UsesTheFactoryProvider()
        {
            RemoteConfigService.ProviderFactory = () => _provider;
            RemoteConfigService.Initialize(Defaults(), _quiet);
            Assert.AreSame(_provider, RemoteConfigService.Provider);
        }

        [Test]
        public void Getters_ReadTheProviderValues()
        {
            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);
            _provider.Set("lives", "7");
            _provider.Set("ratio", "0.75");
            _provider.Set("flag", "yes");
            _provider.Set("name", "arrows");
            _provider.Set("big", "3000000000");

            Assert.AreEqual(7, RemoteConfigService.GetInt("lives", 5));
            Assert.AreEqual(7L, RemoteConfigService.GetLong("lives"));
            Assert.AreEqual(0.75f, RemoteConfigService.GetFloat("ratio"));
            Assert.AreEqual(0.75, RemoteConfigService.GetDouble("ratio"));
            Assert.IsTrue(RemoteConfigService.GetBool("flag"));
            Assert.AreEqual("arrows", RemoteConfigService.GetString("name"));
            Assert.AreEqual(3000000000L, RemoteConfigService.GetLong("big"));
            Assert.IsTrue(RemoteConfigService.HasValue("lives"));
        }

        [Test]
        public void MissingKey_ReturnsTheCallerDefault()
        {
            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);

            Assert.AreEqual(9, RemoteConfigService.GetInt("unknown", 9));
            Assert.AreEqual("x", RemoteConfigService.GetString("unknown", "x"));
            Assert.IsFalse(RemoteConfigService.HasValue("unknown"));
            Assert.AreEqual(1, RemoteConfigService.GetInt(null, 1));
            Assert.AreEqual(1, RemoteConfigService.GetInt(string.Empty, 1));
        }

        [Test]
        public void RegisteredDefault_IsReturnedWithOurOwnFormatting_NotTheSdkOne()
        {
            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);

            Assert.AreEqual(5, RemoteConfigService.GetInt("lives", 0));
            Assert.AreEqual(2.5, RemoteConfigService.GetDouble("ratio", 0));
            Assert.AreEqual("2.5", RemoteConfigService.GetString("ratio"));
            Assert.IsTrue(RemoteConfigService.GetBool("flag", false));
            Assert.AreEqual("viva", RemoteConfigService.GetString("name", "other"));
            Assert.IsTrue(RemoteConfigService.HasValue("lives"));
        }

        [Test]
        public void ProviderDoesNotKnowAKey_ButADefaultIsRegistered_ReturnsTheDefault()
        {
            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);
            _provider.ReceivedDefaults = new Dictionary<string, object>(); // el SDK "olvidó" los defaults (SetDefaultsAsync falló)

            Assert.AreEqual(5, RemoteConfigService.GetInt("lives", 0));
            Assert.IsTrue(RemoteConfigService.HasValue("lives"));
        }

        [Test]
        public void ProviderThrowsOnRead_ReturnsTheRegisteredDefaultOrTheCallerOne()
        {
            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);
            _provider.ThrowOnRead = true;

            LogAssert.Expect(LogType.Warning, new Regex("provider failed to read"));
            Assert.AreEqual(5, RemoteConfigService.GetInt("lives", 0));
            Assert.AreEqual(3, RemoteConfigService.GetInt("unknown", 3));
        }

        [Test]
        public void InvalidValue_ReturnsTheCallerDefault_AndWarnsOnlyOnce()
        {
            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);
            _provider.Set("lives", "abc");
            int warnings = 0;
            Application.LogCallback counter = (message, stack, type) =>
            {
                if (type == LogType.Warning && message.Contains("\"lives\"")) warnings++;
            };
            Application.logMessageReceived += counter;
            try
            {
                LogAssert.Expect(LogType.Warning, new Regex("not a valid int"));
                Assert.AreEqual(3, RemoteConfigService.GetInt("lives", 3));
                Assert.AreEqual(4, RemoteConfigService.GetInt("lives", 4));
                Assert.AreEqual(1, warnings, "the warning is logged once per key");
            }
            finally
            {
                Application.logMessageReceived -= counter;
            }
        }

        [Test]
        public void IntOutOfRange_ReturnsTheCallerDefault()
        {
            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);
            _provider.Set("big", "3000000000");

            LogAssert.Expect(LogType.Warning, new Regex("not a valid int"));
            Assert.AreEqual(-1, RemoteConfigService.GetInt("big", -1));
        }

        [Test]
        public void GetJson_ParsesObjects_AndFallsBackOnEmptyOrInvalidJson()
        {
            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);
            _provider.Set("cfg", "{\"a\":3}");
            _provider.Set("empty", "");
            _provider.Set("bad", "{not json");
            var fallback = new Probe { a = -1 };

            Assert.AreEqual(3, RemoteConfigService.GetJson<Probe>("cfg").a);
            Assert.AreSame(fallback, RemoteConfigService.GetJson("empty", fallback));
            Assert.AreSame(fallback, RemoteConfigService.GetJson("missing", fallback));
            LogAssert.Expect(LogType.Warning, new Regex("could not be parsed"));
            Assert.AreSame(fallback, RemoteConfigService.GetJson("bad", fallback));
        }

        [Test]
        public void ReadyFlow_SourceTransitions_OnReadyOnce_WhenReadyBeforeAndAfter()
        {
            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);
            var log = new List<string>();
            RemoteConfigService.OnReady += () => log.Add("event");
            RemoteConfigService.WhenReady(() => log.Add("queued"));

            _provider.ValuesAvailable(RemoteConfigSource.Cache);
            Assert.AreEqual(RemoteConfigSource.Cache, RemoteConfigService.Source);
            Assert.IsFalse(RemoteConfigService.IsReady);
            Assert.AreEqual(0, log.Count, "nothing fires until the fetch concludes");

            _provider.Conclude(RemoteConfigSource.Remote);
            Assert.IsTrue(RemoteConfigService.IsReady);
            Assert.AreEqual(RemoteConfigSource.Remote, RemoteConfigService.Source);
            CollectionAssert.AreEqual(new[] { "queued", "event" }, log);

            RemoteConfigService.WhenReady(() => log.Add("late"));
            Assert.AreEqual("late", log[log.Count - 1], "a late WhenReady runs immediately");

            LogAssert.Expect(LogType.Warning, new Regex("more than once"));
            _provider.Conclude(RemoteConfigSource.Remote);
            Assert.AreEqual(3, log.Count, "OnReady fires exactly once");
        }

        [Test]
        public void OnReadyHandlerException_DoesNotStopTheOtherHandlers()
        {
            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);
            bool secondRan = false;
            RemoteConfigService.OnReady += () => throw new InvalidOperationException("handler broke");
            RemoteConfigService.OnReady += () => secondRan = true;

            LogAssert.Expect(LogType.Error, new Regex("OnReady handler failed"));
            _provider.Conclude(RemoteConfigSource.Defaults);

            Assert.IsTrue(secondRan);
            Assert.IsTrue(RemoteConfigService.IsReady);
        }

        [Test]
        public void ProviderThrowsOnInitialize_ServiceIsReadyWithTheRegisteredDefaults()
        {
            _provider.ThrowOnInitialize = true;
            LogAssert.Expect(LogType.Error, new Regex("provider failed to initialize"));
            bool readyFired = false;
            RemoteConfigService.OnReady += () => readyFired = true;

            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);

            Assert.IsTrue(RemoteConfigService.IsReady);
            Assert.IsTrue(readyFired);
            Assert.AreEqual(RemoteConfigSource.Defaults, RemoteConfigService.Source);
            Assert.AreEqual(5, RemoteConfigService.GetInt("lives", 0));
        }

        [Test]
        public void NotifyValuesUpdated_RaisesTheEventAndUpdatesTheSource()
        {
            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);
            int updates = 0;
            RemoteConfigService.OnValuesUpdated += () => updates++;

            RemoteConfigService.NotifyValuesUpdated(RemoteConfigSource.Remote);

            Assert.AreEqual(1, updates);
            Assert.AreEqual(RemoteConfigSource.Remote, RemoteConfigService.Source);
        }

        [Test]
        public void LocalProvider_ServesDefaultsAndOverrides_AndIsReadyAtOnceWhenSynchronous()
        {
            var local = new LocalRemoteConfigProvider { CompleteSynchronously = true };
            bool readyFired = false;
            RemoteConfigService.OnReady += () => readyFired = true;

            RemoteConfigService.Initialize(local, Defaults(), _quiet);

            Assert.IsTrue(RemoteConfigService.IsReady);
            Assert.IsTrue(readyFired);
            Assert.AreEqual(RemoteConfigSource.Defaults, RemoteConfigService.Source);
            Assert.AreEqual(5, RemoteConfigService.GetInt("lives", 0));
            Assert.AreEqual("viva", RemoteConfigService.GetString("name"));
            Assert.AreEqual(0, RemoteConfigService.GetInt("unknown"));

            local.SetOverride("lives", 9);
            Assert.AreEqual(9, RemoteConfigService.GetInt("lives", 0));
            local.ClearOverrides();
            Assert.AreEqual(5, RemoteConfigService.GetInt("lives", 0));
        }

        [Test]
        public void Reset_LeavesTheServiceAsFreshlyLoaded()
        {
            RemoteConfigService.Initialize(_provider, Defaults(), _quiet);
            _provider.Conclude(RemoteConfigSource.Remote);

            RemoteConfigService.Reset();

            Assert.IsFalse(RemoteConfigService.IsInitialized);
            Assert.IsFalse(RemoteConfigService.IsReady);
            Assert.AreEqual(RemoteConfigSource.None, RemoteConfigService.Source);
            Assert.AreEqual(0, RemoteConfigService.GetInt("lives"));
        }
    }
}
