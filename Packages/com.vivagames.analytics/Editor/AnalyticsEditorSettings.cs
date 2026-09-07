using System;
using System.IO;
using UnityEngine;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Ajustes de la herramienta para este proyecto. Se guardan en ProjectSettings/VivaAnalyticsSettings.json
    /// para que se versionen junto al proyecto.
    /// </summary>
    [Serializable]
    public class AnalyticsEditorSettings
    {
        public const string DEFAULT_EVENTS_FOLDER = "Assets/VivaAnalytics/Events";
        public const string DEFAULT_INIT_SCRIPT_PATH = "Assets/VivaAnalytics/AnalyticsInit.cs";
        private const string FILE_PATH = "ProjectSettings/VivaAnalyticsSettings.json";

        public string eventsFolder = DEFAULT_EVENTS_FOLDER;
        public string initScriptPath = DEFAULT_INIT_SCRIPT_PATH;
        public bool setupShown;

        private static AnalyticsEditorSettings _instance;

        public static AnalyticsEditorSettings Instance => _instance ?? (_instance = Load());

        public static bool Exists => File.Exists(FILE_PATH);

        /// <summary>Carpeta del proyecto donde se generan los eventos.</summary>
        public static string EventsFolder => NormalizePath(Instance.eventsFolder, DEFAULT_EVENTS_FOLDER);

        /// <summary>Ruta del AnalyticsInit.cs del proyecto.</summary>
        public static string InitScriptPath => NormalizePath(Instance.initScriptPath, DEFAULT_INIT_SCRIPT_PATH);

        public void Save()
        {
            try
            {
                File.WriteAllText(FILE_PATH, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[Analytics] Could not save {FILE_PATH}: {e.Message}");
            }
        }

        /// <summary>true si la ruta está dentro de Assets, que es donde deben vivir los ficheros del usuario.</summary>
        public static bool IsValidProjectPath(string path)
        {
            var normalized = NormalizePath(path, string.Empty);
            return normalized.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private static AnalyticsEditorSettings Load()
        {
            try
            {
                if (File.Exists(FILE_PATH))
                {
                    var loaded = JsonUtility.FromJson<AnalyticsEditorSettings>(File.ReadAllText(FILE_PATH));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] Could not read {FILE_PATH}, using defaults: {e.Message}");
            }
            return new AnalyticsEditorSettings();
        }

        private static string NormalizePath(string path, string fallback)
        {
            if (string.IsNullOrWhiteSpace(path)) return fallback;
            return path.Trim().Replace('\\', '/').TrimEnd('/');
        }
    }
}
