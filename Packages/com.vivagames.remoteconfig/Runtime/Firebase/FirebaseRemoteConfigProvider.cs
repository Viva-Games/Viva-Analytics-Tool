using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Extensions;
using Firebase.RemoteConfig;
using UnityEngine;
using Viva.Core;

namespace Viva.Services.RemoteConfig
{
    /// <summary>
    /// Proveedor sobre el SDK de Firebase Remote Config. Flujo, una vez que VivaFirebase (Viva Core) ha
    /// resuelto las dependencias de Firebase:
    /// 1. SetDefaultsAsync con los defaults registrados.
    /// 2. ActivateAsync para aplicar lo descargado en la sesión anterior (valores disponibles: Cache o Defaults).
    /// 3. FetchAsync(intervalo mínimo) y, si va bien, ActivateAsync (valores del servidor: Remote).
    /// 4. Fetch terminado, con éxito o sin él: el servicio queda listo con lo mejor que haya.
    /// Nunca llama a CheckAndFixDependenciesAsync: eso lo hace VivaFirebase una sola vez para todos los módulos.
    /// Compilado solo con VIVA_FIREBASE_REMOTE_CONFIG (Firebase.RemoteConfig.dll) y VIVA_FIREBASE (Firebase.App.dll).
    /// </summary>
    public sealed class FirebaseRemoteConfigProvider : IRemoteConfigProvider
    {
        private const string LOG_PREFIX = "[RemoteConfig]";

        private FirebaseRemoteConfig _remoteConfig;
        private RemoteConfigOptions _options;
        private IDictionary<string, object> _defaults;
        private readonly Dictionary<string, string> _rawDefaults = new Dictionary<string, string>();
        private Action<RemoteConfigSource> _onValuesAvailable;
        private Action<RemoteConfigSource> _onFetchConcluded;
        private RemoteConfigSource _source = RemoteConfigSource.None;
        private bool _fetchSucceeded;
        private bool _concluded;
        private bool _readFailureLogged;

        public void Initialize(IDictionary<string, object> defaults, RemoteConfigOptions options,
            Action<RemoteConfigSource> onValuesAvailable, Action<RemoteConfigSource> onFetchConcluded)
        {
            _defaults = defaults ?? new Dictionary<string, object>();
            _options = RemoteConfigOptions.OrDefault(options);
            _onValuesAvailable = onValuesAvailable;
            _onFetchConcluded = onFetchConcluded;

            _rawDefaults.Clear();
            foreach (var pair in _defaults)
            {
                _rawDefaults[pair.Key] = RemoteConfigConverter.ToRaw(pair.Value);
            }

            VivaFirebase.EnsureInitialized("Viva Remote Config");
            VivaFirebase.WhenReady(Start, reason =>
            {
                Log($"Firebase is not available ({reason}). Using the in-app defaults.", LogType.Warning);
                ValuesAvailable(RemoteConfigSource.Defaults);
                Conclude();
            });
        }

        public bool TryGetRaw(string key, out string raw, out RemoteConfigSource source)
        {
            raw = null;
            source = RemoteConfigSource.None;
            if (string.IsNullOrEmpty(key)) return false;

            if (_remoteConfig == null)
            {
                // Firebase no está (o aún no está) disponible: se sirven los defaults registrados.
                if (!_rawDefaults.TryGetValue(key, out raw)) return false;
                source = RemoteConfigSource.Defaults;
                return true;
            }

            ConfigValue value;
            try
            {
                value = _remoteConfig.GetValue(key);
            }
            catch (Exception e)
            {
                if (!_readFailureLogged)
                {
                    _readFailureLogged = true;
                    Log($"GetValue failed, serving the in-app defaults: {e.Message}", LogType.Warning);
                }
                if (!_rawDefaults.TryGetValue(key, out raw)) return false;
                source = RemoteConfigSource.Defaults;
                return true;
            }

            switch (value.Source)
            {
                case ValueSource.StaticValue:
                    return false;
                case ValueSource.DefaultValue:
                    source = RemoteConfigSource.Defaults;
                    break;
                default:
                    source = _fetchSucceeded ? RemoteConfigSource.Remote : RemoteConfigSource.Cache;
                    break;
            }

            raw = value.StringValue ?? string.Empty;
            return true;
        }

        #region Flow

        private void Start()
        {
            try
            {
                _remoteConfig = FirebaseRemoteConfig.DefaultInstance;
            }
            catch (Exception e)
            {
                Fail("Could not get the Remote Config instance", e);
                return;
            }

            if (_options.FetchTimeout.HasValue)
            {
                var settings = _remoteConfig.ConfigSettings;
                settings.FetchTimeoutInMilliseconds = (ulong)Math.Max(0, _options.FetchTimeout.Value.TotalMilliseconds);
                Then(_remoteConfig.SetConfigSettingsAsync(settings), "SetConfigSettingsAsync", SetDefaults);
            }
            else
            {
                SetDefaults();
            }
        }

