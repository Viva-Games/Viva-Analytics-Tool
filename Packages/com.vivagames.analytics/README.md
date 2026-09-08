# Viva Analytics

This tool makes the creation and modification of analytics events easier to implement and use, and integrates Firebase Analytics.

## Installation

1. Install the latest [Firebase Analytics SDK for Unity](https://firebase.google.com/docs/analytics/unity/start) in your project. The package does not install it: it detects `Firebase.Analytics.dll` and enables the `VIVA_FIREBASE_ANALYTICS` define by itself. Until the SDK is there, events only go to the console.
2. Install [Viva Core](../com.vivagames.core/README.md) and, in **Viva > Package Installer**, press **Install** next to Viva Analytics.
3. On its first run the package offers to run the initial setup: it creates `Assets/VivaAnalytics/Events`, imports the standard events and generates `Assets/VivaAnalytics/AnalyticsInit.cs`. You can run it later from **Viva > Analytics > Setup**.

Coming from the `.unitypackage` version? See [Migration](#migration-from-the-unitypackage-version).

## Documentation

### Initialization

Put the `AnalyticsInit` component in the first scene of your game. `AnalyticsInit.cs` belongs to your project and updates never overwrite it. It initializes the service with the Firebase tracker, adds the Facebook and Singular trackers when their SDKs are in the project, sets the user id, and it is where you register common parameters and user properties and put the code that needs Firebase ready, in `OnFirebaseReady()`.

The Firebase dependency check is run once by Viva Core 2.1.0 or newer (`VivaFirebase`) and shared with every other Viva module that uses Firebase, such as Viva Remote Config. Each module keeps its own init component and the order in which they run does not matter: the first one starts the check and the rest wait for it. The console shows `[Viva] Checking Firebase dependencies (requested by Viva Analytics)...`, then `[Viva] Firebase ready to use.` and `[Analytics] Firebase ready: sending N queued event(s).` With an older core the tracker checks on its own, as in 2.0.0, and says so in the console.

### Event creation and modification

- To display the tool, go to **Viva > Analytics > Event Manager**.
- The tool displays all events in the `Assets/VivaAnalytics/Events/` folder (configurable in Setup).
- You can create, edit and delete events. **New Event** asks for a snake_case name, creates the script and opens the Event Editor, where you set the event name and its parameters (`int`, `long`, `float`, `double`, `bool`, `string`). If you do not save the changes, they will be lost. The tool will warn you.
- The **standard events** of the studio (ad impression, FTUE landmark, virtual currency...) that are not in the project are listed below with an **Add** button. A modified standard event shows **Restore**.

To log an event, use `{NameOfEvent}.Track(...)` in the desired part of your code, providing the necessary parameters.

### Common parameters

Common parameters are sent with every event. Register them in `RegisterCommonParameters()` of `AnalyticsInit.cs`. There are two ways, and the difference is when the value is read:

- `RegisterCommonParameter(key, function)`: the function runs every time an event is tracked, so each event carries the current value. Use it for anything that changes during the session: level, hours played, currency.
- `SetCommonParameter(key, value)`: the value is read once, when you call it, and every event carries that same value until you call it again. Use it for what is fixed for the whole session: build type, platform, app version.

```csharp
AnalyticsService.RegisterCommonParameter("player_level", () => SaveManager.Data.Level);
AnalyticsService.SetCommonParameter("build_type", Debug.isDebugBuild ? "debug" : "release");
```

The usual mistake is passing a changing value to `SetCommonParameter`: every event would carry the value the player had at startup.

### User properties and consent

```csharp
AnalyticsService.SetUserProperty("player_segment", "whale");
AnalyticsService.SetUserId(VivaUserId.GetOrCreate()); // or your own player id
AnalyticsService.SetConsent(ConsentModeMapper.Map(tcf)); // Google Consent Mode, from the TCF state of your CMP
```

`Viva.Services.Analytics.Consent` has `TcfConsent` (the TCF purposes read from your CMP, for example AppLovin MAX), `ConsentModeMapper` (TCF to the four Consent Mode signals, following Google's rules) and unit tests. Every tracker receives user data and consent; the Firebase tracker applies them as soon as Firebase is ready, before any queued event.

**With Viva Ads installed you do not call `SetConsent` at all.** Viva Ads publishes the result of the AppLovin MAX consent flow in `VivaConsent` (Viva Core 2.2.0) and `AnalyticsService` subscribes on its own, translates it with `ConsentModeMapper` and applies it. The console shows `[Viva] Consent resolved: ...` followed by `[Analytics] Consent Mode applied from AppLovin MAX.` A project with its own CMP can publish the same way, `VivaConsent.Set(new ConsentState { ... })`, or keep calling `SetConsent`; the last call wins.

### Event targets: Firebase, Facebook and Singular

Firebase receives every event. Facebook (Meta App Events) and Singular receive only the events you tick for them in the Event Editor, under **Send to**. The Event Manager shows the extra targets of each event (`+ Facebook`), and the studio catalog can mark an event with `"targets": ["facebook"]`.

A ticked event declares its targets in the generated class (`public AnalyticsTargets Targets => AnalyticsTargets.Firebase | AnalyticsTargets.Facebook;`) and `AnalyticsService` hands it only to the trackers that serve one of them; the console tracker shows every event. Common parameters, user properties and the user id go to every tracker as before.

The Event Editor warns when a name or the parameters break a platform rule. Facebook accepts names of 2 to 40 characters (letters, digits, `_`, `-` and spaces), up to 25 parameters and values of 100 characters. Singular accepts names of up to 32 ASCII characters. At runtime the Facebook tracker skips an event with an invalid name (one warning) and drops the parameters past the limit; the Singular tracker sends the event anyway and warns once.

Projects that generated `AnalyticsInit.cs` with 2.2.0 or older keep their file: add the `#if` blocks shown below after `AnalyticsService.Initialize`. Setup says so when an SDK is found and the file does not create its tracker.

### Facebook tracker

Import the Facebook SDK for Unity and set the App ID in **Facebook > Edit Settings**. The tool finds `Facebook.Unity.dll`, enables `VIVA_FACEBOOK` and the generated `AnalyticsInit.cs` adds the tracker:

```csharp
#if VIVA_FACEBOOK
AnalyticsService.AddTracker(new FacebookAnalyticsTracker());
#endif
```

`FacebookAnalyticsTracker` initializes the SDK if the project has not (`FB.Init`), calls `FB.ActivateApp()` at start and every time the app resumes, sends the ticked events with `FB.LogAppEvent`, sets `FB.Mobile.UserID` with the user id and queues everything until the SDK is ready. The tracking flags (`SetAutoLogAppEventsEnabled`, `SetAdvertiserIDCollectionEnabled`, `SetAdvertiserTrackingEnabled`) start off and turn on when the consent allows it: outside GDPR, or with TCF purpose 1 granted. The consent arrives from `VivaConsent` (automatic with Viva Ads) or from `AnalyticsService.SetConsent` (the `AdStorage` signal). `LogToConsole` echoes each sent event. Without an App ID `FB.Init` fails: the tracker logs the error and keeps queueing, and the other trackers are not affected. In the editor the SDK only logs the events it would send.

### Singular tracker

Install the Singular Unity SDK (Package Manager with the git URL `https://github.com/singular-labs/Singular-Unity-SDK.git`, or its `.unitypackage`) and put its `SingularSDK` component, with the API key and secret, in the first scene: the SDK initializes itself from that component. The tool finds the `SingularSDK` assembly, enables `VIVA_SINGULAR` and the generated `AnalyticsInit.cs` adds the tracker:

```csharp
#if VIVA_SINGULAR
AnalyticsService.AddTracker(new SingularAnalyticsTracker());
#endif
```

`SingularAnalyticsTracker` waits for `SingularSDK.Initialized`, then sends the ticked events with `SingularSDK.Event`, the user id with `SetCustomUserId` and the user properties with `SetGlobalProperty`; everything received before is queued. With **Attribute the ad revenue of Viva Ads in Singular** ticked in **Viva > Analytics > Setup** (on by default) every impression that Viva Ads 1.1.0 publishes in `VivaAdRevenue` (Viva Core 2.3.0) is sent with `SingularSDK.AdRevenue`: platform, currency, revenue, ad type, ad unit, network, placement and precision. Untick it if the project attributes the revenue on its own. In the editor the Singular SDK never initializes and every call is a no-op, so the tracker counts as ready at once and, with `LogToConsole`, shows what it would send; the real check is a device build. The toggle is stored in `Assets/VivaAnalytics/Resources/VivaAnalyticsSettings.asset` (`AnalyticsSettings`, read at runtime); `SingularAnalyticsTracker.AttributeAdRevenue` takes that value and code can override it before adding the tracker. `LogToConsole` echoes each sent event and impression.

### User id

`VivaUserId.GetOrCreate()` returns a GUID created once and kept in PlayerPrefs, and the generated `AnalyticsInit.cs` passes it to `AnalyticsService.SetUserId`, so Firebase (user id) and Singular (custom user id) see the same player. `VivaUserId.Reset()` creates a new one, for example when the player deletes the account. If the game already has a player id, pass that one instead.

### Several trackers

`AnalyticsService.Initialize` accepts any number of trackers and `AddTracker` adds more later. Each tracker declares the targets it serves (`Targets`, Firebase by default) and receives the events that share one; extend `AAnalyticsTracker` for a new SDK. `FirebaseAnalyticsTracker` queues events until Firebase is ready and raises `FirebaseReady`; `ConsoleAnalyticsTracker` only logs.

### Migration from the .unitypackage version

Install Viva Core and press **Install** next to Viva Analytics: the installer backs up and removes the old `Runtime` and `Editor` folders. On its first run the package offers to migrate `Scripts/FirebaseAnalytics.cs` and `Scripts/AnalyticsInit.cs`: it backs them up in `Assets/VivaAnalytics/Legacy`, keeps the GUID of `AnalyticsInit.cs` so scene references survive, moves the common parameters to `RegisterCommonParameters()` (as comments to rewrite when the old code computed them with its own logic) and keeps any other custom line as comments in `OnFirebaseReady()`. `Legacy/MIGRATION_NOTES.txt` lists everything. Events and `.Track()` calls need no change. If an assembly definition referenced `VivaAnalytics` by name, point it to `VivaGames.Analytics`.
