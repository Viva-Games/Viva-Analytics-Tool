using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Viva.Core.Editor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Viva.Services.Ads.Editor
{
    /// <summary>
    /// Detecta el plugin de AppLovin MAX por su assembly MaxSdk.Scripts (lo traen tanto el paquete UPM
    /// com.applovin.mediation.ads como el .unitypackage) y mantiene el símbolo VIVA_APPLOVIN_MAX, que activa
    /// el assembly VivaGames.Ads.MaxSdk. Como el plugin son fuentes, en la primera carga el assembly ya existe.
    /// </summary>
    [InitializeOnLoad]
    public static class MaxSdkDetector
    {
        public const string DEFINE = "VIVA_APPLOVIN_MAX";
        public const string ASSEMBLY_NAME = "MaxSdk.Scripts";
        public const string UPM_PACKAGE_NAME = "com.applovin.mediation.ads";
        private const string LEGACY_SCRIPT = "Assets/MaxSdk/Scripts/MaxSdk.cs";
        private const string LEGACY_ASMDEF = "Assets/MaxSdk/Scripts/MaxSdk.Scripts.asmdef";

        static MaxSdkDetector()
        {
            if (Application.isBatchMode) Refresh();
            else EditorApplication.delayCall += () => Refresh();
        }

        /// <summary>true si el assembly MaxSdk.Scripts forma parte de la compilación del proyecto.</summary>
        public static bool IsSdkPresent()
        {
            try
            {
                foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player))
                {
                    if (string.Equals(assembly.name, ASSEMBLY_NAME, StringComparison.Ordinal)) return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{AdsPackage.LOG_PREFIX} Could not inspect the assemblies: {e.Message}");
            }
            return false;
        }

        /// <summary>Versión del paquete UPM del plugin, o null si no está instalado como paquete.</summary>
        public static string UpmPackageVersion()
        {
            try
            {
                var info = PackageInfo.FindForAssetPath("Packages/" + UPM_PACKAGE_NAME);
                return info?.version;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>true si el plugin está en Assets/MaxSdk (instalación por .unitypackage).</summary>
        public static bool IsInAssets()
        {
            return File.Exists(LEGACY_SCRIPT);
        }

        /// <summary>true si hay un plugin en Assets sin el assembly definition: demasiado antiguo para referenciarlo.</summary>
        public static bool IsLegacyWithoutAssembly()
        {
            return File.Exists(LEGACY_SCRIPT) && !File.Exists(LEGACY_ASMDEF);
        }

        /// <summary>Texto para la ventana de Setup: cómo está instalado el plugin.</summary>
        public static string Describe()
        {
            string version = UpmPackageVersion();
            if (version != null) return $"{UPM_PACKAGE_NAME} {version} (Unity Package Manager)";
            if (IsInAssets()) return IsLegacyWithoutAssembly() ? "Assets/MaxSdk without MaxSdk.Scripts.asmdef (too old)" : "Assets/MaxSdk (.unitypackage)";
            return "not found";
        }

        public static bool IsDefineEnabled()
        {
            return ScriptingDefines.IsDefined(DEFINE);
        }

        /// <summary>Sincroniza el símbolo con la presencia del plugin. Devuelve true si ha cambiado algo.</summary>
        public static bool Refresh()
        {
            bool present = IsSdkPresent();
            bool changed = ScriptingDefines.Set(DEFINE, present);
            if (changed)
            {
                Debug.Log(present
                    ? $"{AdsPackage.LOG_PREFIX} AppLovin MAX detected ({Describe()}). {DEFINE} enabled."
                    : $"{AdsPackage.LOG_PREFIX} AppLovin MAX not found. {DEFINE} disabled.");
            }
            return changed;
        }
    }
}
