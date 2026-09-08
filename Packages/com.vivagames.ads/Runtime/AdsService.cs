using System;
using System.Collections.Generic;
using UnityEngine;
using Viva.Core;

namespace Viva.Services.Ads
{
    /// <summary>
    /// Punto de acceso a los anuncios. El juego declara formatos y placements en Viva > Ads > Ad Units, pone el
    /// componente AdsInit en la primera escena y llama a ShowRewarded, TryShowInterstitial o ShowBanner.
    /// Inicialización, consentimiento, carga, reintentos, protección del juego mientras hay un anuncio y el
    /// estado de cada formato los lleva el servicio. Todo en el hilo principal. Ningún método lanza.
    /// </summary>
    public static class AdsService
    {
        private const string LOG_PREFIX = "[Ads]";

        private sealed class FormatState
        {
            public AdStatus Status = AdStatus.Disabled;
            public int RetryAttempt;
            public object RetryHandle;
        }

        private static IAdsProvider _provider;
        private static AdUnitConfiguration _configuration;
        private static AdsOptions _options;
        private static IAdsScheduler _scheduler;
        private static Func<bool> _hasInternet;
        private static AdsRuntime _runtime;
        private static readonly Dictionary<AdFormat, FormatState> _formats = new Dictionary<AdFormat, FormatState>();
        private static ReadyGate _initialized = new ReadyGate("Ads");
        private static object _initializationTimeoutHandle;

        // Anuncio a pantalla completa en curso.
        private static AdFormat? _showing;
        private static Action<RewardedResult> _rewardedCallback;
        private static Action<InterstitialResult> _interstitialCallback;
        private static bool _rewardReceived;
        private static object _rewardGraceHandle;
        private static object _closeWatchdogHandle;

        // Espera de carga de ShowRewarded.
        private static object _waitHandle;
        private static Action<RewardedResult> _waitCallback;
        private static string _waitPlacement;

        // Política de interstitials.
        private static float _lastInterstitialTime = float.NegativeInfinity;
        private static bool _skipNextInterstitial;

        private static bool _bannerVisible;

        #region Estado y eventos

        /// <summary>Fábrica del proveedor del SDK. La registra el assembly VivaGames.Ads.MaxSdk al cargar; sin ella se usa NoAdsProvider.</summary>
        public static Func<IAdsProvider> ProviderFactory { get; set; }

        public static AdsState State { get; private set; } = AdsState.NotInitialized;

        /// <summary>true cuando el SDK ha terminado de inicializarse (flujo de consentimiento incluido).</summary>
        public static bool IsReady => State == AdsState.Ready;

        public static bool IsInitialized => _provider != null;

        /// <summary>true mientras hay un rewarded o un interstitial en pantalla.</summary>
        public static bool IsShowingAd => _showing.HasValue;

        /// <summary>Una sola vez, cuando la inicialización termina, con éxito o sin él (mira State).</summary>
        public static event Action OnInitialized;

        /// <summary>El CMP de MAX ha resuelto el consentimiento (también tras ShowConsentDialog). A analíticas llega por Viva Core sin este evento.</summary>
        public static event Action<AdsConsentInfo> OnConsentResolved;

        /// <summary>Impresión con ingresos, de cualquier formato.</summary>
        public static event Action<AdRevenueInfo> OnAdRevenue;

        /// <summary>Cambio de estado de un formato.</summary>
        public static event Action<AdFormat, AdStatus> OnStatusChanged;

        /// <summary>true al empezar a mostrar un rewarded o interstitial, false al terminar.</summary>
        public static event Action<bool> OnShowingAdChanged;

        /// <summary>true mientras ShowRewarded espera a que cargue un anuncio (para un spinner), false al terminar la espera.</summary>
        public static event Action<bool> OnWaitingForRewarded;

        public static AdStatus RewardedStatus => StatusOf(AdFormat.Rewarded);
        public static AdStatus InterstitialStatus => StatusOf(AdFormat.Interstitial);
        public static AdStatus BannerStatus => StatusOf(AdFormat.Banner);

