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

namespace Viva.Services.Analytics
{
    public class AnalyticsEventCreatorTool : EditorWindow
    {
        private string _eventName;
        private List<EventParameter> _eventParameters = new();
        private string[] _parameterTypes = new[] { "int", "string", "double" };

        private void OnEnable()
        {
            // Set the minimum size of the window
            minSize = new Vector2(600, 200);
        }

        private void OnGUI()
        {
            // Display the description of the tool
            GUILayout.Label(
                "Event Creator Tool for Firebase Analytics. Please, write all the string labels in default event format (level_start), " +
                "UpperCamelCase or normal words separated by spaces.",
                EditorStyles.wordWrappedLabel);

            // Display the event name field
            EditorGUILayout.BeginHorizontal();
            _eventName = EditorGUILayout.TextField("Event Name", _eventName);
            if (!string.IsNullOrEmpty(_eventName))
            {
                if (GUILayout.Button("Load Parameters"))
                {
                    LoadParameters();
                }
            }

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
                        EditorGUILayout.EndHorizontal();
                        break; // Exit the loop to avoid modifying the collection while iterating
                    }

                    EditorGUILayout.EndHorizontal();
                }
            }

            if (GUILayout.Button("Add Parameter"))
            {
                _eventParameters.Add(new EventParameter("NewParam", "int"));
            }

            if (GUILayout.Button("Create or Modify Event"))
            {
                CreateEvent();
            }
        }

        private void LoadParameters()
        {
            var scriptName = _eventName;
            scriptName = ToUpperCamelCase(scriptName);
            var folderPath = "Assets/VivaAnalytics/Events";
            var assetPath = folderPath + $"/{scriptName}.cs";
            // If the script already exists, load the parameters
            if (File.Exists(assetPath))
            {
                var fileContent = File.ReadAllText(assetPath);
                // Regular expression to find the event parameters
                var regex = new Regex(@"new EventParameter\(""(.+?)"", ""(.+?)""\)", RegexOptions.Singleline);
                var matches = regex.Matches(fileContent);

                if (matches.Count == 0)
                {
                    EditorUtility.DisplayDialog("No parameters found",
                        "No parameters found in the event script.", "Ok");
                    return;
                }

                _eventParameters.Clear(); // Clear existing parameters before loading new ones

                foreach (Match match in matches)
                {
                    if (match.Groups.Count != 3) continue; // Ensure there are two groups captured: name and type
                    var eventName = match.Groups[1].Value;
                    var type = match.Groups[2].Value;
                    _eventParameters.Add(new EventParameter(eventName, type));
                }
            }
            else
            {
                EditorUtility.DisplayDialog("Event not found",
                    "No event found with that name", "Ok");
            }
        }

        private void CreateEvent()
        {
            var scriptName = _eventName;
            // Check if the event name is empty
            if (string.IsNullOrEmpty(scriptName))
            {
                EditorUtility.DisplayDialog("Empty event name",
                    "The event name cannot be empty.", "Ok");
                return;
            }

            scriptName = ToUpperCamelCase(scriptName);

            var folderPath = "Assets/VivaAnalytics/Events";
            // Create the directory if it doesn't exist
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            var assetPath = folderPath + $"/{scriptName}.cs";

            // if the script already exists, ask the user if they want to overwrite it
            if (File.Exists(assetPath))
            {
                if (!EditorUtility.DisplayDialog(
                        "Event already exists",
                        "The event already exists. Do you want to overwrite it?", "Yes", "No"))
                {
                    Debug.Log("Cancelled event creation.");
                    return;
                }
            }

            if (_eventParameters.Count == 0)
            {
                EditorUtility.DisplayDialog("No parameters",
                    "The event must have at least one parameter.", "Ok");
                return;
            }

            CreateEventScript(scriptName, assetPath);
        }

        private void CreateEventScript(string scriptName, string assetPath)
        {
            Debug.Log($"Creating script: {scriptName} at {assetPath}");
            var eventParameters = _eventParameters;
            foreach (var param in eventParameters)
                param.Name = ToSnakeCase(param.Name);
            // Create the desired script
            using StreamWriter outfile = new StreamWriter(assetPath);
            outfile.WriteLine("using System.Collections.Generic;");
            outfile.WriteLine("");
            outfile.WriteLine("namespace Viva.Services.Analytics");
            outfile.WriteLine("{");
            outfile.WriteLine($"\tpublic class {scriptName} : IAnalyticsEvent");
            outfile.WriteLine("\t{");

            // Save the parameters if is Editor
            outfile.WriteLine("#if UNITY_EDITOR");
            outfile.WriteLine($"\t\tpublic List<EventParameter> eventParameters = new()");
            outfile.WriteLine(EditorParametersToPseudoCode(eventParameters));
            outfile.WriteLine("#endif");
            outfile.WriteLine("");

            // Create the event parameters
            outfile.WriteLine(ParameterNamesToPseudoCode(eventParameters));
            outfile.WriteLine(ParameterVariablesToPseudoCode(eventParameters));

            // Create the constructor
            outfile.WriteLine(CreateConstructor(scriptName, eventParameters));

            // Create the event key constant in snake case format
            var eventKey = ToSnakeCase(scriptName);
            outfile.WriteLine($"\t\tpublic string GetEventKey() => \"{eventKey}\";");
            outfile.WriteLine("");
            outfile.WriteLine(TrackingFieldsDictionaryToPseudoCode(eventParameters));
            outfile.WriteLine(TrackMethodToPseudoCode(scriptName, eventParameters));
            outfile.WriteLine("\t}");
            outfile.WriteLine("}");
            
            outfile.Close();
            
            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
        }

        #region Method Constructors

        private string CreateConstructor(string scriptName, List<EventParameter> parameters)
        {
            var result = new StringBuilder();
            result.AppendLine("\t\t// Constructor");
            result.Append($"\t\tprivate {scriptName}(");
            foreach (var param in parameters)
            {
                result.Append($"{param.Type} {ToLowerCamelCase(param.Name)}, ");
            }

            result.Remove(result.Length - 2, 2);
            result.AppendLine(")");
            result.AppendLine("\t\t{");
            foreach (var param in parameters)
            {
                result.AppendLine($"\t\t\t_{ToLowerCamelCase(param.Name)} = {ToLowerCamelCase(param.Name)};");
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
                result.AppendLine($"\t\t\t{{ {param.Name.ToUpper()}, _{ToLowerCamelCase(param.Name)} }},");
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
                result.Append($"{param.Type} {ToLowerCamelCase(param.Name)}, ");
            }

            result.Remove(result.Length - 2, 2);
            result.AppendLine($") =>");
            result.Append($"\t\t\tAnalyticsService.TrackEvent(new {scriptName}(");
            foreach (var param in eventParameters)
            {
                result.Append($"{ToLowerCamelCase(param.Name)}, ");
            }

            result.Remove(result.Length - 2, 2);
            result.Append("));");

            return result.ToString();
        }

        #endregion

        #region Parameters

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
                result.AppendLine($"\t\tprivate readonly {param.Type} _{ToLowerCamelCase(param.Name)};");
            }

            return result.ToString();
        }

        #endregion

        #region Utils

        private string ToUpperCamelCase(string stringToChange)
        {
            if (string.IsNullOrEmpty(stringToChange)) return stringToChange;

            // Replace spaces with underscores to handle both formats uniformly
            stringToChange = stringToChange.Replace(" ", "_");
            var words = stringToChange.Split('_');
            var result = new StringBuilder();

            foreach (var word in words)
            {
                if (!string.IsNullOrEmpty(word))
                {
                    result.Append(char.ToUpper(word[0]) + word.Substring(1).ToLower());
                }
            }

            return result.ToString();
        }

        private string ToLowerCamelCase(string snakeCaseString)
        {
            if (string.IsNullOrEmpty(snakeCaseString)) return snakeCaseString;

            var words = snakeCaseString.Split('_');
            var result = new StringBuilder(words[0].ToLower());

            for (int i = 1; i < words.Length; i++)
            {
                if (!string.IsNullOrEmpty(words[i]))
                {
                    result.Append(char.ToUpper(words[i][0]) + words[i].Substring(1).ToLower());
                }
            }

            return result.ToString();
        }

        private string ToSnakeCase(string stringToConvert)
        {
            if (string.IsNullOrEmpty(stringToConvert)) return stringToConvert;

            var result = new StringBuilder();
            result.Append(char.ToLower(stringToConvert[0]));

            for (int i = 1; i < stringToConvert.Length; i++)
            {
                // If the character is uppercase or a space, add an underscore
                if (char.IsUpper(stringToConvert[i]) || stringToConvert[i] == ' ')
                {
                    result.Append('_');
                }

                result.Append(char.ToLower(stringToConvert[i]));
            }

            return result.ToString().Replace(" ", "");
        }

        #endregion

        [MenuItem("Viva/Analytics/Event Creator")]
        public static void ShowWindow()
        {
            GetWindow<AnalyticsEventCreatorTool>("Analytics Event Creator");
        }
    }
}
#endif