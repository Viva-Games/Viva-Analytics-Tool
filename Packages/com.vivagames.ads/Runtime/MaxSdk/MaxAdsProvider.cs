using System;
using UnityEngine;

namespace Viva.Services.Ads
{
    /// <summary>
    /// Proveedor sobre el plugin de AppLovin MAX (assembly MaxSdk.Scripts). Traduce las llamadas de AdsService
    /// a MaxSdk y los callbacks estáticos de MaxSdkCallbacks a IAdsListener. El flujo de consentimiento de MAX
    /// corre dentro de InitializeSdk y se lee al terminar con MaxSdkUtils.
    /// Compilado solo con VIVA_APPLOVIN_MAX, que el editor define cuando encuentra el assembly del plugin.
    /// </summary>
    public sealed class MaxAdsProvider : IAdsProvider
    {
        private const string LOG_PREFIX = "[Ads]";

        private IAdsListener _listener;
        private AdUnitConfiguration _configuration;
        private AdsOptions _options;
        private string _rewardedUnit;
        private string _interstitialUnit;
        private string _bannerUnit;

        public void Initialize(AdUnitConfiguration configuration, AdsOptions options, IAdsListener listener)
        {
            _configuration = configuration;
            _options = AdsOptions.OrDefault(options);
            _listener = listener;
            _rewardedUnit = configuration.IsEnabled(AdFormat.Rewarded) ? configuration.Rewarded.AdUnitId : null;
            _interstitialUnit = configuration.IsEnabled(AdFormat.Interstitial) ? configuration.Interstitial.AdUnitId : null;
            _bannerUnit = configuration.IsEnabled(AdFormat.Banner) ? configuration.Banner.AdUnitId : null;

            MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInitialized;

            if (_rewardedUnit != null)
            {
                MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedLoaded;
                MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedLoadFailed;
                MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedDisplayed;
                MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedDisplayFailed;
                MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedHidden;
                MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedReceived;
                MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedRevenuePaid;
            }

            if (_interstitialUnit != null)
            {
                MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoaded;
                MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailed;
                MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialDisplayed;
                MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialDisplayFailed;
                MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHidden;
                MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnInterstitialRevenuePaid;
            }

            if (_bannerUnit != null)
            {
                MaxSdkCallbacks.Banner.OnAdLoadedEvent += OnBannerLoaded;
                MaxSdkCallbacks.Banner.OnAdLoadFailedEvent += OnBannerLoadFailed;
                MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerRevenuePaid;
            }

            if (_options.TestDeviceAdvertisingIds != null && _options.TestDeviceAdvertisingIds.Length > 0)
            {
                MaxSdk.SetTestDeviceAdvertisingIdentifiers(_options.TestDeviceAdvertisingIds);
            }
            if (_options.ReturnAudioFocus)
            {
                MaxSdk.SetExtraParameter("return_audio_focus", "true");
            }

            MaxSdk.InitializeSdk(configuration.EnabledAdUnitIds());
        }

        #region Rewarded

        public void LoadRewarded()
        {
            if (_rewardedUnit != null) MaxSdk.LoadRewardedAd(_rewardedUnit);
        }

        public bool IsRewardedReady => _rewardedUnit != null && MaxSdk.IsRewardedAdReady(_rewardedUnit);

        public void ShowRewarded(string placement)
        {
            MaxSdk.ShowRewardedAd(_rewardedUnit, placement);
        }

