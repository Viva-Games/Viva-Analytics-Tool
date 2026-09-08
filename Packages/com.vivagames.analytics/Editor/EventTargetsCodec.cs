using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Destinos de un evento en el código generado y en el catálogo. La línea generada es
    /// "public AnalyticsTargets Targets => AnalyticsTargets.Firebase | AnalyticsTargets.Facebook;" y solo
    /// existe cuando hay destinos además de Firebase, para que los eventos ya generados no cambien.
    /// </summary>
    public static class EventTargetsCodec
    {
        private static readonly Regex TargetsLine = new Regex(@"AnalyticsTargets\s+Targets\s*=>\s*([^;]+);", RegexOptions.Compiled);

        /// <summary>Destinos declarados en el fuente de un evento; solo Firebase si no hay línea.</summary>
        public static AnalyticsTargets Parse(string source)
        {
            if (string.IsNullOrEmpty(source)) return AnalyticsTargets.Firebase;
            var match = TargetsLine.Match(source);
            if (!match.Success) return AnalyticsTargets.Firebase;

            var targets = AnalyticsTargets.None;
            foreach (var part in match.Groups[1].Value.Split('|'))
            {
                string name = part.Trim();
                int dot = name.LastIndexOf('.');
                if (dot >= 0) name = name.Substring(dot + 1);
                if (Enum.TryParse(name, true, out AnalyticsTargets parsed)) targets |= parsed;
            }
            return targets == AnalyticsTargets.None ? AnalyticsTargets.Firebase : targets | AnalyticsTargets.Firebase;
        }

        /// <summary>Expresión C# de los destinos: "AnalyticsTargets.Firebase | AnalyticsTargets.Facebook".</summary>
        public static string ToCode(AnalyticsTargets targets)
        {
            var parts = new List<string>();
            foreach (var target in Ordered(targets)) parts.Add("AnalyticsTargets." + target);
            return string.Join(" | ", parts);
        }

        /// <summary>Nombres para la ventana: "Firebase, Facebook".</summary>
        public static string Describe(AnalyticsTargets targets)
        {
            var parts = new List<string>();
            foreach (var target in Ordered(targets)) parts.Add(target.ToString());
            return string.Join(", ", parts);
        }

        /// <summary>Destinos del catálogo: nombres en cualquier caja ("facebook", "Singular"). Firebase siempre incluido.</summary>
        public static AnalyticsTargets FromNames(IEnumerable<string> names)
        {
            var targets = AnalyticsTargets.Firebase;
            if (names == null) return targets;
            foreach (var name in names)
            {
                if (Enum.TryParse((name ?? string.Empty).Trim(), true, out AnalyticsTargets parsed)) targets |= parsed;
            }
            return targets;
        }

        private static IEnumerable<AnalyticsTargets> Ordered(AnalyticsTargets targets)
        {
            targets |= AnalyticsTargets.Firebase;
            if (targets.HasFlag(AnalyticsTargets.Firebase)) yield return AnalyticsTargets.Firebase;
            if (targets.HasFlag(AnalyticsTargets.Facebook)) yield return AnalyticsTargets.Facebook;
            if (targets.HasFlag(AnalyticsTargets.Singular)) yield return AnalyticsTargets.Singular;
        }
    }
}
