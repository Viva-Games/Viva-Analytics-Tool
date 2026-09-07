using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Compara un script de la versión .unitypackage con las copias originales que distribuía la herramienta
    /// y devuelve las líneas que añadió el proyecto, para que la migración no las pierda por el camino.
    /// </summary>
    internal static class LegacyCustomCodeFinder
    {
        private static readonly Regex WhitespaceRegex = new Regex(@"\s+");

        /// <summary>
        /// Líneas de userSource que no aparecen en ninguna de las versiones originales.
        /// alreadyMigrated permite descartar las que ya se han trasladado por otra vía (parámetros comunes).
        /// </summary>
        public static List<string> FindCustomLines(string userSource, IEnumerable<string> pristineSources, Func<string, bool> alreadyMigrated)
        {
            var known = new HashSet<string>();
            if (pristineSources != null)
            {
                foreach (var pristine in pristineSources)
                {
                    foreach (var line in SplitLines(pristine))
                    {
                        string normalized = Normalize(line);
                        if (normalized.Length > 0) known.Add(normalized);
                    }
                }
            }

            var custom = new List<string>();
            var seen = new HashSet<string>();
            foreach (var line in SplitLines(userSource))
            {
                string normalized = Normalize(line);
                if (normalized.Length == 0 || IsNoise(normalized) || known.Contains(normalized)) continue;
                if (alreadyMigrated != null && alreadyMigrated(line)) continue;
                if (seen.Add(normalized)) custom.Add(line.Trim());
            }
            return custom;
        }

        private static string[] SplitLines(string source)
        {
            if (string.IsNullOrEmpty(source)) return new string[0];
            return source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        private static string Normalize(string line)
        {
            return WhitespaceRegex.Replace(line.Trim().TrimStart('﻿'), " ");
        }

        /// <summary>Llaves sueltas, comentarios y directivas no cuentan como código propio.</summary>
        private static bool IsNoise(string normalized)
        {
            return normalized == "{"
                   || normalized == "}"
                   || normalized.StartsWith("//", StringComparison.Ordinal)
                   || normalized.StartsWith("#", StringComparison.Ordinal);
        }
    }
}
