using UnityEngine;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Ajustes del módulo que el juego necesita en ejecución. Los escribe la ventana Viva > Analytics > Setup en
    /// Assets/VivaAnalytics/Resources/VivaAnalyticsSettings.asset y los lee el runtime con Resources.Load. Sin
    /// asset, valen los valores por defecto de esta clase.
    /// </summary>
    public sealed class AnalyticsSettings : ScriptableObject
    {
        public const string RESOURCE_NAME = "VivaAnalyticsSettings";

        [Tooltip("Attribute in Singular every ad impression published by Viva Ads (VivaAdRevenue). Needs the Singular SDK and Viva Ads.")]
        public bool attributeAdRevenueInSingular = true;

        private static AnalyticsSettings _instance;

        /// <summary>El asset del proyecto, o una instancia con los valores por defecto si no existe.</summary>
        public static AnalyticsSettings Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = Resources.Load<AnalyticsSettings>(RESOURCE_NAME);
                if (_instance == null) _instance = CreateInstance<AnalyticsSettings>();
                return _instance;
            }
        }

        /// <summary>Toggle "Attribute the ad revenue of Viva Ads in Singular" de la ventana Setup. true por defecto.</summary>
        public static bool AttributeAdRevenueInSingular => Instance.attributeAdRevenueInSingular;

        /// <summary>Olvida la instancia cargada, para releer el asset tras cambiarlo en el editor.</summary>
        public static void ClearCache()
        {
            _instance = null;
        }
    }
}
