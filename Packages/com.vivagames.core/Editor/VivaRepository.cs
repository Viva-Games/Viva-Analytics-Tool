namespace Viva.Core.Editor
{
    /// <summary>
    /// Datos del repositorio de GitHub donde viven todos los módulos de Viva.
    /// Es el único sitio que hay que tocar si el repositorio cambia de nombre o de organización.
    /// </summary>
    public static class VivaRepository
    {
        /// <summary>URL git del monorepo que contiene todos los paquetes.</summary>
        public const string GitUrl = "https://github.com/Viva-Games/Viva-Analytics-Tool.git";

        /// <summary>Carpeta del repositorio que contiene los paquetes.</summary>
        public const string PackagesFolder = "Packages";

        /// <summary>Prefijo del número de versión dentro del tag (analytics/v2.0.1).</summary>
        public const string VersionPrefix = "v";

        /// <summary>
        /// Construye la URL que entiende el Package Manager para un paquete y una referencia git (tag, rama o commit).
        /// Ejemplo: https://github.com/Viva-Games/Viva-Analytics-Tool.git?path=/Packages/com.vivagames.analytics#analytics/v2.0.1
        /// </summary>
        public static string BuildPackageUrl(string packageFolderName, string reference)
        {
            return $"{GitUrl}?path=/{PackagesFolder}/{packageFolderName}#{reference}";
        }

        /// <summary>
        /// Tag de release de un módulo: prefijo del módulo, barra y versión (analytics/v2.0.1).
        /// Cada módulo lleva su propia numeración, así que se puede publicar uno sin tocar los demás.
        /// </summary>
        public static string BuildTag(VivaModule module, VivaVersion version)
        {
            return module.TagPrefix + "/" + VersionPrefix + version;
        }

        /// <summary>
        /// Descompone un tag de release en prefijo de módulo y versión. Devuelve false si no tiene ese formato,
        /// por ejemplo para los tags antiguos de los .unitypackage (v.1.1.4).
        /// </summary>
        public static bool TryParseTag(string tag, out string modulePrefix, out VivaVersion version)
        {
            modulePrefix = null;
            version = default;
            if (string.IsNullOrEmpty(tag)) return false;

            int slash = tag.LastIndexOf('/');
            if (slash <= 0 || slash == tag.Length - 1) return false;
            if (!VivaVersion.TryParse(tag.Substring(slash + 1), out version)) return false;

            modulePrefix = tag.Substring(0, slash);
            return true;
        }
    }
}
