# Viva Analytics

Event creation tool and Firebase Analytics integration.

- **Viva > Analytics > Setup**: prepares the project (events folder, `AnalyticsInit.cs`, standard events) and migrates old `.unitypackage` installations.
- **Viva > Analytics > Event Manager**: create, edit, restore and delete events. Each event becomes a class with a static `Track(...)` method.
- The Firebase Analytics SDK is installed by you. The package detects it and enables the `VIVA_FIREBASE_ANALYTICS` define automatically.

Requires `com.vivagames.core`. Full documentation in the [repository README](https://github.com/Viva-Games/Viva-Analytics-Tool#readme).
