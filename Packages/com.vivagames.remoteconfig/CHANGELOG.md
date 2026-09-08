# Changelog: Viva Remote Config

Releases of this package are the git tags `remoteconfig/vX.Y.Z`. Each Viva module has its own version and changelog.

## [1.0.0] - 2026-09-08

### Added
- `RemoteConfigService`: static access to Firebase Remote Config with in-app defaults, cached values of the previous session activated before the fetch, `IsReady`, `Source` (Defaults, Cache or Remote), `OnReady` (fires once when the first fetch attempt ends, with or without success) and `WhenReady`. Getters for `int`, `long`, `float`, `double`, `bool`, `string` and JSON (`JsonUtility`) that never throw: a value with the wrong format is reported once and the default is returned.
- `FirebaseRemoteConfigProvider`, compiled only with `VIVA_FIREBASE_REMOTE_CONFIG` and `VIVA_FIREBASE`, registered automatically at load. It waits for the shared Firebase dependency check of Viva Core (`VivaFirebase`) and never runs its own. `LocalRemoteConfigProvider` serves the defaults when the SDK is not in the project.
- **Viva > Remote Config > Parameters**: declare key, type, validated default and description; Save writes `Assets/VivaRemoteConfig/RemoteConfigParameters.json` and regenerates the typed `RemoteConfigParameters` class (`Lives`, `Keys.Lives`, `Defaults()`). JSON defaults are checked with a built-in syntax validator.
- **Viva > Remote Config > Setup**: project status, SDK detection, `RemoteConfigInit.cs` generation, path settings and first-run setup.
- Migration from a project-specific `RemoteConfigManager`: **Import from code** fills the parameter list from the existing calls (literal keys, `const string` keys, literal defaults, non-literal defaults flagged), and **Migrate** turns the old script into a compatibility wrapper over `RemoteConfigService` that keeps the file, its GUID and its API.
- Automatic `VIVA_FIREBASE_REMOTE_CONFIG` scripting define when `Firebase.RemoteConfig.dll` is found.
- Unit tests for the converter, the service, the validators, the naming, the code generator, the parameter file and the importer.
