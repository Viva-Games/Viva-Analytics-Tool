# Changelog

All notable changes to the Viva Unity packages. Both packages share the same version number and git tag.

## [2.0.0] - 2026-09-07

### Changed
- Distribution moves from a `.unitypackage` release to Unity Package Manager packages installed from this repository's git URL.
- The repository is now the Unity development project. The packages live in `Packages/` (`com.vivagames.core` and `com.vivagames.analytics`).
- Generated events and `AnalyticsInit.cs` live in the user's project (`Assets/VivaAnalytics/` by default), never inside the package.
- Common parameters are registered through `AnalyticsService.RegisterCommonParameter` instead of editing the Firebase tracker.
- The Firebase tracker is now `FirebaseAnalyticsTracker`, in its own assembly (`VivaGames.Analytics.Firebase`) compiled only when the SDK is present.
- Assembly `VivaAnalytics` is renamed to `VivaGames.Analytics` (same asmdef GUID, so GUID-based references keep working). The `Viva.Services.Analytics` namespace does not change.

### Added
- `com.vivagames.core`: **Viva > Package Installer** window to install, update and remove modules from GitHub, with release tag detection through `git ls-remote`. When the core is installed from a branch, modules are installed from the same branch.
- The installer migrates `.unitypackage` installations automatically: with confirmation, it backs up the old `Runtime` and `Editor` folders into `Library/VivaLegacyBackup` and removes them before installing the package.
- On its first run Viva Analytics offers to migrate the legacy scripts or to run the initial setup.
- `AnalyticsService` accepts several trackers at once; every event goes to all of them.
- `ConsoleAnalyticsTracker`, used when no SDK is available.
- `FirebaseAnalyticsTracker` waits for the Firebase dependency check and queues the events received before Firebase is ready.
- **Viva > Analytics > Setup** window: project status, events folder settings, standard events import, `AnalyticsInit.cs` generation and migration from the `.unitypackage` version.
- Standard events catalog (`Templates~/EventCatalog.json`) shown in the Event Manager; each event can be added, modified, restored or deleted per project.
- Automatic `VIVA_FIREBASE_ANALYTICS` scripting define when `Firebase.Analytics.dll` is found.
- Extra parameter types in the event editor: `long`, `float`, `bool`.

### Fixed
- `string` parameters fell through to the generic branch because of a duplicated `float` check.
- Deleting an event from the Event Manager left its `.meta` file behind.
