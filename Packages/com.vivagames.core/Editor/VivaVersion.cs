using System;
using System.Text.RegularExpressions;

namespace Viva.Core.Editor
{
    /// <summary>
    /// Versión semántica mínima (major.minor.patch). Sirve para comparar los tags del repositorio
    /// con la versión declarada en el package.json de cada paquete instalado.
    /// </summary>
    public readonly struct VivaVersion : IComparable<VivaVersion>, IEquatable<VivaVersion>
    {
        private static readonly Regex Pattern = new Regex(@"^v?(\d+)\.(\d+)\.(\d+)$", RegexOptions.Compiled);

        public readonly int Major;
        public readonly int Minor;
        public readonly int Patch;

        public VivaVersion(int major, int minor, int patch)
        {
            Major = major;
            Minor = minor;
            Patch = patch;
        }

        /// <summary>
        /// Acepta "2.0.0" y "v2.0.0". Devuelve false para cualquier otro formato,
        /// por ejemplo los tags antiguos de los .unitypackage ("v.1.1.4").
        /// </summary>
        public static bool TryParse(string text, out VivaVersion version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(text)) return false;

            var match = Pattern.Match(text.Trim());
            if (!match.Success) return false;

            version = new VivaVersion(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value));
            return true;
        }

        public int CompareTo(VivaVersion other)
        {
            if (Major != other.Major) return Major.CompareTo(other.Major);
            if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
            return Patch.CompareTo(other.Patch);
        }

        public bool Equals(VivaVersion other) => CompareTo(other) == 0;

        public override bool Equals(object obj) => obj is VivaVersion other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Major * 397) ^ Minor) * 397 ^ Patch;
            }
        }

        public override string ToString() => $"{Major}.{Minor}.{Patch}";

        public static bool operator >(VivaVersion a, VivaVersion b) => a.CompareTo(b) > 0;
        public static bool operator <(VivaVersion a, VivaVersion b) => a.CompareTo(b) < 0;
        public static bool operator >=(VivaVersion a, VivaVersion b) => a.CompareTo(b) >= 0;
        public static bool operator <=(VivaVersion a, VivaVersion b) => a.CompareTo(b) <= 0;
    }
}