        public static bool IsRewardedReady => StatusOf(AdFormat.Rewarded) == AdStatus.Ready && _provider != null && _provider.IsRewardedReady;
        public static bool IsInterstitialReady => StatusOf(AdFormat.Interstitial) == AdStatus.Ready && _provider != null && _provider.IsInterstitialReady;
        public static bool IsBannerVisible => _bannerVisible;

        /// <summary>Estado de un formato. Disabled si no está en la configuración.</summary>
        public static AdStatus StatusOf(AdFormat format)
        {
            return _formats.TryGetValue(format, out var state) ? state.Status : AdStatus.Disabled;
        }

        /// <summary>Ejecuta el callback en el acto si la inicialización ya terminó, o una vez cuando termine.</summary>
        public static void WhenInitialized(Action callback)
        {
            if (callback != null) _initialized.WhenReady(callback);
        }

        #endregion

        #region Initialize

        /// <summary>
        /// Inicializa el SDK con la configuración generada (AdPlacements.Configuration()). Una segunda llamada no hace nada.
        /// </summary>
        public static void Initialize(AdUnitConfiguration configuration, AdsOptions options = null)
        {
            if (IsInitialized)
            {
                Debug.LogWarning($"{LOG_PREFIX} Already initialized; ignoring the second Initialize call.");
                return;
            }
            if (configuration == null)
            {
                Debug.LogError($"{LOG_PREFIX} Initialize needs a configuration (AdPlacements.Configuration()).");
                return;
            }

            _configuration = configuration;
            _options = AdsOptions.OrDefault(options);
            if (_scheduler == null)
            {
                _runtime = AdsRuntime.GetOrCreate();
                _scheduler = _runtime;
            }
            if (_hasInternet == null) _hasInternet = () => Application.internetReachability != NetworkReachability.NotReachable;

            _formats.Clear();
            foreach (AdFormat format in Enum.GetValues(typeof(AdFormat)))
            {
                _formats[format] = new FormatState { Status = configuration.IsEnabled(format) ? AdStatus.NotInitialized : AdStatus.Disabled };
            }

            _provider = ProviderFactory != null ? ProviderFactory() ?? new NoAdsProvider() : new NoAdsProvider();
            State = AdsState.Initializing;
            if (_options.InterstitialCooldownStartsAtInitialize) _lastInterstitialTime = _scheduler.Now;

            Log($"Initializing with {_provider.GetType().Name}: rewarded={configuration.IsEnabled(AdFormat.Rewarded)} interstitial={configuration.IsEnabled(AdFormat.Interstitial)} banner={configuration.IsEnabled(AdFormat.Banner)}.");

            // Sin red no se espera al SDK: el menú no se bloquea. Si la conexión vuelve, el SDK avisa y las cargas reintentan.
            float timeout = _hasInternet() ? Math.Max(0f, _options.InitializationTimeoutSeconds) : 0f;
            _initializationTimeoutHandle = _scheduler.Delay(timeout, OnInitializationTimeout);

            try
            {
                _provider.Initialize(configuration, _options, new Listener());
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG_PREFIX} The provider failed to initialize: {e}");
                FinishInitialization(AdsState.Unavailable, "provider exception");
            }
        }

        private static void OnInitializationTimeout()
        {
            _initializationTimeoutHandle = null;
            if (State != AdsState.Initializing) return;
            Debug.LogWarning($"{LOG_PREFIX} The SDK did not finish initializing in time (no network?). Continuing; loads will retry when it is ready.");
            FinishInitialization(AdsState.Unavailable, "timeout");
            RequestLoads();
        }

        private static void FinishInitialization(AdsState state, string reason)
        {
            State = state;
            if (_initializationTimeoutHandle != null)
            {
                _scheduler.Cancel(_initializationTimeoutHandle);
                _initializationTimeoutHandle = null;
            }
            if (_initialized.IsReady) return;

            Log(state == AdsState.Ready ? "SDK ready." : $"SDK unavailable ({reason}).");
            _initialized.SetReady();
            Raise(OnInitialized, nameof(OnInitialized));
        }

