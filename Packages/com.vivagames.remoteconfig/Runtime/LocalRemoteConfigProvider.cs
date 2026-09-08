using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Viva.Services.RemoteConfig
{
    /// <summary>
    /// Proveedor sin SDK: sirve los valores por defecto registrados. Se usa cuando el SDK de Firebase Remote
    /// Config no está en el proyecto (VIVA_FIREBASE_REMOTE_CONFIG sin definir) y en los tests.
    /// Los avisos de "valores disponibles" y "fetch terminado" se entregan en el siguiente tick del hilo
    /// principal, como haría Firebase, para que quien se suscriba a OnReady en el mismo frame que Initialize
    /// no se lo pierda. Con <see cref="CompleteSynchronously"/> se entregan dentro de Initialize (tests).
    /// </summary>
    public sealed class LocalRemoteConfigProvider : IRemoteConfigProvider
    {
        private readonly Dictionary<string, string> _defaults = new Dictionary<string, string>();
        private readonly Dictionary<string, string> _overrides = new Dictionary<string, string>();

        /// <summary>Si es true, Initialize avisa de valores disponibles y fetch terminado antes de volver.</summary>
        public bool CompleteSynchronously { get; set; }

        /// <summary>
        /// Valor que sustituye al default de una clave (se devuelve como si viniera del servidor).
        /// Pensado para probar valores en el editor sin tocar la consola de Firebase.
        /// </summary>
        public void SetOverride(string key, object value)
        {
            if (string.IsNullOrEmpty(key)) return;
            _overrides[key] = RemoteConfigConverter.ToRaw(value);
        }

        public void ClearOverrides()
        {
            _overrides.Clear();
        }

        public void Initialize(IDictionary<string, object> defaults, RemoteConfigOptions options,
            Action<RemoteConfigSource> onValuesAvailable, Action<RemoteConfigSource> onFetchConcluded)
        {
            _defaults.Clear();
            if (defaults != null)
            {
                foreach (var pair in defaults)
                {
                    if (!string.IsNullOrEmpty(pair.Key)) _defaults[pair.Key] = RemoteConfigConverter.ToRaw(pair.Value);
                }
            }

            options = RemoteConfigOptions.OrDefault(options);
            if (options.LogToConsole)
            {
                Debug.LogWarning($"[RemoteConfig] Firebase Remote Config SDK not found. Using the {_defaults.Count} in-app default(s).");
            }

            void Complete()
            {
                onValuesAvailable?.Invoke(RemoteConfigSource.Defaults);
                onFetchConcluded?.Invoke(RemoteConfigSource.Defaults);
            }

            var context = SynchronizationContext.Current;
            if (CompleteSynchronously || context == null)
            {
                Complete();
            }
            else
            {
                context.Post(_ => Complete(), null);
            }
        }

        public bool TryGetRaw(string key, out string raw, out RemoteConfigSource source)
        {
            raw = null;
            source = RemoteConfigSource.None;
            if (string.IsNullOrEmpty(key)) return false;

            if (_overrides.TryGetValue(key, out raw))
            {
                source = RemoteConfigSource.Remote;
                return true;
            }
            if (_defaults.TryGetValue(key, out raw))
            {
                source = RemoteConfigSource.Defaults;
                return true;
            }
            return false;
        }
    }
}