        private void OnRewardedLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnitId == _rewardedUnit) _listener.OnAdLoaded(AdFormat.Rewarded);
        }

        private void OnRewardedLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo error)
        {
            if (adUnitId == _rewardedUnit) _listener.OnAdLoadFailed(AdFormat.Rewarded, Map(error), Describe(error));
        }

        private void OnRewardedDisplayed(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnitId == _rewardedUnit) _listener.OnAdDisplayed(AdFormat.Rewarded);
        }

        private void OnRewardedDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo error, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnitId == _rewardedUnit) _listener.OnAdDisplayFailed(AdFormat.Rewarded, Describe(error));
        }

        private void OnRewardedHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnitId == _rewardedUnit) _listener.OnAdHidden(AdFormat.Rewarded);
        }

        private void OnRewardedReceived(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnitId == _rewardedUnit) _listener.OnRewardReceived();
        }

        private void OnRewardedRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnitId == _rewardedUnit) _listener.OnAdRevenue(ToRevenue(AdFormat.Rewarded, adInfo));
        }

        #endregion

        #region Interstitial

        public void LoadInterstitial()
        {
            if (_interstitialUnit != null) MaxSdk.LoadInterstitial(_interstitialUnit);
        }

        public bool IsInterstitialReady => _interstitialUnit != null && MaxSdk.IsInterstitialReady(_interstitialUnit);

        public void ShowInterstitial(string placement)
        {
            MaxSdk.ShowInterstitial(_interstitialUnit, placement);
        }

        private void OnInterstitialLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnitId == _interstitialUnit) _listener.OnAdLoaded(AdFormat.Interstitial);
        }

        private void OnInterstitialLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo error)
        {
            if (adUnitId == _interstitialUnit) _listener.OnAdLoadFailed(AdFormat.Interstitial, Map(error), Describe(error));
        }

        private void OnInterstitialDisplayed(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnitId == _interstitialUnit) _listener.OnAdDisplayed(AdFormat.Interstitial);
        }

        private void OnInterstitialDisplayFailed(string adUnitId, MaxSdkBase.ErrorInfo error, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnitId == _interstitialUnit) _listener.OnAdDisplayFailed(AdFormat.Interstitial, Describe(error));
        }

        private void OnInterstitialHidden(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnitId == _interstitialUnit) _listener.OnAdHidden(AdFormat.Interstitial);
        }

        private void OnInterstitialRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnitId == _interstitialUnit) _listener.OnAdRevenue(ToRevenue(AdFormat.Interstitial, adInfo));
        }

        #endregion

        #region Banner

        public void CreateBanner()
        {
            if (_bannerUnit == null) return;
            var setup = _configuration.Banner;
            // CreateBanner carga el primer banner y arranca el auto-refresh; queda oculto hasta ShowBanner.
            var configuration = new MaxSdkBase.AdViewConfiguration(ToMax(setup.Position)) { IsAdaptive = setup.Adaptive };
            MaxSdk.CreateBanner(_bannerUnit, configuration);
            MaxSdk.SetBannerBackgroundColor(_bannerUnit, setup.BackgroundColor);
        }

        public void ShowBanner(string placement)
        {
            if (_bannerUnit == null) return;
            if (!string.IsNullOrEmpty(placement)) MaxSdk.SetBannerPlacement(_bannerUnit, placement);
            MaxSdk.ShowBanner(_bannerUnit);
        }

        public void HideBanner()
        {
            if (_bannerUnit != null) MaxSdk.HideBanner(_bannerUnit);
        }

        public void SetBannerPosition(BannerPosition position)
        {
            if (_bannerUnit != null) MaxSdk.UpdateBannerPosition(_bannerUnit, ToMax(position));
        }

        private void OnBannerLoaded(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnitId == _bannerUnit) _listener.OnAdLoaded(AdFormat.Banner);
        }

        private void OnBannerLoadFailed(string adUnitId, MaxSdkBase.ErrorInfo error)
        {
            if (adUnitId == _bannerUnit) _listener.OnAdLoadFailed(AdFormat.Banner, Map(error), Describe(error));
        }

        private void OnBannerRevenuePaid(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            if (adUnitId == _bannerUnit) _listener.OnAdRevenue(ToRevenue(AdFormat.Banner, adInfo));
        }

        private static MaxSdkBase.AdViewPosition ToMax(BannerPosition position)
        {
            switch (position)
            {
                case BannerPosition.TopLeft: return MaxSdkBase.AdViewPosition.TopLeft;
                case BannerPosition.TopCenter: return MaxSdkBase.AdViewPosition.TopCenter;
                case BannerPosition.TopRight: return MaxSdkBase.AdViewPosition.TopRight;
                case BannerPosition.Centered: return MaxSdkBase.AdViewPosition.Centered;
                case BannerPosition.CenterLeft: return MaxSdkBase.AdViewPosition.CenterLeft;
                case BannerPosition.CenterRight: return MaxSdkBase.AdViewPosition.CenterRight;
                case BannerPosition.BottomLeft: return MaxSdkBase.AdViewPosition.BottomLeft;
                case BannerPosition.BottomRight: return MaxSdkBase.AdViewPosition.BottomRight;
                default: return MaxSdkBase.AdViewPosition.BottomCenter;
            }
        }

        #endregion

        #region Consentimiento y utilidades

        public bool HasConsentDialog
        {
            get
            {
                try { return MaxSdk.CmpService.HasSupportedCmp; }
                catch (Exception) { return false; }
            }
        }

        public void ShowConsentDialog(Action<bool> onCompleted)
        {
            MaxSdk.CmpService.ShowCmpForExistingUser(error =>
            {
                bool ok = error == null;
                if (!ok) Debug.LogWarning($"{LOG_PREFIX} The consent dialog could not be shown: {error.Message}");
                else _listener.OnConsentChanged(ReadConsent(MaxSdk.GetSdkConfiguration()));
                onCompleted?.Invoke(ok);
            });
        }

        public void ShowMediationDebugger()
        {
            MaxSdk.ShowMediationDebugger();
        }

        private void OnSdkInitialized(MaxSdkBase.SdkConfiguration configuration)
        {
            _listener.OnSdkInitialized(ReadConsent(configuration));
        }

        /// <summary>Lee el resultado del flujo de consentimiento de MAX. Debe llamarse con el SDK ya inicializado.</summary>
        private static AdsConsentInfo ReadConsent(MaxSdkBase.SdkConfiguration configuration)
        {
            var info = new AdsConsentInfo();
            try
            {
                var geography = configuration != null ? configuration.ConsentFlowUserGeography : MaxSdkBase.ConsentFlowUserGeography.Unknown;
                info.Geography = geography.ToString();
                info.IsGdpr = geography == MaxSdkBase.ConsentFlowUserGeography.Gdpr;
                info.Purpose1 = MaxSdkUtils.GetPurposeConsentStatus(1);
                info.Purpose3 = MaxSdkUtils.GetPurposeConsentStatus(3);
                info.Purpose4 = MaxSdkUtils.GetPurposeConsentStatus(4);
                info.Purpose7 = MaxSdkUtils.GetPurposeConsentStatus(7);
                info.GoogleVendor = MaxSdkUtils.GetTcfConsentStatus(755);
#if UNITY_EDITOR || UNITY_IOS
                if (configuration != null) info.AppTrackingStatus = configuration.AppTrackingStatus.ToString();
#endif
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LOG_PREFIX} Could not read the consent state from MAX: {e.Message}");
            }
            return info;
        }

        private static AdLoadError Map(MaxSdkBase.ErrorInfo error)
        {
            if (error == null) return AdLoadError.Other;
            switch (error.Code)
            {
                case MaxSdkBase.ErrorCode.NoNetwork:
                case MaxSdkBase.ErrorCode.NetworkError:
                case MaxSdkBase.ErrorCode.NetworkTimeout:
                    return AdLoadError.NoNetwork;
                case MaxSdkBase.ErrorCode.NoFill:
                    return AdLoadError.NoFill;
                default:
                    return AdLoadError.Other;
            }
        }

        private static string Describe(MaxSdkBase.ErrorInfo error)
        {
            return error == null ? "unknown" : $"{error.Code} {error.Message}";
        }

        private static AdRevenueInfo ToRevenue(AdFormat format, MaxSdkBase.AdInfo info)
        {
            return new AdRevenueInfo
            {
                Format = format,
                FormatName = info?.AdFormat,
                Placement = info?.Placement ?? string.Empty,
                AdUnitId = info?.AdUnitIdentifier,
                NetworkName = info?.NetworkName,
                NetworkPlacement = info?.NetworkPlacement,
                CreativeId = info?.CreativeIdentifier,
                Revenue = info?.Revenue ?? 0d,
                RevenuePrecision = info?.RevenuePrecision,
                Currency = "USD"
            };
        }

        #endregion
    }

    /// <summary>Registra el proveedor de MAX en AdsService al cargar, antes de cualquier Awake.</summary>
    internal static class MaxAdsProviderRegistration
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            AdsService.ProviderFactory = () => new MaxAdsProvider();
        }
    }
}
