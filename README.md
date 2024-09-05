# Firebase Analytics Event Creator

This tool makes the creation and modification of analytics events easier to implement and use.

## Installation

In order to use this tool, you need the latest version of [Firebase Analytics sdk](https://firebase.google.com/docs/analytics/unity/start?hl=es) installed.

After configuring and installing Firebase Analytics, download the latest [.unitypackage release](https://github.com/Viva-Games/Viva-Analytics-Tool/releases) and add it to your project.

## Documentation

### Initialization

Put the AnalyticsInit.cs script in the first scene of your game.

### Event creation and modification

- To display the tool, go to Viva/Analytics/Event Creator

![image](https://github.com/user-attachments/assets/9a806723-047d-42a5-88d7-8252323e8730)

![image](https://github.com/user-attachments/assets/57b15372-508d-4da2-864d-12ac1e90be8b)

- Introduce the name of the desired event. If it already exists, you can load the parameters.
- Add, remove and modify the event parameters. You can change the name and the type of value of each parameter.
- When you have all done, use the "Create or Modify Event" button. A new script will be created in "Assets/VivaAnalytics/Events/".
- To log this event, use {NameOfEvent}.Track() in the desired part of your code, providing the necessary parameters.
- You can also clear the data or delete an event with the desired name. It will be deleted if exists.

### Common Parameters

To add common parameters, modify the "FirebaseAnalytics.cs" script (there are comments to help you). All you need is a key or name for the parameter and the value. These values are usually the player's level, or hours played, or something similar. Therefore, it is recommended to store these values in some way (PlayerPrefs, JSON...) to update them during the game flow and to reflect this update in every event.
