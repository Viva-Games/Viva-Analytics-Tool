using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Viva.Services.Ads.Editor
{
    public enum AdsGeneratedState
    {
        Missing,
        UpToDate,
        Outdated
    }

    /// <summary>
    /// Ficheros del proyecto que mantiene la herramienta: el JSON de ad units, la clase generada y AdsInit.
    /// Lo comparten las dos ventanas.
    /// </summary>
    public static class AdsProjectFiles
    {
        private const string LOG_PREFIX = AdsPackage.LOG_PREFIX;
        private const string ANALYTICS_PACKAGE = "com.vivagames.analytics";
        private const string ANALYTICS_SETTINGS_PATH = "ProjectSettings/VivaAnalyticsSettings.json";
        private const string DEFAULT_EVENTS_FOLDER = "Assets/VivaAnalytics/Events";

        public static AdUnitsDefinition LoadDefinition()
        {
            return AdUnitsFile.Load(AdsEditorSettings.AdUnitsFilePath);
        }

        public static bool AdUnitsFileExists => File.Exists(AdsEditorSettings.AdUnitsFilePath);
        public static bool GeneratedScriptExists => File.Exists(AdsEditorSettings.GeneratedScriptPath);
        public static bool InitScriptExists => File.Exists(AdsEditorSettings.InitScriptPath);

        /// <summary>Guarda el JSON (con ad units y placements sin espacios) y regenera la clase.</summary>
        public static bool SaveAndGenerate(AdUnitsDefinition definition)
        {
            foreach (AdFormat format in Enum.GetValues(typeof(AdFormat)))
            {
                var section = definition.Get(format);
                if (section == null) continue;
                section.androidAdUnitId = (section.androidAdUnitId ?? string.Empty).Trim();
                section.iosAdUnitId = (section.iosAdUnitId ?? string.Empty).Trim();
                for (int i = 0; i < section.placements.Count; i++) section.placements[i] = (section.placements[i] ?? string.Empty).Trim();
            }

            if (!AdUnitsFile.Save(AdsEditorSettings.AdUnitsFilePath, definition)) return false;
            if (!WriteGeneratedScript(definition)) return false;

            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
            return true;
        }

        public static bool CreateAdUnitsFileIfMissing()
        {
            return AdUnitsFileExists || SaveAndGenerate(new AdUnitsDefinition());
        }

        public static bool WriteGeneratedScript(AdUnitsDefinition definition)
        {
            var path = AdsEditorSettings.GeneratedScriptPath;
            try
            {
                EnsureFolder(Path.GetDirectoryName(path));
                File.WriteAllText(path, AdsCodeGenerator.Generate(definition, AdsEditorSettings.AdUnitsFilePath));
                Debug.Log($"{LOG_PREFIX} Generated {path}.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG_PREFIX} Could not write {path}: {e.Message}");
                return false;
            }
        }

        public static bool RegenerateScript()
        {
            if (!WriteGeneratedScript(LoadDefinition())) return false;
            AssetDatabase.Refresh();
            CompilationPipeline.RequestScriptCompilation();
            return true;
        }

        public static AdsGeneratedState GetGeneratedState()
        {
            var path = AdsEditorSettings.GeneratedScriptPath;
            if (!File.Exists(path)) return AdsGeneratedState.Missing;
            try
            {
                var expected = AdsCodeGenerator.Generate(LoadDefinition(), AdsEditorSettings.AdUnitsFilePath);
                return Normalize(File.ReadAllText(path)) == Normalize(expected) ? AdsGeneratedState.UpToDate : AdsGeneratedState.Outdated;
            }
            catch (Exception)
            {
                return AdsGeneratedState.Outdated;
            }
        }

        #region AdsInit

        public static bool IsAnalyticsInstalled()
        {
            try
            {
                return PackageInfo.FindForAssetPath("Packages/" + ANALYTICS_PACKAGE) != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Ruta del evento AdImpression del catálogo de Viva Analytics en este proyecto, exista o no.</summary>
        public static string AdImpressionEventPath()
        {
            string folder = DEFAULT_EVENTS_FOLDER;
            try
            {
                if (File.Exists(ANALYTICS_SETTINGS_PATH))
                {
                    var probe = JsonUtility.FromJson<AnalyticsSettingsProbe>(File.ReadAllText(ANALYTICS_SETTINGS_PATH));
                    if (probe != null && !string.IsNullOrWhiteSpace(probe.eventsFolder)) folder = probe.eventsFolder.Trim().Replace('\\', '/').TrimEnd('/');
                }
            }
            catch (Exception)
            {
                // Ajustes ilegibles: carpeta por defecto.
            }
            return folder + "/AdImpression.cs";
        }

        public static bool HasAdImpressionEvent() => File.Exists(AdImpressionEventPath());

        /// <summary>
        /// Genera AdsInit.cs desde la plantilla. Con Viva Analytics y el evento ad_impression en el proyecto, el
        /// gancho de ingresos se rellena con la llamada real; si no, con el ejemplo comentado. Nunca sobrescribe.
        /// </summary>
        public static bool GenerateInitScript()
        {
            var path = AdsEditorSettings.InitScriptPath;
            if (File.Exists(path))
            {
                Debug.Log($"{LOG_PREFIX} {path} already exists; it belongs to the project and is not overwritten.");
                return true;
            }

            var template = AdsPackage.ReadTemplate(AdsPackage.InitTemplatePath);
            if (template == null) return false;

            bool trackImpressions = IsAnalyticsInstalled() && HasAdImpressionEvent();
            string hook = trackImpressions
                ? "            // Viva Analytics: the standard ad_impression event of the studio catalog.\n" +
                  "            AdImpression.Track(adName: ad.Placement, adPlatform: \"AppLovin\", adSource: ad.NetworkName,\n" +
                  "                adUnitName: ad.AdUnitId, adFormat: ad.FormatName, value: ad.Revenue, currency: ad.Currency);"
                : "            // With Viva Analytics and the ad_impression event imported (Viva > Analytics > Event Manager):\n" +
                  "            // AdImpression.Track(adName: ad.Placement, adPlatform: \"AppLovin\", adSource: ad.NetworkName,\n" +
                  "            //     adUnitName: ad.AdUnitId, adFormat: ad.FormatName, value: ad.Revenue, currency: ad.Currency);";
            var content = template.Replace("//{{REVENUE_HOOK}}", hook);
            if (trackImpressions) content = content.Replace("using Viva.Services.Ads;", "using Viva.Services.Ads;\nusing Viva.Services.Analytics;");

            try
            {
                EnsureFolder(Path.GetDirectoryName(path));
                File.WriteAllText(path, content);
                AssetDatabase.Refresh();
                Debug.Log($"{LOG_PREFIX} Generated {path}. Add the AdsInit component to a GameObject in the first scene.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG_PREFIX} Could not write {path}: {e.Message}");
                return false;
            }
        }

        #endregion

        public static void EnsureFolder(string folder)
        {
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);
        }

        private static string Normalize(string text) => (text ?? string.Empty).Replace("\r\n", "\n").Trim();

        [Serializable]
        private class AnalyticsSettingsProbe
        {
            public string eventsFolder;
        }
    }
}
