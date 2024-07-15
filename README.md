![image](https://github.com/user-attachments/assets/1e4176d0-25a3-4ab1-b374-4858bc34933d)
# Firebase Analytics Event Creator

This tool makes the creation and modification of analytics events easier to implement and use.

## Installation

In order to use this tool, you need the latest version of [Firebase Analytics sdk](https://firebase.google.com/download/unity?hl=es) installed.

After installing Firebase Analytics, add this .unitypackage to your project.

## Documentation

### Initialization

Put the AnalyticsInit.cs script in the first scene of your game.

### Event creation and modification

- To display the tool, go to Viva/Analytics/Event Creator

![image](https://github.com/user-attachments/assets/9a806723-047d-42a5-88d7-8252323e8730)

![image](https://github.com/user-attachments/assets/be47c386-4616-41bf-a409-397694094208)

- Introduce the name of the desired event. If it already exists, you can load the parameters.
- Add, remove and modify the event parameters. You can change the name and the type of value of each parameter.
- When you have all done, use the "Create or Modify Event" button. A new script will be created in "Assets/VivaAnalytics/Events/".
- To log this event, use {NameOfEvent}.Track() in the desired part of your code, providing the necessary parameters.

### Common Parameters
