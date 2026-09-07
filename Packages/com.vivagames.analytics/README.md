# Viva Analytics

This tool makes the creation and modification of analytics events easier to implement and use, and integrates Firebase Analytics.

## Installation

1. Install the latest [Firebase Analytics SDK for Unity](https://firebase.google.com/docs/analytics/unity/start) in your project. The package does not install it: it detects `Firebase.Analytics.dll` and enables the `VIVA_FIREBASE_ANALYTICS` define by itself. Until the SDK is there, events only go to the console.
2. Install [Viva Core](../com.vivagames.core/README.md) and, in **Viva > Package Installer**, press **Install** next to Viva Analytics.
3. On its first run the package offers to run the initial setup: it creates `Assets/VivaAnalytics/Events`, imports the standard events and generates `Assets/VivaAnalytics/AnalyticsInit.cs`. You can run it later from **Viva > Analytics > Setup**.

Coming from the `.unitypackage` version? See [Migration](#migration-from-the-unitypackage-version).

## Documentation

### Initialization

Put the `AnalyticsInit` component in the first scene of your game. `AnalyticsInit.cs` belongs to your project and updates never overwrite it. It initializes the service with the Firebase tracker, and it is where you register common parameters and user properties and put the code that needs Firebase ready, in `OnFirebaseReady()`.

### Event creation and modification

- To display the tool, go to **Viva > Analytics > Event Manager**.
- The tool displays all events in the `Assets/VivaAnalytics/Events/` folder (configurable in Setup).
- You can create, edit and delete events. **New Event** asks for a snake_case name, creates the script and opens the Event Editor, where you set the event name and its parameters (`int`, `long`, `float`, `double`, `bool`, `string`). If you do not save the changes, they will be lost. The tool will warn you.
- The **standard events** of the studio (ad impression, FTUE landmark, virtual currency...) that are not in the project are listed below with an **Add** button. A modified standard event shows **Restore**.

To log an event, use `{NameOfEvent}.Track(...)` in the desired part of your code, providing the necessary parameters.

### Common parameters

Common parameters are sent with every event. Register them in `RegisterCommonParameters()` of `AnalyticsInit.cs` with a function, which is evaluated on every event, so they can point to your save data or player prefs:

```csharp
AnalyticsService.RegisterCommonParameter("player_level", () => SaveManager.Data.Level);
AnalyticsService.SetCommonParameter("build_type", Debug.isDebugBuild ? "debug" : "release");
```

### User properties and consent

```csharp
AnalyticsService.SetUserProperty("player_segment", "whale");
AnalyticsService.SetUserId(SaveManager.Data.PlayerId);
AnalyticsService.SetConsent(ConsentModeMapper.Map(tcf)); // Google Consent Mode, from the TCF state of your CMP
```

`Viva.Services.Analytics.Consent` has `TcfConsent` (the TCF purposes read from your CMP, for example AppLovin MAX), `ConsentModeMapper` (TCF to the four Consent Mode signals, following Google's rules) and unit tests. Every tracker receives user data and consent; the Firebase tracker applies them as soon as Firebase is ready, before any queued event.

### Several trackers

`AnalyticsService.Initialize` accepts any number of trackers and every event goes to all of them. Extend `AAnalyticsTracker` for a new SDK. `FirebaseAnalyticsTracker` queues events until Firebase is ready and raises `FirebaseReady`; `ConsoleAnalyticsTracker` only logs.

### Migration from the .unitypackage version

Install Viva Core and press **Install** next to Viva Analytics: the installer backs up and removes the old `Runtime` and `Editor` folders. On its first run the package offers to migrate `Scripts/FirebaseAnalytics.cs` and `Scripts/AnalyticsInit.cs`: it backs them up in `Assets/VivaAnalytics/Legacy`, keeps the GUID of `AnalyticsInit.cs` so scene references survive, moves the common parameters to `RegisterCommonParameters()` (as comments to rewrite when the old code computed them with its own logic) and keeps any other custom line as comments in `OnFirebaseReady()`. `Legacy/MIGRATION_NOTES.txt` lists everything. Events and `.Track()` calls need no change. If an assembly definition referenced `VivaAnalytics` by name, point it to `VivaGames.Analytics`.
