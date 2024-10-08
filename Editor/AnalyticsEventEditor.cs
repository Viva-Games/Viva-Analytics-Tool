#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Viva.Services.Utils;

namespace Viva.Services.Analytics
{
    public class AnalyticsEventEditor : EditorWindow
    {
        public static bool IsOpen;

        private string _eventName;
        private string _previousEventName;
        private List<EventParameter> _eventParameters = new();
        private string[] _parameterTypes = new[] { "int", "string", "double" };

        private void OnEnable()
        {
            // Set the minimum size of the window
            minSize = new Vector2(600, 200);
            LoadParameters(_eventName);
        }

        private void OnDisable()
        {
            IsOpen = false;
        }

        private void OnDestroy()
        {
            var outfile = CreateAllScriptString(StringUtils.ToUpperCamelCase(_eventName));
            var folderPath = AnalyticsEventManager.EVENTS_FOLDER;
            var assetPath = $"{folderPath}/{_previousEventName}.cs";
            if (!HasChanges(outfile, assetPath)) return;
            if (!EditorUtility.DisplayDialog(
                    "Unsaved Changes",
                    "Your changes will be lost. Are you sure you want to continue?",
                    "Yes", "No"))
            {
                // Reopen the window to prevent it from closing
                EditorApplication.delayCall += () => ReOpenWindow();
            }
        }

