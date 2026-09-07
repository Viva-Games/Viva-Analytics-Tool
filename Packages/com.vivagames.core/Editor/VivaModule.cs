namespace Viva.Core.Editor
{
    /// <summary>
    /// Un módulo instalable desde el Viva Package Installer.
    /// </summary>
    public sealed class VivaModule
    {
        /// <summary>Nombre del paquete (com.vivagames.analytics).</summary>
        public string PackageName { get; }

        /// <summary>Nombre que se muestra en el instalador.</summary>
        public string DisplayName { get; }

        /// <summary>Descripción corta que se muestra en el instalador.</summary>
        public string Description { get; }

        /// <summary>
        /// Prefijo de los tags de release de este módulo: sus versiones se publican como prefijo/vX.Y.Z
        /// (analytics/v2.0.1). Cada módulo se versiona y se publica por separado.
        /// </summary>
        public string TagPrefix { get; }

        /// <summary>true para el paquete core, que no se puede desinstalar desde la ventana.</summary>
        public bool IsCore { get; }

        /// <summary>
        /// Carpetas que dejaba la instalación antigua por .unitypackage y que chocan con el paquete
        /// (definen los mismos tipos). El instalador las retira, previo aviso, antes de instalar el módulo.
        /// </summary>
        public string[] LegacyFolders { get; }

        /// <summary>Carpeta del paquete dentro de Packages/ en el repositorio. Coincide con el nombre del paquete.</summary>
        public string FolderName => PackageName;

        public VivaModule(string packageName, string displayName, string description, string tagPrefix,
            bool isCore = false, string[] legacyFolders = null)
        {
            PackageName = packageName;
            DisplayName = displayName;
            Description = description;
            TagPrefix = tagPrefix;
            IsCore = isCore;
            LegacyFolders = legacyFolders ?? new string[0];
        }
    }

    /// <summary>
    /// Lista de módulos que ofrece el instalador. Para publicar un módulo nuevo basta con añadirlo aquí
    /// y crear su carpeta en Packages/ del repositorio.
    /// </summary>
    public static class VivaModuleCatalog
    {
        public const string CorePackageName = "com.vivagames.core";

        public static readonly VivaModule[] Modules =
        {
            new VivaModule(CorePackageName, "Viva Core",
                "Package installer and shared editor utilities. Required by every other module.",
                tagPrefix: "core", isCore: true),
            new VivaModule("com.vivagames.analytics", "Viva Analytics",
                "Analytics event creation tool and Firebase Analytics integration.",
                tagPrefix: "analytics",
                legacyFolders: new[]
                {
                    // Código de la herramienta en la versión .unitypackage (1.x). Events y Scripts se conservan.
                    "Assets/VivaAnalytics/Runtime",
                    "Assets/VivaAnalytics/Editor"
                }),
            // Próximos módulos, una línea por cada uno:
            // new VivaModule("com.vivagames.remoteconfig", "Viva Remote Config", "Firebase Remote Config wrapper.", tagPrefix: "remoteconfig"),
            // new VivaModule("com.vivagames.ads", "Viva Ads", "AdsManager built on AppLovin MAX.", tagPrefix: "ads"),
        };

        public static VivaModule Find(string packageName)
        {
            foreach (var module in Modules)
            {
                if (module.PackageName == packageName) return module;
            }
            return null;
        }

        public static VivaModule FindByTagPrefix(string tagPrefix)
        {
            foreach (var module in Modules)
            {
                if (module.TagPrefix == tagPrefix) return module;
            }
            return null;
        }
    }
}
