# Changelog: Viva Analytics

Releases of this package are the git tags `analytics/vX.Y.Z`. Each Viva module has its own version and changelog.

## [2.3.0] - 2026-09-08

### Added
- Event targets: every event goes to Firebase, and the ones you tick in the Event Editor (**Send to**: Facebook, Singular) also go to those trackers. `AnalyticsTargets`, `IRoutedAnalyticsEvent` and `AAnalyticsTracker.Targets` (Firebase by default, `All` in the console tracker) do the routing in `AnalyticsService.TrackEvent`; events generated with only Firebase do not change at all. The Event Manager shows the extra targets of each event and the catalog accepts `"targets": ["facebook", "singular"]`. Unit tests included.
- `FacebookAnalyticsTracker` (assembly `VivaGames.Analytics.Facebook`, compiled with `VIVA_FACEBOOK`, enabled by `FacebookSdkDetector` when `Facebook.Unity.dll` is found): Meta App Events with `FB.LogAppEvent`, SDK initialization if the project has not done it, `FB.ActivateApp` at start and on resume, tracking flags off until the consent allows them (`VivaConsent` published by Viva Ads, or `AnalyticsService.SetConsent` with the `AdStorage` signal), user id, queue until the SDK is ready (if `FB.Init` fails for a missing App ID the tracker logs it and the rest keep working). The Event Editor warns about the Facebook rules (names of 2 to 40 characters, 25 parameters, values of 100 characters); `FacebookEventConverter` applies them at runtime.
- `SingularAnalyticsTracker` (assembly `VivaGames.Analytics.Singular`, compiled with `VIVA_SINGULAR`, enabled by `SingularSdkDetector` when the `SingularSDK` assembly is found): custom events with `SingularSDK.Event`, custom user id, user properties as global properties, queue until the `SingularSDK` component of the scene initializes the SDK (in the editor, where the SDK never initializes, the tracker counts as ready and only logs). With the Setup toggle **Attribute the ad revenue of Viva Ads in Singular** (on by default, stored in the new `AnalyticsSettings` asset under `Resources`) every impression published by Viva Ads 1.1.0 in `VivaAdRevenue` is attributed with `SingularSDK.AdRevenue`, no project code; `AttributeAdRevenue` takes that value. The Event Editor warns when a name is longer than 32 characters; `SingularEventConverter` truncates attributes to 500 characters.
- `VivaUserId.GetOrCreate()`: a GUID created once and kept in PlayerPrefs; the generated `AnalyticsInit.cs` passes it to `SetUserId` so Firebase and Singular see the same player. `VivaUserId.Reset()` creates a new one.
- `AnalyticsRuntime`: hidden MonoBehaviour for the trackers (application pause and resume, waits).
- **Setup** shows the Facebook and Singular SDKs (not installed, detected, define enabled, App ID in `FacebookSettings.asset`, whether `AnalyticsInit.cs` creates the tracker) and the toggle that attributes the ad revenue of Viva Ads in Singular. **Run full setup** creates the settings asset.

### Changed
- The `AnalyticsInit.cs` template adds the Facebook and Singular trackers under their defines and sets the user id with `VivaUserId`. Existing projects keep their file: add the two `#if` blocks after `Initialize` (the README shows them; Setup says so when an SDK is found and the file does not create its tracker) or regenerate the file from Setup.
- Requires Viva Core 2.3.0 or newer (`VivaAdRevenue`).

## [2.2.0] - 2026-09-08

### Added
- Consent arrives on its own: `AnalyticsService` subscribes to `VivaConsent` (Viva Core 2.2.0) and applies every published state through `ConsentModeMapper` and `SetConsent`. With Viva Ads installed, the result of the AppLovin MAX consent flow reaches Firebase (Google Consent Mode) without any project code; a project with its own CMP can publish it with `VivaConsent.Set` or keep calling `AnalyticsService.SetConsent`. Requires Viva Core 2.2.0 or newer.

