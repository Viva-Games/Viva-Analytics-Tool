# Changelog: Viva Core

Releases of this package are the git tags `core/vX.Y.Z`. Each Viva module has its own version and changelog.

## [2.0.0] - 2026-09-07

### Added
- First release as a Unity Package Manager package installed from the repository git URL.
- **Viva > Package Installer** window: installs, updates and removes the Viva modules from GitHub pinned to a release tag. Reads the published releases with `git ls-remote`, keeps its operation queue across domain reloads and, when the core itself is installed from a branch, installs the modules from that same branch.
- Automatic migration of `.unitypackage` installations: before installing a module, the folders left by the old version are backed up in `Library/VivaLegacyBackup` and removed, with confirmation.
- Shared editor utilities for the modules: `ScriptingDefines` (per-platform define management) and `VivaPackageUtility` (package version and path lookup).