        private static void RequestLoads()
        {
            if (IsEnabled(AdFormat.Rewarded)) Load(AdFormat.Rewarded);
            if (IsEnabled(AdFormat.Interstitial)) Load(AdFormat.Interstitial);
            if (IsEnabled(AdFormat.Banner))
            {
                SetStatus(AdFormat.Banner, AdStatus.Loading);
                Try(() => _provider.CreateBanner(), "CreateBanner");
            }
        }

        private static void Load(AdFormat format)
        {
            var state = _formats[format];
            if (state.RetryHandle != null)
            {
                _scheduler.Cancel(state.RetryHandle);
                state.RetryHandle = null;
            }
            if (state.Status != AdStatus.NoInternet && state.Status != AdStatus.Showing) SetStatus(format, AdStatus.Loading);

            switch (format)
            {
                case AdFormat.Rewarded: Try(() => _provider.LoadRewarded(), "LoadRewarded"); break;
                case AdFormat.Interstitial: Try(() => _provider.LoadInterstitial(), "LoadInterstitial"); break;
            }
        }

        #endregion

        #region Rewarded

        /// <summary>
        /// Muestra un rewarded. onResult llega siempre, en el acto si no se puede mostrar (con el motivo) o al
        /// cerrarse el anuncio. waitForLoadSeconds > 0: si el anuncio está cargando y hay red, espera hasta ese
        /// tiempo a que cargue (OnWaitingForRewarded avisa) antes de rendirse.
        /// </summary>
        public static void ShowRewarded(Enum placement, Action<RewardedResult> onResult, float waitForLoadSeconds = 0f)
        {
            if (_configuration == null)
            {
                onResult?.Invoke(RewardedResult.NotInitialized);
                return;
            }
            if (!TryResolve(AdFormat.Rewarded, placement, out string name))
            {
                Debug.LogError($"{LOG_PREFIX} ShowRewarded: {Describe(placement)} is not a rewarded placement of the generated AdPlacements.");
                onResult?.Invoke(RewardedResult.InvalidPlacement);
                return;
            }
            ShowRewarded(name, onResult, waitForLoadSeconds);
        }

        public static void ShowRewarded(string placement, Action<RewardedResult> onResult, float waitForLoadSeconds = 0f)
        {
            if (!IsEnabled(AdFormat.Rewarded) || string.IsNullOrEmpty(placement))
            {
                onResult?.Invoke(RewardedResult.InvalidPlacement);
                return;
            }
            if (State != AdsState.Ready)
            {
                onResult?.Invoke(RewardedResult.NotInitialized);
                return;
            }
            if (IsShowingAd || _waitHandle != null)
            {
                onResult?.Invoke(RewardedResult.AlreadyShowing);
                return;
            }

            if (!_provider.IsRewardedReady)
            {
                var status = StatusOf(AdFormat.Rewarded);
                if (waitForLoadSeconds > 0f && status == AdStatus.Loading && _hasInternet())
                {
                    Log($"Rewarded '{placement}' not loaded yet; waiting up to {waitForLoadSeconds}s.");
                    _waitCallback = onResult;
                    _waitPlacement = placement;
                    _waitHandle = _scheduler.Delay(waitForLoadSeconds, OnWaitForRewardedExpired);
                    Raise(OnWaitingForRewarded, true, nameof(OnWaitingForRewarded));
                    return;
                }
                onResult?.Invoke(status == AdStatus.NoInternet || !_hasInternet() ? RewardedResult.NoInternet : RewardedResult.NotReady);
                return;
            }

            BeginFullscreen(AdFormat.Rewarded);
            _rewardedCallback = onResult;
            _rewardReceived = false;
            Log($"Showing rewarded '{placement}'.");
            if (!Try(() => _provider.ShowRewarded(placement), "ShowRewarded"))
            {
                FinishFullscreen(AdFormat.Rewarded, true);
            }
        }

