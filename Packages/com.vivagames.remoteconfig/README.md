# Viva Remote Config

Firebase Remote Config for Viva Games projects without retyping keys and defaults all over the game: declare the parameters once in an editor window, read them through a generated typed class.

```csharp
int lives = RemoteConfigParameters.Lives;                    // int, default built in
if (RemoteConfigParameters.ShowInterstitials) ...             // bool
var list = RemoteConfigParameters.IdfaList<IdfaEntries>();    // JSON parsed with JsonUtility
```

## Installation

1. Install the [Firebase Remote Config SDK for Unity](https://firebase.google.com/docs/remote-config/get-started?platform=unity) in your project. The package does not install it: it detects `Firebase.RemoteConfig.dll` and enables the `VIVA_FIREBASE_REMOTE_CONFIG` define by itself. Until the SDK is there, the game uses the in-app defaults.
2. Install [Viva Core](../com.vivagames.core/README.md) 2.1.0 or newer and, in **Viva > Package Installer**, press **Install** next to Viva Remote Config. If Viva Analytics is installed it must be 2.1.0 or newer; the installer says so.
3. On its first run the package offers the initial setup: it creates `Assets/VivaRemoteConfig/RemoteConfigParameters.json`, generates `RemoteConfigParameters.cs` from it and generates `RemoteConfigInit.cs`. You can run it later from **Viva > Remote Config > Setup**.
4. Add the `RemoteConfigInit` component to a GameObject in the first scene.

Coming from a project with its own `RemoteConfigManager` (Arrows)? See [Migration](#migration-from-an-old-remoteconfigmanager).

## Documentation

### Declaring parameters

**Viva > Remote Config > Parameters** lists the parameters of the project. Each row has:

- **Key**: exactly as in the Firebase console. Keys are never renamed or normalized (letters, digits and underscores, starting with a letter or underscore, up to 256 characters).
- **Type**: `int`, `long`, `float`, `double`, `bool`, `string` or `json`. The four numeric types are **Number** in the console, and the C# type decides how the value is read. `json` gets a multi-line field with a syntax check.
- **Default value**: validated against the type before saving. It is the in-app default, registered in Firebase with `SetDefaultsAsync` and used when the key is not in the console.
- **Description**: optional; it becomes the `<summary>` of the generated property.

**Save** writes the JSON (the source of truth, commit it) and regenerates `RemoteConfigParameters.cs`. Rows with errors are marked in red and block saving. The file paths are configurable in Setup.

### Generated class

```csharp
public static class RemoteConfigParameters
{
    /// <summary>Max lives per level.</summary>
    public static int Lives => RemoteConfigService.GetInt(Keys.Lives, 5);
    public static string IdfaListJson => RemoteConfigService.GetString(Keys.IdfaList, "{\"entries\":[]}");
    public static T IdfaList<T>(T defaultValue = default) => RemoteConfigService.GetJson(Keys.IdfaList, defaultValue);

    public static class Keys { public const string Lives = "lives"; ... }
    public static Dictionary<string, object> Defaults() => ...;   // registered in Firebase by RemoteConfigInit
}
```

Property names derive from the keys (`startInterstitialsLevel` becomes `StartInterstitialsLevel`, `idfa_list` becomes `IdfaList`). A `json` parameter gets a raw string property and a generic parser. Do not edit the file: it is regenerated on every save.

### Initialization

`RemoteConfigInit.cs` belongs to your project and updates never overwrite it. Its `Awake` calls `RemoteConfigService.Initialize(RemoteConfigParameters.Defaults(), options)`. The order with `AnalyticsInit` does not matter: Viva Core runs the Firebase dependency check once and both modules wait for it.

What happens after `Initialize`, when the SDK is present:

1. The defaults are registered in Firebase.
2. The values downloaded in a previous session are activated, so they are readable at once (`RemoteConfigService.Source` is `Cache`, or `Defaults` if there is nothing cached).
3. A fetch runs and, if it succeeds, its values are activated (`Source` is `Remote`). If it fails (no network, throttled), the cached or default values stay.
4. `RemoteConfigService.IsReady` becomes true and `OnReady` fires once, with or without success.

```csharp
RemoteConfigService.WhenReady(() => ApplyLives(RemoteConfigParameters.Lives)); // runs now if already ready, else once when ready
RemoteConfigService.OnReady += OnRemoteConfigReady;                             // plain event, fires once
```

Reading a property before the fetch ends is fine: you get the cached value, else the registered default, else the default of the getter.

Options (`RemoteConfigOptions`):

| Option | Default | Meaning |
|---|---|---|
| `MinimumFetchInterval` | `TimeSpan.Zero` | Passed to `FetchAsync`. Zero fetches on every cold start. Firebase's own default is 12 hours and it throttles apps that fetch too often. |
| `FetchTimeout` | `null` (SDK: 60 s) | Connection timeout of the fetch. |
| `ActivateCachedValuesOnStart` | `true` | Activate the values of the previous session before fetching. |
| `LogToConsole` | `true` | Log what the service does and where the values come from. |

### Reading values directly

`RemoteConfigService.GetInt/GetLong/GetFloat/GetDouble/GetBool/GetString/GetJson<T>(key, default)` are public for defaults that are not literals (a value from a ScriptableObject, for instance). Getters never throw: a value that does not convert to the requested type is reported once in the console and the default is returned. `GetJson` uses `JsonUtility`, so wrap top-level arrays in a class.

### Without the SDK

If `Firebase.RemoteConfig.dll` (or `Firebase.App.dll`) is not in the project, `Initialize` uses `LocalRemoteConfigProvider`: the registered defaults, ready on the next frame. `LocalRemoteConfigProvider.SetOverride(key, value)` replaces a value for tests.

### Firebase console

Create the same keys in the console (**Remote Config > Add parameter**) with the matching type: Number for the numeric types, Boolean, String or JSON. Values that do not match the type in the window are ignored with a warning in the console of Unity, so the game keeps its default.

### Migration from an old RemoteConfigManager

Projects that had their own `RemoteConfigManager` (Arrows) keep working as they are; the package detects the script and offers two steps in Setup:

1. **Import from code** (Parameters window): scans the project for `RemoteConfigManager.GetX("key", default)` calls, including keys passed through `const string` fields, and fills the list with their keys, types and literal defaults. Keys whose default was not a literal (`config.livesPerLevel`) are marked in the description for you to type a value. Nothing is written until you Save.
2. **Migrate legacy manager** (Setup): backs the old script up as `Legacy/RemoteConfigManager.legacy.txt` and replaces its content with a wrapper that keeps the class, the namespace and the API (`IsReady`, `OnReady`, `Initialize`, `GetInt`, `GetString`, `GetBool`, `GetJson`) forwarding to `RemoteConfigService`. The file and its GUID stay, so nothing else in the project changes and `AnalyticsInit.OnFirebaseReady()` can keep calling `RemoteConfigManager.Initialize()`.

Differences you will notice after migrating: the values of the previous session are available before the fetch ends, `OnReady` also fires when the fetch fails (check `RemoteConfigService.Source`), and a value with the wrong format no longer throws. Move the call sites to `RemoteConfigParameters.<Name>` when convenient and delete the wrapper when none is left.

### Requirements

- Viva Core 2.1.0 or newer (`VivaFirebase`, the shared Firebase dependency check).
- Viva Analytics 2.1.0 or newer if it is installed: 2.0.0 runs its own dependency check, which cannot run at the same time as the shared one.
- Unity 2021.3 or newer. Firebase Unity SDK 11 or newer for the Remote Config SDK.
