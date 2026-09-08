using System;
using UnityEditor;
using UnityEngine;

namespace Viva.Services.RemoteConfig.Editor
{
    /// <summary>
    /// Ventana Viva > Remote Config > Setup: estado del proyecto (JSON de parámetros, clase generada, SDK,
    /// inicialización, manager antiguo), ajustes de rutas y configuración inicial.
    /// </summary>
    public class RemoteConfigSetupWindow : EditorWindow
    {
        private static readonly Color OkColor = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color WarningColor = new Color(1f, 0.72f, 0.4f);

        private string _parametersPathDraft;
        private string _generatedPathDraft;
        private string _initPathDraft;
        private string _legacyManagerPath;
        private bool _legacyIsWrapper;
        private string _analyticsInitPath;
        private bool _analyticsInitInitializes;
        private Vector2 _scroll;

        [MenuItem("Viva/Remote Config/Setup")]
        public static void Open()
        {
            var window = GetWindow<RemoteConfigSetupWindow>("Remote Config Setup");
            window.minSize = new Vector2(600, 440);
        }

        /// <summary>
        /// Primer arranque del paquete en un proyecto: ofrece la configuración inicial (y la migración del
        /// manager antiguo si lo hay) y abre la ventana.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void OpenOnFirstRun()
        {
            if (Application.isBatchMode) return;
            if (RemoteConfigEditorSettings.Instance.setupShown) return;

            EditorApplication.delayCall += () =>
            {
                RemoteConfigEditorSettings.Instance.setupShown = true;
                RemoteConfigEditorSettings.Instance.Save();
                OfferInitialSetup();
                Open();
            };
        }

        private static void OfferInitialSetup()
        {
            string legacy = LegacyRemoteConfigImporter.FindLegacyManager();
            bool legacyIsWrapper = legacy != null && LegacyRemoteConfigImporter.IsCompatibilityWrapper(legacy);

            var message = "Viva Remote Config is installed. The initial setup will:\n\n";
            message += "1. Create " + RemoteConfigEditorSettings.ParametersFilePath + ", where you declare the parameters and their defaults.\n";
            message += "2. Generate " + RemoteConfigEditorSettings.GeneratedScriptPath + ", the RemoteConfigParameters class the game reads.\n";
            message += "3. Generate " + RemoteConfigEditorSettings.InitScriptPath + ": add the RemoteConfigInit component to the first scene.\n";
            if (legacy != null && !legacyIsWrapper)
            {
                message += "\nAn old RemoteConfigManager was found in " + legacy + ". In the Parameters window, Import from code fills the list " +
                           "from its calls; then Migrate in Setup turns it into a wrapper so the existing code keeps compiling.\n";
            }
            message += "\nYou can also run it later from Viva > Remote Config > Setup.";

            if (EditorUtility.DisplayDialog("Set up Viva Remote Config", message, "Run setup", "Later"))
                RunFullSetup();
        }

        private void OnEnable()
        {
            _parametersPathDraft = RemoteConfigEditorSettings.ParametersFilePath;
            _generatedPathDraft = RemoteConfigEditorSettings.GeneratedScriptPath;
            _initPathDraft = RemoteConfigEditorSettings.InitScriptPath;
            RefreshProjectState();
        }

        private void OnFocus()
        {
            RefreshProjectState();
            Repaint();
        }

