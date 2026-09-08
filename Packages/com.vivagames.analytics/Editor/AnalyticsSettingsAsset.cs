using System.IO;
using UnityEditor;
using UnityEngine;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Acceso desde el editor al asset <see cref="AnalyticsSettings"/>: lo busca en el proyecto y, si no existe,
    /// lo crea en la carpeta Resources junto a AnalyticsInit.cs (Assets/VivaAnalytics/Resources por defecto).
    /// </summary>
    public static class AnalyticsSettingsAsset
    {
        public const string FILE_NAME = AnalyticsSettings.RESOURCE_NAME + ".asset";

        /// <summary>Ruta donde se crea el asset si no existe: la carpeta Resources junto a AnalyticsInit.cs.</summary>
        public static string DefaultPath
        {
            get
            {
                string folder = Path.GetDirectoryName(AnalyticsEditorSettings.InitScriptPath) ?? "Assets/VivaAnalytics";
                return folder.Replace('\\', '/') + "/Resources/" + FILE_NAME;
            }
        }

        /// <summary>El asset del proyecto, o null si aún no se ha creado.</summary>
        public static AnalyticsSettings Find()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(AnalyticsSettings)))
            {
                var asset = AssetDatabase.LoadAssetAtPath<AnalyticsSettings>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) return asset;
            }
            return null;
        }

        public static string PathOf(AnalyticsSettings settings)
        {
            return settings != null ? AssetDatabase.GetAssetPath(settings) : null;
        }

        public static AnalyticsSettings GetOrCreate()
        {
            var existing = Find();
            if (existing != null) return existing;

            string path = DefaultPath;
            string folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh(); // la carpeta tiene que existir como asset antes de CreateAsset
            }

            var settings = ScriptableObject.CreateInstance<AnalyticsSettings>();
            AssetDatabase.CreateAsset(settings, path);
            AssetDatabase.SaveAssets();
            AnalyticsSettings.ClearCache();
            Debug.Log("[Analytics] Settings asset created at " + path);
            return settings;
        }

        /// <summary>Valor del toggle de Setup; true (el valor por defecto del runtime) mientras no exista el asset.</summary>
        public static bool AttributeAdRevenueInSingular
        {
            get
            {
                var settings = Find();
                return settings == null || settings.attributeAdRevenueInSingular;
            }
        }

        public static void SetAttributeAdRevenueInSingular(bool value)
        {
            var settings = GetOrCreate();
            if (settings.attributeAdRevenueInSingular == value) return;
            settings.attributeAdRevenueInSingular = value;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            AnalyticsSettings.ClearCache();
        }
    }
}
