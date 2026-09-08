# Viva Ads

AppLovin MAX for Viva Games projects with the least possible code in the game: declare the formats and placements once in an editor window, put one component in the first scene, and call one method per ad.

```csharp
AdsService.ShowRewarded(RewardedPlacement.Continue, result => { if (result == RewardedResult.Rewarded) Continue(); });
if (!AdsService.TryShowInterstitial(InterstitialPlacement.LevelStart, StartLevel)) StartLevel();
AdsService.ShowBanner(BannerPlacement.MainMenu);
```

Initialization, the consent flow, loading and reloading, retries, what happens while an ad is on screen and what to do when there is no ad are handled by the module.

## Installation

1. Install the [AppLovin MAX Unity plugin](https://support.applovin.com/en/max/unity/overview/integration/), either through the Package Manager registry (`com.applovin.mediation.ads`) or the `.unitypackage`. Both work: the module detects the `MaxSdk.Scripts` assembly and enables the `VIVA_APPLOVIN_MAX` define by itself. Until the plugin is there, the game compiles and every ad call reports `NotInitialized`.
2. In **AppLovin > Integration Manager** enter your SDK key, install the mediation adapters you use and enable the **MAX Terms and Privacy Policy Flow** with your privacy policy URL. That flow is the CMP (Google UMP in GDPR regions, ATT on iOS). Setup checks these three points and says what is missing.
3. Install [Viva Core](../com.vivagames.core/README.md) 2.3.0 or newer and, in **Viva > Package Installer**, press **Install** next to Viva Ads. If Viva Analytics is installed it must be 2.2.0 or newer; the installer says so.
4. On its first run the package offers the initial setup: it creates `Assets/VivaAds/AdUnits.json`, generates `AdPlacements.cs` and `AdsInit.cs`. You can run it later from **Viva > Ads > Setup**.
5. Add the `AdsInit` component to a GameObject in the first scene. The order with `AnalyticsInit` does not matter.

## Documentation

### Declaring formats and placements

**Viva > Ads > Ad Units** has one section per format. **Add rewarded**, **Add interstitial** or **Add banner** enables a format; each one takes:

- **Android ad unit id** and **iOS ad unit id**, from the MAX dashboard. One per format and platform; a missing platform only disables the format there.
- **Placements**: the names reported to MAX for each place the ad is shown (`hint`, `continue`, `level_start`...). They become the members of the generated enum, so `daily_recharge` is `RewardedPlacement.DailyRecharge`. Letters, digits and underscores; unique within the format.
- Banner only: **Position**, **Adaptive** (the height MAX recommends for the device) and **Background color**.

**Save** validates, writes the JSON (the source of truth, commit it) and regenerates `AdPlacements.cs`:

```csharp
public enum RewardedPlacement { Hint = 0, Continue = 1, DailyRecharge = 2 }
public enum InterstitialPlacement { LevelStart = 0 }
public enum BannerPlacement { MainMenu = 0 }

public static class AdPlacements
{
    public static AdUnitConfiguration Configuration() => ...; // what AdsInit passes to AdsService.Initialize
}
```

Only the enabled formats get an enum. Do not edit the file: it is regenerated on every save.

### Initialization

`AdsInit.cs` belongs to your project and updates never overwrite it. Its `Start` calls `AdsService.Initialize(AdPlacements.Configuration(), options)`. If another prompt must go first (a notifications permission, for instance), call `Initialize` when it finishes.

What happens then, with the plugin present:

1. The SDK initializes and runs its consent flow (UMP, ATT). Without network the module does not wait; otherwise it waits up to `InitializationTimeoutSeconds`.
2. `State` becomes `Ready` (or `Unavailable` after the timeout; loads still retry if the SDK recovers), `OnInitialized` fires once and `WhenInitialized` callbacks run.
3. The consent result is published (see [Consent](#consent)).
4. Every enabled format starts loading. A failed load retries after 2, 4, 8... up to 64 seconds, as AppLovin recommends; the banner is created hidden and MAX refreshes it on its own.

| `AdsOptions` | Default | Meaning |
|---|---|---|
| `InitializationTimeoutSeconds` | 30 | Wait for the SDK (consent flow included) before continuing as `Unavailable`. Not applied without network. |
| `PauseAudioWhileShowing` | true | `AudioListener.pause` while a rewarded or interstitial is on screen. |
| `BlockUiInputWhileShowing` | true | Disables the `EventSystem` while an ad is on screen, so no button reacts before the native overlay covers the screen. |
| `RewardGraceSeconds` | 0.5 | Time to wait after a rewarded closes in case the reward callback arrives late (some networks do that). |
| `CloseWatchdogSeconds` | 1 | After the app resumes with an ad on screen, if the SDK does not report the close in this time the ad is ended as `DisplayFailed`. |
| `MaxRetryExponent` | 6 | Retry delay is 2^n seconds, capped at 2^6 = 64. |
| `InterstitialCooldownSeconds` | 0 | Minimum seconds between two interstitials shown through `TryShowInterstitial`. 0 = none. |
| `InterstitialCooldownStartsAtInitialize` | true | The cooldown starts counting at `Initialize`; with false, the first interstitial can show as soon as one is loaded. |
| `SkipNextInterstitialAfterRewarded` | true | After a completed rewarded, the next `TryShowInterstitial` shows nothing and consumes the mark. |
| `InterstitialsEnabled` | true | Debug toggle: with false, `TryShowInterstitial` never shows. Rewarded ads are not affected. |
| `ReturnAudioFocus` | true | Android: ask the SDK to return the audio focus when an ad closes. |
| `TestDeviceAdvertisingIds` | null | Advertising ids of your test devices, so MAX serves test ads on them. |
| `LogToConsole` | true | Log what the service does and why. |

### Rewarded

```csharp
AdsService.ShowRewarded(RewardedPlacement.Hint, result =>
{
    if (result == RewardedResult.Rewarded) ShowHint();
});
```

`onResult` always arrives: at once, with the reason, if the ad cannot be shown, or when the ad closes.

| `RewardedResult` | Meaning |
|---|---|
| `Rewarded` | The player watched the ad: give the reward. |
| `NotRewarded` | Closed without reward (skipped). |
| `DisplayFailed` | The SDK could not show it, or the app resumed without a close callback. |
| `NotReady` | No ad loaded right now. |
| `NoInternet` | No ad and no connection. |
| `NotInitialized` | The SDK is not initialized or not available. |
| `AlreadyShowing` | Another ad is on screen (or a previous `ShowRewarded` is still waiting for a load). |
| `InvalidPlacement` | The placement is not a member of the generated `RewardedPlacement`, or the format is not enabled. |

The third parameter, `waitForLoadSeconds`, covers the pattern "tap, wait a few seconds, then give up": if the ad is still loading and there is network, `ShowRewarded` waits up to that time (`OnWaitingForRewarded` fires `true` and `false`, for a spinner) and shows the ad as soon as it loads.

### Knowing whether an ad is available

The module keeps a status per format, derived from the SDK callbacks and its error codes, with no polling:

| `AdStatus` | Meaning |
|---|---|
| `Disabled` | The format is not enabled in the window. |
| `NotInitialized` | Before `Initialize`, or the SDK is not ready. |
| `NoInternet` | No ad and the last load failed for a network reason (or the device has no connection). |
| `Loading` | No ad yet: loading or retrying for another reason (no fill). |
| `Ready` | An ad is loaded. |
| `Showing` | The ad is on screen. |

`AdsService.RewardedStatus`, `InterstitialStatus` and `BannerStatus` give the current value; `AdsService.OnStatusChanged(format, status)` fires on every change. `IsRewardedReady`, `IsInterstitialReady`, `IsShowingAd` and `OnShowingAdChanged` are shortcuts.

### Rewarded Ad Button

**Viva > Ads > Rewarded Ad Button** (component menu) goes on any `Button` and needs no code for the common case:

| Field | What it does |
|---|---|
| Placement | Dropdown with the rewarded placements of the window. |
| When unavailable | `DisableButton`: the button is not interactable while there is no ad. `KeepInteractable`: the button stays active and pressing it without an ad raises `OnUnavailable(status)`, for a popup. |
| Wait for load seconds | Passed to `ShowRewarded`. |
| Shown when ready / loading / no internet | Optional objects toggled with the status (a video icon, a spinner, a "no connection" label). |
| On Rewarded / On Not Rewarded / On Unavailable | UnityEvents; also available as C# events `Rewarded`, `NotRewarded`, `Unavailable`. |

The button disables itself while the ad shows or while it waits for a load.

### Interstitial

```csharp
// What depends on the game (minimum level, tutorial, first session) is decided before calling.
bool eligible = level > RemoteConfigParameters.StartInterstitialsLevel;
if (!eligible || !AdsService.TryShowInterstitial(InterstitialPlacement.LevelStart, StartLevel))
    StartLevel();
```

`TryShowInterstitial(placement, onClosed)` returns `true` only when it launches the ad; then `onClosed` runs when the ad closes (or fails to display), never earlier. With `false` nothing is called: continue at once. It returns `false` when:

- `InterstitialsEnabled` is false.
- `InterstitialCooldownSeconds` have not passed since the last interstitial (`InterstitialCooldownRemaining` says how long is left; `ResetInterstitialCooldown` restarts it).
- `SkipNextInterstitialAfterRewarded` is on and the player completed a rewarded since the last check. The mark is consumed by that check whatever its outcome. `SkipNextInterstitial()` sets it by hand, for a reward given another way.
- No ad is loaded, or another ad is on screen.

`CanShowInterstitial` answers the same question without showing. `ShowInterstitial(placement, onResult)` ignores the policy and reports an `InterstitialResult` (`Closed`, `DisplayFailed`, `NotReady`, `NoInternet`, `NotInitialized`, `AlreadyShowing`, `InvalidPlacement`).

### Banner

```csharp
AdsService.ShowBanner(BannerPlacement.MainMenu);
AdsService.HideBanner();
AdsService.SetBannerPosition(BannerPosition.TopCenter);
```

The banner is created hidden at initialization with the position, adaptive size and background color of the window, MAX loads and refreshes it, and `IsBannerVisible` says whether it is shown. Its revenue arrives through `OnAdRevenue` like the other formats.

### Consent

The MAX consent flow runs inside the SDK initialization. When it ends, the module reads the IAB TCF result (purposes 1, 3, 4, 7 and the Google vendor) and:

- Publishes it in `VivaConsent` (Viva Core). **Viva Analytics 2.2.0 subscribes on its own** and applies Google Consent Mode to Firebase: with both modules installed there is nothing to write. The console shows `[Viva] Consent resolved: ...` and `[Analytics] Consent Mode applied from AppLovin MAX.`
- Raises `AdsService.OnConsentResolved(AdsConsentInfo)` for SDKs outside the Viva modules. `AdsInit.cs` shows the Facebook flags as an example; `AdsConsentInfo.AdStorageGranted` is the usual gate.

`AdsService.ShowConsentDialog(onCompleted)` reopens the CMP from a settings menu (`HasConsentDialog` says whether it can); the new choice is published the same way. `ShowMediationDebugger()` opens the MAX debugger.

### Ad revenue

Every impression with revenue raises `AdsService.OnAdRevenue(AdRevenueInfo)`: format, placement, ad unit, network, creative, revenue, precision and currency. The same impression is published in `VivaAdRevenue` (Viva Core 2.3.0), so Viva Analytics 2.3.0 attributes it in Singular on its own (toggle **Attribute the ad revenue of Viva Ads in Singular** in Viva > Analytics > Setup, on by default), no code needed. With Viva Analytics installed and the standard `ad_impression` event imported, the generated `AdsInit.cs` already tracks it.

### Without the plugin

If the `MaxSdk.Scripts` assembly is not in the project, `Initialize` uses a provider that reports every format as `Disabled` and every call as `NotInitialized`, with one warning in the console. In the Unity editor the MAX plugin shows test ads, so the whole flow can be tried in Play Mode.

### API reference

| Member | Description |
|---|---|
| `AdsService.Initialize(configuration, options)` | Starts the SDK. Second call ignored. |
| `AdsService.State`, `IsReady`, `IsInitialized`, `IsShowingAd` | SDK state (`NotInitialized`, `Initializing`, `Ready`, `Unavailable`) and shortcuts. |
| `AdsService.OnInitialized`, `WhenInitialized(callback)` | Once, when initialization ends, whatever the outcome. |
| `AdsService.ShowRewarded(placement, onResult, waitForLoadSeconds = 0)` | Shows a rewarded; `placement` is a `RewardedPlacement` value or a string. |
| `AdsService.RewardedStatus`, `IsRewardedReady`, `OnWaitingForRewarded` | Rewarded status, shortcut and wait-for-load signal. |
| `AdsService.TryShowInterstitial(placement, onClosed = null)` | Shows an interstitial if the policy allows; `true` only when launched. |
| `AdsService.ShowInterstitial(placement, onResult = null)` | Shows an interstitial ignoring the policy. |
| `AdsService.CanShowInterstitial`, `InterstitialStatus`, `IsInterstitialReady`, `InterstitialCooldownRemaining` | Interstitial state and policy. |
| `AdsService.ResetInterstitialCooldown()`, `SkipNextInterstitial()` | Policy controls for the game. |
| `AdsService.ShowBanner(placement)`, `HideBanner()`, `SetBannerPosition(position)`, `BannerStatus`, `IsBannerVisible` | Banner. |
| `AdsService.OnStatusChanged`, `OnShowingAdChanged` | Status events. |
| `AdsService.OnConsentResolved`, `HasConsentDialog`, `ShowConsentDialog(onCompleted)` | Consent. |
| `AdsService.OnAdRevenue` | Ad revenue. |
| `AdsService.ShowMediationDebugger()` | MAX mediation debugger. |
| `AdsService.StatusOf(format)` | Status of any format. |
| `RewardedAdButton` | Ready-made rewarded button component. |
| `AdPlacements.Configuration()`, `RewardedPlacement`, `InterstitialPlacement`, `BannerPlacement` | Generated from the window. |

### Requirements

- Viva Core 2.3.0 or newer (`ReadyGate`, `VivaConsent`, `VivaAdRevenue`).
- Viva Analytics 2.2.0 or newer if it is installed (it receives the consent through the core).
- AppLovin MAX Unity plugin 8.x (the `MaxSdk.Scripts` assembly; older plugins without it cannot be referenced). Unity 2021.3 or newer.
