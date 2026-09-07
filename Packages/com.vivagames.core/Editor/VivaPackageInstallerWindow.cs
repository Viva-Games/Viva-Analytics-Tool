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
    /// si hay una versión nueva en el repositorio, y permite instalarlos, actualizarlos o quitarlos.
    /// </summary>
    public class VivaPackageInstallerWindow : EditorWindow
    {
        private readonly Dictionary<string, PackageInfo> _installed = new Dictionary<string, PackageInfo>();
        private ListRequest _listRequest;
        private bool _listLoaded;
        private string _listError;

        private bool _checkingUpdates;
        private string _updatesError;
        private VivaVersion? _latestVersion;
        private Vector2 _scroll;

        [MenuItem("Viva/Package Installer")]
        public static void Open()
        {
            var window = GetWindow<VivaPackageInstallerWindow>("Viva Packages");
            window.minSize = new Vector2(540, 340);
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
                EditorGUILayout.LabelField("Latest release: checking...");
            else if (!string.IsNullOrEmpty(_updatesError))
                EditorGUILayout.LabelField("Latest release: unknown");
            else if (_latestVersion.HasValue)
                EditorGUILayout.LabelField($"Latest release: {_latestVersion.Value.ToTag()}");
            else
                EditorGUILayout.LabelField("Latest release: no release tags found");

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

            DrawLegacyNotice(module, info, busy);

            EditorGUILayout.BeginHorizontal();
            if (info == null)
            {
                GUILayout.Label("Not installed", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                var tag = TargetTag();
                using (new EditorGUI.DisabledScope(busy || tag == null))
                {
                    if (GUILayout.Button(tag == null ? "Install" : $"Install {tag}", GUILayout.Width(140)))
                    {
                        // Si hay una instalación antigua por .unitypackage, avisa y la retira antes de instalar.
                        if (LegacyInstallCleaner.PrepareForInstall(module))
                            VivaPackageOperations.Install(module, tag);
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
                    if (IsOutdated(info))
                    {
                        using (new EditorGUI.DisabledScope(busy))
                        {
                            if (GUILayout.Button($"Update to {_latestVersion.Value.ToTag()}", GUILayout.Width(140)))
                                VivaPackageOperations.Install(module, _latestVersion.Value.ToTag());
                        }
                    }
                    else if (_latestVersion.HasValue)
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
                    if (GUILayout.Button($"Update all ({outdated.Count}) to {_latestVersion.Value.ToTag()}"))
                        VivaPackageOperations.InstallAll(outdated, _latestVersion.Value.ToTag());
                }
            }

            EditorGUILayout.HelpBox(
                "Installing or updating writes to Packages/manifest.json and packages-lock.json. " +
                "Commit both files so everyone in the project gets the same versions.",
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

        private bool IsOutdated(PackageInfo info)
        {
            if (!_latestVersion.HasValue) return false;
            if (!VivaVersion.TryParse(info.version, out var installed)) return false;
            return _latestVersion.Value > installed;
        }

        private List<VivaModule> OutdatedModules()
        {
            var result = new List<VivaModule>();
            if (!_latestVersion.HasValue) return result;

            foreach (var module in VivaModuleCatalog.Modules)
            {
                if (_installed.TryGetValue(module.PackageName, out var info)
                    && info.source != PackageSource.Embedded
                    && IsOutdated(info))
                {
                    result.Add(module);
                }
            }
            return result;
        }

        /// <summary>
        /// Referencia git a instalar para un módulo nuevo:
        /// 1. Si el core se instaló desde una rama o un commit (no desde un tag de versión), esa misma
        ///    referencia, para que todos los módulos vayan a la par. Sirve para probar antes de publicar un tag.
        /// 2. Si no, la última release conocida.
        /// 3. Si no se ha podido consultar el repositorio, el tag de la misma versión que tenga el core.
        /// </summary>
        private string TargetTag()
        {
            _installed.TryGetValue(VivaModuleCatalog.CorePackageName, out var core);

            if (core != null && core.source == PackageSource.Git)
            {
                var revision = core.git != null ? core.git.revision : null;
                if (!string.IsNullOrEmpty(revision) && !VivaVersion.TryParse(revision, out _))
                    return revision;
            }

            if (_latestVersion.HasValue) return _latestVersion.Value.ToTag();

            if (core != null && VivaVersion.TryParse(core.version, out var coreVersion))
                return coreVersion.ToTag();

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
                    _latestVersion = null;
                    foreach (var tag in tags)
                    {
                        if (!VivaVersion.TryParse(tag, out var version)) continue;
                        if (!_latestVersion.HasValue || version > _latestVersion.Value)
                            _latestVersion = version;
                    }
                }
                Repaint();
            });
        }
    }
}
