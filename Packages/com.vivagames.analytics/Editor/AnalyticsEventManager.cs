using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Viva.Services.Utils;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Ventana Viva > Analytics > Event Manager: lista los eventos del proyecto, permite crearlos,
    /// editarlos y borrarlos, y ofrece los eventos estándar del catálogo que aún no estén importados.
    /// </summary>
    public class AnalyticsEventManager : EditorWindow
    {
        private class EventRow
        {
            public string SnakeName;
            public string ClassName;
            public string FilePath;
            public CatalogEvent CatalogEntry;
            public bool IsModified;
        }

        private readonly List<EventRow> _projectEvents = new List<EventRow>();
        private readonly List<CatalogEvent> _missingCatalogEvents = new List<CatalogEvent>();
        private bool _creatingEvent;
        private string _newEventName;
        private Vector2 _scrollPosition = Vector2.zero;

        /// <summary>Carpeta del proyecto donde se generan los eventos. Configurable en Viva > Analytics > Setup.</summary>
        public static string EventsFolder => AnalyticsEditorSettings.EventsFolder;

        [MenuItem("Viva/Analytics/Event Manager")]
        public static void ShowWindow()
        {
            GetWindow<AnalyticsEventManager>("Analytics Event Manager");
        }

        private void OnEnable()
        {
            minSize = new Vector2(520, 200);
            Reload();
        }

        private void OnFocus()
        {
            Reload();
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Event List", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                Reload();
            if (GUILayout.Button("Setup", GUILayout.Width(70)))
                AnalyticsSetupWindow.Open();
            EditorGUILayout.EndHorizontal();
            GUILayout.Label(EventsFolder, EditorStyles.miniLabel);

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical();
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));

            if (_projectEvents.Count == 0)
                GUILayout.Label("No events found.", EditorStyles.centeredGreyMiniLabel);
            else
                DisplayEvents();

            DisplayMissingCatalogEvents();

            EditorGUILayout.EndScrollView();

            GUILayout.Space(10);

            if (_creatingEvent)
            {
                EditorGUILayout.LabelField("Enter the name of the new event in snake_case:");
                EditorGUILayout.BeginHorizontal();
                _newEventName = EditorGUILayout.TextField(_newEventName);
                GUILayout.FlexibleSpace();
                bool enterPressed = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;
                if (GUILayout.Button("Create") || enterPressed)
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
            // Se recorre una copia porque las acciones recargan la lista.
            foreach (var row in _projectEvents.ToArray())
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(row.SnakeName, GUILayout.Width(200));

                string tag = row.CatalogEntry == null ? string.Empty : (row.IsModified ? "standard (modified)" : "standard");
                GUILayout.Label(tag, EditorStyles.miniLabel, GUILayout.Width(120));

                if (GUILayout.Button("Edit"))
                    EditEvent(row.SnakeName);

                if (row.CatalogEntry != null && row.IsModified && GUILayout.Button("Restore"))
                {
                    if (EditorUtility.DisplayDialog("Restore standard event",
                            $"Overwrite {row.SnakeName} with the standard version from the catalog? Your changes will be lost.",
                            "Restore", "Cancel"))
                    {
                        AnalyticsEventEditor.CheckCloseOnDeleteEvent(row.ClassName);
                        AnalyticsEventCatalog.Import(row.CatalogEntry);
                        GUI.FocusControl(null);
                        Reload();
                    }
                }

                if (GUILayout.Button("Delete"))
                {
                    if (EditorUtility.DisplayDialog("Delete Event",
                            $"Are you sure you want to delete the {row.SnakeName} event?", "Yes", "No"))
                    {
                        DeleteEvent(row);
                    }
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void DisplayMissingCatalogEvents()
        {
            if (_missingCatalogEvents.Count == 0) return;

            GUILayout.Space(10);
            GUILayout.Label("Standard events not in this project", EditorStyles.boldLabel);

            foreach (var catalogEvent in _missingCatalogEvents.ToArray())
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(catalogEvent.name, GUILayout.Width(200));
                GUILayout.Label(catalogEvent.description ?? string.Empty, EditorStyles.miniLabel);
                if (GUILayout.Button("Add", GUILayout.Width(60)))
                {
                    AnalyticsEventCatalog.Import(catalogEvent);
                    GUI.FocusControl(null);
                    Reload();
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DeleteEvent(EventRow row)
        {
            AnalyticsEventEditor.CheckCloseOnDeleteEvent(row.ClassName);

            // DeleteAsset se encarga también del .meta. Si Unity aún no lo tenía importado, se borra a mano.
            if (!AssetDatabase.DeleteAsset(row.FilePath))
            {
                if (File.Exists(row.FilePath)) File.Delete(row.FilePath);
                var metaPath = row.FilePath + ".meta";
                if (File.Exists(metaPath)) File.Delete(metaPath);
            }

            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
            GUI.FocusControl(null);
            Reload();
        }

        private static void EditEvent(string eventName)
        {
            AnalyticsEventEditor.ShowWindow(eventName);
            GUI.FocusControl(null);
        }

        private void Reload()
        {
            _projectEvents.Clear();
            _missingCatalogEvents.Clear();

            var folder = EventsFolder;
            if (Directory.Exists(folder))
            {
                foreach (var file in Directory.GetFiles(folder, "*.cs").OrderBy(f => f))
                {
                    var className = Path.GetFileNameWithoutExtension(file);
                    var snakeName = StringUtils.ToSnakeCase(className);
                    var catalogEntry = AnalyticsEventCatalog.Find(snakeName);

                    _projectEvents.Add(new EventRow
                    {
                        SnakeName = snakeName,
                        ClassName = className,
                        FilePath = $"{folder}/{className}.cs",
                        CatalogEntry = catalogEntry,
                        IsModified = catalogEntry != null && AnalyticsEventCatalog.IsModified(catalogEntry)
                    });
                }
            }

            _missingCatalogEvents.AddRange(AnalyticsEventCatalog.Missing());
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
            string folderPath = EventsFolder;
            string filePath = $"{folderPath}/{upperCamelCaseName}.cs";

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            if (File.Exists(filePath))
            {
                EditorUtility.DisplayDialog("Event already exists", "An event with this name already exists.", "OK");
                return;
            }

            File.WriteAllText(filePath, AnalyticsEventCodeGenerator.GenerateWithoutParameters(upperCamelCaseName));

            _newEventName = string.Empty;
            _creatingEvent = false;
            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
            Reload();

            if (!AnalyticsEventEditor.IsOpen)
                EditEvent(snakeCaseName);
        }
    }
}
