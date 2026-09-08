using System;
using System.IO;
using UnityEditor.Compilation;
using UnityEngine;

namespace Viva.Core.Editor
{
    /// <summary>
    /// Ayudas para que cada módulo detecte el SDK del que depende y mantenga su define de compilación.
    /// </summary>
    public static class SdkDetection
    {
        /// <summary>true si una DLL con ese nombre de fichero está entre los assemblies precompilados del proyecto.</summary>
        public static bool IsPrecompiledAssemblyPresent(string fileName)
        {
            try
            {
                var paths = CompilationPipeline.GetPrecompiledAssemblyPaths(CompilationPipeline.PrecompiledAssemblySources.All);
                foreach (var path in paths)
                {
                    if (string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Viva] Could not inspect precompiled assemblies: {e.Message}");
            }
            return false;
        }

        /// <summary>
        /// Sincroniza un define con la presencia de una DLL. Devuelve true si ha cambiado algo
        /// (en ese caso Unity recompila).
        /// </summary>
        public static bool SyncDefine(string define, string dllFileName, string logPrefix)
        {
            bool present = IsPrecompiledAssemblyPresent(dllFileName);
            bool changed = ScriptingDefines.Set(define, present);
            if (changed)
            {
                Debug.Log(present
                    ? $"{logPrefix} {dllFileName} detected. {define} enabled."
                    : $"{logPrefix} {dllFileName} not found. {define} disabled.");
            }
            return changed;
        }
    }
}
