using System;
using System.IO;
using UnityEngine;

namespace Viva.Services.RemoteConfig.Editor
{
    /// <summary>
    /// Ajustes de la herramienta para este proyecto. Se guardan en ProjectSettings/VivaRemoteConfigSettings.json
    /// para que se versionen junto al proyecto.
    /// </summary>
    [Serializable]
    public class RemoteConfigEditorSettings
    {
        public const string DEFAULT_FOLDER = "Assets/VivaRemoteConfig";
        public const string DEFAULT_PARAMETERS_FILE_PATH = DEFAULT_FOLDER + "/RemoteConfigParameters.json";
        public const string DEFAULT_GENERATED_SCRIPT_PATH = DEFAULT_FOLDER + "/RemoteConfigParameters.cs";
        public const string DEFAULT_INIT_SCRIPT_PATH = DEFAULT_FOLDER + "/RemoteConfigInit.cs";
        private const string FILE_PATH = "ProjectSettings/VivaRemoteConfigSettings.json";

        public string parametersFilePath = DEFAULT_PARAMETERS_FILE_PATH;
        public string generatedScriptPath = DEFAULT_GENERATED_SCRIPT_PATH;
        public string initScriptPath = DEFAULT_INIT_SCRIPT_PATH;
        public bool setupShown;

        private static RemoteConfigEditorSettings _instance;

        public static RemoteConfigEditorSettings Instance => _instance ?? (_instance = Load());

        public static bool Exists => File.Exists(FILE_PATH);

        /// <summary>JSON con los parámetros: la fuente de verdad que edita la ventana.</summary>
        public static string ParametersFilePath => NormalizePath(Instance.parametersFilePath, DEFAULT_PARAMETERS_FILE_PATH);

        /// <summary>Clase RemoteConfigParameters generada a partir del JSON.</summary>
        public static string GeneratedScriptPath => NormalizePath(Instance.generatedScriptPath, DEFAULT_GENERATED_SCRIPT_PATH);

        /// <summary>RemoteConfigInit.cs del proyecto.</summary>
        public static string InitScriptPath => NormalizePath(Instance.initScriptPath, DEFAULT_INIT_SCRIPT_PATH);

        public void Save()
        {
            try
            {
                File.WriteAllText(FILE_PATH, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"{RemoteConfigPackage.LOG_PREFIX} Could not save {FILE_PATH}: {e.Message}");
            }
        }

        /// <summary>true si la ruta está dentro de Assets, que es donde deben vivir los ficheros del usuario.</summary>
        public static bool IsValidProjectPath(string path)
        {
            var normalized = NormalizePath(path, string.Empty);
            return normalized.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private static RemoteConfigEditorSettings Load()
        {
            try
            {
                if (File.Exists(FILE_PATH))
                {
                    var loaded = JsonUtility.FromJson<RemoteConfigEditorSettings>(File.ReadAllText(FILE_PATH));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{RemoteConfigPackage.LOG_PREFIX} Could not read {FILE_PATH}, using defaults: {e.Message}");
            }
            return new RemoteConfigEditorSettings();
        }

        private static string NormalizePath(string path, string fallback)
        {
            if (string.IsNullOrWhiteSpace(path)) return fallback;
            return path.Trim().Replace('\\', '/').TrimEnd('/');
        }
    }
}
