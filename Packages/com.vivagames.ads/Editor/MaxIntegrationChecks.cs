using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Viva.Services.Ads.Editor
{
    /// <summary>
    /// Lee lo que el usuario configura en AppLovin > Integration Manager, solo para avisar en Setup de lo
    /// que falta: SDK key (Assets/MaxSdk/Resources/AppLovinSettings.asset), flujo de consentimiento
    /// (ProjectSettings/AppLovinInternalSettings.json) y adaptadores de mediación. Nunca escribe en esos ficheros.
    /// </summary>
    public static class MaxIntegrationChecks
    {
        public const string SETTINGS_ASSET_PATH = "Assets/MaxSdk/Resources/AppLovinSettings.asset";
        public const string INTERNAL_SETTINGS_PATH = "ProjectSettings/AppLovinInternalSettings.json";
        private const string MANIFEST_PATH = "Packages/manifest.json";
        private const string MEDIATION_FOLDER = "Assets/MaxSdk/Mediation";

        private static readonly Regex SdkKeyPattern = new Regex(@"^\s*sdkKey:\s*(.*?)\s*$", RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex AdapterPackagePattern = new Regex("\"com\\.applovin\\.mediation\\.adapters\\.[^\"]+\"\\s*:", RegexOptions.Compiled);

        [Serializable]
        private class InternalSettingsProbe
        {
            public bool consentFlowEnabled;
            public string consentFlowPrivacyPolicyUrl;
        }

        public static bool SettingsAssetExists => File.Exists(SETTINGS_ASSET_PATH);

        /// <summary>SDK key configurada, cadena vacía si el campo está vacío, null si el asset no existe.</summary>
        public static string ReadSdkKey()
        {
            if (!SettingsAssetExists) return null;
            try
            {
                var match = SdkKeyPattern.Match(File.ReadAllText(SETTINGS_ASSET_PATH));
                return match.Success ? match.Groups[1].Value.Trim() : string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <summary>true si el flujo de consentimiento de MAX está activado y tiene URL de política de privacidad.</summary>
        public static bool ReadConsentFlow(out bool enabled, out string privacyPolicyUrl)
        {
            enabled = false;
            privacyPolicyUrl = string.Empty;
            if (!File.Exists(INTERNAL_SETTINGS_PATH)) return false;
            try
            {
                var probe = JsonUtility.FromJson<InternalSettingsProbe>(File.ReadAllText(INTERNAL_SETTINGS_PATH));
                if (probe == null) return false;
                enabled = probe.consentFlowEnabled;
                privacyPolicyUrl = probe.consentFlowPrivacyPolicyUrl ?? string.Empty;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>Adaptadores de mediación instalados: paquetes com.applovin.mediation.adapters.* del manifest más carpetas de Assets/MaxSdk/Mediation.</summary>
        public static int CountAdapters()
        {
            int count = 0;
            try
            {
                if (File.Exists(MANIFEST_PATH)) count += AdapterPackagePattern.Matches(File.ReadAllText(MANIFEST_PATH)).Count;
                if (Directory.Exists(MEDIATION_FOLDER)) count += Directory.GetDirectories(MEDIATION_FOLDER).Length;
            }
            catch (Exception)
            {
                // Sin acceso: se informa como cero y el aviso lo dice.
            }
            return count;
        }
    }
}