        private static void OnWaitForRewardedExpired()
        {
            var callback = EndWait();
            var status = StatusOf(AdFormat.Rewarded);
            Log("Rewarded did not load in time.");
            callback?.Invoke(status == AdStatus.NoInternet || !_hasInternet() ? RewardedResult.NoInternet : RewardedResult.NotReady);
        }

        private static Action<RewardedResult> EndWait()
        {
            if (_waitHandle != null) _scheduler.Cancel(_waitHandle);
            _waitHandle = null;
            var callback = _waitCallback;
            _waitCallback = null;
            _waitPlacement = null;
            Raise(OnWaitingForRewarded, false, nameof(OnWaitingForRewarded));
            return callback;
        }

        #endregion

        #region Interstitial

        /// <summary>true si el anuncio se ha lanzado y la política lo permite: entonces onClosed llega al cerrarse (o al fallar el display). false si no se muestra, y onClosed no se llama.</summary>
        public static bool CanShowInterstitial => CheckInterstitialPolicy(false) && IsInterstitialReady && !IsShowingAd;

        /// <summary>Segundos que faltan para que la espera entre interstitials permita otro. 0 si ya se puede.</summary>
        public static float InterstitialCooldownRemaining
        {
            get
            {
                if (_options == null || _options.InterstitialCooldownSeconds <= 0f || float.IsNegativeInfinity(_lastInterstitialTime)) return 0f;
                return Math.Max(0f, _options.InterstitialCooldownSeconds - (_scheduler.Now - _lastInterstitialTime));
            }
        }

        /// <summary>
        /// Muestra un interstitial si la política lo permite (toggle, espera entre interstitials, salto tras un
        /// rewarded) y hay anuncio. Devuelve true solo si se lanza: entonces onClosed se invoca al cerrarse o al
        /// fallar el display, nunca de forma espuria. Con false, el llamante sigue su flujo en el acto.
        /// Lo que depende del juego (nivel mínimo, tutorial...) se decide antes de llamar.
        /// </summary>
        public static bool TryShowInterstitial(Enum placement, Action onClosed = null)
        {
            if (_configuration == null) return false;
            if (!TryResolve(AdFormat.Interstitial, placement, out string name))
            {
                Debug.LogError($"{LOG_PREFIX} TryShowInterstitial: {Describe(placement)} is not an interstitial placement of the generated AdPlacements.");
                return false;
            }
            return TryShowInterstitial(name, onClosed);
        }

        public static bool TryShowInterstitial(string placement, Action onClosed = null)
        {
            if (!CheckInterstitialPolicy(true)) return false;
            if (!IsEnabled(AdFormat.Interstitial) || State != AdsState.Ready || IsShowingAd || !_provider.IsInterstitialReady) return false;

            bool launched = false;
            ShowInterstitial(placement, result =>
            {
                if (launched) onClosed?.Invoke();
            });
            launched = _showing == AdFormat.Interstitial;
            return launched;
        }

        /// <summary>Muestra un interstitial sin aplicar la política. onResult llega siempre, con el motivo si no se puede mostrar.</summary>
        public static void ShowInterstitial(Enum placement, Action<InterstitialResult> onResult = null)
        {
            if (_configuration == null)
            {
                onResult?.Invoke(InterstitialResult.NotInitialized);
                return;
            }
            if (!TryResolve(AdFormat.Interstitial, placement, out string name))
            {
                Debug.LogError($"{LOG_PREFIX} ShowInterstitial: {Describe(placement)} is not an interstitial placement of the generated AdPlacements.");
                onResult?.Invoke(InterstitialResult.InvalidPlacement);
                return;
            }
            ShowInterstitial(name, onResult);
        }

