# Viva Core

Base package of the Viva Unity Tools. It contains the **Viva > Package Installer** window, the editor utilities the other modules share, and a small runtime shared by the modules that use Firebase: a single Firebase dependency check every module waits on. It needs no SDK.

## Installation

In Unity open **Window > Package Manager**, press **+** and choose **Install package from git URL**:

```
https://github.com/Viva-Games/Viva-Analytics-Tool.git?path=/Packages/com.vivagames.core#core/v2.1.0
```

Git 2.14 or newer must be in the `PATH`.

## Package Installer

**Viva > Package Installer** lists every Viva module and what is installed:

- **Install** adds a module at its latest release. **Update** appears when a newer release exists; **Update all** updates every outdated module, the core last. **Remove** takes a module out of the project.
- A module can need a minimum version of another module (Viva Analytics 2.1.0 needs Viva Core 2.1.0). While an installed module is older than that, **Install** and **Update** are disabled for the module that needs it and the window says which one to update first. Modules that are not installed never block anything.
- If the project had the old `.unitypackage` version of a module, the installer asks for confirmation, backs up the old folders in `Library/VivaLegacyBackup` and removes them before installing.
- **Show development branches** adds a branch selector per module, with **Install from**, **Switch to** and **Pull latest**, to try unreleased work on one module without touching the others. Once a release exists, **Switch to** pins the module back to it.

Every install or update writes to `Packages/manifest.json` and `packages-lock.json`: commit both. The lock file pins the exact commit, so the whole team gets the same version.

## Shared Firebase initialization

The Firebase Unity SDK does not support two `FirebaseApp.CheckAndFixDependenciesAsync` calls running at the same time: the second one fails with `Don't call Firebase functions before CheckDependencies has finished`. Every Viva module is independent and initializes on its own, so the check lives in the core and runs exactly once, whatever modules are installed and whatever order they initialize in:

```csharp
using Viva.Core;

VivaFirebase.EnsureInitialized("My module");              // starts the check the first time, does nothing afterwards
VivaFirebase.WhenReady(OnFirebaseReady, OnFirebaseFailed); // main thread; runs at once if Firebase is already ready
```

`VivaFirebase` exists only when `Firebase.App.dll` is in the project: `FirebaseAppDetector` keeps the `VIVA_FIREBASE` define in sync. Module code that uses Firebase must never call `CheckAndFixDependenciesAsync` on its own when `VIVA_FIREBASE` is defined; a module that also supports older cores keeps its own check behind `#if !VIVA_FIREBASE`, as Viva Analytics does, so the project compiles in every combination and the define can be applied. The console shows who started the check (`[Viva] Checking Firebase dependencies (requested by Viva Analytics)...`) and when it finished (`[Viva] Firebase ready to use.`).

`ReadyGate` (assembly `VivaGames.Core`, no SDK needed) is the generic once-only gate behind `VivaFirebase`: `TryStart()` tells the first caller to do the initialization, `WhenReady()` queues or runs callbacks, `SetReady()` / `SetFailed()` release them. It can gate any other SDK the same way.

## For maintainers

- Each module lives in `Packages/<name>` of the repository and has its own `version`, `CHANGELOG.md` and release tags `<prefix>/vX.Y.Z`. The prefix is declared in `VivaModuleCatalog`.
- To release a module: bump its `version`, add the changelog entry, commit, tag (`analytics/v2.0.1`) and push the tag. The installer reads the tags with `git ls-remote`.
- To add a module: create its folder in `Packages/` with `package.json`, assemblies and `.meta` files, and add one line to `VivaModuleCatalog.Modules`. If it depends on an SDK, compile the integration in an assembly with a define constraint and enable the define from an editor script when the SDK is detected (`SdkDetection.SyncDefine`; see `FirebaseAppDetector` here and `FirebaseSdkDetector` in Viva Analytics). If it uses Firebase, reference `VivaGames.Core.Firebase` and go through `VivaFirebase`.
- If a module release needs a newer version of another module, declare it with `requiredModules` in its catalog line. The requirement only applies when that other module is installed.
- The Package Manager cannot declare dependencies between packages installed from git; that is why the installer exists. Keep `ScriptingDefines`, `VivaPackageUtility`, `ReadyGate` and `VivaFirebase` backwards compatible, because a project may mix module versions.
