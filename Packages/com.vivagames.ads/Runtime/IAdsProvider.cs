using System;

namespace Viva.Services.Ads
{
    /// <summary>
    /// Lo que <see cref="AdsService"/> pide al SDK. El proveedor de MAX lo implementa sobre MaxSdk; el de tests
    /// lo simula. Todo en el hilo principal. Los avisos del SDK vuelven por <see cref="IAdsListener"/>.
    /// </summary>
    public interface IAdsProvider
    {
        void Initialize(AdUnitConfiguration configuration, AdsOptions options, IAdsListener listener);

        void LoadRewarded();
        bool IsRewardedReady { get; }
        void ShowRewarded(string placement);

        void LoadInterstitial();
        bool IsInterstitialReady { get; }
        void ShowInterstitial(string placement);

        /// <summary>Crea el banner (oculto) en la posición de la configuración; el SDK lo carga y lo refresca solo.</summary>
        void CreateBanner();
        void ShowBanner(string placement);
        void HideBanner();
        void SetBannerPosition(BannerPosition position);

        bool HasConsentDialog { get; }
        /// <summary>Vuelve a mostrar el CMP. Al terminar, el proveedor vuelve a leer el consentimiento y avisa por el listener.</summary>
        void ShowConsentDialog(Action<bool> onCompleted);

        void ShowMediationDebugger();
    }

    /// <summary>Avisos del proveedor al servicio. Siempre en el hilo principal.</summary>
    public interface IAdsListener
    {
        /// <summary>El SDK está listo, flujo de consentimiento incluido. consent puede ser null si el SDK no da datos.</summary>
        void OnSdkInitialized(AdsConsentInfo consent);

        /// <summary>El SDK no va a poder inicializarse (sin SDK, error).</summary>
        void OnInitializationFailed(string reason);

        void OnAdLoaded(AdFormat format);
        void OnAdLoadFailed(AdFormat format, AdLoadError error, string message);
        void OnAdDisplayed(AdFormat format);
        void OnAdDisplayFailed(AdFormat format, string message);
        void OnAdHidden(AdFormat format);
        void OnRewardReceived();
        void OnAdRevenue(AdRevenueInfo revenue);

        /// <summary>El usuario ha cambiado su consentimiento desde el diálogo del CMP.</summary>
        void OnConsentChanged(AdsConsentInfo consent);
    }

    /// <summary>
    /// Esperas del servicio (timeout de inicialización, backoff, gracia del reward, vigilante de cierre, espera
    /// de carga). En el juego corre sobre corrutinas en tiempo real; en los tests se controla a mano.
    /// </summary>
    public interface IAdsScheduler
    {
        /// <summary>Segundos de reloj real desde el arranque.</summary>
        float Now { get; }

        /// <summary>Ejecuta la acción pasados los segundos indicados (0 = en el siguiente tick). Devuelve un handle para cancelar.</summary>
        object Delay(float seconds, Action action);

        void Cancel(object handle);
    }
}
