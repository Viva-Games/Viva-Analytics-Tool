# Changelog: Viva Analytics

Releases of this package are the git tags `analytics/vX.Y.Z`. Each Viva module has its own version and changelog.

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
