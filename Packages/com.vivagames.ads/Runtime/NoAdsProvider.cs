using System;
using UnityEngine;

namespace Viva.Services.Ads
{
    /// <summary>
    /// Proveedor sin SDK: el juego compila y corre, pero no hay anuncios. Se usa cuando el plugin de MAX no
    /// está en el proyecto (VIVA_APPLOVIN_MAX sin definir).
    /// </summary>
    public sealed class NoAdsProvider : IAdsProvider
    {
        private IAdsListener _listener;

        public void Initialize(AdUnitConfiguration configuration, AdsOptions options, IAdsListener listener)
        {
            _listener = listener;
            Debug.LogWarning("[Ads] AppLovin MAX SDK not found. Ads are disabled: every Show call reports NotInitialized.");
            listener.OnInitializationFailed("AppLovin MAX SDK not found");
        }

        public void LoadRewarded() => _listener?.OnAdLoadFailed(AdFormat.Rewarded, AdLoadError.Other, "no SDK");
        public bool IsRewardedReady => false;
        public void ShowRewarded(string placement) => _listener?.OnAdDisplayFailed(AdFormat.Rewarded, "no SDK");

        public void LoadInterstitial() => _listener?.OnAdLoadFailed(AdFormat.Interstitial, AdLoadError.Other, "no SDK");
        public bool IsInterstitialReady => false;
        public void ShowInterstitial(string placement) => _listener?.OnAdDisplayFailed(AdFormat.Interstitial, "no SDK");

        public void CreateBanner() => _listener?.OnAdLoadFailed(AdFormat.Banner, AdLoadError.Other, "no SDK");
        public void ShowBanner(string placement) { }
        public void HideBanner() { }
        public void SetBannerPosition(BannerPosition position) { }

        public bool HasConsentDialog => false;
        public void ShowConsentDialog(Action<bool> onCompleted) => onCompleted?.Invoke(false);

        public void ShowMediationDebugger() => Debug.LogWarning("[Ads] Mediation debugger needs the AppLovin MAX SDK.");
    }
}
