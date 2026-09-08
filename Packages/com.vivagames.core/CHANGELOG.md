# Changelog: Viva Core

Releases of this package are the git tags `core/vX.Y.Z`. Each Viva module has its own version and changelog.

## [2.2.0] - 2026-09-08

### Added
- Consent bridge between modules: `VivaConsent` and `ConsentState` (assembly `VivaGames.Core`). The module that owns the CMP publishes the IAB TCF state with `VivaConsent.Set` (Viva Ads does it after the AppLovin MAX consent flow) and the modules that need it subscribe with `VivaConsent.Subscribe`, which delivers the current state at once and every later change. Viva Analytics 2.2.0 subscribes on its own, so with both modules installed Google Consent Mode is applied without any project code. Unit tests included.

## [2.1.0] - 2026-09-08

### Added
- Shared Firebase initialization: `VivaFirebase` (assembly `VivaGames.Core.Firebase`, compiled only with the `VIVA_FIREBASE` define) runs `FirebaseApp.CheckAndFixDependenciesAsync` once for every Viva module. Modules call `VivaFirebase.EnsureInitialized()` and `VivaFirebase.WhenReady(onReady, onFailed)` instead of checking on their own, because the Firebase Unity SDK does not support two concurrent dependency checks. Any number of modules can initialize in any order and the check still runs exactly once.
- `ReadyGate` (assembly `VivaGames.Core`, no SDK needed): the generic once-only initialization gate behind `VivaFirebase`, with unit tests in `Tests/Editor`.
- `FirebaseAppDetector` keeps the `VIVA_FIREBASE` define in sync with the presence of `Firebase.App.dll`, and `SdkDetection` offers the same detection to the modules.
- Module requirements (`VivaModule.RequiredModules`): a module can need a minimum version of another module. While an installed module is older than required, the installer disables **Install** and **Update** for the module that needs it and says which one to update first. Viva Analytics 2.1.0 requires Viva Core 2.1.0.

## [2.0.0] - 2026-09-07

### Added
- First release as a Unity Package Manager package installed from the repository git URL.
- **Viva > Package Installer** window: installs, updates and removes the Viva modules from GitHub pinned to a release tag. Reads the published releases and branches with `git ls-remote` and keeps its operation queue across domain reloads.
- **Show development branches** toggle: each module gets its own branch selector with Install from, Switch to and Pull latest, independent of the other modules, plus Switch to the latest release once it is published.
- Automatic migration of `.unitypackage` installations: before installing a module, the folders left by the old version are backed up in `Library/VivaLegacyBackup` and removed, with confirmation.
- Shared editor utilities for the modules: `ScriptingDefines` (per-platform define management) and `VivaPackageUtility` (package version and path lookup).