        public static void ShowInterstitial(string placement, Action<InterstitialResult> onResult = null)
        {
            if (!IsEnabled(AdFormat.Interstitial) || string.IsNullOrEmpty(placement))
            {
                onResult?.Invoke(InterstitialResult.InvalidPlacement);
                return;
            }
            if (State != AdsState.Ready)
            {
                onResult?.Invoke(InterstitialResult.NotInitialized);
                return;
            }
            if (IsShowingAd)
            {
                onResult?.Invoke(InterstitialResult.AlreadyShowing);
                return;
            }
            if (!_provider.IsInterstitialReady)
            {
                var status = StatusOf(AdFormat.Interstitial);
                onResult?.Invoke(status == AdStatus.NoInternet || !_hasInternet() ? InterstitialResult.NoInternet : InterstitialResult.NotReady);
                return;
            }

            BeginFullscreen(AdFormat.Interstitial);
            _interstitialCallback = onResult;
            Log($"Showing interstitial '{placement}'.");
            if (!Try(() => _provider.ShowInterstitial(placement), "ShowInterstitial"))
            {
                FinishFullscreen(AdFormat.Interstitial, true);
            }
        }

        /// <summary>Reinicia la espera entre interstitials como si acabara de mostrarse uno.</summary>
        public static void ResetInterstitialCooldown()
        {
            if (_scheduler != null) _lastInterstitialTime = _scheduler.Now;
        }

        /// <summary>Hace que la siguiente llamada a TryShowInterstitial no muestre anuncio (por ejemplo, el juego acaba de dar una recompensa por otra vía).</summary>
        public static void SkipNextInterstitial()
        {
            _skipNextInterstitial = true;
        }

        /// <summary>Comprueba la política. consume = true gasta la marca de "saltar tras rewarded", como hace Arrows en cada comprobación.</summary>
        private static bool CheckInterstitialPolicy(bool consume)
        {
            if (_options == null) return false;

            bool skip = _skipNextInterstitial;
            if (consume) _skipNextInterstitial = false;

            if (!_options.InterstitialsEnabled) return false;
            if (skip)
            {
                if (consume) Log("Interstitial skipped: the player just watched a rewarded ad.");
                return false;
            }
            if (InterstitialCooldownRemaining > 0f) return false;
            return true;
        }

        #endregion

        #region Banner

        public static void ShowBanner(Enum placement)
        {
            if (_configuration == null) return;
            if (!TryResolve(AdFormat.Banner, placement, out string name))
            {
                Debug.LogError($"{LOG_PREFIX} ShowBanner: {Describe(placement)} is not a banner placement of the generated AdPlacements.");
                return;
            }
            ShowBanner(name);
        }

        public static void ShowBanner(string placement)
        {
            if (!IsEnabled(AdFormat.Banner) || _provider == null) return;
            _bannerVisible = true;
            Log($"Showing banner '{placement}'.");
            Try(() => _provider.ShowBanner(placement ?? string.Empty), "ShowBanner");
        }

        public static void HideBanner()
        {
            if (!IsEnabled(AdFormat.Banner) || _provider == null) return;
            _bannerVisible = false;
            Try(() => _provider.HideBanner(), "HideBanner");
        }

        public static void SetBannerPosition(BannerPosition position)
        {
            if (!IsEnabled(AdFormat.Banner) || _provider == null) return;
            Try(() => _provider.SetBannerPosition(position), "SetBannerPosition");
        }

        #endregion

        #region Consentimiento y utilidades

        /// <summary>true si el CMP puede volver a mostrarse (ajustes de privacidad).</summary>
        public static bool HasConsentDialog => _provider != null && IsReady && _provider.HasConsentDialog;

        /// <summary>Vuelve a mostrar el diálogo de consentimiento. onCompleted(true) si se mostró y cerró sin error; el nuevo consentimiento se publica como al inicio.</summary>
        public static void ShowConsentDialog(Action<bool> onCompleted = null)
        {
            if (!HasConsentDialog)
            {
                onCompleted?.Invoke(false);
                return;
            }
            Try(() => _provider.ShowConsentDialog(ok => onCompleted?.Invoke(ok)), "ShowConsentDialog");
        }

