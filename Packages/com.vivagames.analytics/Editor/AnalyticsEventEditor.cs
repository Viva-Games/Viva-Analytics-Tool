using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditorInternal;
using UnityEngine;
using Viva.Services.Utils;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Ventana de edición de un evento: nombre y lista de parámetros.
    /// El fichero .cs se genera con <see cref="AnalyticsEventCodeGenerator"/>.
    /// </summary>
    public class AnalyticsEventEditor : EditorWindow
    {
        public static bool IsOpen;

        private string _eventName;
        private string _previousEventName;
        private readonly List<EventParameter> _eventParameters = new List<EventParameter>();
        private AnalyticsTargets _targets = AnalyticsTargets.Firebase;
        private ReorderableList _reorderableList;

        private static string EventsFolder => AnalyticsEditorSettings.EventsFolder;

        private void OnEnable()
        {
            minSize = new Vector2(600, 200);
            LoadParameters(_eventName);

            _reorderableList = new ReorderableList(_eventParameters, typeof(EventParameter), true, false, true, true);
            _reorderableList.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                var param = _eventParameters[index];
                var types = AnalyticsEventCodeGenerator.ParameterTypes;
                float halfWidth = rect.width / 2f;
                rect.y += 2;
                rect.height = EditorGUIUtility.singleLineHeight;
                param.Name = EditorGUI.TextField(new Rect(rect.x, rect.y, halfWidth - 5, rect.height), param.Name);
                int currentIndex = Array.IndexOf(types, param.Type);
                if (currentIndex == -1) currentIndex = 0;
                param.Type = types[EditorGUI.Popup(new Rect(rect.x + halfWidth, rect.y, halfWidth - 5, rect.height), currentIndex, types)];
            };
            _reorderableList.onAddCallback = list =>
            {
                var previousType = _eventParameters.LastOrDefault()?.Type ?? AnalyticsEventCodeGenerator.ParameterTypes[0];
                _eventParameters.Add(new EventParameter("new_param", previousType));
            };
            _reorderableList.onRemoveCallback = list => _eventParameters.RemoveAt(list.index);
        }

        private void OnDisable()
        {
            IsOpen = false;
        }

        private void OnDestroy()
        {
            if (string.IsNullOrEmpty(_previousEventName)) return;

            var outfile = CreateScript(StringUtils.ToUpperCamelCase(_eventName));
            var assetPath = $"{EventsFolder}/{_previousEventName}.cs";
            if (!HasChanges(outfile, assetPath)) return;

            if (!EditorUtility.DisplayDialog(
                    "Unsaved Changes",
                    "Your changes will be lost. Are you sure you want to continue?",
                    "Yes", "No"))
            {
                // Se vuelve a abrir la ventana con los cambios que había para no perderlos.
                EditorApplication.delayCall += ReOpenWindow;
            }
        }

        private void OnGUI()
        {
            GUILayout.Label(
                "Please, write all the string labels in default event format (level_start), " +
                "UpperCamelCase or normal words separated by spaces.",
                EditorStyles.wordWrappedLabel);

            EditorGUILayout.BeginHorizontal();
            _eventName = EditorGUILayout.TextField("Event Name", _eventName);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Parameters");
            if (_reorderableList != null)
            {
                _reorderableList.DoLayoutList();
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Clear Parameters"))
            {
                _eventParameters.Clear();
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();

            DrawTargets();

            if (GUILayout.Button("Save"))
            {
                SaveEvent();
                GUI.FocusControl(null);
            }
        }

        /// <summary>
        /// Destinos del evento: Firebase siempre; Facebook y Singular solo si se marcan. Avisa de los límites
        /// de cada plataforma (nombre de 32 caracteres en Singular; 40 y 25 parámetros en Facebook).
        /// </summary>
        private void DrawTargets()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Send to", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ToggleLeft("Firebase", true, GUILayout.Width(90));
            }
            bool facebook = EditorGUILayout.ToggleLeft("Facebook", _targets.HasFlag(AnalyticsTargets.Facebook), GUILayout.Width(90));
            bool singular = EditorGUILayout.ToggleLeft("Singular", _targets.HasFlag(AnalyticsTargets.Singular), GUILayout.Width(90));
            EditorGUILayout.EndHorizontal();

            _targets = AnalyticsTargets.Firebase
                       | (facebook ? AnalyticsTargets.Facebook : AnalyticsTargets.None)
                       | (singular ? AnalyticsTargets.Singular : AnalyticsTargets.None);

            string eventKey = StringUtils.ToSnakeCase(_eventName ?? string.Empty);
            if (facebook)
            {
                var names = new List<string>();
                foreach (var parameter in _eventParameters) names.Add(StringUtils.ToSnakeCase(parameter.Name));
                foreach (var issue in Facebook.FacebookEventConverter.Validate(eventKey, names))
                    EditorGUILayout.HelpBox("Facebook: " + issue, MessageType.Warning);
            }
            if (singular)
            {
                string issue = Singular.SingularEventConverter.ValidateName(eventKey);
                if (issue != null) EditorGUILayout.HelpBox("Singular: " + issue, MessageType.Warning);
            }
        }

        private void LoadParameters(string scriptName)
        {
            scriptName = StringUtils.ToUpperCamelCase(scriptName);
            var assetPath = $"{EventsFolder}/{scriptName}.cs";
            _targets = AnalyticsTargets.Firebase;
            if (!File.Exists(assetPath)) return;

            _previousEventName = scriptName;
            var fileContent = File.ReadAllText(assetPath);
            _targets = EventTargetsCodec.Parse(fileContent);

            // Los parámetros se leen de la lista eventParameters que lleva cada evento generado.
            var regex = new Regex(@"new EventParameter\(""(.+?)"", ""(.+?)""\)", RegexOptions.Singleline);
            var matches = regex.Matches(fileContent);

            _eventParameters.Clear();
            foreach (Match match in matches)
            {
                if (match.Groups.Count != 3) continue;
                _eventParameters.Add(new EventParameter(match.Groups[1].Value, match.Groups[2].Value));
            }
        }

        private void SaveEvent()
        {
            if (string.IsNullOrEmpty(_eventName))
            {
                EditorUtility.DisplayDialog("Empty event name", "The event name cannot be empty.", "Ok");
                return;
            }

            var scriptName = StringUtils.ToUpperCamelCase(_eventName);
            SaveEventScript(scriptName, $"{EventsFolder}/{scriptName}.cs");
        }

        private void SaveEventScript(string scriptName, string assetPath)
        {
            if (HasRepeatedParameterNames(GetSnakeCaseEventParameters()))
            {
                EditorUtility.DisplayDialog("Repeated parameter names", "The event parameters must have unique names.", "Ok");
                return;
            }

            var outfile = CreateScript(scriptName);
            if (!HasChanges(outfile, assetPath))
            {
                EditorUtility.DisplayDialog("No changes", "No changes were made.", "Ok");
                return;
            }

            if (!EditorUtility.DisplayDialog("Save Confirmation", "Do you want to overwrite the event?", "Yes", "No"))
            {
                Debug.Log("Cancelled event creation.");
                return;
            }

            Debug.Log($"Modifying script: {scriptName} at {assetPath}");

            var folder = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                Directory.CreateDirectory(folder);
            File.WriteAllText(assetPath, outfile);
            _previousEventName = scriptName;

            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
        }

        private static bool HasChanges(string outfile, string previousAssetPath)
        {
            if (!File.Exists(previousAssetPath)) return true;

            var currentFile = File.ReadAllText(previousAssetPath);
            return StringUtils.Normalize(outfile) != StringUtils.Normalize(currentFile);
        }

        private string CreateScript(string scriptName)
        {
            return AnalyticsEventCodeGenerator.Generate(scriptName, _eventParameters, _targets);
        }

        /// <summary>Normaliza a snake_case los nombres de la lista (se reflejan en la ventana) y la devuelve.</summary>
        private List<EventParameter> GetSnakeCaseEventParameters()
        {
            foreach (var param in _eventParameters)
                param.Name = StringUtils.ToSnakeCase(param.Name);
            return _eventParameters;
        }

        private static bool HasRepeatedParameterNames(List<EventParameter> eventParameters)
        {
            var names = new HashSet<string>();
            foreach (var param in eventParameters)
            {
                if (!names.Add(param.Name)) return true;
            }
            return false;
        }

        private void TryShowWindow(string newEventName)
        {
            if (!string.IsNullOrEmpty(_eventName) && _eventName != newEventName && !string.IsNullOrEmpty(_previousEventName))
            {
                var outfile = CreateScript(StringUtils.ToUpperCamelCase(_eventName));
                var assetPath = $"{EventsFolder}/{_previousEventName}.cs";
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
            _previousEventName = null;
            _eventParameters.Clear();
            LoadParameters(newEventName);
        }

        private void ReOpenWindow()
        {
            var window = GetWindow<AnalyticsEventEditor>("Analytics Event Editor");
            window._eventName = _eventName;
            window._previousEventName = _previousEventName;
            window._targets = _targets;
            window._eventParameters.Clear();
            foreach (var param in _eventParameters)
                window._eventParameters.Add(new EventParameter(param.Name, param.Type));
            IsOpen = true;
        }

        public static void ShowWindow(string eventName)
        {
            var window = GetWindow<AnalyticsEventEditor>("Analytics Event Editor");
            window.TryShowWindow(eventName);
        }

        /// <summary>
        /// Cierra el editor si está mostrando el evento que se va a borrar o sobrescribir.
        /// </summary>
        public static void CheckCloseOnDeleteEvent(string deletedEventName)
        {
            if (!IsOpen) return;

            var window = GetWindow<AnalyticsEventEditor>("Analytics Event Editor");
            var eventName = StringUtils.ToUpperCamelCase(window._eventName);
            if (eventName == deletedEventName)
            {
                window._previousEventName = null; // el fichero deja de existir: no hay nada que comparar al cerrar
                window.Close();
            }
        }
    }
}
