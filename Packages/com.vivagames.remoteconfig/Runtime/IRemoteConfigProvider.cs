using System;
using System.Collections.Generic;

namespace Viva.Services.RemoteConfig
{
    /// <summary>
    /// Fuente de valores de <see cref="RemoteConfigService"/>: Firebase Remote Config cuando el SDK está en el
    /// proyecto, o los defaults locales si no. Devuelve los valores como cadena, igual que hace Firebase;
    /// el servicio hace las conversiones, para que todos los proveedores se comporten igual.
    /// Todo corre en el hilo principal.
    /// </summary>
    public interface IRemoteConfigProvider
    {
        /// <summary>
        /// Arranca el proveedor.
        /// onValuesAvailable: ya hay valores que leer (defaults, o la caché de la sesión anterior); puede llamarse más de una vez si la fuente mejora.
        /// onFetchConcluded: el primer intento de fetch ha terminado, con éxito o sin él. Se llama exactamente una vez;
        /// el servicio marca IsReady y dispara OnReady.
        /// </summary>
        void Initialize(IDictionary<string, object> defaults, RemoteConfigOptions options,
            Action<RemoteConfigSource> onValuesAvailable, Action<RemoteConfigSource> onFetchConcluded);

        /// <summary>
        /// Valor crudo de una clave. false si la clave no existe ni en remoto ni en los defaults registrados.
        /// </summary>
        bool TryGetRaw(string key, out string raw, out RemoteConfigSource source);
    }
}
