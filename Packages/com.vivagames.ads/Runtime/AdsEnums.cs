namespace Viva.Services.Ads
{
    public enum AdFormat
    {
        Rewarded,
        Interstitial,
        Banner
    }

    /// <summary>Estado del SDK de anuncios.</summary>
    public enum AdsState
    {
        NotInitialized,
        Initializing,
        /// <summary>El SDK ha terminado de inicializarse, flujo de consentimiento incluido.</summary>
        Ready,
        /// <summary>El SDK no ha podido inicializarse (sin SDK, timeout, sin red). Las cargas siguen reintentando por si se recupera.</summary>
        Unavailable
    }

    /// <summary>Estado de un formato. Derivado de los callbacks del SDK y de los códigos de error, sin sondear.</summary>
    public enum AdStatus
    {
        /// <summary>El formato no está activo en la configuración.</summary>
        Disabled,
        /// <summary>Antes de Initialize, o el SDK todavía no está listo.</summary>
        NotInitialized,
        /// <summary>No hay anuncio y la última carga falló por red (o el dispositivo no tiene conexión).</summary>
        NoInternet,
        /// <summary>No hay anuncio todavía: cargando o reintentando por otro motivo (sin inventario, por ejemplo).</summary>
        Loading,
        /// <summary>Hay un anuncio cargado.</summary>
        Ready,
        /// <summary>El anuncio está en pantalla.</summary>
        Showing
    }

    public enum RewardedResult
    {
        /// <summary>El jugador ha visto el anuncio y hay que dar la recompensa.</summary>
        Rewarded,
        /// <summary>El anuncio se cerró sin recompensa (saltado o cerrado antes de tiempo).</summary>
        NotRewarded,
        /// <summary>El SDK no pudo mostrar el anuncio, o la app volvió de segundo plano sin que el anuncio se cerrara.</summary>
        DisplayFailed,
        /// <summary>No hay anuncio cargado ahora mismo.</summary>
        NotReady,
        /// <summary>No hay anuncio y no hay conexión.</summary>
        NoInternet,
        /// <summary>El SDK no está inicializado o no está disponible.</summary>
        NotInitialized,
        /// <summary>Ya hay un anuncio en pantalla.</summary>
        AlreadyShowing,
        /// <summary>El placement no es del enum de rewarded generado.</summary>
        InvalidPlacement
    }

    public enum InterstitialResult
    {
        Closed,
        DisplayFailed,
        NotReady,
        NoInternet,
        NotInitialized,
        AlreadyShowing,
        InvalidPlacement
    }

    /// <summary>Posición del banner. Mismos valores que el SDK de MAX.</summary>
    public enum BannerPosition
    {
        TopLeft,
        TopCenter,
        TopRight,
        Centered,
        CenterLeft,
        CenterRight,
        BottomLeft,
        BottomCenter,
        BottomRight
    }

    /// <summary>Motivo de un fallo de carga, reducido a lo que importa para el estado.</summary>
    public enum AdLoadError
    {
        /// <summary>Sin inventario, timeout del waterfall, error desconocido.</summary>
        Other,
        /// <summary>Sin red, timeout de red o error de red.</summary>
        NoNetwork,
        /// <summary>Sin anuncio disponible ahora mismo (no fill).</summary>
        NoFill
    }
}
