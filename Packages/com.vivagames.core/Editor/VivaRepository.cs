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

        /// <summary>Prefijo de los tags de versión (v2.0.0).</summary>
        public const string TagPrefix = "v";

        /// <summary>
        /// Construye la URL que entiende el Package Manager para un paquete concreto en un tag concreto.
        /// Ejemplo: https://github.com/Viva-Games/Viva-Analytics-Tool.git?path=/Packages/com.vivagames.analytics#v2.0.0
        /// </summary>
        public static string BuildPackageUrl(string packageFolderName, string tag)
        {
            return $"{GitUrl}?path=/{PackagesFolder}/{packageFolderName}#{tag}";
        }
    }
}
