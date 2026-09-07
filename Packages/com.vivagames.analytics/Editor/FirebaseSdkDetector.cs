using System;
using System.IO;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Viva.Core.Editor;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Detecta si el SDK de Firebase Analytics está en el proyecto y mantiene el símbolo
    /// VIVA_FIREBASE_ANALYTICS, que activa el assembly VivaGames.Analytics.Firebase y el código
    /// del AnalyticsInit del proyecto que dependa de Firebase.
    /// </summary>
    [InitializeOnLoad]
    public static class FirebaseSdkDetector
    {
        public const string DEFINE = "VIVA_FIREBASE_ANALYTICS";
        private const string FIREBASE_ANALYTICS_DLL = "Firebase.Analytics.dll";

        static FirebaseSdkDetector()
        {
            // Se pospone para no tocar PlayerSettings en mitad de la carga del dominio.
            EditorApplication.delayCall += () => Refresh();
        }

        /// <summary>true si Firebase.Analytics.dll está entre los assemblies precompilados del proyecto.</summary>
        public static bool IsFirebaseAnalyticsPresent()
        {
            try
            {
                var paths = CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.All);
                foreach (var path in paths)
                {
                    if (string.Equals(Path.GetFileName(path), FIREBASE_ANALYTICS_DLL, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Analytics] Could not inspect precompiled assemblies: {e.Message}");
            }
            return false;
        }

        public static bool IsDefineEnabled()
        {
            return ScriptingDefines.IsDefined(DEFINE);
        }

        /// <summary>Sincroniza el símbolo con la presencia del SDK. Devuelve true si ha cambiado algo.</summary>
        public static bool Refresh()
        {
            bool present = IsFirebaseAnalyticsPresent();
            bool changed = ScriptingDefines.Set(DEFINE, present);
            if (changed)
            {
                Debug.Log(present
                    ? $"[Analytics] Firebase Analytics SDK detected. {DEFINE} enabled."
                    : $"[Analytics] Firebase Analytics SDK not found. {DEFINE} disabled.");
            }
            return changed;
        }
    }
}