        public static void ShowMediationDebugger()
        {
            if (_provider == null) return;
            Try(() => _provider.ShowMediationDebugger(), "ShowMediationDebugger");
        }

        #endregion

        #region Ciclo de un anuncio a pantalla completa

        private static void BeginFullscreen(AdFormat format)
        {
            _showing = format;
            SetStatus(format, AdStatus.Showing);
            if (_runtime != null)
            {
                if (_options.PauseAudioWhileShowing) _runtime.SetAudioPaused(true);
                if (_options.BlockUiInputWhileShowing) _runtime.SetUiInputBlocked(true);
            }
            Raise(OnShowingAdChanged, true, nameof(OnShowingAdChanged));
        }

        /// <summary>Cierra el ciclo de un anuncio: restaura el juego, decide el resultado y avisa una sola vez.</summary>
        private static void FinishFullscreen(AdFormat format, bool failed)
        {
            if (_showing != format) return;

            if (_rewardGraceHandle != null) _scheduler.Cancel(_rewardGraceHandle);
            if (_closeWatchdogHandle != null) _scheduler.Cancel(_closeWatchdogHandle);
            _rewardGraceHandle = null;
            _closeWatchdogHandle = null;

            _showing = null;
            if (_runtime != null)
            {
                _runtime.SetAudioPaused(false);
                _runtime.SetUiInputBlocked(false);
            }
            SetStatus(format, AdStatus.Loading);
            Raise(OnShowingAdChanged, false, nameof(OnShowingAdChanged));

            if (format == AdFormat.Rewarded)
            {
                var result = failed ? RewardedResult.DisplayFailed : (_rewardReceived ? RewardedResult.Rewarded : RewardedResult.NotRewarded);
                if (result == RewardedResult.Rewarded && _options.SkipNextInterstitialAfterRewarded) _skipNextInterstitial = true;
                Log($"Rewarded finished: {result}.");
                var callback = _rewardedCallback;
                _rewardedCallback = null;
                Invoke(callback, result);
            }
            else
            {
                if (!failed) _lastInterstitialTime = _scheduler.Now;
                var result = failed ? InterstitialResult.DisplayFailed : InterstitialResult.Closed;
                Log($"Interstitial finished: {result}.");
                var callback = _interstitialCallback;
                _interstitialCallback = null;
                Invoke(callback, result);
            }

            Load(format);
        }

        /// <summary>Al volver de segundo plano con un anuncio en pantalla: si el SDK no avisa del cierre, se libera.</summary>
        internal static void HandleApplicationPause(bool paused)
        {
            if (paused || !_showing.HasValue || _closeWatchdogHandle != null || _options == null) return;
            _closeWatchdogHandle = _scheduler.Delay(_options.CloseWatchdogSeconds, () =>
            {
                _closeWatchdogHandle = null;
                if (!_showing.HasValue) return;
                Debug.LogWarning($"{LOG_PREFIX} No close callback after resuming; forcing the end of the {_showing.Value} ad.");
                FinishFullscreen(_showing.Value, true);
            });
        }

        #endregion

        #region Avisos del proveedor

        private sealed class Listener : IAdsListener
        {
            public void OnSdkInitialized(AdsConsentInfo consent)
            {
                if (_provider == null) return;
                bool wasUnavailable = State == AdsState.Unavailable;
                FinishInitialization(AdsState.Ready, "ready");
                State = AdsState.Ready;
                if (consent != null) PublishConsent(consent);
                if (!wasUnavailable) RequestLoads(); // tras un timeout las cargas ya estaban pedidas
                else Log("SDK ready after the timeout.");
            }

            public void OnInitializationFailed(string reason)
            {
                if (_provider == null) return;
                FinishInitialization(AdsState.Unavailable, reason);
            }

