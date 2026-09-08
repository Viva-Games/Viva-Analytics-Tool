using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Viva.Services.RemoteConfig.Editor
{
    public enum GeneratedScriptState
    {
        Missing,
        UpToDate,
        Outdated
    }

    /// <summary>
    /// Ficheros del proyecto que mantiene la herramienta: el JSON de parámetros, la clase generada, el
    /// RemoteConfigInit y la migración del manager antiguo. Lo comparten las dos ventanas.
    /// </summary>
    public static class RemoteConfigProjectFiles
    {
        private const string LOG_PREFIX = RemoteConfigPackage.LOG_PREFIX;

        public static List<RemoteConfigParameterDefinition> LoadParameters()
        {
            return RemoteConfigParameterFile.Load(RemoteConfigEditorSettings.ParametersFilePath);
        }

        public static bool ParametersFileExists => File.Exists(RemoteConfigEditorSettings.ParametersFilePath);

        public static bool GeneratedScriptExists => File.Exists(RemoteConfigEditorSettings.GeneratedScriptPath);

        public static bool InitScriptExists => File.Exists(RemoteConfigEditorSettings.InitScriptPath);

        /// <summary>
        /// Guarda el JSON y regenera la clase. Los valores se normalizan antes (números en forma canónica).
        /// Devuelve false si algo no se pudo escribir.
        /// </summary>
        public static bool SaveParametersAndGenerate(List<RemoteConfigParameterDefinition> parameters)
        {
            foreach (var parameter in parameters)
            {
                parameter.key = (parameter.key ?? string.Empty).Trim();
                parameter.defaultValue = RemoteConfigValidator.NormalizeValue(parameter.type, parameter.defaultValue);
                parameter.description = (parameter.description ?? string.Empty).Trim();
            }

            if (!RemoteConfigParameterFile.Save(RemoteConfigEditorSettings.ParametersFilePath, parameters)) return false;
            if (!WriteGeneratedScript(parameters)) return false;

            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
            return true;
        }

        /// <summary>Crea el JSON vacío y la clase generada si no existen.</summary>
        public static bool CreateParametersFileIfMissing()
        {
            if (ParametersFileExists) return true;
            return SaveParametersAndGenerate(new List<RemoteConfigParameterDefinition>());
        }

        public static bool WriteGeneratedScript(IReadOnlyList<RemoteConfigParameterDefinition> parameters)
        {
            var path = RemoteConfigEditorSettings.GeneratedScriptPath;
            try
            {
                EnsureFolder(Path.GetDirectoryName(path));
                File.WriteAllText(path, RemoteConfigCodeGenerator.Generate(parameters, RemoteConfigEditorSettings.ParametersFilePath));
                Debug.Log($"{LOG_PREFIX} Generated {path} with {parameters.Count} parameter(s).");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG_PREFIX} Could not write {path}: {e.Message}");
                return false;
            }
        }

        /// <summary>Regenera la clase a partir del JSON guardado.</summary>
        public static bool RegenerateScript()
        {
            if (!WriteGeneratedScript(LoadParameters())) return false;
            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
            return true;
        }

        /// <summary>Estado de la clase generada respecto al JSON guardado.</summary>
        public static GeneratedScriptState GetGeneratedScriptState()
        {
            var path = RemoteConfigEditorSettings.GeneratedScriptPath;
            if (!File.Exists(path)) return GeneratedScriptState.Missing;

            try
            {
                var expected = RemoteConfigCodeGenerator.Generate(LoadParameters(), RemoteConfigEditorSettings.ParametersFilePath);
                return Normalize(File.ReadAllText(path)) == Normalize(expected) ? GeneratedScriptState.UpToDate : GeneratedScriptState.Outdated;
            }
            catch (Exception)
            {
                return GeneratedScriptState.Outdated;
            }
        }

        /// <summary>Genera RemoteConfigInit.cs desde la plantilla si no existe. Nunca sobrescribe el del usuario.</summary>
        public static bool GenerateInitScript()
        {
            var path = RemoteConfigEditorSettings.InitScriptPath;
            if (File.Exists(path))
            {
                Debug.Log($"{LOG_PREFIX} {path} already exists; it belongs to the project and is not overwritten.");
                return true;
            }

            var template = RemoteConfigPackage.ReadTemplate(RemoteConfigPackage.InitTemplatePath);
            if (template == null) return false;

            try
            {
                EnsureFolder(Path.GetDirectoryName(path));
                File.WriteAllText(path, template);
                AssetDatabase.Refresh();
                Debug.Log($"{LOG_PREFIX} Generated {path}. Add the RemoteConfigInit component to a GameObject in the first scene.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG_PREFIX} Could not write {path}: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Sustituye el RemoteConfigManager antiguo por el envoltorio de compatibilidad, guardando el original
        /// como .txt. El fichero .cs se conserva (mismo GUID). Exige que la clase generada exista.
        /// </summary>
        public static bool MigrateLegacyManager(string legacyManagerPath)
        {
            if (!GeneratedScriptExists)
            {
                Debug.LogError($"{LOG_PREFIX} Generate {RemoteConfigEditorSettings.GeneratedScriptPath} first: the wrapper uses RemoteConfigParameters.Defaults().");
                return false;
            }

            var template = RemoteConfigPackage.ReadTemplate(RemoteConfigPackage.CompatManagerTemplatePath);
            if (template == null) return false;

            var backupPath = LegacyRemoteConfigImporter.BackupPathFor(legacyManagerPath);
            try
            {
                EnsureFolder(Path.GetDirectoryName(backupPath));
                File.Copy(legacyManagerPath, backupPath, true);
                File.WriteAllText(legacyManagerPath, template.Replace("{{BACKUP_PATH}}", backupPath));
                AssetDatabase.Refresh();
                CompilationPipeline.RequestScriptCompilation();
                Debug.Log($"{LOG_PREFIX} {legacyManagerPath} is now a compatibility wrapper over RemoteConfigService. Original kept in {backupPath}.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG_PREFIX} Could not migrate {legacyManagerPath}: {e.Message}");
                return false;
            }
        }

        /// <summary>Ruta del AnalyticsInit.cs del proyecto (según los ajustes de Viva Analytics), o null si no existe.</summary>
        public static string FindAnalyticsInitScript()
        {
            const string settingsPath = "ProjectSettings/VivaAnalyticsSettings.json";
            string path = "Assets/VivaAnalytics/AnalyticsInit.cs";
            try
            {
                if (File.Exists(settingsPath))
                {
                    var settings = JsonUtility.FromJson<AnalyticsSettingsProbe>(File.ReadAllText(settingsPath));
                    if (settings != null && !string.IsNullOrWhiteSpace(settings.initScriptPath)) path = settings.initScriptPath.Trim().Replace('\\', '/');
                }
            }
            catch (Exception)
            {
                // Ajustes ilegibles: se prueba la ruta por defecto.
            }
            return File.Exists(path) ? path : null;
        }

        /// <summary>true si AnalyticsInit.cs ya inicializa Remote Config (Arrows: RemoteConfigManager.Initialize() en OnFirebaseReady).</summary>
        public static bool AnalyticsInitInitializesRemoteConfig(string analyticsInitPath)
        {
            try
            {
                var content = File.ReadAllText(analyticsInitPath);
                return content.Contains("RemoteConfigManager.Initialize(") || content.Contains("RemoteConfigService.Initialize(");
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void EnsureFolder(string folder)
        {
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);
        }

        private static string Normalize(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Trim();
        }

        [Serializable]
        private class AnalyticsSettingsProbe
        {
            public string initScriptPath;
        }
    }
}
