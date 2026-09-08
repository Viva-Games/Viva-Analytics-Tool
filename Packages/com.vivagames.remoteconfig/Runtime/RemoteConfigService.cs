using System;
using System.Collections.Generic;
using UnityEngine;
using Viva.Core;

namespace Viva.Services.RemoteConfig
{
    /// <summary>
    /// Punto de acceso a Firebase Remote Config. Se inicializa una vez (desde RemoteConfigInit) con los
    /// valores por defecto generados en RemoteConfigParameters.Defaults(), y desde ese momento los getters
    /// devuelven el mejor valor disponible: el del servidor si el fetch ha terminado, el de la caché de la
    /// sesión anterior si aún no, el default registrado si la clave no está en el servidor, y el default del
    /// llamador si la clave no existe en ningún sitio o el valor no convierte al tipo pedido.
    /// Los getters nunca lanzan. Todo corre en el hilo principal.
    /// </summary>
    public static class RemoteConfigService
    {
        private const string LOG_PREFIX = "[RemoteConfig]";

        private static IRemoteConfigProvider _provider;
        private static RemoteConfigOptions _options;
        private static readonly Dictionary<string, string> _defaults = new Dictionary<string, string>();
        private static readonly HashSet<string> _warnedKeys = new HashSet<string>();
        private static ReadyGate _gate = new ReadyGate("Remote Config");

        /// <summary>
        /// Fábrica del proveedor de Firebase. La registra el assembly VivaGames.RemoteConfig.Firebase al cargar
        /// (solo existe cuando el SDK está en el proyecto). Sin ella, Initialize usa <see cref="LocalRemoteConfigProvider"/>.
        /// </summary>
        public static Func<IRemoteConfigProvider> ProviderFactory { get; set; }

        /// <summary>Proveedor en uso, o null antes de Initialize.</summary>
        public static IRemoteConfigProvider Provider => _provider;

        public static bool IsInitialized => _provider != null;

        /// <summary>true cuando el primer intento de fetch ha terminado, con éxito o sin él.</summary>
        public static bool IsReady => _gate.IsReady;

        /// <summary>De dónde salen los valores ahora mismo.</summary>
        public static RemoteConfigSource Source { get; private set; } = RemoteConfigSource.None;

        /// <summary>
        /// Se dispara una sola vez, en el hilo principal, cuando el primer intento de fetch ha terminado
        /// (con éxito o sin él: mira <see cref="Source"/>). Quien pueda llegar tarde debe usar <see cref="WhenReady"/>.
        /// </summary>
        public static event Action OnReady;

        /// <summary>
        /// Activaciones posteriores al primer fetch que cambian valores (actualizaciones en tiempo real).
        /// Reservado: la 1.0 no lo dispara.
        /// </summary>
        public static event Action OnValuesUpdated;

        /// <summary>
        /// Inicializa el servicio con el proveedor de Firebase si el SDK está en el proyecto, o con los
        /// defaults locales si no. Una segunda llamada no hace nada.
        /// </summary>
        public static void Initialize(IDictionary<string, object> defaults, RemoteConfigOptions options = null)
        {
            if (IsInitialized)
            {
                Debug.LogWarning($"{LOG_PREFIX} Already initialized; ignoring the second Initialize call.");
                return;
            }

            var provider = ProviderFactory != null ? ProviderFactory() : null;
            Initialize(provider ?? new LocalRemoteConfigProvider(), defaults, options);
        }

        /// <summary>Inicializa el servicio con un proveedor concreto (tests, proyectos especiales).</summary>
        public static void Initialize(IRemoteConfigProvider provider, IDictionary<string, object> defaults, RemoteConfigOptions options = null)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            if (IsInitialized)
            {
                Debug.LogWarning($"{LOG_PREFIX} Already initialized; ignoring the second Initialize call.");
                return;
            }

            _provider = provider;
            _options = RemoteConfigOptions.OrDefault(options);
            _defaults.Clear();
            var safeDefaults = new Dictionary<string, object>();
            if (defaults != null)
            {
                foreach (var pair in defaults)
                {
                    if (string.IsNullOrEmpty(pair.Key)) continue;
                    _defaults[pair.Key] = RemoteConfigConverter.ToRaw(pair.Value);
                    safeDefaults[pair.Key] = pair.Value;
                }
            }

            if (_options.LogToConsole)
            {
                Debug.Log($"{LOG_PREFIX} Initializing with {provider.GetType().Name} and {_defaults.Count} default(s).");
            }

