# Viva Core

Base package of the Viva Unity Tools. It contains the **Viva > Package Installer** window, the editor utilities the other modules share, and a small runtime the modules cooperate through: a single Firebase dependency check every module waits on, and the consent bridge that carries the CMP result from Viva Ads to Viva Analytics. It needs no SDK.

## Installation

In Unity open **Window > Package Manager**, press **+** and choose **Install package from git URL**:

```
https://github.com/Viva-Games/Viva-Analytics-Tool.git?path=/Packages/com.vivagames.core#core/v2.3.0
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

## Shared consent

Viva modules never reference each other, so the consent obtained by the module that owns the CMP reaches the others through the core:

```csharp
using Viva.Core;

// The module (or your own CMP code) that knows the user's choice publishes it:
VivaConsent.Set(new ConsentState { IsGdpr = true, Purpose1 = true, Purpose3 = false, Purpose4 = false, Purpose7 = true, GoogleVendor = true, Source = "My CMP" });

// Whoever needs it subscribes; the callback runs at once if a state already exists, and again on every change:
VivaConsent.Subscribe(state => ApplyConsent(state));
```

`ConsentState` carries the IAB TCF purposes 1, 3, 4 and 7 and the Google vendor (755) as nullable booleans (null means no TCF data, as for users outside the EEA) plus `IsGdpr`. Viva Ads publishes it after the AppLovin MAX consent flow; Viva Analytics 2.2.0 subscribes on its own and translates it to Google Consent Mode. A project without Viva Ads can publish from its own CMP and get the same result.

## Shared ad revenue

The module that shows the ads publishes every paid impression, and whoever attributes it subscribes. It is a stream, not a state: a subscriber only receives the impressions published after it subscribes.

```csharp
using Viva.Core;

// Viva Ads 1.1.0 does this from the AppLovin MAX revenue callback:
VivaAdRevenue.Publish(new AdRevenueEvent { Platform = "AppLovin", Format = "REWARDED", Placement = "level_end", AdUnitId = "...", NetworkName = "AdMob", Revenue = 0.0123, RevenuePrecision = "exact", Currency = "USD" });

// Viva Analytics 2.3.0 does this from its Singular tracker:
VivaAdRevenue.Subscribe(impression => SingularSDK.AdRevenue(ToSingular(impression)));
```

`AdRevenueEvent` carries platform, format, placement, ad unit, network, network placement, creative, revenue, precision and currency. A subscriber that throws is logged and does not stop the others.

## For maintainers

- Each module lives in `Packages/<name>` of the repository and has its own `version`, `CHANGELOG.md` and release tags `<prefix>/vX.Y.Z`. The prefix is declared in `VivaModuleCatalog`.
- To release a module: bump its `version`, add the changelog entry, commit, tag (`analytics/v2.0.1`) and push the tag. The installer reads the tags with `git ls-remote`.
- To add a module: create its folder in `Packages/` with `package.json`, assemblies and `.meta` files, and add one line to `VivaModuleCatalog.Modules`. If it depends on an SDK, compile the integration in an assembly with a define constraint and enable the define from an editor script when the SDK is detected (`SdkDetection.SyncDefine`; see `FirebaseAppDetector` here and `FirebaseSdkDetector` in Viva Analytics). If it uses Firebase, reference `VivaGames.Core.Firebase` and go through `VivaFirebase`.
- If a module release needs a newer version of another module, declare it with `requiredModules` in its catalog line. The requirement only applies when that other module is installed.
- The Package Manager cannot declare dependencies between packages installed from git; that is why the installer exists. Keep `ScriptingDefines`, `VivaPackageUtility`, `ReadyGate`, `VivaFirebase` and `VivaConsent` backwards compatible, because a project may mix module versions.
