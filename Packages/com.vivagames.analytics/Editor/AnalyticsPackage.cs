using System;
using System.IO;
using UnityEngine;
using Viva.Core.Editor;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Localiza el paquete de Analytics en disco: versión y plantillas (Templates~), que Unity no importa como assets.
    /// </summary>
    internal static class AnalyticsPackage
    {
        public const string PACKAGE_NAME = "com.vivagames.analytics";

        public static string Version => VivaPackageUtility.GetPackageVersion(typeof(AnalyticsPackage).Assembly) ?? "unknown";

        public static string ResolvedPath => VivaPackageUtility.GetPackageResolvedPath(typeof(AnalyticsPackage).Assembly);

        public static string TemplatesPath => ResolvedPath == null ? null : Path.Combine(ResolvedPath, "Templates~");

        public static string EventCatalogPath => TemplatesPath == null ? null : Path.Combine(TemplatesPath, "EventCatalog.json");

        public static string InitTemplatePath => TemplatesPath == null ? null : Path.Combine(TemplatesPath, "AnalyticsInit.cs.txt");

        /// <summary>Lee una plantilla. Devuelve null (y deja un error en la consola) si no se encuentra.</summary>
        public static string ReadTemplate(string path)
        {
            if (path == null || !File.Exists(path))
            {
                Debug.LogError($"[Analytics] Template not found: {path ?? "(package path unresolved)"}");
                return null;
            }

            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Analytics] Could not read template {path}: {e.Message}");
                return null;
            }
        }
    }
}
