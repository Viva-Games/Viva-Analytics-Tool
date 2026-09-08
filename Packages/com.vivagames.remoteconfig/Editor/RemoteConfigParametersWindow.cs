using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Viva.Services.RemoteConfig.Editor
{
    /// <summary>
    /// Ventana Viva > Remote Config > Parameters: lista de parámetros (clave, tipo, valor por defecto
    /// validado, descripción). Save escribe el JSON del proyecto y regenera RemoteConfigParameters.cs.
    /// </summary>
    public class RemoteConfigParametersWindow : EditorWindow
    {
        private const float TYPE_WIDTH = 70f;
        private const float LABEL_WIDTH = 74f;
        private const float PADDING = 4f;
        private const int JSON_LINES = 3;

        private static readonly Color ErrorTint = new Color(1f, 0.25f, 0.25f, 0.12f);
        private static readonly Color ErrorText = new Color(1f, 0.45f, 0.45f);
        private static readonly Color OkColor = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color WarningColor = new Color(1f, 0.72f, 0.4f);

        private readonly List<RemoteConfigParameterDefinition> _parameters = new List<RemoteConfigParameterDefinition>();
        private string _savedSnapshot = string.Empty;
        private ReorderableList _list;
        private ValidationResult _validation = new ValidationResult();
        private GeneratedScriptState _generatedState;
        private Vector2 _scroll;
        private bool _discardOnClose;

        [MenuItem("Viva/Remote Config/Parameters")]
        public static void Open()
        {
            var window = GetWindow<RemoteConfigParametersWindow>("Remote Config");
            window.minSize = new Vector2(640, 360);
        }

        private void OnEnable()
        {
            Reload();
        }

        private void OnFocus()
        {
            // Si el JSON cambió fuera (git, otro editor) y aquí no hay cambios, se recarga.
            if (!IsDirty) Reload();
            else RefreshGeneratedState();
        }

        private void OnDestroy()
        {
            if (_discardOnClose || !IsDirty) return;

            if (EditorUtility.DisplayDialog("Unsaved Remote Config parameters",
                    "You have unsaved changes in the parameter list. Save them?", "Save", "Discard"))
            {
                if (!TrySave())
                {
                    // No se pudo guardar (errores de validación): se vuelve a abrir la ventana con los cambios.
                    var pending = CloneAll(_parameters);
                    EditorApplication.delayCall += () =>
                    {
                        var window = GetWindow<RemoteConfigParametersWindow>("Remote Config");
                        window._parameters.Clear();
                        window._parameters.AddRange(pending);
                        window.Validate();
                    };
                }
            }
        }

        #region GUI

        private void OnGUI()
        {
            DrawToolbar();
            DrawStatus();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            EditorGUI.BeginChangeCheck();
            _list.DoLayoutList();
            if (EditorGUI.EndChangeCheck()) Validate();
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            bool dirty = IsDirty;

            using (new EditorGUI.DisabledScope(!dirty || !_validation.IsValid))
            {
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    TrySave();
                    GUI.FocusControl(null);
                }
            }
            using (new EditorGUI.DisabledScope(!dirty))
            {
                if (GUILayout.Button("Revert", EditorStyles.toolbarButton, GUILayout.Width(70)))
                {
                    Reload();
                    GUI.FocusControl(null);
                }
            }
            if (GUILayout.Button("Import from code", EditorStyles.toolbarButton, GUILayout.Width(120)))
                ImportFromCode();

            GUILayout.FlexibleSpace();
            GUILayout.Label(dirty ? "unsaved changes" : "saved", EditorStyles.miniLabel);
            if (GUILayout.Button("Setup", EditorStyles.toolbarButton, GUILayout.Width(60)))
                RemoteConfigSetupWindow.Open();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawStatus()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(RemoteConfigEditorSettings.ParametersFilePath, EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            var previous = GUI.color;
            switch (_generatedState)
            {
                case GeneratedScriptState.UpToDate:
                    GUI.color = OkColor;
                    GUILayout.Label("Generated class up to date", EditorStyles.miniLabel);
                    break;
                case GeneratedScriptState.Outdated:
                    GUI.color = WarningColor;
                    GUILayout.Label("Generated class outdated", EditorStyles.miniLabel);
                    break;
                default:
                    GUI.color = WarningColor;
                    GUILayout.Label("Generated class missing", EditorStyles.miniLabel);
                    break;
            }
            GUI.color = previous;

            if (_generatedState != GeneratedScriptState.UpToDate)
            {
                if (GUILayout.Button("Regenerate", EditorStyles.miniButton, GUILayout.Width(80)))
                {
                    RemoteConfigProjectFiles.RegenerateScript();
                    RefreshGeneratedState();
                }
            }
            EditorGUILayout.EndHorizontal();

            foreach (var warning in _validation.Warnings)
            {
                if (warning.Index < 0) EditorGUILayout.HelpBox(warning.Message, MessageType.Warning);
            }
        }

        private void DrawFooter()
        {
            if (!_validation.IsValid)
            {
                EditorGUILayout.HelpBox($"{_validation.Errors.Count} error(s). Fix the rows marked in red to save.", MessageType.Error);
            }
            EditorGUILayout.HelpBox(
                "Keys must match the Firebase console exactly (they are never renamed). Save writes the JSON and regenerates " +
                "RemoteConfigParameters, so the game reads RemoteConfigParameters.<Name> without typing keys or defaults.",
                MessageType.None);
        }

        private void BuildList()
        {
            _list = new ReorderableList(_parameters, typeof(RemoteConfigParameterDefinition), true, true, true, true)
            {
                drawHeaderCallback = rect =>
                {
                    float keyWidth = (rect.width - TYPE_WIDTH - LABEL_WIDTH) * 0.35f;
                    GUI.Label(new Rect(rect.x + 14, rect.y, keyWidth, rect.height), "Key", EditorStyles.miniBoldLabel);
                    GUI.Label(new Rect(rect.x + 14 + keyWidth + PADDING, rect.y, TYPE_WIDTH, rect.height), "Type", EditorStyles.miniBoldLabel);
                    GUI.Label(new Rect(rect.x + 14 + keyWidth + TYPE_WIDTH + 2 * PADDING, rect.y, 200, rect.height), "Default value", EditorStyles.miniBoldLabel);
                },
                elementHeightCallback = ElementHeight,
                drawElementCallback = DrawElement,
                onAddCallback = list =>
                {
                    string type = _parameters.Count > 0 ? _parameters[_parameters.Count - 1].type : RemoteConfigParameterTypes.INT;
                    _parameters.Add(new RemoteConfigParameterDefinition("newParameter", type, DefaultValueFor(type)));
                    Validate();
                },
                onRemoveCallback = list =>
                {
                    _parameters.RemoveAt(list.index);
                    Validate();
                },
                onReorderCallback = list => Validate()
            };
        }

        private float ElementHeight(int index)
        {
            float line = EditorGUIUtility.singleLineHeight;
            float height = 2 * line + 3 * PADDING;
            if (index < _parameters.Count && _parameters[index].type == RemoteConfigParameterTypes.JSON) height += (JSON_LINES - 1) * line;
            if (_validation.HasErrorAt(index)) height += 2 * line;
            return height;
        }

        private void DrawElement(Rect rect, int index, bool active, bool focused)
        {
            if (index >= _parameters.Count) return;
            var parameter = _parameters[index];
            bool hasError = _validation.HasErrorAt(index);
            if (hasError) EditorGUI.DrawRect(rect, ErrorTint);

            float line = EditorGUIUtility.singleLineHeight;
            float y = rect.y + PADDING;
            float keyWidth = (rect.width - TYPE_WIDTH - LABEL_WIDTH) * 0.35f;
            float valueX = rect.x + keyWidth + TYPE_WIDTH + 2 * PADDING;
            float valueWidth = rect.width - keyWidth - TYPE_WIDTH - 2 * PADDING;

            parameter.key = EditorGUI.TextField(new Rect(rect.x, y, keyWidth, line), parameter.key);

            int typeIndex = Array.IndexOf(RemoteConfigParameterTypes.All, parameter.type);
            if (typeIndex < 0) typeIndex = 0;
            int newTypeIndex = EditorGUI.Popup(new Rect(rect.x + keyWidth + PADDING, y, TYPE_WIDTH, line), typeIndex, RemoteConfigParameterTypes.All);
            if (newTypeIndex != typeIndex)
            {
                parameter.type = RemoteConfigParameterTypes.All[newTypeIndex];
                if (parameter.type == RemoteConfigParameterTypes.BOOL && parameter.defaultValue != "true") parameter.defaultValue = "false";
            }

            float valueHeight = line;
            switch (parameter.type)
            {
                case RemoteConfigParameterTypes.BOOL:
                    bool current = parameter.defaultValue == "true";
                    bool next = EditorGUI.Toggle(new Rect(valueX, y, valueWidth, line), current);
                    if (next != current) parameter.defaultValue = next ? "true" : "false";
                    break;
                case RemoteConfigParameterTypes.JSON:
                    valueHeight = JSON_LINES * line;
                    parameter.defaultValue = EditorGUI.TextArea(new Rect(valueX, y, valueWidth, valueHeight), parameter.defaultValue ?? string.Empty, EditorStyles.textArea);
                    break;
                default:
                    parameter.defaultValue = EditorGUI.TextField(new Rect(valueX, y, valueWidth, line), parameter.defaultValue ?? string.Empty);
                    break;
            }

            y += valueHeight + PADDING;
            GUI.Label(new Rect(rect.x, y, LABEL_WIDTH, line), "Description", EditorStyles.miniLabel);
            parameter.description = EditorGUI.TextField(new Rect(rect.x + LABEL_WIDTH, y, rect.width - LABEL_WIDTH, line), parameter.description ?? string.Empty);

            if (hasError)
            {
                y += line + PADDING;
                var previous = GUI.color;
                GUI.color = ErrorText;
                GUI.Label(new Rect(rect.x, y, rect.width, 2 * line), _validation.ErrorsAt(index), EditorStyles.wordWrappedMiniLabel);
                GUI.color = previous;
            }
        }

        #endregion

        #region Actions

        private void Reload()
        {
            _parameters.Clear();
            _parameters.AddRange(RemoteConfigProjectFiles.LoadParameters());
            _savedSnapshot = RemoteConfigParameterFile.Serialize(_parameters);
            if (_list == null) BuildList();
            Validate();
            RefreshGeneratedState();
        }

        private void RefreshGeneratedState()
        {
            _generatedState = RemoteConfigProjectFiles.GetGeneratedScriptState();
        }

        private void Validate()
        {
            _validation = RemoteConfigValidator.Validate(_parameters);
        }

        private bool IsDirty => RemoteConfigParameterFile.Serialize(_parameters) != _savedSnapshot;

        private bool TrySave()
        {
            Validate();
            if (!_validation.IsValid)
            {
                EditorUtility.DisplayDialog("Remote Config", $"Fix the {_validation.Errors.Count} error(s) marked in red before saving.", "OK");
                return false;
            }

            if (!RemoteConfigProjectFiles.SaveParametersAndGenerate(_parameters)) return false;
            _savedSnapshot = RemoteConfigParameterFile.Serialize(_parameters);
            RefreshGeneratedState();
            return true;
        }

        private void ImportFromCode()
        {
            var result = LegacyRemoteConfigImporter.ScanCode("Assets",
                RemoteConfigEditorSettings.GeneratedScriptPath, RemoteConfigEditorSettings.InitScriptPath);

            var existing = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parameter in _parameters) existing.Add(parameter.key);

            int added = 0, skipped = 0;
            foreach (var imported in result.Parameters)
            {
                if (existing.Contains(imported.Key))
                {
                    skipped++;
                    continue;
                }
                _parameters.Add(new RemoteConfigParameterDefinition(imported.Key, imported.Type, imported.DefaultValue, imported.Note));
                added++;
            }
            Validate();

            var message = $"Scanned {result.ScannedFiles} script(s) and found {result.CallSites} RemoteConfigManager call(s) with {result.Parameters.Count} key(s).\n\n";
            message += $"Added {added} parameter(s) to the list";
            if (skipped > 0) message += $", {skipped} already there";
            message += ".\n";
            if (result.WithoutLiteralDefault > 0)
                message += $"{result.WithoutLiteralDefault} key(s) have no literal default in the code: type their value (see the description).\n";
            if (result.Unresolved.Count > 0)
                message += "Could not resolve: " + string.Join("; ", result.Unresolved) + "\n";
            message += "\nNothing is written until you press Save.";
            EditorUtility.DisplayDialog("Import from code", message, "OK");
        }

        private static string DefaultValueFor(string type)
        {
            switch (type)
            {
                case RemoteConfigParameterTypes.INT:
                case RemoteConfigParameterTypes.LONG:
                    return "0";
                case RemoteConfigParameterTypes.FLOAT:
                case RemoteConfigParameterTypes.DOUBLE:
                    return "0.0";
                case RemoteConfigParameterTypes.BOOL:
                    return "false";
                case RemoteConfigParameterTypes.JSON:
                    return "{}";
                default:
                    return string.Empty;
            }
        }

        private static List<RemoteConfigParameterDefinition> CloneAll(List<RemoteConfigParameterDefinition> parameters)
        {
            var clones = new List<RemoteConfigParameterDefinition>(parameters.Count);
            foreach (var parameter in parameters) clones.Add(parameter.Clone());
            return clones;
        }

        #endregion

        /// <summary>Abre la ventana con parámetros añadidos (desde Setup). Los cambios quedan sin guardar.</summary>
        public static void OpenWith(IEnumerable<RemoteConfigParameterDefinition> extra)
        {
            Open();
            var window = GetWindow<RemoteConfigParametersWindow>("Remote Config");
            foreach (var parameter in extra) window._parameters.Add(parameter);
            window.Validate();
        }
    }
}
