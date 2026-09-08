using UnityEngine;
using Viva.Services.Ads;
using Viva.Services.Analytics;

/// <summary>
/// Initializes the ads. Add this component to a GameObject in the first scene of the game.
/// This file belongs to your project: the Viva Ads package never overwrites it when updating.
/// </summary>
public class AdsInit : MonoBehaviour
{
    private static bool _initialized;

    private void Start()
    {
        if (_initialized) return;
        _initialized = true;

        // If another prompt must go first (notifications, a custom ATT screen), call Initialize when it finishes instead.
        // The order with AnalyticsInit does not matter. Formats and placements come from Viva > Ads > Ad Units.
        AdsService.Initialize(AdPlacements.Configuration(), new AdsOptions
        {
            InterstitialCooldownSeconds = 0f,            // seconds between interstitials (Arrows: 125, from Remote Config)
            // SkipNextInterstitialAfterRewarded = true, // the check right after a completed rewarded shows nothing
            // InterstitialCooldownStartsAtInitialize = true,
            // InitializationTimeoutSeconds = 30f,
            // TestDeviceAdvertisingIds = new[] { "your-test-device-gaid-or-idfa" },
        });

        // Consent: with Viva Analytics installed there is nothing to do, it receives the CMP result through Viva Core.
        // For SDKs outside the Viva modules (Facebook tracking flags, for instance):
        AdsService.OnConsentResolved += consent =>
        {
            // bool adStorage = consent.AdStorageGranted;
            // FB.Mobile.SetAdvertiserTrackingEnabled(adStorage); FB.Mobile.SetAdvertiserIDCollectionEnabled(adStorage);
        };

        AdsService.OnAdRevenue += ad =>
        {
            // Viva Analytics: the standard ad_impression event of the studio catalog.
            AdImpression.Track(adName: ad.Placement, adPlatform: "AppLovin", adSource: ad.NetworkName,
                adUnitName: ad.AdUnitId, adFormat: ad.FormatName, value: ad.Revenue, currency: ad.Currency);
            // Other MMPs, for example Singular:
            // SingularSDK.AdRevenue(new SingularAdData("AppLovin", ad.Currency, ad.Revenue).WithAdType(ad.FormatName)
            //     .WithAdUnitId(ad.AdUnitId).WithNetworkName(ad.NetworkName).WithAdPlacmentName(ad.Placement).WithPrecision(ad.RevenuePrecision));
        };

        AdsService.WhenInitialized(() =>
        {
            // AppLovin asks to initialize MMPs and other third-party SDKs here, after its consent flow. State says whether the SDK is Ready.
        });
    }
}
