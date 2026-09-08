# Viva Unity Tools

Unity packages shared by Viva Games projects, installed from this repository through the Unity Package Manager. The repository is also the Unity project used to develop them.

| Module | Package | What it does |
|---|---|---|
| [Viva Core](Packages/com.vivagames.core/README.md) | `com.vivagames.core` | Installer for the other modules. Required. |
| [Viva Analytics](Packages/com.vivagames.analytics/README.md) | `com.vivagames.analytics` | Analytics event creation tool and Firebase Analytics integration. |
| [Viva Remote Config](Packages/com.vivagames.remoteconfig/README.md) | `com.vivagames.remoteconfig` | Firebase Remote Config parameters declared in an editor window and read through a generated typed class. |
| [Viva Ads](Packages/com.vivagames.ads/README.md) | `com.vivagames.ads` | AppLovin MAX integration: formats and placements declared in an editor window, one call to show each ad. |

The modules do not install third-party SDKs: each project imports the SDKs it needs (Firebase, AppLovin...) and the modules detect them. Each module README says which SDK it needs.

## Installation

Requirements: Unity 2021.3 or newer, and git 2.14 or newer in the `PATH` (the Package Manager uses it).

1. In Unity open **Window > Package Manager**, press **+** and choose **Install package from git URL**:

   ```
   https://github.com/Viva-Games/Viva-Analytics-Tool.git?path=/Packages/com.vivagames.core#core/v2.3.0
   ```

2. Open **Viva > Package Installer** and press **Install** next to the modules you need. Each module README explains its setup.
3. Commit `Packages/manifest.json` and `Packages/packages-lock.json`, so the whole team gets the same versions.

## Updating

Open **Viva > Package Installer** and press **Check for updates**. Each module is versioned on its own: **Update** moves one module to its latest release and **Update all** moves every outdated one. Nothing updates by itself.

## Releasing a module (maintainers)

1. Bump `version` in the module's `package.json` and add the entry to its `CHANGELOG.md`.
2. Commit, tag with the module prefix (`analytics/v2.0.1`, `core/v2.1.0`) and push the tag.

More in the [Viva Core README](Packages/com.vivagames.core/README.md#for-maintainers).