            public void OnAdLoaded(AdFormat format)
            {
                if (!_formats.TryGetValue(format, out var state)) return;
                state.RetryAttempt = 0;
                if (state.Status != AdStatus.Showing) SetStatus(format, AdStatus.Ready);
                Log($"{format} loaded.");

                if (state.RetryHandle != null)
                {
                    _scheduler.Cancel(state.RetryHandle);
                    state.RetryHandle = null;
                }

                if (format == AdFormat.Rewarded && _waitHandle != null)
                {
                    string placement = _waitPlacement;
                    var callback = EndWait();
                    ShowRewarded(placement ?? string.Empty, callback);
                }
            }

            public void OnAdLoadFailed(AdFormat format, AdLoadError error, string message)
            {
                if (!_formats.TryGetValue(format, out var state) || state.Status == AdStatus.Disabled) return;
                state.RetryAttempt++;
                bool noInternet = error == AdLoadError.NoNetwork || !_hasInternet();
                if (state.Status != AdStatus.Showing) SetStatus(format, noInternet ? AdStatus.NoInternet : AdStatus.Loading);

                if (format == AdFormat.Banner) return; // el SDK refresca el banner solo

                float delay = (float)Math.Pow(2, Math.Min(_options.MaxRetryExponent, state.RetryAttempt));
                Log($"{format} load failed ({error}: {message}); retrying in {delay}s.");
                if (state.RetryHandle != null) _scheduler.Cancel(state.RetryHandle);
                state.RetryHandle = _scheduler.Delay(delay, () =>
                {
                    state.RetryHandle = null;
                    if (state.Status != AdStatus.Showing) Load(format);
                });
            }

            public void OnAdDisplayed(AdFormat format)
            {
                Log($"{format} displayed.");
            }

            public void OnAdDisplayFailed(AdFormat format, string message)
            {
                Debug.LogWarning($"{LOG_PREFIX} {format} display failed: {message}");
                if (_showing == format) FinishFullscreen(format, true);
                else Load(format);
            }

            public void OnAdHidden(AdFormat format)
            {
                if (_showing != format) return;
                if (format == AdFormat.Rewarded)
                {
                    // El reward puede llegar después del cierre en algunas redes: se le da un margen.
                    if (_rewardReceived || _options.RewardGraceSeconds <= 0f) FinishFullscreen(format, false);
                    else _rewardGraceHandle = _scheduler.Delay(_options.RewardGraceSeconds, () => FinishFullscreen(AdFormat.Rewarded, false));
                }
                else
                {
                    FinishFullscreen(format, false);
                }
            }

            public void OnRewardReceived()
            {
                _rewardReceived = true;
                if (_showing == AdFormat.Rewarded && _rewardGraceHandle != null) FinishFullscreen(AdFormat.Rewarded, false);
            }

            public void OnAdRevenue(AdRevenueInfo revenue)
            {
                if (revenue == null) return;
                // Cualificado: dentro de Listener, "OnAdRevenue" a secas es este método, no el evento del servicio.
                Raise(AdsService.OnAdRevenue, revenue, nameof(AdsService.OnAdRevenue));

                // Y por el core, para los módulos que atribuyen ingresos sin conocer este (el tracker de Singular de Viva Analytics).
                VivaAdRevenue.Publish(new AdRevenueEvent
                {
                    Platform = "AppLovin",
                    Format = revenue.FormatName ?? revenue.Format.ToString(),
                    Placement = revenue.Placement,
                    AdUnitId = revenue.AdUnitId,
                    NetworkName = revenue.NetworkName,
                    NetworkPlacement = revenue.NetworkPlacement,
                    CreativeId = revenue.CreativeId,
                    Revenue = revenue.Revenue,
                    RevenuePrecision = revenue.RevenuePrecision,
                    Currency = revenue.Currency ?? "USD"
                });
            }

            public void OnConsentChanged(AdsConsentInfo consent)
            {
                if (consent != null) PublishConsent(consent);
            }
        }