        private void SetDefaults()
        {
            Then(_remoteConfig.SetDefaultsAsync(_defaults), "SetDefaultsAsync", ActivateCache);
        }

        private void ActivateCache()
        {
            if (!_options.ActivateCachedValuesOnStart)
            {
                ValuesAvailable(RemoteConfigSource.Defaults);
                Fetch();
                return;
            }

            // Aplica lo descargado en la sesión anterior. Lo que ya estaba activado sigue disponible aunque devuelva false.
            Then(_remoteConfig.ActivateAsync(), "ActivateAsync (cache)", () =>
            {
                ValuesAvailable(HasRemoteValues() ? RemoteConfigSource.Cache : RemoteConfigSource.Defaults);
                Fetch();
            });
        }

        private void Fetch()
        {
            Log($"Fetching (minimum interval {_options.MinimumFetchInterval})...");
            Then(_remoteConfig.FetchAsync(_options.MinimumFetchInterval), "FetchAsync", OnFetched);
        }

        private void OnFetched()
        {
            var info = _remoteConfig.Info;
            if (info.LastFetchStatus != LastFetchStatus.Success)
            {
                string detail = info.LastFetchFailureReason == FetchFailureReason.Throttled
                    ? "the server throttled the request (too many fetches in a short time)"
                    : info.LastFetchFailureReason.ToString();
                Log($"Fetch failed: {info.LastFetchStatus}, {detail}. Keeping the {_source} values.", LogType.Warning);
                Conclude();
                return;
            }

            Then(_remoteConfig.ActivateAsync(), "ActivateAsync (fetched)", () =>
            {
                _fetchSucceeded = true;
                ValuesAvailable(RemoteConfigSource.Remote);
                Log($"Fetch succeeded. Last fetch time: {info.FetchTime}.");
                Conclude();
            });
        }

        /// <summary>
        /// Continúa en el hilo principal cuando la tarea termina. Si falla, se avisa y se sigue igualmente:
        /// un paso fallido nunca deja el servicio sin terminar de inicializar.
        /// </summary>
        private void Then(Task task, string stepName, Action next)
        {
            try
            {
                task.ContinueWithOnMainThread(finished =>
                {
                    if (finished.IsFaulted || finished.IsCanceled)
                    {
                        Log($"{stepName} failed: {(finished.Exception != null ? finished.Exception.GetBaseException().Message : "cancelled")}", LogType.Warning);
                        if (stepName.StartsWith("FetchAsync", StringComparison.Ordinal))
                        {
                            Conclude();
                            return;
                        }
                    }

                    try
                    {
                        next();
                    }
                    catch (Exception e)
                    {
                        Fail($"Step after {stepName} failed", e);
                    }
                });
            }
            catch (Exception e)
            {
                Fail($"{stepName} could not start", e);
            }
        }

        private void Then<T>(Task<T> task, string stepName, Action next)
        {
            Then((Task)task, stepName, next);
        }

        private bool HasRemoteValues()
        {
            try
            {
                foreach (var pair in _remoteConfig.AllValues)
                {
                    if (pair.Value.Source == ValueSource.RemoteValue) return true;
                }
            }
            catch (Exception e)
            {
                Log("Could not inspect the activated values: " + e.Message, LogType.Warning);
            }
            return false;
        }

        private void ValuesAvailable(RemoteConfigSource source)
        {
            _source = source;
            _onValuesAvailable?.Invoke(source);
        }

        private void Fail(string what, Exception e)
        {
            Log($"{what}: {e.Message}. Using the {(_source == RemoteConfigSource.None ? RemoteConfigSource.Defaults : _source)} values.", LogType.Error);
            if (_source == RemoteConfigSource.None) ValuesAvailable(RemoteConfigSource.Defaults);
            Conclude();
        }

        private void Conclude()
        {
            if (_concluded) return;
            _concluded = true;
            _onFetchConcluded?.Invoke(_source == RemoteConfigSource.None ? RemoteConfigSource.Defaults : _source);
        }

        private void Log(string message, LogType type = LogType.Log)
        {
            if (type == LogType.Log && !_options.LogToConsole) return;
            Debug.unityLogger.Log(type, $"{LOG_PREFIX} {message}");
        }

        #endregion
    }

    /// <summary>
    /// Registra el proveedor de Firebase en el servicio al cargar el juego, antes de cualquier Awake,
    /// para que RemoteConfigService.Initialize lo use sin que el proyecto tenga que referenciar este assembly.
    /// </summary>
    internal static class FirebaseRemoteConfigProviderRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            RemoteConfigService.ProviderFactory = () => new FirebaseRemoteConfigProvider();
        }
    }
}
