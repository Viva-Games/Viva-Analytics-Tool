# Changelog: Viva Ads

Releases of this package are the git tags `ads/vX.Y.Z`. Each Viva module has its own version and changelog.

## [1.1.0] - 2026-09-08

### Added
- Every impression with revenue is also published in `VivaAdRevenue` (Viva Core 2.3.0) as an `AdRevenueEvent`, so Viva Analytics 2.3.0 attributes it in Singular without project code. `AdsService.OnAdRevenue` and the `ad_impression` event of the generated `AdsInit.cs` behave as before.

### Changed
- Requires Viva Core 2.3.0 or newer. The `AdsInit.cs` template no longer shows a Singular example in the revenue hook: the attribution is automatic when Viva Analytics has the Singular tracker.

## [1.0.0] - 2026-09-08

### Added
- `AdsService`: single entry point for AppLovin MAX. Initialization with timeout and no-network fast path, preload and reload of every format, exponential retry (up to 64 s, as AppLovin recommends), one ad at a time, audio paused and UI input blocked while an ad shows, half a second of grace for late rewards, a watchdog for ads that never report their close after the app resumes, and a status per format (`Disabled`, `NotInitialized`, `NoInternet`, `Loading`, `Ready`, `Showing`) derived from the SDK callbacks and error codes.
- Rewarded: `ShowRewarded(placement, onResult, waitForLoadSeconds)` with a result that always says why (`Rewarded`, `NotRewarded`, `DisplayFailed`, `NotReady`, `NoInternet`, `NotInitialized`, `AlreadyShowing`, `InvalidPlacement`) and an optional wait for the ad to load.
- Interstitial: `TryShowInterstitial(placement, onClosed)` with the policy that is the same in every game (cooldown between interstitials, skip the next one after a completed rewarded, debug toggle); `ShowInterstitial` without policy.
- Banner: `ShowBanner`, `HideBanner`, `SetBannerPosition`, adaptive size and background color from the window.
- `RewardedAdButton` component: reflects the ad status on a button and its indicators, disables it or reports why when there is no ad, shows the rewarded and raises `OnRewarded` / `OnNotRewarded` / `OnUnavailable`.
- Consent: the result of the MAX consent flow is published in `VivaConsent` (Viva Core 2.2.0), so Viva Analytics 2.2.0 applies Google Consent Mode without project code; `OnConsentResolved` for other SDKs; `ShowConsentDialog` to reopen the CMP.
- Ad revenue: `OnAdRevenue` with the data MAX reports; the generated `AdsInit.cs` tracks the `ad_impression` event when Viva Analytics is installed.
- **Viva > Ads > Ad Units**: add the formats the game uses, their Android and iOS ad units and their placements; Save writes `Assets/VivaAds/AdUnits.json` and generates `AdPlacements.cs` with one enum per format.
- **Viva > Ads > Setup**: project status, MAX plugin detection (Package Manager or `.unitypackage`), Integration Manager checks (SDK key, consent flow, adapters), `AdsInit.cs` generation and first-run setup.
- `MaxAdsProvider` compiled only with `VIVA_APPLOVIN_MAX`, enabled automatically when the `MaxSdk.Scripts` assembly is found; without the plugin the game compiles and every call reports `NotInitialized`.
- Unit tests for the state machine, the interstitial policy, the validator, the generator and the file.