        private static void PublishConsent(AdsConsentInfo consent)
        {
            VivaConsent.Set(new ConsentState
            {
                IsGdpr = consent.IsGdpr,
                Purpose1 = consent.Purpose1,
                Purpose3 = consent.Purpose3,
                Purpose4 = consent.Purpose4,
                Purpose7 = consent.Purpose7,
                GoogleVendor = consent.GoogleVendor,
                Source = "AppLovin MAX"
            });
            Raise(OnConsentResolved, consent, nameof(OnConsentResolved));
        }

        #endregion

        #region Helpers

        private static bool IsEnabled(AdFormat format)
        {
            return _configuration != null && _configuration.IsEnabled(format);
        }

        private static bool TryResolve(AdFormat format, Enum placement, out string name)
        {
            name = null;
            var setup = _configuration?.Get(format);
            return setup != null && setup.TryResolvePlacement(placement, out name);
        }

        private static string Describe(Enum placement)
        {
            return placement == null ? "null" : $"{placement.GetType().Name}.{placement}";
        }

        private static void SetStatus(AdFormat format, AdStatus status)
        {
            if (!_formats.TryGetValue(format, out var state) || state.Status == status) return;
            state.Status = status;
            Raise(OnStatusChanged, format, status, nameof(OnStatusChanged));
        }

        private static bool Try(Action action, string what)
        {
            try
            {
                action();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG_PREFIX} {what} failed: {e}");
                return false;
            }
        }

        private static void Invoke<T>(Action<T> callback, T value)
        {
            if (callback == null) return;
            try
            {
                callback(value);
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG_PREFIX} A callback failed: {e}");
            }
        }

        private static void Raise(Action handlers, string name)
        {
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                try { ((Action)handler)(); }
                catch (Exception e) { Debug.LogError($"{LOG_PREFIX} {name} handler failed: {e}"); }
            }
        }

        private static void Raise<T>(Action<T> handlers, T value, string name)
        {
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                try { ((Action<T>)handler)(value); }
                catch (Exception e) { Debug.LogError($"{LOG_PREFIX} {name} handler failed: {e}"); }
            }
        }

        private static void Raise<T1, T2>(Action<T1, T2> handlers, T1 a, T2 b, string name)
        {
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                try { ((Action<T1, T2>)handler)(a, b); }
                catch (Exception e) { Debug.LogError($"{LOG_PREFIX} {name} handler failed: {e}"); }
            }
        }

        private static void Log(string message)
        {
            if (_options == null || _options.LogToConsole) Debug.Log($"{LOG_PREFIX} {message}");
        }

        /// <summary>Solo para tests: inyecta el planificador y la comprobación de red antes de Initialize.</summary>
        internal static void ConfigureForTests(IAdsScheduler scheduler, Func<bool> hasInternet)
        {
            _scheduler = scheduler;
            _hasInternet = hasInternet;
            _runtime = null;
        }

        /// <summary>Deja el servicio como recién cargado. Solo para tests.</summary>
        internal static void Reset()
        {
            _provider = null;
            _configuration = null;
            _options = null;
            _scheduler = null;
            _hasInternet = null;
            _runtime = null;
            _formats.Clear();
            _initialized = new ReadyGate("Ads");
            _initializationTimeoutHandle = null;
            _showing = null;
            _rewardedCallback = null;
            _interstitialCallback = null;
            _rewardReceived = false;
            _rewardGraceHandle = null;
            _closeWatchdogHandle = null;
            _waitHandle = null;
            _waitCallback = null;
            _waitPlacement = null;
            _lastInterstitialTime = float.NegativeInfinity;
            _skipNextInterstitial = false;
            _bannerVisible = false;
            State = AdsState.NotInitialized;
            ProviderFactory = null;
            OnInitialized = null;
            OnConsentResolved = null;
            OnAdRevenue = null;
            OnStatusChanged = null;
            OnShowingAdChanged = null;
            OnWaitingForRewarded = null;
        }

        #endregion
    }
}
