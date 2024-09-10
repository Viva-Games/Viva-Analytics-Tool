#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Viva.Services.Utils;

namespace Viva.Services.Analytics
{
    public class AnalyticsEventManager : EditorWindow
    {
        private string[] _eventNames;
        private bool _creatingEvent;
        private string _newEventName; // Declare as a class-level variable
        
        public const string EVENTS_FOLDER = "Assets/VivaAnalytics/Events";

        private void OnEnable()
        {
            // Set the minimum size of the window
            minSize = new Vector2(500, 200);
            LoadEventNames();
        }

        private void OnGUI()
        {
            GUILayout.Label("Event List", EditorStyles.boldLabel);

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical();

            if (_eventNames == null || _eventNames.Length == 0)
            {
                // If no events are found, display a message centered in the window
                GUILayout.Label("No events found.", EditorStyles.centeredGreyMiniLabel);
            }
            else
                DisplayEvents();

            GUILayout.Space(10);

            if (_creatingEvent)
            {
                EditorGUILayout.LabelField("Enter the name of the new event in snake_case:");
                EditorGUILayout.BeginHorizontal();
                _newEventName = EditorGUILayout.TextField(_newEventName); // Use the class-level variable
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Create") || Event.current.keyCode == KeyCode.Return)
                    CreateNewEvent();
                if (GUILayout.Button("Cancel"))
                {
                    GUI.FocusControl(null);
                    _creatingEvent = false;
                    _newEventName = string.Empty;
                }
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                if (GUILayout.Button("New Event"))
                    _creatingEvent = true;
            }

            EditorGUILayout.EndVertical();
        }

        private void DisplayEvents()
        {
            // Display all event names
            foreach (var eventName in _eventNames)
            {
                EditorGUILayout.BeginHorizontal();
                // Display the event name with the option to edit or delete it with the same size.
                GUILayout.Label(eventName, GUILayout.Width(200));

                if (GUILayout.Button("Edit"))
                    EditEvent(eventName);

                if (GUILayout.Button("Delete"))
                {
                    // Display a confirmation dialog
                    if (EditorUtility.DisplayDialog("Delete Event",
                            $"Are you sure you want to delete the {eventName} event?", "Yes", "No"))
                    {
                        // Delete the event
                        var upperCamelCaseName = StringUtils.ToUpperCamelCase(eventName);
                        var folderPath = EVENTS_FOLDER;
                        var filePath = $"{folderPath}/{upperCamelCaseName}.cs";
                        var metaPath = $"{folderPath}/{upperCamelCaseName}.meta";
                        File.Delete(filePath);
                        if (Directory.Exists(metaPath))
                            File.Delete(metaPath);
                        else
                            Debug.Log("No meta file found.");
                        if (AnalyticsEventEditor.IsOpen)
                            AnalyticsEventEditor.CheckCloseOnDeleteEvent(upperCamelCaseName);
                        AssetDatabase.Refresh();
                        CompilationPipeline.RequestScriptCompilation();
                        GUI.FocusControl(null);
                        LoadEventNames();
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private static void EditEvent(string eventName)
        {
            AnalyticsEventEditor.ShowWindow(eventName);
            GUI.FocusControl(null);
        }

        private void LoadEventNames()
        {
            var folderPath = EVENTS_FOLDER;
            if (Directory.Exists(folderPath))
            {
                // Get all the event names from the files in the Events folder and transform them to snake case
                _eventNames = Directory.GetFiles(folderPath, "*.cs")
                    .Select(Path.GetFileNameWithoutExtension)
                    .ToArray();

                for (var i = 0; i < _eventNames.Length; i++)
                    _eventNames[i] = StringUtils.ToSnakeCase(_eventNames[i]);
            }
            else
                _eventNames = Array.Empty<string>();
        }
        
        private void CreateNewEvent()
        {
            GUI.FocusControl(null);
            if (string.IsNullOrEmpty(_newEventName))
            {
                EditorUtility.DisplayDialog("Invalid event name", "The event name cannot be empty.", "OK");
                return;
            }
            var snakeCaseName = StringUtils.ToSnakeCase(_newEventName);
            if (snakeCaseName != _newEventName)
            {
                EditorUtility.DisplayDialog("Invalid event name", "The event name must be in snake_case.", "OK");
                return;
            }

            string upperCamelCaseName = StringUtils.ToUpperCamelCase(_newEventName);
            string folderPath = EVENTS_FOLDER;
            string filePath = $"{folderPath}/{upperCamelCaseName}.cs";

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            if (File.Exists(filePath))
            {
                EditorUtility.DisplayDialog("Event already exists", "An event with this name already exists.", "OK");
                return;
            }

            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.Write(AnalyticsEventEditor.GetEventScriptWithoutParameters(upperCamelCaseName));
            }

            _newEventName = string.Empty;
            _creatingEvent = false;
            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
            LoadEventNames();
            if (!AnalyticsEventEditor.IsOpen)
                EditEvent(snakeCaseName);
        }

        [MenuItem("Viva/Analytics/Event Manager")]
        public static void ShowWindow()
        {
            GetWindow<AnalyticsEventManager>("Analytics Event Manager");
        }
    }
}
#endif