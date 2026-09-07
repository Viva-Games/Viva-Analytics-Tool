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
    /// Con "Show development branches" cada módulo puede instalarse además desde cualquier rama del repositorio,
    /// de forma independiente al resto, para probar trabajo sin publicar.
    /// </summary>
    public class VivaPackageInstallerWindow : EditorWindow
    {
        private const string SHOW_BRANCHES_PREF = "Viva.PackageInstaller.ShowBranches";
        private const string DEFAULT_BRANCH = "develop";

        private readonly Dictionary<string, PackageInfo> _installed = new Dictionary<string, PackageInfo>();
        private ListRequest _listRequest;
        private bool _listLoaded;
        private string _listError;

        private bool _checkingRepository;
        private bool _repositoryChecked;
        private string _repositoryError;

        // Última release publicada de cada módulo, por nombre de paquete.
        private readonly Dictionary<string, VivaVersion> _latestByModule = new Dictionary<string, VivaVersion>();
        private readonly List<string> _branches = new List<string>();
        private readonly Dictionary<string, string> _selectedBranch = new Dictionary<string, string>();
        private bool _showBranches;
        private Vector2 _scroll;

        [MenuItem("Viva/Package Installer")]
        public static void Open()
        {
            var window = GetWindow<VivaPackageInstallerWindow>("Viva Packages");
            window.minSize = new Vector2(600, 360);
        }

        private void OnEnable()
        {
            _showBranches = EditorPrefs.GetBool(SHOW_BRANCHES_PREF, false);
            VivaPackageOperations.Changed += OnOperationsChanged;
            RefreshInstalled();
            CheckRepository();
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

        #region Header and footer

        private void DrawHeader()
        {
            GUILayout.Label("Viva Packages", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Repository", VivaRepository.GitUrl, EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (_checkingRepository)
                EditorGUILayout.LabelField("Repository: checking...");
            else if (!string.IsNullOrEmpty(_repositoryError))
                EditorGUILayout.LabelField("Repository: unknown");
            else if (_repositoryChecked)
                EditorGUILayout.LabelField($"Repository: {_latestByModule.Count} module release(s), {_branches.Count} branch(es)");
            else
                EditorGUILayout.LabelField("Repository: not checked");

            using (new EditorGUI.DisabledScope(_checkingRepository))
            {
                if (GUILayout.Button("Check for updates", GUILayout.Width(140)))
                    CheckRepository();
            }
            EditorGUILayout.EndHorizontal();

            bool showBranches = EditorGUILayout.ToggleLeft("Show development branches (unreleased work, per module)", _showBranches);
            if (showBranches != _showBranches)
            {
                _showBranches = showBranches;
                EditorPrefs.SetBool(SHOW_BRANCHES_PREF, showBranches);
            }

            if (_showBranches)
            {
                EditorGUILayout.HelpBox(
                    "Branches contain unreleased work. A module installed from a branch stays pinned to the commit it was " +
                    "installed at: use Pull latest to move it forward, and Switch to the release once it is published. " +
                    "Each module chooses its branch on its own.",
                    MessageType.Info);
            }

            if (!string.IsNullOrEmpty(_repositoryError))
                EditorGUILayout.HelpBox("Could not read the repository: " + _repositoryError, MessageType.Warning);
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

        private void DrawFooter()
        {
            EditorGUILayout.Space();

            var outdated = OutdatedModules();
            if (outdated.Count > 0)
            {
                using (new EditorGUI.DisabledScope(VivaPackageOperations.IsBusy))
                {
                    if (GUILayout.Button($"Update all ({outdated.Count}) to their latest release"))
                        VivaPackageOperations.InstallAll(outdated, LatestTag);
                }
            }

            EditorGUILayout.HelpBox(
                "Each module has its own version and release tags. Installing or updating writes to " +
                "Packages/manifest.json and packages-lock.json: commit both files so everyone in the project gets the same versions.",
                MessageType.None);
        }

        #endregion

        #region Module rows

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
            DrawReleaseRow(module, info, latestTag, busy);

            if (_showBranches && (info == null || info.source != PackageSource.Embedded))
                DrawBranchRow(module, info, busy);

            EditorGUILayout.EndVertical();
        }

        /// <summary>Estado del módulo y acciones sobre releases: instalar, actualizar, anclar a release, quitar.</summary>
        private void DrawReleaseRow(VivaModule module, PackageInfo info, string latestTag, bool busy)
        {
            EditorGUILayout.BeginHorizontal();
            if (info == null)
            {
                GUILayout.Label(latestTag == null
                        ? "Not installed. No release published yet."
                        : $"Not installed. Latest release: {latestTag}",
                    EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledScope(busy || latestTag == null))
                {
                    if (GUILayout.Button(latestTag == null ? "Install" : $"Install {latestTag}", GUILayout.Width(190)))
                        InstallModule(module, latestTag);
                }
            }
            else
            {
                GUILayout.Label(DescribeInstalled(info) + (latestTag != null ? $"   Latest release: {latestTag}" : string.Empty), EditorStyles.miniLabel);
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
                            if (GUILayout.Button($"Update to {latestTag}", GUILayout.Width(190)))
                                VivaPackageOperations.Install(module, latestTag);
                        }
                    }
                    else if (latestTag != null && IsInstalledFromBranch(info))
                    {
                        // Instalado desde una rama: se ofrece anclarlo a la release publicada.
                        using (new EditorGUI.DisabledScope(busy))
                        {
                            if (GUILayout.Button($"Switch to {latestTag}", GUILayout.Width(190)))
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
        }

        /// <summary>Selector de rama del módulo con instalar desde rama, cambiar de rama o traer el último commit.</summary>
        private void DrawBranchRow(VivaModule module, PackageInfo info, bool busy)
        {
            if (_branches.Count == 0)
            {
                GUILayout.Label(_repositoryChecked ? "No branches found in the repository." : "Branches not loaded yet.", EditorStyles.miniLabel);
                return;
            }

            string installedBranch = info != null && IsInstalledFromBranch(info) ? info.git.revision : null;
            string selected = SelectedBranch(module, installedBranch);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Branch", EditorStyles.miniLabel, GUILayout.Width(50));

            int index = Mathf.Max(0, _branches.IndexOf(selected));
            int newIndex = EditorGUILayout.Popup(index, _branches.ToArray(), GUILayout.Width(180));
            selected = _branches[newIndex];
            _selectedBranch[module.PackageName] = selected;

            if (installedBranch != null)
                GUILayout.Label($"installed from {installedBranch}", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(busy))
            {
                if (info == null)
                {
                    if (GUILayout.Button($"Install from {selected}", GUILayout.Width(190)))
                        InstallModule(module, selected);
                }
                else if (installedBranch == selected)
                {
                    // Volver a añadir la misma URL hace que el Package Manager resuelva el último commit de la rama.
                    if (GUILayout.Button($"Pull latest {selected}", GUILayout.Width(190)))
                        VivaPackageOperations.Install(module, selected);
                }
                else
                {
                    if (GUILayout.Button($"Switch to {selected}", GUILayout.Width(190)))
                        VivaPackageOperations.Install(module, selected);
                }
            }
            EditorGUILayout.EndHorizontal();
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

        /// <summary>Instalación nueva: retira antes la instalación antigua por .unitypackage si la hay.</summary>
        private static void InstallModule(VivaModule module, string reference)
        {
            if (LegacyInstallCleaner.PrepareForInstall(module))
                VivaPackageOperations.Install(module, reference);
        }

        #endregion

        #region State helpers

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

        /// <summary>true si está instalado desde un tag de release y hay una release más nueva.</summary>
        private bool IsOutdated(VivaModule module, PackageInfo info)
        {
            if (IsInstalledFromBranch(info)) return false;
            if (!_latestByModule.TryGetValue(module.PackageName, out var latest)) return false;
            if (!VivaVersion.TryParse(info.version, out var installed)) return false;
            return latest > installed;
        }

        /// <summary>true si el paquete viene de git con una rama o un commit, no con un tag de release.</summary>
        private static bool IsInstalledFromBranch(PackageInfo info)
        {
            if (info.source != PackageSource.Git || info.git == null) return false;
            var revision = info.git.revision;
            return !string.IsNullOrEmpty(revision) && !VivaRepository.TryParseTag(revision, out _, out _);
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

        /// <summary>Rama seleccionada para el módulo: la elegida en la ventana, la instalada, develop, o la primera.</summary>
        private string SelectedBranch(VivaModule module, string installedBranch)
        {
            if (_selectedBranch.TryGetValue(module.PackageName, out var chosen) && _branches.Contains(chosen))
                return chosen;
            if (installedBranch != null && _branches.Contains(installedBranch))
                return installedBranch;
            if (_branches.Contains(DEFAULT_BRANCH))
                return DEFAULT_BRANCH;
            return _branches[0];
        }

        #endregion

        #region Requests

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

        private void CheckRepository()
        {
            if (_checkingRepository) return;
            _checkingRepository = true;
            _repositoryError = null;

            GitTagFetcher.FetchRefs(VivaRepository.GitUrl, (refs, error) =>
            {
                if (this == null) return; // la ventana se cerró mientras tanto

                _checkingRepository = false;
                if (error != null)
                {
                    _repositoryError = error;
                }
                else
                {
                    _repositoryChecked = true;

                    _latestByModule.Clear();
                    foreach (var tag in refs.Tags)
                    {
                        if (!VivaRepository.TryParseTag(tag, out var prefix, out var version)) continue;

                        var module = VivaModuleCatalog.FindByTagPrefix(prefix);
                        if (module == null) continue;

                        if (!_latestByModule.TryGetValue(module.PackageName, out var current) || version > current)
                            _latestByModule[module.PackageName] = version;
                    }

                    _branches.Clear();
                    _branches.AddRange(refs.Branches);
                }
                Repaint();
            });
        }

        #endregion
    }
}
