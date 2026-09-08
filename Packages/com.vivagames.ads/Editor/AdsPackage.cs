using System;
using System.IO;
using UnityEngine;
using Viva.Core.Editor;

namespace Viva.Services.Ads.Editor
{
    /// <summary>Localiza el paquete de Ads en disco: versión y plantillas (Templates~), que Unity no importa como assets.</summary>
    internal static class AdsPackage
    {
        public const string PACKAGE_NAME = "com.vivagames.ads";
        public const string LOG_PREFIX = "[Ads]";

        public static string Version => VivaPackageUtility.GetPackageVersion(typeof(AdsPackage).Assembly) ?? "unknown";

        public static string ResolvedPath => VivaPackageUtility.GetPackageResolvedPath(typeof(AdsPackage).Assembly);

        public static string TemplatesPath => ResolvedPath == null ? null : Path.Combine(ResolvedPath, "Templates~");

        public static string InitTemplatePath => TemplatesPath == null ? null : Path.Combine(TemplatesPath, "AdsInit.cs.txt");

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
