namespace Viva.Services.RemoteConfig
{
    /// <summary>
    /// De dónde salen los valores que devuelve <see cref="RemoteConfigService"/> ahora mismo.
    /// </summary>
    public enum RemoteConfigSource
    {
        /// <summary>El servicio no se ha inicializado.</summary>
        None,

        /// <summary>Solo hay valores por defecto de la app.</summary>
        Defaults,

        /// <summary>Valores del servidor descargados en una sesión anterior, mientras el fetch de esta sesión no termina o ha fallado.</summary>
        Cache,

        /// <summary>Valores del servidor: el fetch de esta sesión ha terminado bien (o la caché era más reciente que el intervalo mínimo).</summary>
        Remote
    }
}