## [2.1.0] - 2026-09-08

### Changed
- With Viva Core 2.1.0 or newer, `FirebaseAnalyticsTracker` no longer calls `FirebaseApp.CheckAndFixDependenciesAsync` itself: it goes through `VivaFirebase`, which runs the check once for every Viva module, so other modules such as Viva Remote Config can initialize on their own without a second, concurrent check. Queueing, `IsFirebaseReady` and the `FirebaseReady` event behave as before; the console now shows `[Viva] Checking Firebase dependencies (requested by Viva Analytics)...` and `[Analytics] Firebase ready: sending N queued event(s).`
- With an older core (or until its detector defines `VIVA_FIREBASE`) the tracker keeps checking on its own, as in 2.0.0, so the project compiles in every combination. To use it together with other Viva modules that use Firebase, update Viva Core to 2.1.0 first; the installer asks for it.

## [2.0.0] - 2026-09-07

### Changed
- Distribution moves from a `.unitypackage` release to a Unity Package Manager package installed through **Viva > Package Installer**.
- Generated events and `AnalyticsInit.cs` live in the user's project (`Assets/VivaAnalytics/` by default), never inside the package.
- Common parameters are registered through `AnalyticsService.RegisterCommonParameter` instead of editing the Firebase tracker.
- The Firebase tracker is now `FirebaseAnalyticsTracker`, in its own assembly (`VivaGames.Analytics.Firebase`) compiled only when the SDK is present.
- Assembly `VivaAnalytics` is renamed to `VivaGames.Analytics` (same asmdef GUID, so GUID-based references keep working). The `Viva.Services.Analytics` namespace does not change.

### Added
- `AnalyticsService` accepts several trackers at once; every event goes to all of them.
- User properties and user id: `AnalyticsService.SetUserProperty` and `SetUserId`, forwarded to every tracker (virtual no-ops in `AAnalyticsTracker`, implemented by the Firebase and console trackers).
- Consent (Google Consent Mode): `ConsentSignal`, `TcfConsent` and `ConsentModeMapper` in `Viva.Services.Analytics.Consent`, plus `AnalyticsService.SetConsent` forwarded to every tracker. `FirebaseAnalyticsTracker` applies it through `FirebaseAnalytics.SetConsent`, queued until Firebase is ready. Unit tests in `Tests/Editor`.
- `AnalyticsService.GetTracker<T>()` to reach SDK-specific members of a tracker.
- `ConsoleAnalyticsTracker`, used when no SDK is available.
- `FirebaseAnalyticsTracker` waits for the Firebase dependency check, queues the events and user data received before Firebase is ready, and raises `FirebaseReady` on the main thread. The generated `AnalyticsInit.cs` has an `OnFirebaseReady()` hook for Crashlytics, consent and similar.
- **Viva > Analytics > Setup** window: project status, events folder settings, standard events import, `AnalyticsInit.cs` generation and migration from the `.unitypackage` version. On its first run in a project it offers to migrate the legacy scripts or to run the initial setup.
- The migration moves the common parameters of the old `FirebaseAnalytics.cs` into the new `AnalyticsInit.cs` (as comments to rewrite when the old code computed them with its own logic), keeps any other custom line of the old scripts as comments in `OnFirebaseReady()` and writes `Assets/VivaAnalytics/Legacy/MIGRATION_NOTES.txt`.
- Standard events catalog (`Templates~/EventCatalog.json`) shown in the Event Manager; each event can be added, modified, restored or deleted per project.
- Automatic `VIVA_FIREBASE_ANALYTICS` scripting define when `Firebase.Analytics.dll` is found, synced immediately in batch mode.
- Extra parameter types in the event editor: `long`, `float`, `bool`.

### Fixed
- `string` parameters fell through to the generic branch because of a duplicated `float` check.
- Deleting an event from the Event Manager left its `.meta` file behind.
