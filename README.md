# Viva Unity Tools

Unity packages shared across Viva Games projects. This repository is at the same time the Unity development project and the source of the packages, which live in [`Packages/`](Packages/):

| Package | Menu | What it does |
|---|---|---|
| `com.vivagames.core` | Viva > Package Installer | Installs, updates and removes the other modules from GitHub. Required by every module. |
| `com.vivagames.analytics` | Viva > Analytics | Analytics event creation tool and Firebase Analytics integration. |

The packages are integrators: they do not install third-party SDKs. Each project imports the SDKs it needs (for example the Firebase Analytics SDK) and the packages detect them.

## Requirements

- Unity 2021.3 or newer.
- Git 2.14 or newer installed and available in the `PATH`. The Package Manager uses it to download packages from a URL.
- For Viva Analytics: the [Firebase Analytics SDK for Unity](https://firebase.google.com/docs/analytics/unity/start) imported in the project. It can be imported before or after the package.

## Installation

1. In Unity open **Window > Package Manager**, press **+** and choose **Install package from git URL**. Paste:

   ```
   https://github.com/Viva-Games/Viva-Analytics-Tool.git?path=/Packages/com.vivagames.core#v2.0.0
   ```

   Replace `v2.0.0` with the release you want. Releases are the repository tags with that format.

2. Open **Viva > Package Installer** and press **Install** next to each module you need. The window reads the release tags from GitHub and installs every module at the same version as the core.

3. On its first run Viva Analytics offers to run the initial setup: it creates the events folder, imports the standard events and generates `AnalyticsInit.cs`. You can also run it later from **Viva > Analytics > Setup** with **Run full setup**.

Every install or update writes to `Packages/manifest.json` and `Packages/packages-lock.json`. Commit both files so the whole team uses the same versions.

## Updating

Open **Viva > Package Installer** and press **Check for updates**. Modules with a newer release show an **Update** button; **Update all** updates every module at once, the core last.

Versions are pinned to a git tag. Nothing updates on its own, and the lock file stores the exact commit, so every team member gets the same version after pulling the project.

You can also update through the Package Manager: **Install package from git URL** with the same URL and a new tag replaces the installed version.

## Viva Analytics

### Setup window (Viva > Analytics > Setup)

Shows the state of the project and fixes whatever is missing:

- **Events folder**: where the event classes are generated. Default `Assets/VivaAnalytics/Events`.
- **AnalyticsInit.cs**: the initialization script of your project. Default `Assets/VivaAnalytics/AnalyticsInit.cs`.
- **Firebase Analytics SDK**: detected automatically. When `Firebase.Analytics.dll` is in the project the package defines `VIVA_FIREBASE_ANALYTICS`, which enables the Firebase tracker.
- **Standard events**: how many events of the studio catalog are in the project.

Both paths can be changed in the window and are stored in `ProjectSettings/VivaAnalyticsSettings.json`.

### Initialization

`AnalyticsInit.cs` belongs to your project. The package never overwrites it during updates. Add the `AnalyticsInit` component to a GameObject in the first scene of the game. It initializes the service with the Firebase tracker (or with a console tracker when Firebase is not installed) and registers the common parameters.

### Common parameters

Common parameters travel with every event. They are registered with a function that is evaluated each time an event is tracked, so they can point to values that change during the game:

```csharp
private void RegisterCommonParameters()
{
    AnalyticsService.RegisterCommonParameter("player_level", () => SaveManager.Data.Level);
    AnalyticsService.RegisterCommonParameter("hours_played", () => PlayerPrefs.GetFloat("hours_played"));
    AnalyticsService.SetCommonParameter("build_type", Debug.isDebugBuild ? "debug" : "release");
}
```

If an event defines a parameter with the same key, the value of the event wins.

### Events (Viva > Analytics > Event Manager)

- The window lists the events of the events folder. Each one can be edited or deleted.
- **New Event** asks for a snake_case name, creates the class and opens the editor to add parameters. Supported types: `int`, `long`, `float`, `double`, `bool`, `string`.
- **Standard events not in this project** lists the studio catalog events that are missing, with an **Add** button. Standard events already in the project are marked as `standard`; if their file differs from the catalog they show `(modified)` and a **Restore** button.
- Deleting a standard event is safe: it stays available in the catalog.

To log an event call its static method from your code:

```csharp
FtueLandmark.Track(1, "First step of the tutorial");
AdImpression.Track(adName, adPlatform, adSource, adUnitName, adFormat, value, currency);
```

### Several trackers

`AnalyticsService.Initialize` accepts any number of trackers and every event is sent to all of them. New trackers (Singular, Facebook...) extend `AAnalyticsTracker`:

```csharp
AnalyticsService.Initialize(new FirebaseAnalyticsTracker(), new ConsoleAnalyticsTracker());
AnalyticsService.AddTracker(new MyOtherTracker()); // at any later time
```

## Migrating from the .unitypackage version (1.x)

The namespace `Viva.Services.Analytics` and the `AAnalyticsTracker` API do not change, so existing events and `.Track()` calls keep compiling. The migration is automatic:

1. Install the core package from its git URL (see [Installation](#installation)). It does not collide with the old code.
2. Open **Viva > Package Installer** and press **Install** next to Viva Analytics. The installer detects the old installation, asks for confirmation, backs up `Assets/VivaAnalytics/Runtime` and `Assets/VivaAnalytics/Editor` into `Library/VivaLegacyBackup` and removes them. `Events` and `Scripts` are kept. Unity shows compile errors for a moment until the package finishes importing.
3. On its first run the package detects `Scripts/FirebaseAnalytics.cs` and `Scripts/AnalyticsInit.cs` and offers to migrate them: both are backed up as `.txt` in `Assets/VivaAnalytics/Legacy`, `AnalyticsInit.cs` moves to its new location keeping its GUID (scene references survive) and gets the new template, and the old scripts are deleted. You can answer **Later** and do it from **Viva > Analytics > Setup** whenever you want; the old scripts keep working until then.
4. Copy your common parameters from the backup into `RegisterCommonParameters()` using `AnalyticsService.RegisterCommonParameter`.

If you installed Viva Analytics from its git URL directly instead of from the installer, the old code and the package define the same types and the project does not compile. Delete `Assets/VivaAnalytics/Runtime` and `Assets/VivaAnalytics/Editor` by hand, or press **Remove legacy files** in the installer.

If a project has an assembly definition that references the old `VivaAnalytics` assembly by name, update the reference to `VivaGames.Analytics`. References by GUID keep working.

## Development

- Clone this repository and open it with Unity. The packages in `Packages/` are embedded, so they are editable and behave like the installed ones.
- The Firebase SDK is not committed. Import it into the development project to compile and test the Firebase tracker.
- Assets under `Packages/` need their `.meta` files committed. Unity generates them when the project is opened.

### Releasing a version

1. Set the same `version` in `Packages/com.vivagames.core/package.json` and `Packages/com.vivagames.analytics/package.json`.
2. Add the entry to `CHANGELOG.md`.
3. Commit, create the tag `vX.Y.Z` and push it. The installer only considers tags with that exact format.

### Adding a module

1. Create `Packages/com.vivagames.<module>` with its `package.json`, assemblies and `.meta` files.
2. Add one line to `VivaModuleCatalog.Modules` in the core package.
3. If the module depends on a third-party SDK, compile its integration in a separate assembly with a define constraint, and enable the define from an editor script when the SDK is detected (see `FirebaseSdkDetector` in the analytics package).

## Limitations worth knowing

- The Package Manager cannot declare dependencies between packages installed from git. That is why the core has the installer window and each module is installed from it, always at the same version as the core.
- The Package Manager does not check GitHub for new releases by itself; the installer window does it through `git ls-remote`.