        private void OnGUI()
        {
            // Display the description of the tool
            GUILayout.Label(
                "Please, write all the string labels in default event format (level_start), " +
                "UpperCamelCase or normal words separated by spaces.",
                EditorStyles.wordWrappedLabel);

            // Display the event name field
            EditorGUILayout.BeginHorizontal();
            _eventName = EditorGUILayout.TextField("Event Name", _eventName);

            EditorGUILayout.EndHorizontal();

            // Display the parameters section
            EditorGUILayout.LabelField("Parameters");

            if (_eventParameters.Count != 0)
            {
                for (int i = 0; i < _eventParameters.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();
                    _eventParameters[i].Name = EditorGUILayout.TextField("Name", _eventParameters[i].Name);

                    // Determine the current index of the parameter type
                    int currentIndex = Array.IndexOf(_parameterTypes, _eventParameters[i].Type);
                    if (currentIndex == -1) currentIndex = 0; // Default to the first type if not found
                    // Create the popup and update the parameter type based on the selected index
                    currentIndex = EditorGUILayout.Popup("Type", currentIndex, _parameterTypes);
                    _eventParameters[i].Type = _parameterTypes[currentIndex];

                    if (GUILayout.Button("Remove"))
                    {
                        _eventParameters.RemoveAt(i);
                        GUI.FocusControl(null);
                        EditorGUILayout.EndHorizontal();
                        break; // Exit the loop to avoid modifying the collection while iterating
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Add Parameter"))
            {
                var previousType = _eventParameters.LastOrDefault()?.Type ?? "int";
                _eventParameters.Add(new EventParameter("new_param", previousType));
                GUI.FocusControl(null);
            }

            if (GUILayout.Button("Clear Parameters"))
            {
                _eventParameters.Clear();
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button("Save"))
            {
                SaveEvent();
                GUI.FocusControl(null);
            }
        }

        private void LoadParameters(string scriptName)
        {
            scriptName = StringUtils.ToUpperCamelCase(scriptName);
            var folderPath = AnalyticsEventManager.EVENTS_FOLDER;
            var assetPath = folderPath + $"/{scriptName}.cs";
            // If the script already exists, load the parameters
            if (File.Exists(assetPath))
            {
                _previousEventName = scriptName;
                var fileContent = File.ReadAllText(assetPath);
                // Regular expression to find the event parameters
                var regex = new Regex(@"new EventParameter\(""(.+?)"", ""(.+?)""\)", RegexOptions.Singleline);
                var matches = regex.Matches(fileContent);

                _eventParameters.Clear(); // Clear existing parameters before loading new ones

                if (matches.Count == 0)
                    return;

                foreach (Match match in matches)
                {
                    if (match.Groups.Count != 3) continue; // Ensure there are two groups captured: name and type
                    var eventName = match.Groups[1].Value;
                    var type = match.Groups[2].Value;
                    _eventParameters.Add(new EventParameter(eventName, type));
                }
            }
        }

        private void SaveEvent()
        {
            var scriptName = _eventName;
            // Check if the event name is empty
            if (string.IsNullOrEmpty(scriptName))
            {
                EditorUtility.DisplayDialog("Empty event name",
                    "The event name cannot be empty.", "Ok");
                return;
            }

            scriptName = StringUtils.ToUpperCamelCase(scriptName);

            var folderPath = AnalyticsEventManager.EVENTS_FOLDER;
            var assetPath = folderPath + $"/{scriptName}.cs";

            SaveEventScript(scriptName, assetPath);
        }

        private void SaveEventScript(string scriptName, string assetPath)
        {
            // Check if there are repeated parameter names
            if (HasRepeatedParameterNames(GetSnakeCaseEventParameters()))
            {
                EditorUtility.DisplayDialog("Repeated parameter names",
                    "The event parameters must have unique names.", "Ok");
                return;
            }

            var outfile = CreateAllScriptString(scriptName);
            if (!HasChanges(outfile, assetPath))
            {
                EditorUtility.DisplayDialog("No changes",
                    "No changes were made.", "Ok");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Save Confirmation",
                    "Do you want to overwrite the event?", "Yes", "No"))
            {
                Debug.Log("Cancelled event creation.");
                return;
            }

            Debug.Log($"Modifying script: {scriptName} at {assetPath}");

            using StreamWriter newEventFile = new StreamWriter(assetPath);
            newEventFile.Write(outfile);
            newEventFile.Close();

            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
        }

        private bool HasChanges(string outfile, string previousAssetPath)
        {
            var currentFile = File.ReadAllText(previousAssetPath);
            // Remove all spaces from both strings
            var normalizedCurrentFile = StringUtils.Normalize(currentFile);
            var normalizedOutfile = StringUtils.Normalize(outfile);
            return normalizedOutfile != normalizedCurrentFile;
        }

        #region Method Constructors

        private string CreateAllScriptString(string scriptName)
        {
            var outfile = new StringBuilder();
            
            if (_eventParameters.Count == 0)
                outfile.Append(GetEventScriptWithoutParameters(scriptName));
            else
            {
                var eventParameters = GetSnakeCaseEventParameters();

                // Create the desired script
                outfile.AppendLine("using System.Collections.Generic;");
                outfile.AppendLine("");
                outfile.AppendLine("namespace Viva.Services.Analytics");
                outfile.AppendLine("{");
                outfile.AppendLine($"\tpublic class {scriptName} : IAnalyticsEvent");
                outfile.AppendLine("\t{");

                // Save the parameters if is Editor
                outfile.AppendLine("#if UNITY_EDITOR");
                outfile.AppendLine($"\t\tpublic List<EventParameter> eventParameters = new()");
                outfile.AppendLine(EditorParametersToPseudoCode(eventParameters));
                outfile.AppendLine("#endif");
                outfile.AppendLine("");

                // Create the event parameters
                outfile.AppendLine(ParameterNamesToPseudoCode(eventParameters));
                outfile.AppendLine(ParameterVariablesToPseudoCode(eventParameters));

                // Create the constructor
                outfile.AppendLine(CreateConstructor(scriptName, eventParameters));

                // Create the event key constant in snake case format
                var eventKey = StringUtils.ToSnakeCase(scriptName);
                outfile.AppendLine($"\t\tpublic string GetEventKey() => \"{eventKey}\";");
                outfile.AppendLine("");
                outfile.AppendLine(TrackingFieldsDictionaryToPseudoCode(eventParameters));
                outfile.AppendLine(TrackMethodToPseudoCode(scriptName, eventParameters));
                outfile.AppendLine("\t}");
                outfile.AppendLine("}");
            }

            return outfile.ToString();
        }

        public static string GetEventScriptWithoutParameters(string scriptName)
        {
            var outfile = new StringBuilder();
            outfile.AppendLine("using System.Collections.Generic;");
            outfile.AppendLine("");
            outfile.AppendLine("namespace Viva.Services.Analytics");
            outfile.AppendLine("{");
            outfile.AppendLine($"\tpublic class {scriptName} : IAnalyticsEvent");
            outfile.AppendLine("\t{");
                
            var eventKey = StringUtils.ToSnakeCase(scriptName);
            outfile.AppendLine($"\t\tpublic string GetEventKey() => \"{eventKey}\";");
            outfile.AppendLine("");
            outfile.AppendLine("\t\tpublic Dictionary<string, object> GetTrackingFields() => new();");
            outfile.AppendLine("");
            outfile.AppendLine($"\t\tpublic static void Track() => AnalyticsService.TrackEvent(new {scriptName}());");
            outfile.AppendLine("\t}");
            outfile.AppendLine("}");
            return outfile.ToString();
        }

        private List<EventParameter> GetSnakeCaseEventParameters()
        {
            var eventParameters = _eventParameters;
            foreach (var param in eventParameters)
                param.Name = StringUtils.ToSnakeCase(param.Name);
            return eventParameters;
        }

        private string CreateConstructor(string scriptName, List<EventParameter> parameters)
        {
            var result = new StringBuilder();
            result.AppendLine("\t\t// Constructor");
            result.Append($"\t\tprivate {scriptName}(");
            foreach (var param in parameters)
            {
                result.Append($"{param.Type} {StringUtils.ToLowerCamelCase(param.Name)}, ");
            }

            result.Remove(result.Length - 2, 2);
            result.AppendLine(")");
            result.AppendLine("\t\t{");
            foreach (var param in parameters)
            {
                result.AppendLine(
                    $"\t\t\t_{StringUtils.ToLowerCamelCase(param.Name)} = {StringUtils.ToLowerCamelCase(param.Name)};");
            }

            result.AppendLine("\t\t}");
            return result.ToString();
        }

        private string TrackingFieldsDictionaryToPseudoCode(List<EventParameter> eventParameters)
        {
            var result = new StringBuilder();
            result.AppendLine("\t\tpublic Dictionary<string, object> GetTrackingFields() => new()");
            result.AppendLine("\t\t{");
            foreach (var param in eventParameters)
            {
                result.AppendLine($"\t\t\t{{ {param.Name.ToUpper()}, _{StringUtils.ToLowerCamelCase(param.Name)} }},");
            }

            result.AppendLine("\t\t};");

            return result.ToString();
        }

        private string TrackMethodToPseudoCode(string scriptName, List<EventParameter> eventParameters)
        {
            //Template:
            // public static void Track(string levelName, int isFarm, string levelInitials) =>
            //     AnalyticsService.TrackEvent(new AnalyticsLevelStart(levelName, isFarm, levelInitials));
            var result = new StringBuilder();
            result.Append($"\t\tpublic static void Track(");
            foreach (var param in eventParameters)
            {
                result.Append($"{param.Type} {StringUtils.ToLowerCamelCase(param.Name)}, ");
            }

            result.Remove(result.Length - 2, 2);
            result.AppendLine($") =>");
            result.Append($"\t\t\tAnalyticsService.TrackEvent(new {scriptName}(");
            foreach (var param in eventParameters)
            {
                result.Append($"{StringUtils.ToLowerCamelCase(param.Name)}, ");
            }

            result.Remove(result.Length - 2, 2);
            result.Append("));");

            return result.ToString();
        }

        #endregion

        #region Parameters

        private bool HasRepeatedParameterNames(List<EventParameter> eventParameters)
        {
            var names = new HashSet<string>();
            foreach (var param in eventParameters)
            {
                if (!names.Add(param.Name))
                {
                    return true;
                }
            }

            return false;
        }

        private string EditorParametersToPseudoCode(List<EventParameter> eventParameters)
        {
            var result = new StringBuilder();
            result.AppendLine("\t\t{");
            foreach (var param in eventParameters)
            {
                result.AppendLine($"\t\t\tnew EventParameter(\"{param.Name}\", \"{param.Type}\"),");
            }

            result.Append("\t\t};");

            return result.ToString();
        }

        private string ParameterNamesToPseudoCode(List<EventParameter> eventParameters)
        {
            var result = new StringBuilder();
            result.AppendLine("\t\t// Event Parameters Names");
            foreach (var param in eventParameters)
            {
                result.AppendLine($"\t\tpublic const string {param.Name.ToUpper()} = \"{param.Name}\";");
            }

            return result.ToString();
        }

        private string ParameterVariablesToPseudoCode(List<EventParameter> eventParameters)
        {
            var result = new StringBuilder();
            result.AppendLine("\t\t// Event Parameters Variables");

            foreach (var param in eventParameters)
            {
                result.AppendLine($"\t\tprivate readonly {param.Type} _{StringUtils.ToLowerCamelCase(param.Name)};");
            }

            return result.ToString();
        }

        #endregion

        private void TryShowWindow(string newEventName)
        {
            if (_eventName != null && _eventName != newEventName)
            {
                var eventName = StringUtils.ToUpperCamelCase(_eventName);
                // Creamos el outfile del nuevo evento.
                var outfile = CreateAllScriptString(eventName);
                // Obtenemos el asset path del evento anterior.
                var folderPath = AnalyticsEventManager.EVENTS_FOLDER;
                var assetPath = $"{folderPath}/{_previousEventName}.cs";
                if (HasChanges(outfile, assetPath))
                {
                    if (!EditorUtility.DisplayDialog(
                            "Unsaved Changes",
                            "Your changes will be lost. Are you sure you want to continue?",
                            "Yes", "No"))
                        return;
                }
            }

            IsOpen = true;
            _eventName = newEventName;
            LoadParameters(newEventName);
        }

        private void ReOpenWindow()
        {
            var window = GetWindow<AnalyticsEventEditor>("Analytics Event Editor");
            window._eventName = _eventName;
            window._previousEventName = _previousEventName;
            window.LoadParameters(_previousEventName);
        }

        public static void ShowWindow(string eventName)
        {
            var window = GetWindow<AnalyticsEventEditor>("Analytics Event Editor");
            window.TryShowWindow(eventName);
        }

        public static void CheckCloseOnDeleteEvent(string deletedEventName)
        {
            var window = GetWindow<AnalyticsEventEditor>("Analytics Event Editor");
            var eventName = StringUtils.ToUpperCamelCase(window._eventName);
            // If the event being edited is the one being deleted and the window is opened, close the window
            if (eventName == deletedEventName)
                window.Close();
        }
    }
}
#endif