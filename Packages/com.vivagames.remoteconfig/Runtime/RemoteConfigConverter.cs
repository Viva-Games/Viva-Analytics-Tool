using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Viva.Services.RemoteConfig
{
    /// <summary>
    /// Conversiones de cadena a tipo con la misma semántica que ConfigValue del SDK de Firebase
    /// (BooleanValue, LongValue, DoubleValue), pero sin lanzar excepciones: devuelven false cuando la
    /// cadena no convierte. Cultura invariante siempre.
    /// </summary>
    public static class RemoteConfigConverter
    {
        // Mismas expresiones que ConfigValue.BooleanValue del SDK de Unity (ConfigValue.cs).
        private static readonly Regex TruePattern = new Regex("^(1|true|t|yes|y|on)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex FalsePattern = new Regex("^(0|false|f|no|n|off|)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool TryToBool(string raw, out bool value)
        {
            value = false;
            if (raw == null) return false;
            var trimmed = raw.Trim();
            if (TruePattern.IsMatch(trimmed))
            {
                value = true;
                return true;
            }
            return FalsePattern.IsMatch(trimmed);
        }

        public static bool TryToLong(string raw, out long value)
        {
            value = 0;
            return raw != null && long.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryToInt(string raw, out int value)
        {
            value = 0;
            if (!TryToLong(raw, out long asLong) || asLong < int.MinValue || asLong > int.MaxValue) return false;
            value = (int)asLong;
            return true;
        }

        public static bool TryToDouble(string raw, out double value)
        {
            value = 0;
            return raw != null && double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public static bool TryToFloat(string raw, out float value)
        {
            value = 0;
            if (!TryToDouble(raw, out double asDouble)) return false;
            if (!double.IsInfinity(asDouble) && !double.IsNaN(asDouble) && Math.Abs(asDouble) > float.MaxValue) return false;
            value = (float)asDouble;
            return true;
        }

        /// <summary>
        /// Cadena con la que se registra un valor por defecto: la misma forma que tendría en la consola de
        /// Firebase (números con cultura invariante, booleanos en minúsculas).
        /// </summary>
        public static string ToRaw(object value)
        {
            switch (value)
            {
                case null:
                    return string.Empty;
                case string text:
                    return text;
                case bool flag:
                    return flag ? "true" : "false";
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString();
            }
        }
    }
}
