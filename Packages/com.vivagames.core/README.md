# Viva Core

Base package of the Viva Unity Tools. It contains the **Viva > Package Installer** window and the editor utilities the other modules share. It has no runtime code and needs no SDK.

## Installation

In Unity open **Window > Package Manager**, press **+** and choose **Install package from git URL**:

```
https://github.com/Viva-Games/Viva-Analytics-Tool.git?path=/Packages/com.vivagames.core#core/v2.0.0
```

Git 2.14 or newer must be in the `PATH`.

## Package Installer

**Viva > Package Installer** lists every Viva module and what is installed:

- **Install** adds a module at its latest release. **Update** appears when a newer release exists; **Update all** updates every outdated module, the core last. **Remove** takes a module out of the project.
- If the project had the old `.unitypackage` version of a module, the installer asks for confirmation, backs up the old folders in `Library/VivaLegacyBackup` and removes them before installing.
- **Show development branches** adds a branch selector per module, with **Install from**, **Switch to** and **Pull latest**, to try unreleased work on one module without touching the others. Once a release exists, **Switch to** pins the module back to it.

Every install or update writes to `Packages/manifest.json` and `packages-lock.json`: commit both. The lock file pins the exact commit, so the whole team gets the same version.

## For maintainers

- Each module lives in `Packages/<name>` of the repository and has its own `version`, `CHANGELOG.md` and release tags `<prefix>/vX.Y.Z`. The prefix is declared in `VivaModuleCatalog`.
- To release a module: bump its `version`, add the changelog entry, commit, tag (`analytics/v2.0.1`) and push the tag. The installer reads the tags with `git ls-remote`.
- To add a module: create its folder in `Packages/` with `package.json`, assemblies and `.meta` files, and add one line to `VivaModuleCatalog.Modules`. If it depends on an SDK, compile the integration in an assembly with a define constraint and enable the define from an editor script when the SDK is detected (see `FirebaseSdkDetector` in Viva Analytics).
- The Package Manager cannot declare dependencies between packages installed from git; that is why the installer exists. Keep `ScriptingDefines` and `VivaPackageUtility` backwards compatible, because a project may mix module versions.
