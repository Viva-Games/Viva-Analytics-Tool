namespace Viva.Services.Ads
{
    /// <summary>
    /// Opciones de <see cref="AdsService.Initialize"/>. Los valores por defecto reproducen el comportamiento
    /// probado en producción en Arrows.
    /// </summary>
    public sealed class AdsOptions
    {
        /// <summary>Cuánto se espera a que el SDK termine de inicializarse (flujo de consentimiento incluido) antes de darlo por no disponible. Sin conexión no se espera.</summary>
        public float InitializationTimeoutSeconds = 30f;

        /// <summary>Pausa el AudioListener mientras hay un anuncio a pantalla completa.</summary>
        public bool PauseAudioWhileShowing = true;

        /// <summary>Desactiva el EventSystem mientras hay un anuncio a pantalla completa, para que ningún botón responda en la ventana en la que el overlay nativo aún no cubre la pantalla.</summary>
        public bool BlockUiInputWhileShowing = true;

        /// <summary>Tiempo que se espera tras cerrarse un rewarded por si la recompensa llega después del cierre, como pasa en algunas redes.</summary>
        public float RewardGraceSeconds = 0.5f;

        /// <summary>Al volver de segundo plano con un anuncio en pantalla, si en este tiempo el SDK no avisa del cierre, se da por cerrado con fallo para no dejar al jugador atrapado.</summary>
        public float CloseWatchdogSeconds = 1f;

        /// <summary>Reintento de carga con espera 2^n segundos, n limitado a este exponente (6 = 64 s), como recomienda AppLovin.</summary>
        public int MaxRetryExponent = 6;

        /// <summary>Segundos mínimos entre dos interstitials. 0 = sin espera. Arrows usa 125.</summary>
        public float InterstitialCooldownSeconds = 0f;

        /// <summary>Si es true, la espera entre interstitials empieza a contar en Initialize; si no, el primero puede salir en cuanto haya anuncio.</summary>
        public bool InterstitialCooldownStartsAtInitialize = true;

        /// <summary>Tras completar un rewarded, la siguiente llamada a TryShowInterstitial no muestra anuncio (y consume la marca).</summary>
        public bool SkipNextInterstitialAfterRewarded = true;

        /// <summary>Toggle de depuración: con false, TryShowInterstitial nunca muestra. Los rewarded no se ven afectados.</summary>
        public bool InterstitialsEnabled = true;

        /// <summary>Android: pide al SDK que devuelva el foco de audio al cerrar un anuncio.</summary>
        public bool ReturnAudioFocus = true;

        /// <summary>Identificadores de publicidad de los dispositivos de prueba (MAX muestra anuncios de test en ellos).</summary>
        public string[] TestDeviceAdvertisingIds;

        /// <summary>Escribe en la consola qué hace el servicio y por qué.</summary>
        public bool LogToConsole = true;

        public static AdsOptions OrDefault(AdsOptions options) => options ?? new AdsOptions();
    }
}
