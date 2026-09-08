using System;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Viva.Core.Editor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Detecta el SDK de Singular por su assembly SingularSDK (paquete UPM desde GitHub, o fuentes en Assets
    /// con su asmdef) y mantiene el símbolo VIVA_SINGULAR, que activa el assembly VivaGames.Analytics.Singular.
    /// </summary>
    [InitializeOnLoad]
    public static class SingularSdkDetector
    {
        public const string DEFINE = "VIVA_SINGULAR";
        public const string ASSEMBLY_NAME = "SingularSDK";
        public const string UPM_PACKAGE_NAME = "singular-unity-package";

        static SingularSdkDetector()
        {
            if (Application.isBatchMode) Refresh();
            else EditorApplication.delayCall += () => Refresh();
        }

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
                Debug.LogWarning($"[Analytics] Could not inspect the assemblies: {e.Message}");
            }
            return false;
        }

        /// <summary>Versión del paquete UPM de Singular, o null si no está instalado como paquete.</summary>
        public static string UpmPackageVersion()
        {
            try
            {
                return PackageInfo.FindForAssetPath("Packages/" + UPM_PACKAGE_NAME)?.version;
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static bool IsDefineEnabled() => ScriptingDefines.IsDefined(DEFINE);

        public static bool Refresh()
        {
            bool present = IsSdkPresent();
            bool changed = ScriptingDefines.Set(DEFINE, present);
            if (changed)
            {
                Debug.Log(present
                    ? $"[Analytics] Singular SDK detected. {DEFINE} enabled."
                    : $"[Analytics] Singular SDK not found. {DEFINE} disabled.");
            }
            return changed;
        }
    }
}