        private void RefreshProjectState()
        {
            _legacyManagerPath = LegacyRemoteConfigImporter.FindLegacyManager();
            _legacyIsWrapper = _legacyManagerPath != null && LegacyRemoteConfigImporter.IsCompatibilityWrapper(_legacyManagerPath);
            _analyticsInitPath = RemoteConfigProjectFiles.FindAnalyticsInitScript();
            _analyticsInitInitializes = _analyticsInitPath != null && RemoteConfigProjectFiles.AnalyticsInitInitializesRemoteConfig(_analyticsInitPath);
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            GUILayout.Label($"Viva Remote Config {RemoteConfigPackage.Version}", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            DrawLegacySection();
            DrawStatusSection();
            DrawSettingsSection();
            DrawHelpSection();

            EditorGUILayout.EndScrollView();
        }

        #region Status

        private void DrawStatusSection()
        {
            GUILayout.Label("Project status", EditorStyles.boldLabel);

            bool parametersExist = RemoteConfigProjectFiles.ParametersFileExists;
            DrawRow(parametersExist, "Parameters file", parametersExist ? RemoteConfigEditorSettings.ParametersFilePath : "missing",
                parametersExist ? "Open" : "Create", () =>
                {
                    if (!parametersExist) RemoteConfigProjectFiles.CreateParametersFileIfMissing();
                    else RemoteConfigParametersWindow.Open();
                });

            var generatedState = RemoteConfigProjectFiles.GetGeneratedScriptState();
            string generatedDetail;
            switch (generatedState)
            {
                case GeneratedScriptState.UpToDate: generatedDetail = RemoteConfigEditorSettings.GeneratedScriptPath; break;
                case GeneratedScriptState.Outdated: generatedDetail = "outdated: the JSON changed after the last generation"; break;
                default: generatedDetail = "missing"; break;
            }
            DrawRow(generatedState == GeneratedScriptState.UpToDate, "RemoteConfigParameters.cs", generatedDetail,
                generatedState == GeneratedScriptState.UpToDate ? null : "Generate", () => RemoteConfigProjectFiles.RegenerateScript());

            bool sdkPresent = RemoteConfigSdkDetector.IsSdkPresent();
            bool defineEnabled = RemoteConfigSdkDetector.IsDefineEnabled();
            bool appDefineEnabled = RemoteConfigSdkDetector.IsFirebaseAppDefineEnabled();
            string sdkDetail;
            if (sdkPresent && defineEnabled && appDefineEnabled)
                sdkDetail = "detected (" + RemoteConfigSdkDetector.DEFINE + " and VIVA_FIREBASE enabled)";
            else if (sdkPresent && defineEnabled)
                sdkDetail = "detected, but Viva Core has not enabled VIVA_FIREBASE yet (Firebase.App.dll missing, or wait for the recompilation)";
            else if (sdkPresent)
                sdkDetail = "detected, enabling " + RemoteConfigSdkDetector.DEFINE + "...";
            else
                sdkDetail = "not found. Import the Firebase Remote Config SDK; until then the in-app defaults are used.";
            DrawRow(sdkPresent && defineEnabled && appDefineEnabled, "Firebase Remote Config SDK", sdkDetail, "Re-check", () =>
            {
                RemoteConfigSdkDetector.Refresh();
                Viva.Core.Editor.FirebaseAppDetector.Refresh();
            });

            bool initExists = RemoteConfigProjectFiles.InitScriptExists;
            string initDetail;
            if (initExists)
                initDetail = RemoteConfigEditorSettings.InitScriptPath + ". Add the RemoteConfigInit component to the first scene.";
            else if (_analyticsInitInitializes)
                initDetail = "initialized from " + _analyticsInitPath + " (OnFirebaseReady). That works; RemoteConfigInit is optional.";
            else
                initDetail = "missing";
            DrawRow(initExists || _analyticsInitInitializes, "Initialization", initDetail,
                initExists ? null : "Generate", () =>
                {
                    RemoteConfigProjectFiles.GenerateInitScript();
                });

            EditorGUILayout.Space();
            if (GUILayout.Button("Run full setup", GUILayout.Height(28)))
                RunFullSetup();
            EditorGUILayout.Space();
        }

        private static void RunFullSetup()
        {
            RemoteConfigProjectFiles.CreateParametersFileIfMissing();
            if (RemoteConfigProjectFiles.GetGeneratedScriptState() != GeneratedScriptState.UpToDate)
                RemoteConfigProjectFiles.RegenerateScript();
            RemoteConfigProjectFiles.GenerateInitScript();
        }

        private static void DrawRow(bool ok, string label, string detail, string buttonLabel, Action action)
        {
            EditorGUILayout.BeginHorizontal();

            var previousColor = GUI.color;
            GUI.color = ok ? OkColor : WarningColor;
            GUILayout.Label(ok ? "OK" : "!", EditorStyles.boldLabel, GUILayout.Width(24));
            GUI.color = previousColor;

            GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(170));
            GUILayout.Label(detail, EditorStyles.wordWrappedLabel);

            if (buttonLabel != null && action != null)
            {
                if (GUILayout.Button(buttonLabel, GUILayout.Width(90)))
                    action();
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Legacy

        private void DrawLegacySection()
        {
            if (_legacyManagerPath == null) return;

            GUILayout.Label("Migration from the old RemoteConfigManager", EditorStyles.boldLabel);

            if (_legacyIsWrapper)
            {
                EditorGUILayout.HelpBox(
                    _legacyManagerPath + " is the compatibility wrapper: the old RemoteConfigManager calls keep working through " +
                    "RemoteConfigService. Move them to RemoteConfigParameters.<Name> when convenient and delete the file when none is left.",
                    MessageType.Info);
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Old RemoteConfigManager found in " + _legacyManagerPath + ". It still works on its own, so there is no rush, but it " +
                    "fetches Remote Config separately from this package.\n\n" +
                    "1. Import from code (Parameters window) fills the parameter list from the RemoteConfigManager.GetX calls of the project; " +
                    "complete the defaults that were not literals and Save.\n" +
                    "2. Migrate (below) backs the old script up as .txt and replaces its content with a wrapper that forwards to RemoteConfigService, " +
                    "keeping the file, its GUID and its API: nothing else in the project changes.",
                    MessageType.Warning);

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Import from code", GUILayout.Width(140)))
                {
                    RemoteConfigParametersWindow.Open();
                }
                using (new EditorGUI.DisabledScope(!RemoteConfigProjectFiles.GeneratedScriptExists))
                {
                    if (GUILayout.Button("Migrate legacy manager", GUILayout.Width(180)) &&
                        EditorUtility.DisplayDialog("Migrate RemoteConfigManager",
                            "Back up " + _legacyManagerPath + " as " + LegacyRemoteConfigImporter.BackupPathFor(_legacyManagerPath) +
                            " and replace its content with the compatibility wrapper?", "Migrate", "Cancel"))
                    {
                        RemoteConfigProjectFiles.MigrateLegacyManager(_legacyManagerPath);
                        RefreshProjectState();
                    }
                }
                EditorGUILayout.EndHorizontal();
                if (!RemoteConfigProjectFiles.GeneratedScriptExists)
                    EditorGUILayout.HelpBox("Migrate needs the generated RemoteConfigParameters class: save the parameters first.", MessageType.None);
            }

            EditorGUILayout.Space();
        }

        #endregion

        #region Settings

        private void DrawSettingsSection()
        {
            GUILayout.Label("Settings", EditorStyles.boldLabel);

            _parametersPathDraft = EditorGUILayout.TextField("Parameters file", _parametersPathDraft);
            _generatedPathDraft = EditorGUILayout.TextField("Generated class path", _generatedPathDraft);
            _initPathDraft = EditorGUILayout.TextField("RemoteConfigInit.cs path", _initPathDraft);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool dirty = _parametersPathDraft != RemoteConfigEditorSettings.ParametersFilePath
                         || _generatedPathDraft != RemoteConfigEditorSettings.GeneratedScriptPath
                         || _initPathDraft != RemoteConfigEditorSettings.InitScriptPath;
            using (new EditorGUI.DisabledScope(!dirty))
            {
                if (GUILayout.Button("Apply", GUILayout.Width(80)))
                    ApplySettings();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Paths are relative to the project and must be inside Assets. They are stored in ProjectSettings/VivaRemoteConfigSettings.json. " +
                "Changing a path does not move the existing files.",
                MessageType.None);
            EditorGUILayout.Space();
        }

        private void ApplySettings()
        {
            if (!RemoteConfigEditorSettings.IsValidProjectPath(_parametersPathDraft)
                || !_parametersPathDraft.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                || !RemoteConfigEditorSettings.IsValidProjectPath(_generatedPathDraft)
                || !_generatedPathDraft.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || !RemoteConfigEditorSettings.IsValidProjectPath(_initPathDraft)
                || !_initPathDraft.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Invalid path",
                    "All paths must start with Assets/. The parameters file must end with .json and the scripts with .cs.", "OK");
                return;
            }

            var settings = RemoteConfigEditorSettings.Instance;
            settings.parametersFilePath = _parametersPathDraft;
            settings.generatedScriptPath = _generatedPathDraft;
            settings.initScriptPath = _initPathDraft;
            settings.Save();

            _parametersPathDraft = RemoteConfigEditorSettings.ParametersFilePath;
            _generatedPathDraft = RemoteConfigEditorSettings.GeneratedScriptPath;
            _initPathDraft = RemoteConfigEditorSettings.InitScriptPath;
        }

        #endregion

        private static void DrawHelpSection()
        {
            GUILayout.Label("How it works", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Declare the parameters (key, type, default, description) in Viva > Remote Config > Parameters and Save.\n" +
                "2. The game reads RemoteConfigParameters.<Name>: typed, with the default built in. RemoteConfigService.WhenReady waits for the fetch.\n" +
                "3. Create the same keys in the Firebase console. Number, Boolean, String and JSON map to the types of the window.\n" +
                "4. RemoteConfigInit initializes the service on its own; with Viva Analytics installed both share one Firebase dependency check.",
                MessageType.None);
        }
    }
}
