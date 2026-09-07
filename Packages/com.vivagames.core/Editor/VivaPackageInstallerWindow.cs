using System.Collections.Generic;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
// UnityEditor también tiene un PackageInfo antiguo: se fija el del Package Manager.
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Viva.Core.Editor
{
    /// <summary>
    /// Ventana Viva > Package Installer: muestra los módulos disponibles, cuáles están instalados,
    /// si hay una release nueva de cada uno en el repositorio, y permite instalarlos, actualizarlos o quitarlos.
    /// Cada módulo se versiona por separado: sus releases son los tags con su prefijo (analytics/v2.0.1).
    /// </summary>
    public class VivaPackageInstallerWindow : EditorWindow
    {
        private readonly Dictionary<string, PackageInfo> _installed = new Dictionary<string, PackageInfo>();
        private ListRequest _listRequest;
        private bool _listLoaded;
        private string _listError;

        private bool _checkingUpdates;
        private bool _updatesChecked;
        private string _updatesError;

        // Última release publicada de cada módulo, por nombre de paquete.
        private readonly Dictionary<string, VivaVersion> _latestByModule = new Dictionary<string, VivaVersion>();
        private Vector2 _scroll;

        [MenuItem("Viva/Package Installer")]
        public static void Open()
        {
            var window = GetWindow<VivaPackageInstallerWindow>("Viva Packages");
            window.minSize = new Vector2(560, 340);
        }

        private void OnEnable()
        {
            VivaPackageOperations.Changed += OnOperationsChanged;
            RefreshInstalled();
            CheckForUpdates();
        }

        private void OnDisable()
        {
            VivaPackageOperations.Changed -= OnOperationsChanged;
            EditorApplication.update -= PollList;
        }

        private void OnOperationsChanged()
        {
            RefreshInstalled();
            Repaint();
        }

        private void OnGUI()
        {
            DrawHeader();
            EditorGUILayout.Space();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var module in VivaModuleCatalog.Modules)
            {
                DrawModule(module);
                EditorGUILayout.Space(4);
            }
            EditorGUILayout.EndScrollView();

            DrawFooter();
        }

        private void DrawHeader()
        {
            GUILayout.Label("Viva Packages", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Repository", VivaRepository.GitUrl, EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (_checkingUpdates)
                EditorGUILayout.LabelField("Releases: checking...");
            else if (!string.IsNullOrEmpty(_updatesError))
                EditorGUILayout.LabelField("Releases: unknown");
            else if (_updatesChecked)
                EditorGUILayout.LabelField($"Releases: {_latestByModule.Count} module(s) with a published version");
            else
                EditorGUILayout.LabelField("Releases: not checked");

            using (new EditorGUI.DisabledScope(_checkingUpdates))
            {
                if (GUILayout.Button("Check for updates", GUILayout.Width(140)))
                    CheckForUpdates();
            }
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(_updatesError))
                EditorGUILayout.HelpBox("Could not read the repository tags: " + _updatesError, MessageType.Warning);
            if (!string.IsNullOrEmpty(_listError))
                EditorGUILayout.HelpBox("Could not list the installed packages: " + _listError, MessageType.Error);

            if (VivaPackageOperations.IsBusy)
            {
                EditorGUILayout.HelpBox(VivaPackageOperations.CurrentLabel ?? "Waiting for Unity to finish importing...", MessageType.Info);
            }
            else if (!string.IsNullOrEmpty(VivaPackageOperations.LastMessage))
            {
                bool isError = VivaPackageOperations.LastMessage.Contains("FAILED");
                EditorGUILayout.HelpBox(VivaPackageOperations.LastMessage, isError ? MessageType.Error : MessageType.Info);
            }
        }

        private void DrawModule(VivaModule module)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(module.DisplayName, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label(module.PackageName, EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            GUILayout.Label(module.Description, EditorStyles.wordWrappedLabel);

            bool busy = VivaPackageOperations.IsBusy || !_listLoaded;
            _installed.TryGetValue(module.PackageName, out var info);
            string latestTag = LatestTag(module);

            DrawLegacyNotice(module, info, busy);

            EditorGUILayout.BeginHorizontal();
            if (info == null)
            {
                GUILayout.Label(latestTag == null ? "Not installed" : $"Not installed (latest release: {latestTag})", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                var reference = TargetReference(module, null);
                using (new EditorGUI.DisabledScope(busy || reference == null))
                {
                    if (GUILayout.Button(reference == null ? "Install" : $"Install {reference}", GUILayout.Width(170)))
                    {
                        // Si hay una instalación antigua por .unitypackage, avisa y la retira antes de instalar.
                        if (LegacyInstallCleaner.PrepareForInstall(module))
                            VivaPackageOperations.Install(module, reference);
                    }
                }
            }
            else
            {
                GUILayout.Label(DescribeInstalled(info), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                if (info.source == PackageSource.Embedded)
                {
                    GUILayout.Label("Development copy", EditorStyles.miniLabel);
                }
                else
                {
                    if (IsOutdated(module, info))
                    {
                        using (new EditorGUI.DisabledScope(busy))
                        {
                            if (GUILayout.Button($"Update to {latestTag}", GUILayout.Width(170)))
                                VivaPackageOperations.Install(module, latestTag);
                        }
                    }
                    else if (latestTag != null)
                    {
                        GUILayout.Label("Up to date", EditorStyles.miniLabel);
                    }

                    if (!module.IsCore)
                    {
                        using (new EditorGUI.DisabledScope(busy))
                        {
                            if (GUILayout.Button("Remove", GUILayout.Width(70)) &&
                                EditorUtility.DisplayDialog("Remove package",
                                    $"Remove {module.DisplayName} ({module.PackageName}) from this project?",
                                    "Remove", "Cancel"))
                            {
                                VivaPackageOperations.Remove(module);
                            }
                        }
                    }
                }
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// Aviso de que quedan restos de la instalación antigua por .unitypackage. Al instalar se retiran
        /// solos; si el módulo ya está instalado (por ejemplo desde una URL a mano), se ofrece retirarlos aquí.
        /// </summary>
        private static void DrawLegacyNotice(VivaModule module, PackageInfo info, bool busy)
        {
            var legacyFolders = LegacyInstallCleaner.FindLegacyFolders(module);
            if (legacyFolders.Count == 0) return;

            EditorGUILayout.HelpBox(
                "Old .unitypackage installation found (" + string.Join(", ", legacyFolders) + "). " +
                (info == null
                    ? "It will be backed up and removed automatically when you install."
                    : "It collides with the installed package and must be removed."),
                MessageType.Warning);

            if (info != null)
            {
                using (new EditorGUI.DisabledScope(busy))
                {
                    if (GUILayout.Button("Remove legacy files", GUILayout.Width(160)) &&
                        EditorUtility.DisplayDialog("Remove legacy files",
                            "Back up " + string.Join(" and ", legacyFolders) + " in Library/VivaLegacyBackup and remove them from the project?",
                            "Remove", "Cancel"))
                    {
                        LegacyInstallCleaner.RemoveLegacyFolders(module, legacyFolders);
                    }
                }
            }
        }

        private void DrawFooter()
        {
            EditorGUILayout.Space();

            var outdated = OutdatedModules();
            if (outdated.Count > 0)
            {
                using (new EditorGUI.DisabledScope(VivaPackageOperations.IsBusy))
                {
                    if (GUILayout.Button($"Update all ({outdated.Count})"))
                        VivaPackageOperations.InstallAll(outdated, LatestTag);
                }
            }

            EditorGUILayout.HelpBox(
                "Each module has its own version and release tags. Installing or updating writes to " +
                "Packages/manifest.json and packages-lock.json: commit both files so everyone in the project gets the same versions.",
                MessageType.None);
        }

        private static string DescribeInstalled(PackageInfo info)
        {
            switch (info.source)
            {
                case PackageSource.Git:
                    var revision = info.git != null ? info.git.revision : null;
                    return string.IsNullOrEmpty(revision)
                        ? $"v{info.version} (git)"
                        : $"v{info.version} (git, {revision})";
                case PackageSource.Embedded:
                    return $"v{info.version} (embedded)";
                default:
                    return $"v{info.version} ({info.source})";
            }
        }

        /// <summary>Tag de la última release publicada del módulo, o null si no se conoce.</summary>
        private string LatestTag(VivaModule module)
        {
            return _latestByModule.TryGetValue(module.PackageName, out var latest)
                ? VivaRepository.BuildTag(module, latest)
                : null;
        }

        private bool IsOutdated(VivaModule module, PackageInfo info)
        {
            if (!_latestByModule.TryGetValue(module.PackageName, out var latest)) return false;
            if (!VivaVersion.TryParse(info.version, out var installed)) return false;
            return latest > installed;
        }

        private List<VivaModule> OutdatedModules()
        {
            var result = new List<VivaModule>();
            foreach (var module in VivaModuleCatalog.Modules)
            {
                if (_installed.TryGetValue(module.PackageName, out var info)
                    && info.source != PackageSource.Embedded
                    && IsOutdated(module, info))
                {
                    result.Add(module);
                }
            }
            return result;
        }

        /// <summary>
        /// Referencia git a instalar para un módulo:
        /// 1. Si el core se instaló desde una rama o un commit (no desde un tag de release), esa misma
        ///    referencia, para que todos los módulos vayan a la par. Sirve para probar antes de publicar.
        /// 2. Si no, la última release publicada del módulo.
        /// 3. Si no se ha podido consultar el repositorio y el módulo ya está instalado, su propia versión.
        /// </summary>
        private string TargetReference(VivaModule module, PackageInfo info)
        {
            if (_installed.TryGetValue(VivaModuleCatalog.CorePackageName, out var core) && core.source == PackageSource.Git)
            {
                var revision = core.git != null ? core.git.revision : null;
                if (!string.IsNullOrEmpty(revision) && !VivaRepository.TryParseTag(revision, out _, out _))
                    return revision;
            }

            var latestTag = LatestTag(module);
            if (latestTag != null) return latestTag;

            if (info != null && VivaVersion.TryParse(info.version, out var installedVersion))
                return VivaRepository.BuildTag(module, installedVersion);

            return null;
        }

        private void RefreshInstalled()
        {
            if (_listRequest != null && !_listRequest.IsCompleted) return;

            _listRequest = Client.List(true, true);
            EditorApplication.update -= PollList;
            EditorApplication.update += PollList;
        }

        private void PollList()
        {
            if (_listRequest == null || !_listRequest.IsCompleted) return;
            EditorApplication.update -= PollList;

            if (_listRequest.Status == StatusCode.Success)
            {
                _installed.Clear();
                foreach (var package in _listRequest.Result)
                    _installed[package.name] = package;
                _listLoaded = true;
                _listError = null;
            }
            else
            {
                _listError = _listRequest.Error != null ? _listRequest.Error.message : "unknown error";
            }

            _listRequest = null;
            Repaint();
        }

        private void CheckForUpdates()
        {
            if (_checkingUpdates) return;
            _checkingUpdates = true;
            _updatesError = null;

            GitTagFetcher.FetchTags(VivaRepository.GitUrl, (tags, error) =>
            {
                if (this == null) return; // la ventana se cerró mientras tanto

                _checkingUpdates = false;
                if (error != null)
                {
                    _updatesError = error;
                }
                else
                {
                    _updatesChecked = true;
                    _latestByModule.Clear();
                    foreach (var tag in tags)
                    {
                        if (!VivaRepository.TryParseTag(tag, out var prefix, out var version)) continue;

                        var module = VivaModuleCatalog.FindByTagPrefix(prefix);
                        if (module == null) continue;

                        if (!_latestByModule.TryGetValue(module.PackageName, out var current) || version > current)
                            _latestByModule[module.PackageName] = version;
                    }
                }
                Repaint();
            });
        }
    }
}
