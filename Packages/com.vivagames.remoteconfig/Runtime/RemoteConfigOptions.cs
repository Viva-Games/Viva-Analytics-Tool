using System;

namespace Viva.Services.RemoteConfig
{
    /// <summary>
    /// Opciones de <see cref="RemoteConfigService.Initialize(System.Collections.Generic.IDictionary{string, object}, RemoteConfigOptions)"/>.
    /// Los valores por defecto reproducen el comportamiento probado en producción: un fetch por arranque en frío.
    /// </summary>
    public sealed class RemoteConfigOptions
    {
        /// <summary>
        /// Se pasa a FetchAsync(TimeSpan). Zero fuerza un fetch en cada arranque. Firebase usa 12 horas por
        /// defecto y avisa de que fetches demasiado frecuentes se rechazan (Throttled): en ese caso el servicio
        /// se queda con la caché de la sesión anterior o con los defaults.
        /// </summary>
        public TimeSpan MinimumFetchInterval = TimeSpan.Zero;

        /// <summary>Timeout de conexión del fetch. null deja el del SDK (60 segundos).</summary>
        public TimeSpan? FetchTimeout = null;

        /// <summary>
        /// Activa al arrancar lo que se descargó en la sesión anterior, para que esos valores estén disponibles
        /// antes de que termine el fetch de esta sesión (y aunque falle).
        /// </summary>
        public bool ActivateCachedValuesOnStart = true;

        /// <summary>Si es true, el servicio escribe en la consola de Unity qué hace y de dónde salen los valores.</summary>
        public bool LogToConsole = true;

        /// <summary>Las opciones dadas, o unas nuevas con los valores por defecto si son null.</summary>
        public static RemoteConfigOptions OrDefault(RemoteConfigOptions options) => options ?? new RemoteConfigOptions();
    }
}
