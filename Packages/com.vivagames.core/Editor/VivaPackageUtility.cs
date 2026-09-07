using System.Reflection;
using UnityEditor.PackageManager;

namespace Viva.Core.Editor
{
    /// <summary>
    /// Ayudas para que cada módulo localice su propio paquete (versión, ruta en disco...).
    /// </summary>
    public static class VivaPackageUtility
    {
        /// <summary>Información del paquete que contiene el assembly, o null si el assembly no está en un paquete.</summary>
        public static PackageInfo GetPackageInfo(Assembly assembly)
        {
            return PackageInfo.FindForAssembly(assembly);
        }

        /// <summary>
        /// Ruta absoluta en disco del paquete que contiene el assembly.
        /// Sirve para leer carpetas acabadas en ~ (Templates~), que Unity no importa como assets.
        /// </summary>
        public static string GetPackageResolvedPath(Assembly assembly)
        {
            return GetPackageInfo(assembly)?.resolvedPath;
        }

        /// <summary>Versión declarada en el package.json del paquete que contiene el assembly.</summary>
        public static string GetPackageVersion(Assembly assembly)
        {
            return GetPackageInfo(assembly)?.version;
        }
    }
}