            try
            {
                // Los avisos solo cuentan mientras este proveedor siga siendo el activo (un Reset en tests, por ejemplo,
                // podría dejar un aviso pendiente de un proveedor anterior).
                _provider.Initialize(safeDefaults, _options,
                    source => { if (ReferenceEquals(_provider, provider)) OnValuesAvailable(source); },
                    source => { if (ReferenceEquals(_provider, provider)) OnFetchConcluded(source); });
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG_PREFIX} The provider failed to initialize; using the in-app defaults. {e}");
                OnFetchConcluded(Source == RemoteConfigSource.None ? RemoteConfigSource.Defaults : Source);
            }
        }

        /// <summary>
        /// Ejecuta el callback en el acto si el servicio ya está listo, o una vez cuando lo esté.
        /// Sustituye al patrón "if (IsReady) X(); else OnReady += X;".
        /// </summary>
        public static void WhenReady(Action callback)
        {
            if (callback == null) return;
            _gate.WhenReady(callback);
        }

        /// <summary>true si hay un valor para la clave (del servidor o un default registrado).</summary>
        public static bool HasValue(string key)
        {
            return TryGetRawValue(key, out _, out _);
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            return Get(key, defaultValue, "int", RemoteConfigConverter.TryToInt);
        }

        public static long GetLong(string key, long defaultValue = 0)
        {
            return Get(key, defaultValue, "long", RemoteConfigConverter.TryToLong);
        }

        public static float GetFloat(string key, float defaultValue = 0f)
        {
            return Get(key, defaultValue, "float", RemoteConfigConverter.TryToFloat);
        }

        public static double GetDouble(string key, double defaultValue = 0d)
        {
            return Get(key, defaultValue, "double", RemoteConfigConverter.TryToDouble);
        }

        public static bool GetBool(string key, bool defaultValue = false)
        {
            return Get(key, defaultValue, "bool", RemoteConfigConverter.TryToBool);
        }

        public static string GetString(string key, string defaultValue = "")
        {
            return TryGetRawValue(key, out var raw, out _) ? raw : defaultValue;
        }

        /// <summary>
        /// Parsea el valor con JsonUtility. Igual que JsonUtility, no admite arrays ni diccionarios de primer
        /// nivel: envuélvelos en una clase. Cadena vacía o JSON inválido devuelven defaultValue con un aviso.
        /// </summary>
        public static T GetJson<T>(string key, T defaultValue = default)
        {
            if (!TryGetRawValue(key, out var raw, out var source)) return defaultValue;
            if (string.IsNullOrWhiteSpace(raw)) return defaultValue;

            try
            {
                var parsed = JsonUtility.FromJson<T>(raw);
                return parsed == null ? defaultValue : parsed;
            }
            catch (Exception e)
            {
                WarnOnce(key, $"{typeof(T).Name} could not be parsed from the {source} value: {e.Message}\nJSON: {raw}");
                return defaultValue;
            }
        }

        /// <summary>Para proveedores: avisa de que una activación posterior al primer fetch ha cambiado valores.</summary>
        public static void NotifyValuesUpdated(RemoteConfigSource source)
        {
            Source = source;
            Raise(OnValuesUpdated, nameof(OnValuesUpdated));
        }

        private delegate bool TryConvert<T>(string raw, out T value);

        private static T Get<T>(string key, T fallback, string typeName, TryConvert<T> convert)
        {
            if (!TryGetRawValue(key, out var raw, out var source)) return fallback;
            if (convert(raw, out var value)) return value;

            WarnOnce(key, $"the {source} value '{raw}' is not a valid {typeName}; using {fallback}.");
            return fallback;
        }

        private static bool TryGetRawValue(string key, out string raw, out RemoteConfigSource source)
        {
            raw = null;
            source = RemoteConfigSource.None;
            if (string.IsNullOrEmpty(key) || _provider == null) return false;

            bool found;
            try
            {
                found = _provider.TryGetRaw(key, out raw, out source);
            }
            catch (Exception e)
            {
                WarnOnce(key, "the provider failed to read the value: " + e.Message);
                found = false;
            }

            if (found && raw != null && source != RemoteConfigSource.Defaults) return true;

            // Default registrado: se devuelve tal y como lo formateamos nosotros, no como lo serializa el SDK.
            // También cubre al proveedor que no conoce la clave o ha fallado: los defaults nunca se pierden.
            if (_defaults.TryGetValue(key, out var ownDefault))
            {
                raw = ownDefault;
                source = RemoteConfigSource.Defaults;
                return true;
            }

            return found && raw != null;
        }

        private static void OnValuesAvailable(RemoteConfigSource source)
        {
            Source = source;
            if (_options.LogToConsole)
            {
                Debug.Log($"{LOG_PREFIX} Values available from {source}.");
            }
        }

        private static void OnFetchConcluded(RemoteConfigSource source)
        {
            if (_gate.IsReady)
            {
                Debug.LogWarning($"{LOG_PREFIX} The provider reported the fetch as concluded more than once; ignoring.");
                return;
            }

            Source = source;
            if (_options == null || _options.LogToConsole)
            {
                Debug.Log($"{LOG_PREFIX} Ready. Values from {source}.");
            }

            _gate.SetReady();
            Raise(OnReady, nameof(OnReady));
        }

        private static void Raise(Action handlers, string name)
        {
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception e)
                {
                    Debug.LogError($"{LOG_PREFIX} {name} handler failed: {e}");
                }
            }
        }

        private static void WarnOnce(string key, string message)
        {
            if (!_warnedKeys.Add(key)) return;
            Debug.LogWarning($"{LOG_PREFIX} \"{key}\": {message}");
        }

        /// <summary>Deja el servicio como recién cargado. Solo para tests.</summary>
        internal static void Reset()
        {
            _provider = null;
            _options = null;
            _defaults.Clear();
            _warnedKeys.Clear();
            _gate = new ReadyGate("Remote Config");
            Source = RemoteConfigSource.None;
            OnReady = null;
            OnValuesUpdated = null;
            ProviderFactory = null;
        }
    }
}
