# Firebase Analytics Event Creator

This tool makes the creation and modification of analytics events easier to implement and use.

## Installation

In order to use this tool, you need the latest version of [Firebase Analytics sdk](https://firebase.google.com/docs/analytics/unity/start?hl=es) installed.

After configuring and installing Firebase Analytics, download the latest [.unitypackage release](https://github.com/Viva-Games/Viva-Analytics-Tool/releases) and add it to your project.

## Documentation

### Initialization

Put the AnalyticsInit.cs script in the first scene of your game.

### Event creation and modification

- To display the tool, go to Viva/Analytics/Event Manager

![image](https://github.com/user-attachments/assets/5980dd3d-4adc-4452-80d3-fab9fcf2564f)

![image](https://github.com/user-attachments/assets/baf0e93c-62f5-4bc8-9887-a2ddda2887b4)

- The tool displays all events in the Assets/VivaAnalytics/Events/ folder.
- You can create, edit and delete events.
- When you press the Create button, you must type the name of the new event and press Enter or the Create button. A new script will be created in the Events folder.
- When you create or edit an event, the Event Editor appears.

![image](https://github.com/user-attachments/assets/3e4ee458-3c1c-4079-b0ca-e8df2c266b9c)

- This editor allows you to change the name of the event and its parameters.
- If you do not save the changes, they will be lost. The tool will warn you.

To log an event, use {NameOfEvent}.Track() in the desired part of your code, providing the necessary parameters.

### Common Parameters

To add common parameters, modify the "FirebaseAnalytics.cs" script (there are comments to help you). All you need is a key or name for the parameter and the value. These values are usually the player's level, or hours played, or something similar. Therefore, it is recommended to store these values in some way (PlayerPrefs, JSON...) to update them during the game flow and to reflect this update in every event.
