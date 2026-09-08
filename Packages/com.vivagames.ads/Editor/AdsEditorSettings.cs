using System;
using System.IO;
using UnityEngine;

namespace Viva.Services.Ads.Editor
{
    /// <summary>Ajustes de la herramienta para este proyecto. Se guardan en ProjectSettings/VivaAdsSettings.json.</summary>
    [Serializable]
    public class AdsEditorSettings
    {
        public const string DEFAULT_FOLDER = "Assets/VivaAds";
        public const string DEFAULT_AD_UNITS_FILE_PATH = DEFAULT_FOLDER + "/AdUnits.json";
        public const string DEFAULT_GENERATED_SCRIPT_PATH = DEFAULT_FOLDER + "/AdPlacements.cs";
        public const string DEFAULT_INIT_SCRIPT_PATH = DEFAULT_FOLDER + "/AdsInit.cs";
        private const string FILE_PATH = "ProjectSettings/VivaAdsSettings.json";

        public string adUnitsFilePath = DEFAULT_AD_UNITS_FILE_PATH;
        public string generatedScriptPath = DEFAULT_GENERATED_SCRIPT_PATH;
        public string initScriptPath = DEFAULT_INIT_SCRIPT_PATH;
        public bool setupShown;

        private static AdsEditorSettings _instance;

        public static AdsEditorSettings Instance => _instance ?? (_instance = Load());

        /// <summary>JSON con formatos, ad units y placements: la fuente de verdad que edita la ventana.</summary>
        public static string AdUnitsFilePath => NormalizePath(Instance.adUnitsFilePath, DEFAULT_AD_UNITS_FILE_PATH);

        /// <summary>Clase AdPlacements generada a partir del JSON.</summary>
        public static string GeneratedScriptPath => NormalizePath(Instance.generatedScriptPath, DEFAULT_GENERATED_SCRIPT_PATH);

        /// <summary>AdsInit.cs del proyecto.</summary>
        public static string InitScriptPath => NormalizePath(Instance.initScriptPath, DEFAULT_INIT_SCRIPT_PATH);

        public void Save()
        {
            try
            {
                File.WriteAllText(FILE_PATH, JsonUtility.ToJson(this, true));
            }
            catch (Exception e)
            {
                Debug.LogError($"{AdsPackage.LOG_PREFIX} Could not save {FILE_PATH}: {e.Message}");
            }
        }

        public static bool IsValidProjectPath(string path)
        {
            var normalized = NormalizePath(path, string.Empty);
            return normalized.StartsWith("Assets/", StringComparison.Ordinal);
        }

        private static AdsEditorSettings Load()
        {
            try
            {
                if (File.Exists(FILE_PATH))
                {
                    var loaded = JsonUtility.FromJson<AdsEditorSettings>(File.ReadAllText(FILE_PATH));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{AdsPackage.LOG_PREFIX} Could not read {FILE_PATH}, using defaults: {e.Message}");
            }
            return new AdsEditorSettings();
        }

        private static string NormalizePath(string path, string fallback)
        {
            if (string.IsNullOrWhiteSpace(path)) return fallback;
            return path.Trim().Replace('\\', '/').TrimEnd('/');
        }
    }
}
