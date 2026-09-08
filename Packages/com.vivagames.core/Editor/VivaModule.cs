using System;

namespace Viva.Core.Editor
{
    /// <summary>
    /// Versión mínima de otro módulo que un módulo necesita para funcionar. Solo se comprueba
    /// si ese otro módulo está instalado: los módulos son independientes entre sí.
    /// </summary>
    public sealed class VivaRequirement
    {
        public string PackageName { get; }
        public VivaVersion MinimumVersion { get; }

        public VivaRequirement(string packageName, string minimumVersion)
        {
            if (!VivaVersion.TryParse(minimumVersion, out var version))
                throw new ArgumentException($"Invalid minimum version '{minimumVersion}' for {packageName}.");
            PackageName = packageName;
            MinimumVersion = version;
        }
    }

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

        /// <summary>
        /// Versiones mínimas de otros módulos que la última release de este necesita. El instalador no
        /// instala ni actualiza el módulo mientras alguno de esos módulos esté instalado en una versión
        /// más antigua, y dice cuál hay que actualizar antes.
        /// </summary>
        public VivaRequirement[] RequiredModules { get; }

        /// <summary>Carpeta del paquete dentro de Packages/ en el repositorio. Coincide con el nombre del paquete.</summary>
        public string FolderName => PackageName;

        public VivaModule(string packageName, string displayName, string description, string tagPrefix,
            bool isCore = false, string[] legacyFolders = null, VivaRequirement[] requiredModules = null)
        {
            PackageName = packageName;
            DisplayName = displayName;
            Description = description;
            TagPrefix = tagPrefix;
            IsCore = isCore;
            LegacyFolders = legacyFolders ?? new string[0];
            RequiredModules = requiredModules ?? new VivaRequirement[0];
        }
    }

    /// <summary>
    /// Lista de módulos que ofrece el instalador. Para publicar un módulo nuevo basta con añadirlo aquí
    /// y crear su carpeta en Packages/ del repositorio.
    /// </summary>
    public static class VivaModuleCatalog
    {
        public const string CorePackageName = "com.vivagames.core";
        public const string AnalyticsPackageName = "com.vivagames.analytics";

        public static readonly VivaModule[] Modules =
        {
            new VivaModule(CorePackageName, "Viva Core",
                "Package installer, shared editor utilities and the shared Firebase initialization. Required by every other module.",
                tagPrefix: "core", isCore: true),
            new VivaModule(AnalyticsPackageName, "Viva Analytics",
                "Analytics event creation tool and Firebase Analytics integration.",
                tagPrefix: "analytics",
                legacyFolders: new[]
                {
                    // Código de la herramienta en la versión .unitypackage (1.x). Events y Scripts se conservan.
                    "Assets/VivaAnalytics/Runtime",
                    "Assets/VivaAnalytics/Editor"
                },
                requiredModules: new[]
                {
                    // 2.1.0: el tracker de Firebase espera a VivaFirebase (core 2.1.0).
                    // 2.2.0: AnalyticsService se suscribe a VivaConsent (core 2.2.0).
                    new VivaRequirement(CorePackageName, "2.2.0")
                }),
            new VivaModule("com.vivagames.remoteconfig", "Viva Remote Config",
                "Firebase Remote Config integration: declare the parameters in an editor window, read them through a generated typed class.",
                tagPrefix: "remoteconfig",
                requiredModules: new[]
                {
                    // Usa VivaFirebase (core 2.1) y, si hay analíticas, estas deben pasar también por él (analytics 2.1).
                    new VivaRequirement(CorePackageName, "2.1.0"),
                    new VivaRequirement(AnalyticsPackageName, "2.1.0")
                }),
            new VivaModule("com.vivagames.ads", "Viva Ads",
                "AppLovin MAX integration: declare formats and placements in an editor window, call ShowRewarded, TryShowInterstitial or ShowBanner.",
                tagPrefix: "ads",
                requiredModules: new[]
                {
                    // Usa ReadyGate y VivaConsent (core 2.2) y, si hay analíticas, estas reciben el consentimiento por el core (analytics 2.2).
                    new VivaRequirement(CorePackageName, "2.2.0"),
                    new VivaRequirement(AnalyticsPackageName, "2.2.0")
                }),
            // Próximos módulos, una línea por cada uno.
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
