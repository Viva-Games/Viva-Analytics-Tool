using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Ventana Viva > Analytics > Setup: prepara el proyecto (carpeta de eventos, AnalyticsInit.cs,
    /// eventos estándar) y ayuda a migrar desde la instalación antigua por .unitypackage.
    /// </summary>
    public class AnalyticsSetupWindow : EditorWindow
    {
        private const string LEGACY_ROOT = "Assets/VivaAnalytics";
        private const string LEGACY_SCRIPTS_FOLDER = LEGACY_ROOT + "/Scripts";
        private const string LEGACY_TRACKER_PATH = LEGACY_SCRIPTS_FOLDER + "/FirebaseAnalytics.cs";
        private const string LEGACY_INIT_PATH = LEGACY_SCRIPTS_FOLDER + "/AnalyticsInit.cs";
        private const string LEGACY_BACKUP_FOLDER = LEGACY_ROOT + "/Legacy";
        private static readonly string[] LEGACY_CODE_FOLDERS = { LEGACY_ROOT + "/Runtime", LEGACY_ROOT + "/Editor" };

        // GUID que tenía AnalyticsInit.cs en el .unitypackage. Se reutiliza para que las escenas
        // que ya tenían el componente sigan encontrándolo.
        private const string LEGACY_INIT_GUID = "dd1331b8a0f2491fb0284e2f94cc06ff";

        private static readonly Color OkColor = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color WarningColor = new Color(1f, 0.72f, 0.4f);

        private string _eventsFolderDraft;
        private string _initPathDraft;
        private Vector2 _scroll;

        [MenuItem("Viva/Analytics/Setup")]
        public static void Open()
        {
            var window = GetWindow<AnalyticsSetupWindow>("Analytics Setup");
            window.minSize = new Vector2(580, 440);
        }

        /// <summary>
        /// Primer arranque del paquete en un proyecto: si hay scripts de la versión .unitypackage ofrece
        /// migrarlos; si el proyecto está vacío ofrece la configuración inicial. En ambos casos abre la ventana.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void OpenOnFirstRun()
        {
            if (Application.isBatchMode) return;
            if (AnalyticsEditorSettings.Instance.setupShown) return;

            EditorApplication.delayCall += () =>
            {
                AnalyticsEditorSettings.Instance.setupShown = true;
                AnalyticsEditorSettings.Instance.Save();

                if (HasLegacyScripts())
                    MigrateLegacyScripts();
                else if (!File.Exists(AnalyticsEditorSettings.InitScriptPath) || AnalyticsEventCatalog.Missing().Count > 0)
                    OfferInitialSetup();

                Open();
            };
        }

        private static void OfferInitialSetup()
        {
            int missing = AnalyticsEventCatalog.Missing().Count;
            bool needsInit = !File.Exists(AnalyticsEditorSettings.InitScriptPath);

            var message = "Viva Analytics is installed. The initial setup will:\n\n";
            message += "1. Create the events folder " + AnalyticsEditorSettings.EventsFolder + ".\n";
            if (missing > 0)
                message += $"2. Import the {missing} standard events of the studio catalog.\n";
            if (needsInit)
                message += (missing > 0 ? "3" : "2") + ". Generate " + AnalyticsEditorSettings.InitScriptPath + ", where you register your common parameters.\n";
            message += "\nYou can also run it later from Viva > Analytics > Setup.";

            if (EditorUtility.DisplayDialog("Set up Viva Analytics", message, "Run setup", "Later"))
                RunFullSetup();
        }

        private void OnEnable()
        {
            _eventsFolderDraft = AnalyticsEditorSettings.EventsFolder;
            _initPathDraft = AnalyticsEditorSettings.InitScriptPath;
        }

        private void OnFocus()
        {
            Repaint();
        }

        private void OnGUI()
        {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            GUILayout.Label($"Viva Analytics {AnalyticsPackage.Version}", EditorStyles.boldLabel);
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

            bool legacyScripts = HasLegacyScripts();
            string eventsFolder = AnalyticsEditorSettings.EventsFolder;
            string initPath = AnalyticsEditorSettings.InitScriptPath;

            bool folderExists = Directory.Exists(eventsFolder);
            DrawRow(folderExists, "Events folder", eventsFolder,
                folderExists ? null : "Create", () =>
                {
                    EnsureFolder(eventsFolder);
                    AssetDatabase.Refresh();
                });

            bool initExists = File.Exists(initPath);
            if (legacyScripts && !initExists)
            {
                DrawRow(false, "AnalyticsInit.cs", "legacy version in " + LEGACY_SCRIPTS_FOLDER + " (see migration above)", null, null);
            }
            else
            {
                DrawRow(initExists, "AnalyticsInit.cs", initExists ? initPath : "missing",
                    initExists ? null : "Generate", GenerateInitScript);
            }

            bool firebasePresent = FirebaseSdkDetector.IsFirebaseAnalyticsPresent();
            bool defineEnabled = FirebaseSdkDetector.IsDefineEnabled();
            string firebaseDetail;
            if (firebasePresent && defineEnabled)
                firebaseDetail = "detected (" + FirebaseSdkDetector.DEFINE + " enabled)";
            else if (firebasePresent)
                firebaseDetail = "detected, enabling " + FirebaseSdkDetector.DEFINE + "...";
            else
                firebaseDetail = "not found. Import the Firebase Analytics SDK; the tool enables itself automatically.";
            DrawRow(firebasePresent && defineEnabled, "Firebase Analytics SDK", firebaseDetail,
                "Re-check", () => FirebaseSdkDetector.Refresh());

            int total = AnalyticsEventCatalog.Events.Count;
            int missing = AnalyticsEventCatalog.Missing().Count;
            DrawRow(missing == 0, "Standard events", $"{total - missing}/{total} in project",
                missing == 0 ? null : $"Import {missing}", () => AnalyticsEventCatalog.ImportMissing());

            EditorGUILayout.Space();
            bool blockedByLegacy = legacyScripts && !initExists;
            using (new EditorGUI.DisabledScope(blockedByLegacy))
            {
                if (GUILayout.Button("Run full setup", GUILayout.Height(28)))
                    RunFullSetup();
            }
            if (blockedByLegacy)
            {
                EditorGUILayout.HelpBox("Migrate the legacy scripts first so the full setup can generate the new AnalyticsInit.cs.", MessageType.None);
            }
            EditorGUILayout.Space();
        }

        private static void DrawRow(bool ok, string label, string detail, string buttonLabel, Action action)
        {
            EditorGUILayout.BeginHorizontal();

            var previousColor = GUI.color;
            GUI.color = ok ? OkColor : WarningColor;
            GUILayout.Label(ok ? "OK" : "!", EditorStyles.boldLabel, GUILayout.Width(24));
            GUI.color = previousColor;

            GUILayout.Label(label, EditorStyles.boldLabel, GUILayout.Width(160));
            GUILayout.Label(detail, EditorStyles.wordWrappedLabel);

            if (buttonLabel != null && action != null)
            {
                if (GUILayout.Button(buttonLabel, GUILayout.Width(110)))
                    action();
            }

            EditorGUILayout.EndHorizontal();
        }

        #endregion

        #region Settings

        private void DrawSettingsSection()
        {
            GUILayout.Label("Settings", EditorStyles.boldLabel);

            _eventsFolderDraft = EditorGUILayout.TextField("Events folder", _eventsFolderDraft);
            _initPathDraft = EditorGUILayout.TextField("AnalyticsInit.cs path", _initPathDraft);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            bool dirty = _eventsFolderDraft != AnalyticsEditorSettings.EventsFolder
                         || _initPathDraft != AnalyticsEditorSettings.InitScriptPath;
            using (new EditorGUI.DisabledScope(!dirty))
            {
                if (GUILayout.Button("Apply", GUILayout.Width(80)))
                    ApplySettings();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Paths are relative to the project and must be inside Assets. " +
                "They are stored in ProjectSettings/VivaAnalyticsSettings.json.",
                MessageType.None);
            EditorGUILayout.Space();
        }

        private void ApplySettings()
        {
            if (!AnalyticsEditorSettings.IsValidProjectPath(_eventsFolderDraft)
                || !AnalyticsEditorSettings.IsValidProjectPath(_initPathDraft)
                || !_initPathDraft.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            {
                EditorUtility.DisplayDialog("Invalid path",
                    "Both paths must start with Assets/ and the AnalyticsInit path must end with .cs.", "OK");
                return;
            }

            var settings = AnalyticsEditorSettings.Instance;
            settings.eventsFolder = _eventsFolderDraft;
            settings.initScriptPath = _initPathDraft;
            settings.Save();

            _eventsFolderDraft = AnalyticsEditorSettings.EventsFolder;
            _initPathDraft = AnalyticsEditorSettings.InitScriptPath;
        }

        #endregion

        #region Legacy migration

        private void DrawLegacySection()
        {
            bool codeFolders = HasLegacyCodeFolders();
            bool scripts = HasLegacyScripts();
            if (!codeFolders && !scripts) return;

            GUILayout.Label("Migration from the .unitypackage version", EditorStyles.boldLabel);

            if (codeFolders)
            {
                EditorGUILayout.HelpBox(
                    "Old tool code found in " + string.Join(" and ", ExistingLegacyCodeFolders()) + ". " +
                    "Delete those folders (Runtime includes the old .asmdef) so they do not collide with the package, then come back here.",
                    MessageType.Error);
            }

            if (scripts)
            {
                EditorGUILayout.HelpBox(
                    "Legacy scripts found in " + LEGACY_SCRIPTS_FOLDER + ". They still work with the new package, so there is no rush.\n\n" +
                    "Migrate will:\n" +
                    "1. Back up FirebaseAnalytics.cs and AnalyticsInit.cs as .txt files in " + LEGACY_BACKUP_FOLDER + ".\n" +
                    "2. Move AnalyticsInit.cs to " + AnalyticsEditorSettings.InitScriptPath + " keeping its GUID (scene references survive) and replace its content with the new template.\n" +
                    "3. Delete the legacy scripts.\n\n" +
                    "Afterwards, copy your common parameters from the backup into RegisterCommonParameters() using AnalyticsService.RegisterCommonParameter.",
                    MessageType.Warning);

                if (GUILayout.Button("Migrate legacy scripts"))
                    MigrateLegacyScripts();
            }

            EditorGUILayout.Space();
        }

        private static bool HasLegacyScripts()
        {
            return File.Exists(LEGACY_TRACKER_PATH) || File.Exists(LEGACY_INIT_PATH);
        }

        private static bool HasLegacyCodeFolders()
        {
            return ExistingLegacyCodeFolders().Count > 0;
        }

        private static List<string> ExistingLegacyCodeFolders()
        {
            var result = new List<string>();
            foreach (var folder in LEGACY_CODE_FOLDERS)
            {
                if (Directory.Exists(folder)) result.Add(folder);
            }
            return result;
        }

        /// <summary>
        /// Avisa de lo que va a hacer y, si el usuario acepta, migra los scripts de la versión .unitypackage.
        /// Se usa desde el primer arranque del paquete y desde el botón de la ventana.
        /// </summary>
        private static void MigrateLegacyScripts()
        {
            var message =
                "Scripts from the old .unitypackage version were found in " + LEGACY_SCRIPTS_FOLDER + ". " +
                "They still work, but the new package handles initialization and common parameters differently.\n\n" +
                "Migrate will:\n" +
                "1. Back up FirebaseAnalytics.cs and AnalyticsInit.cs as .txt files in " + LEGACY_BACKUP_FOLDER + ".\n" +
                "2. Move AnalyticsInit.cs to " + AnalyticsEditorSettings.InitScriptPath + ", keeping its GUID so scene references survive, and replace its content with the new template.\n" +
                "3. Delete the old scripts.\n\n" +
                "Afterwards, copy your common parameters from the backup into RegisterCommonParameters().\n" +
                "You can also do this later from Viva > Analytics > Setup.";

            if (!EditorUtility.DisplayDialog("Migrate legacy scripts", message, "Migrate now", "Later"))
            {
                return;
            }

            EnsureFolder(LEGACY_BACKUP_FOLDER);

            if (File.Exists(LEGACY_TRACKER_PATH))
            {
                File.Copy(LEGACY_TRACKER_PATH, LEGACY_BACKUP_FOLDER + "/FirebaseAnalytics.legacy.txt", true);
                DeleteAsset(LEGACY_TRACKER_PATH);
            }

            if (File.Exists(LEGACY_INIT_PATH))
            {
                File.Copy(LEGACY_INIT_PATH, LEGACY_BACKUP_FOLDER + "/AnalyticsInit.legacy.txt", true);

                var target = AnalyticsEditorSettings.InitScriptPath;
                if (File.Exists(target))
                {
                    // Ya hay un AnalyticsInit nuevo: el antiguo sobra.
                    DeleteAsset(LEGACY_INIT_PATH);
                }
                else
                {
                    EnsureFolder(Path.GetDirectoryName(target));
                    AssetDatabase.Refresh(); // la carpeta destino tiene que existir como asset para MoveAsset

                    var error = AssetDatabase.MoveAsset(LEGACY_INIT_PATH, target);
                    if (!string.IsNullOrEmpty(error))
                    {
                        // Plan B: mover a mano el .cs y su .meta para conservar el GUID.
                        File.Move(LEGACY_INIT_PATH, target);
                        if (File.Exists(LEGACY_INIT_PATH + ".meta"))
                            File.Move(LEGACY_INIT_PATH + ".meta", target + ".meta");
                    }

                    WriteInitScript(target);
                }
            }

            if (Directory.Exists(LEGACY_SCRIPTS_FOLDER) && Directory.GetFileSystemEntries(LEGACY_SCRIPTS_FOLDER).Length == 0)
            {
                DeleteAsset(LEGACY_SCRIPTS_FOLDER);
            }

            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();

            EditorUtility.DisplayDialog("Migration done",
                "Backups saved in " + LEGACY_BACKUP_FOLDER + ".\n\nNow copy your common parameters into RegisterCommonParameters() in " +
                AnalyticsEditorSettings.InitScriptPath + " using AnalyticsService.RegisterCommonParameter.",
                "OK");
        }

        #endregion

        #region Actions

        private static void RunFullSetup()
        {
            EnsureFolder(AnalyticsEditorSettings.EventsFolder);
            int imported = AnalyticsEventCatalog.ImportMissing();

            bool generated = false;
            var initPath = AnalyticsEditorSettings.InitScriptPath;
            if (!File.Exists(initPath) && !HasLegacyScripts())
            {
                generated = WriteInitScript(initPath);
            }

            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
            Debug.Log($"[Analytics] Setup done. Standard events imported: {imported}. AnalyticsInit.cs generated: {generated}.");
        }

        private void GenerateInitScript()
        {
            var path = AnalyticsEditorSettings.InitScriptPath;

            if (File.Exists(LEGACY_INIT_PATH))
            {
                EditorUtility.DisplayDialog("Legacy AnalyticsInit found",
                    "There is already an AnalyticsInit.cs in " + LEGACY_SCRIPTS_FOLDER +
                    ". Use 'Migrate legacy scripts' instead, so you do not end up with two classes with the same name.",
                    "OK");
                return;
            }

            if (File.Exists(path) && !EditorUtility.DisplayDialog("Overwrite AnalyticsInit.cs?",
                    path + " already exists. Overwrite it with the template? Your common parameters would be lost.",
                    "Overwrite", "Cancel"))
            {
                return;
            }

            if (WriteInitScript(path))
            {
                AssetDatabase.Refresh();
                CompilationPipeline.RequestScriptCompilation();
            }
        }

        /// <summary>
        /// Escribe la plantilla de AnalyticsInit. Si ningún asset usa todavía el GUID antiguo, se lo asigna
        /// al fichero nuevo para que las escenas del .unitypackage recuperen la referencia al componente.
        /// </summary>
        private static bool WriteInitScript(string path)
        {
            var template = AnalyticsPackage.ReadTemplate(AnalyticsPackage.InitTemplatePath);
            if (template == null) return false;

            EnsureFolder(Path.GetDirectoryName(path));
            File.WriteAllText(path, template);

            var metaPath = path + ".meta";
            if (!File.Exists(metaPath) && string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(LEGACY_INIT_GUID)))
            {
                File.WriteAllText(metaPath, BuildScriptMeta(LEGACY_INIT_GUID));
            }
            return true;
        }

        private static string BuildScriptMeta(string guid)
        {
            return "fileFormatVersion: 2\n" +
                   "guid: " + guid + "\n" +
                   "MonoImporter:\n" +
                   "  externalObjects: {}\n" +
                   "  serializedVersion: 2\n" +
                   "  defaultReferences: []\n" +
                   "  executionOrder: 0\n" +
                   "  icon: {instanceID: 0}\n" +
                   "  userData: \n" +
                   "  assetBundleName: \n" +
                   "  assetBundleVariant: \n";
        }

        private static void EnsureFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        }

        private static void DeleteAsset(string path)
        {
            if (AssetDatabase.DeleteAsset(path)) return;

            // Si Unity aún no lo tenía importado, se borra a mano junto con su .meta.
            if (File.Exists(path)) File.Delete(path);
            else if (Directory.Exists(path)) Directory.Delete(path, true);
            if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
        }

        #endregion

        private void DrawHelpSection()
        {
            GUILayout.Label("How to use", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1. Add the AnalyticsInit component to a GameObject in the first scene of the game.\n" +
                "2. Register your common parameters in AnalyticsInit.RegisterCommonParameters().\n" +
                "3. Create or edit events in Viva > Analytics > Event Manager.\n" +
                "4. Call {EventName}.Track(...) from your code.",
                MessageType.None);

            if (GUILayout.Button("Open Event Manager"))
                AnalyticsEventManager.ShowWindow();
        }
    }
}
