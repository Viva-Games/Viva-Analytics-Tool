using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
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
        private const string MIGRATION_NOTES_PATH = LEGACY_BACKUP_FOLDER + "/MIGRATION_NOTES.txt";
        private static readonly string[] LEGACY_CODE_FOLDERS = { LEGACY_ROOT + "/Runtime", LEGACY_ROOT + "/Editor" };

        // GUID que tenía AnalyticsInit.cs en el .unitypackage. Se reutiliza para que las escenas
        // que ya tenían el componente sigan encontrándolo.
        private const string LEGACY_INIT_GUID = "dd1331b8a0f2491fb0284e2f94cc06ff";

        private static readonly Color OkColor = new Color(0.55f, 0.9f, 0.55f);
        private static readonly Color WarningColor = new Color(1f, 0.72f, 0.4f);
        private static readonly Color OffColor = new Color(0.65f, 0.65f, 0.65f);

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

            DrawTrackerRows();

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

        #region Optional trackers

        private const string FACEBOOK_SETTINGS_PATH = "Assets/FacebookSDK/SDK/Resources/FacebookSettings.asset";
        private static readonly Regex FacebookAppIdRegex = new Regex(@"appIds:\s*\r?\n\s*-\s*(\S+)");

        /// <summary>
        /// Trackers opcionales. Facebook y Singular se detectan solos (Facebook.Unity.dll y el assembly SingularSDK)
        /// y activan su define; la fila avisa si falta la configuración del SDK o si AnalyticsInit.cs es de una
        /// versión anterior y no crea el tracker.
        /// </summary>
        private static void DrawTrackerRows()
        {
            string initPath = AnalyticsEditorSettings.InitScriptPath;
            string initSource = File.Exists(initPath) ? File.ReadAllText(initPath) : null;

            bool facebookPresent = FacebookSdkDetector.IsSdkPresent();
            bool facebookDefine = FacebookSdkDetector.IsDefineEnabled();
            RowState facebookState;
            string facebookDetail;
            if (!facebookPresent)
            {
                facebookState = RowState.Off;
                facebookDetail = "not installed (optional). Import the Facebook SDK for Unity to send the events marked \"Facebook\" in the Event Editor.";
            }
            else if (!facebookDefine)
            {
                facebookState = RowState.Warning;
                facebookDetail = "detected, enabling " + FacebookSdkDetector.DEFINE + "...";
            }
            else
            {
                string appIdIssue = FacebookAppIdIssue();
                string initIssue = InitIssue(initSource, "FacebookAnalyticsTracker");
                facebookState = appIdIssue == null && initIssue == null ? RowState.Ok : RowState.Warning;
                facebookDetail = "detected (" + FacebookSdkDetector.DEFINE + " enabled)." + (appIdIssue ?? string.Empty) + (initIssue ?? string.Empty);
            }
            DrawRow(facebookState, "Facebook SDK", facebookDetail, "Re-check", () => FacebookSdkDetector.Refresh());

            bool singularPresent = SingularSdkDetector.IsSdkPresent();
            bool singularDefine = SingularSdkDetector.IsDefineEnabled();
            RowState singularState;
            string singularDetail;
            if (!singularPresent)
            {
                singularState = RowState.Off;
                singularDetail = "not installed (optional). Install the Singular Unity SDK to send the events marked \"Singular\" and to attribute the ad revenue of Viva Ads.";
            }
            else if (!singularDefine)
            {
                singularState = RowState.Warning;
                singularDetail = "detected, enabling " + SingularSdkDetector.DEFINE + "...";
            }
            else
            {
                string version = SingularSdkDetector.UpmPackageVersion();
                string initIssue = InitIssue(initSource, "SingularAnalyticsTracker");
                singularState = initIssue == null ? RowState.Ok : RowState.Warning;
                singularDetail = "detected (" + (version != null ? "UPM " + version + ", " : string.Empty) + SingularSdkDetector.DEFINE + " enabled). " +
                                 "The SingularSDK component of the first scene, with the API key and secret, initializes the SDK; the tracker waits for it." +
                                 (initIssue ?? string.Empty);
            }
            DrawRow(singularState, "Singular SDK", singularDetail, "Re-check", () => SingularSdkDetector.Refresh());

            DrawSingularAdRevenueToggle();
        }

        /// <summary>
        /// Toggle "Attribute the ad revenue of Viva Ads in Singular". Se guarda en el asset VivaAnalyticsSettings
        /// (Resources) y lo lee SingularAnalyticsTracker al crearse, así el proyecto no toca código para cambiarlo.
        /// </summary>
        private static void DrawSingularAdRevenueToggle()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(28);
            bool current = AnalyticsSettingsAsset.AttributeAdRevenueInSingular;
            bool value = EditorGUILayout.ToggleLeft("Attribute the ad revenue of Viva Ads in Singular", current, GUILayout.Width(320));
            if (value != current) AnalyticsSettingsAsset.SetAttributeAdRevenueInSingular(value);

            var settings = AnalyticsSettingsAsset.Find();
            string where = settings != null ? AnalyticsSettingsAsset.PathOf(settings) : "stored in " + AnalyticsSettingsAsset.DefaultPath + " when changed";
            GUILayout.Label("needs Viva Ads and the Singular SDK; " + where, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>null si FacebookSettings.asset tiene un App ID; si no, el aviso.</summary>
        private static string FacebookAppIdIssue()
        {
            const string issue = " App ID not set: open Facebook > Edit Settings and fill in the App ID.";
            if (!File.Exists(FACEBOOK_SETTINGS_PATH)) return issue;
            var match = FacebookAppIdRegex.Match(File.ReadAllText(FACEBOOK_SETTINGS_PATH));
            string appId = match.Success ? match.Groups[1].Value.Trim() : string.Empty;
            return appId.Length > 0 && appId != "0" ? null : issue;
        }

        /// <summary>null si AnalyticsInit.cs crea el tracker; si no, el aviso (fichero generado por una versión anterior).</summary>
        private static string InitIssue(string initSource, string trackerName)
        {
            if (initSource == null || initSource.Contains(trackerName)) return null;
            return " AnalyticsInit.cs does not create " + trackerName + " (generated by an older version): add AnalyticsService.AddTracker(new " + trackerName + "()) after Initialize, see the README.";
        }

        #endregion

        /// <summary>Estado de una fila: bien, aviso, o apagado (algo opcional que no está instalado).</summary>
        private enum RowState { Ok, Warning, Off }

        private static void DrawRow(bool ok, string label, string detail, string buttonLabel, Action action)
        {
            DrawRow(ok ? RowState.Ok : RowState.Warning, label, detail, buttonLabel, action);
        }

        private static void DrawRow(RowState state, string label, string detail, string buttonLabel, Action action)
        {
            EditorGUILayout.BeginHorizontal();

            var previousColor = GUI.color;
            GUI.color = state == RowState.Ok ? OkColor : state == RowState.Warning ? WarningColor : OffColor;
            GUILayout.Label(state == RowState.Ok ? "OK" : state == RowState.Warning ? "!" : "–", EditorStyles.boldLabel, GUILayout.Width(24));
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
        /// Los parámetros comunes del FirebaseAnalytics.cs antiguo se trasladan al AnalyticsInit nuevo.
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
                "3. Move the common parameters from InsertCommonParameters() into RegisterCommonParameters() of the new AnalyticsInit.cs.\n" +
                "4. Keep any other code you added to those scripts (user properties, Crashlytics...) as comments in OnFirebaseReady() of the new file, for you to review.\n" +
                "5. Delete the old scripts.\n\n" +
                "You can also do this later from Viva > Analytics > Setup.";

            if (!EditorUtility.DisplayDialog("Migrate legacy scripts", message, "Migrate now", "Later"))
            {
                return;
            }

            EnsureFolder(LEGACY_BACKUP_FOLDER);
            var target = AnalyticsEditorSettings.InitScriptPath;

            var migration = new MigrationData();

            if (File.Exists(LEGACY_TRACKER_PATH))
            {
                var source = File.ReadAllText(LEGACY_TRACKER_PATH);
                migration.Parameters = LegacyTrackerParser.Parse(source);

                // Cualquier otra línea que el proyecto añadiera al tracker se conserva para revisarla.
                var parsed = migration.Parameters;
                migration.AddCustomLines("FirebaseAnalytics.cs", LegacyCustomCodeFinder.FindCustomLines(
                    source, AnalyticsPackage.ReadLegacyTemplates("FirebaseAnalytics"), line => IsMigratedTrackerLine(line, parsed)));

                File.Copy(LEGACY_TRACKER_PATH, LEGACY_BACKUP_FOLDER + "/FirebaseAnalytics.legacy.txt", true);
                DeleteAsset(LEGACY_TRACKER_PATH);
            }

            if (File.Exists(LEGACY_INIT_PATH))
            {
                var source = File.ReadAllText(LEGACY_INIT_PATH);
                migration.AddCustomLines("AnalyticsInit.cs", LegacyCustomCodeFinder.FindCustomLines(
                    source, AnalyticsPackage.ReadLegacyTemplates("AnalyticsInit"), null));

                File.Copy(LEGACY_INIT_PATH, LEGACY_BACKUP_FOLDER + "/AnalyticsInit.legacy.txt", true);

                if (File.Exists(target))
                {
                    // Ya hay un AnalyticsInit nuevo: el antiguo sobra y lo rescatado se añade al existente.
                    DeleteAsset(LEGACY_INIT_PATH);
                    InsertMigratedCode(target, migration);
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

                    WriteInitScript(target, migration);
                }
            }
            else if (migration.HasContent)
            {
                // Había tracker antiguo pero no AnalyticsInit antiguo: lo rescatado va al init que haya, o a uno nuevo.
                if (File.Exists(target)) InsertMigratedCode(target, migration);
                else WriteInitScript(target, migration);
            }

            WriteMigrationNotes(migration, target);

            if (Directory.Exists(LEGACY_SCRIPTS_FOLDER) && Directory.GetFileSystemEntries(LEGACY_SCRIPTS_FOLDER).Length == 0)
            {
                DeleteAsset(LEGACY_SCRIPTS_FOLDER);
            }

            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();

            var summary = new StringBuilder();
            if (migration.ParameterCount == 0)
                summary.Append("No common parameters were found in the old FirebaseAnalytics.cs.");
            else if (migration.Parameters.IsSimpleBody)
                summary.Append(migration.ParameterCount + " common parameter(s) were moved to RegisterCommonParameters() in " + target + ". Review them: the value expressions were copied as they were.");
            else
                summary.Append(migration.ParameterCount + " common parameter(s) were found, but the old code computed their values with its own logic. " +
                               "The original code and the new registrations are in RegisterCommonParameters() of " + target + " as comments: rewrite each lambda and uncomment it. " +
                               "Until then those parameters are NOT sent.");

            if (migration.CustomLines.Count > 0)
            {
                summary.Append("\n\n" + migration.CustomLines.Count + " line(s) of your own code were found in the old scripts (user properties, Crashlytics...). " +
                               "They are kept as comments in OnFirebaseReady() of " + target + ": review them and put each one where it belongs.");
            }

            summary.Append("\n\nDetails in " + MIGRATION_NOTES_PATH + ". The backups of the old scripts are in the same folder.");

            EditorUtility.DisplayDialog("Migration done", summary.ToString(), "OK");
        }

        /// <summary>
        /// Líneas del tracker antiguo que ya se han trasladado como parámetros comunes: las llamadas a Add,
        /// las constantes usadas como clave y, si el cuerpo tenía lógica propia, todo el cuerpo (se copia entero
        /// junto a los parámetros para no duplicarlo en el bloque de código propio).
        /// </summary>
        private static bool IsMigratedTrackerLine(string line, LegacyTrackerParser.Result parsed)
        {
            if (line.Contains("new Parameter(") || line.Contains("stringParams.Add(")) return true;

            var match = ConstLineRegex.Match(line);
            if (match.Success && parsed.ResolvedConstants.Contains(match.Groups[1].Value)) return true;

            return !parsed.IsSimpleBody && parsed.BodyLines.Contains(line.Trim());
        }

        private static void WriteMigrationNotes(MigrationData migration, string initPath)
        {
            var notes = new StringBuilder();
            notes.AppendLine("Viva Analytics: migration from the .unitypackage version");
            notes.AppendLine("Date: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            notes.AppendLine("New initialization script: " + initPath);
            notes.AppendLine();

            notes.AppendLine("Common parameters moved to RegisterCommonParameters():");
            if (migration.ParameterCount == 0)
            {
                notes.AppendLine("  (none)");
            }
            else
            {
                foreach (var line in LegacyTrackerParser.BuildRegistrationLines(migration.Parameters))
                    notes.AppendLine("  " + line);
            }
            notes.AppendLine();

            notes.AppendLine("Other code found in the old scripts (kept as comments in OnFirebaseReady(), review and relocate):");
            if (migration.CustomLines.Count == 0)
            {
                notes.AppendLine("  (none)");
            }
            else
            {
                foreach (var line in migration.CustomLines)
                    notes.AppendLine("  " + line);
            }
            notes.AppendLine();
            notes.AppendLine("Backups: FirebaseAnalytics.legacy.txt and AnalyticsInit.legacy.txt in this folder.");

            File.WriteAllText(MIGRATION_NOTES_PATH, notes.ToString());
        }

        #endregion

        #region Actions

        private static void RunFullSetup()
        {
            EnsureFolder(AnalyticsEditorSettings.EventsFolder);
            int imported = AnalyticsEventCatalog.ImportMissing();
            AnalyticsSettingsAsset.GetOrCreate();

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

        private const string CODE_INDENT = "            ";
        private static readonly Regex ConstLineRegex = new Regex(@"const\s+string\s+(\w+)\s*=");
        private static readonly Regex CommonParametersMarker = new Regex(@"[ \t]*//\{\{COMMON_PARAMETERS\}\}[ \t]*\r?\n");
        private static readonly Regex CustomCodeMarker = new Regex(@"[ \t]*//\{\{LEGACY_CUSTOM_CODE\}\}[ \t]*\r?\n");
        private static readonly Regex RegisterCommonParametersMethod = new Regex(@"void\s+RegisterCommonParameters\s*\(\s*\)\s*\{[ \t]*\r?\n");
        private static readonly Regex OnFirebaseReadyMethod = new Regex(@"void\s+OnFirebaseReady\s*\(\s*\)\s*\{[ \t]*\r?\n");

        /// <summary>Lo que se rescata de los scripts antiguos durante la migración.</summary>
        private sealed class MigrationData
        {
            /// <summary>Parámetros comunes del FirebaseAnalytics.cs antiguo (null si no existía).</summary>
            public LegacyTrackerParser.Result Parameters;

            /// <summary>Líneas de código propio del proyecto que no se pueden colocar solas, ya formateadas como comentario.</summary>
            public readonly List<string> CustomLines = new List<string>();

            public int ParameterCount => Parameters != null ? Parameters.Parameters.Count : 0;

            public bool HasContent => ParameterCount > 0 || CustomLines.Count > 0;

            public void AddCustomLines(string fileName, List<string> lines)
            {
                foreach (var line in lines)
                    CustomLines.Add("// [" + fileName + "] " + line);
            }
        }

        /// <summary>
        /// Escribe la plantilla de AnalyticsInit con lo rescatado de la migración, si lo hay.
        /// Si ningún asset usa todavía el GUID antiguo, se lo asigna al fichero nuevo para que las escenas
        /// del .unitypackage recuperen la referencia al componente.
        /// </summary>
        private static bool WriteInitScript(string path, MigrationData migration = null)
        {
            var template = AnalyticsPackage.ReadTemplate(AnalyticsPackage.InitTemplatePath);
            if (template == null) return false;

            EnsureFolder(Path.GetDirectoryName(path));
            File.WriteAllText(path, ApplyMigration(template, migration));

            var metaPath = path + ".meta";
            if (!File.Exists(metaPath) && string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(LEGACY_INIT_GUID)))
            {
                File.WriteAllText(metaPath, BuildScriptMeta(LEGACY_INIT_GUID));
            }
            return true;
        }

        /// <summary>
        /// Sustituye los marcadores de la plantilla ({{COMMON_PARAMETERS}} y {{LEGACY_CUSTOM_CODE}}) por los bloques
        /// rescatados, o por nada, y añade los usings que necesitaba el tracker antiguo.
        /// </summary>
        private static string ApplyMigration(string template, MigrationData migration)
        {
            string newLine = template.Contains("\r\n") ? "\r\n" : "\n";
            string parametersBlock = BuildParametersBlock(migration, newLine);
            string customBlock = BuildCustomCodeBlock(migration, newLine);

            // Con MatchEvaluator los bloques se insertan tal cual, sin interpretar $ como referencia de grupo.
            var content = CommonParametersMarker.Replace(template, m => parametersBlock);
            content = CustomCodeMarker.Replace(content, m => customBlock);

            if (migration != null && migration.Parameters != null && migration.Parameters.Usings.Count > 0)
            {
                var usings = new StringBuilder();
                foreach (var ns in migration.Parameters.Usings)
                {
                    if (!content.Contains("using " + ns + ";"))
                        usings.Append("using ").Append(ns).Append(';').Append(newLine);
                }

                string extraUsings = usings.ToString();
                content = Regex.Replace(content, @"using UnityEngine;\r?\n", m => m.Value + extraUsings);
            }

            return content;
        }

        private static string BuildParametersBlock(MigrationData migration, string newLine)
        {
            if (migration == null || migration.ParameterCount == 0) return string.Empty;

            var parsed = migration.Parameters;
            var block = new StringBuilder();
            block.Append(CODE_INDENT).Append("// Common parameters migrated from the old FirebaseAnalytics.cs (backup in " + LEGACY_BACKUP_FOLDER + ").").Append(newLine);

            if (parsed.IsSimpleBody)
            {
                block.Append(CODE_INDENT).Append("// Review them: value expressions were copied as they were.").Append(newLine);
            }
            else
            {
                // El cuerpo antiguo calculaba los valores con su propia lógica una vez por evento. Se copia entero como
                // comentario para que cada lambda nueva pueda reproducir ese cálculo.
                block.Append(CODE_INDENT).Append("// The old InsertCommonParameters() computed the values with the logic below, once per event.").Append(newLine);
                block.Append(CODE_INDENT).Append("// Each lambda runs on every event, so move that logic inside each lambda and then uncomment the registrations.").Append(newLine);
                block.Append(CODE_INDENT).Append("// --- original code ---").Append(newLine);
                foreach (var line in parsed.BodyLines)
                {
                    block.Append(CODE_INDENT).Append("// ").Append(line).Append(newLine);
                }
                block.Append(CODE_INDENT).Append("// --- registrations to rewrite ---").Append(newLine);
            }

            foreach (var line in LegacyTrackerParser.BuildRegistrationLines(parsed))
            {
                block.Append(CODE_INDENT).Append(line).Append(newLine);
            }
            block.Append(newLine);
            return block.ToString();
        }

        private static string BuildCustomCodeBlock(MigrationData migration, string newLine)
        {
            if (migration == null || migration.CustomLines.Count == 0) return string.Empty;

            var block = new StringBuilder();
            block.Append(CODE_INDENT).Append("// Code you had added to the old AnalyticsInit.cs / FirebaseAnalytics.cs that the migration could not place by itself.").Append(newLine);
            block.Append(CODE_INDENT).Append("// Review it and move each line where it belongs (backups in " + LEGACY_BACKUP_FOLDER + "):").Append(newLine);
            foreach (var line in migration.CustomLines)
            {
                block.Append(CODE_INDENT).Append(line).Append(newLine);
            }
            block.Append(newLine);
            return block.ToString();
        }

        /// <summary>
        /// Inserta lo rescatado en un AnalyticsInit ya existente: los parámetros al principio de
        /// RegisterCommonParameters() y el código propio al principio de OnFirebaseReady() si existe.
        /// </summary>
        private static void InsertMigratedCode(string path, MigrationData migration)
        {
            if (migration == null || !migration.HasContent || !File.Exists(path)) return;

            var content = File.ReadAllText(path);
            string newLine = content.Contains("\r\n") ? "\r\n" : "\n";
            string parametersBlock = BuildParametersBlock(migration, newLine);
            string customBlock = BuildCustomCodeBlock(migration, newLine);

            if (customBlock.Length > 0 && OnFirebaseReadyMethod.IsMatch(content))
            {
                string customToInsert = customBlock;
                content = OnFirebaseReadyMethod.Replace(content, m => m.Value + customToInsert, 1);
                customBlock = string.Empty;
            }

            string remaining = parametersBlock + customBlock;
            if (remaining.Length > 0)
            {
                if (RegisterCommonParametersMethod.IsMatch(content))
                    content = RegisterCommonParametersMethod.Replace(content, m => m.Value + remaining, 1);
                else
                    Debug.LogWarning("[Analytics] RegisterCommonParameters() not found in " + path + ". Add these lines yourself:\n" + remaining);
            }

            File.WriteAllText(path, content);
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
                "4. Call {EventName}.Track(...) from your code.\n" +
                "5. Send an event to Facebook or Singular too: tick it in the Event Editor (Send to). Firebase always receives every event.",
                MessageType.None);

            if (GUILayout.Button("Open Event Manager"))
                AnalyticsEventManager.ShowWindow();
        }
    }
}
