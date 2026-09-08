using System;
using System.IO;
using UnityEngine;
using Viva.Core.Editor;

namespace Viva.Services.RemoteConfig.Editor
{
    /// <summary>
    /// Localiza el paquete de Remote Config en disco: versión y plantillas (Templates~), que Unity no importa como assets.
    /// </summary>
    internal static class RemoteConfigPackage
    {
        public const string PACKAGE_NAME = "com.vivagames.remoteconfig";
        public const string LOG_PREFIX = "[RemoteConfig]";

        public static string Version => VivaPackageUtility.GetPackageVersion(typeof(RemoteConfigPackage).Assembly) ?? "unknown";

        public static string ResolvedPath => VivaPackageUtility.GetPackageResolvedPath(typeof(RemoteConfigPackage).Assembly);

        public static string TemplatesPath => ResolvedPath == null ? null : Path.Combine(ResolvedPath, "Templates~");

        public static string InitTemplatePath => TemplatesPath == null ? null : Path.Combine(TemplatesPath, "RemoteConfigInit.cs.txt");

        public static string CompatManagerTemplatePath => TemplatesPath == null ? null : Path.Combine(TemplatesPath, "RemoteConfigManager.compat.cs.txt");

        /// <summary>Lee una plantilla. Devuelve null (y deja un error en la consola) si no se encuentra.</summary>
        public static string ReadTemplate(string path)
        {
            if (path == null || !File.Exists(path))
            {
                Debug.LogError($"{LOG_PREFIX} Template not found: {path ?? "(package path unresolved)"}");
                return null;
            }

            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"{LOG_PREFIX} Could not read template {path}: {e.Message}");
                return null;
            }
        }
    }
}
