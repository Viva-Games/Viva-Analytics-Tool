using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Viva.Services.Analytics.Facebook
{
    /// <summary>
    /// Reglas de Meta App Events y conversión de un evento al formato de FB.LogAppEvent. Sin dependencia del
    /// SDK, para poder validar en el editor y probar sin él. Nombres de evento y de parámetro de 2 a 40
    /// caracteres alfanuméricos, guion bajo, guion o espacio; hasta 25 parámetros; valores de hasta 100 caracteres.
    /// </summary>
    public static class FacebookEventConverter
    {
        public const int MIN_NAME_LENGTH = 2;
        public const int MAX_NAME_LENGTH = 40;
        public const int MAX_PARAMETERS = 25;
        public const int MAX_VALUE_LENGTH = 100;

        private static readonly Regex NamePattern = new Regex("^[A-Za-z0-9][A-Za-z0-9_\\- ]*$", RegexOptions.Compiled);

        /// <summary>null si el nombre vale para Facebook; si no, el motivo.</summary>
        public static string ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "the name is empty.";
            if (name.Length < MIN_NAME_LENGTH || name.Length > MAX_NAME_LENGTH) return $"\"{name}\" must be between {MIN_NAME_LENGTH} and {MAX_NAME_LENGTH} characters for Facebook.";
            if (!NamePattern.IsMatch(name)) return $"\"{name}\" may only contain letters, digits, underscores, hyphens and spaces for Facebook, starting with a letter or digit.";
            return null;
        }

        /// <summary>Avisos de diseño para un evento con parámetros: nombres y número de parámetros.</summary>
        public static List<string> Validate(string eventName, IReadOnlyList<string> parameterNames)
        {
            var issues = new List<string>();
            string nameError = ValidateName(eventName);
            if (nameError != null) issues.Add(nameError);
            if (parameterNames != null)
            {
                if (parameterNames.Count > MAX_PARAMETERS) issues.Add($"Facebook accepts up to {MAX_PARAMETERS} parameters per event; this one has {parameterNames.Count}.");
                foreach (var parameter in parameterNames)
                {
                    string error = ValidateName(parameter);
                    if (error != null) issues.Add("Parameter " + error);
                }
            }
            return issues;
        }

        /// <summary>
        /// Parámetros para FB.LogAppEvent: números como números, bool como 1 o 0, el resto como texto recortado
        /// a 100 caracteres. Se quedan los 25 primeros; los descartados salen en dropped.
        /// </summary>
        public static Dictionary<string, object> ToParameters(IReadOnlyDictionary<string, object> fields, out List<string> dropped)
        {
            dropped = new List<string>();
            var parameters = new Dictionary<string, object>();
            if (fields == null) return parameters;

            foreach (var pair in fields)
            {
                if (parameters.Count >= MAX_PARAMETERS)
                {
                    dropped.Add(pair.Key);
                    continue;
                }
                parameters[pair.Key] = ToValue(pair.Value);
            }
            return parameters;
        }

        public static object ToValue(object value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case bool flag: return flag ? 1 : 0;
                case int i: return i;
                case long l: return l;
                case float f: return f;
                case double d: return d;
                case string s: return Truncate(s);
                case IFormattable formattable: return Truncate(formattable.ToString(null, CultureInfo.InvariantCulture));
                default: return Truncate(value.ToString());
            }
        }

        private static string Truncate(string text)
        {
            return text != null && text.Length > MAX_VALUE_LENGTH ? text.Substring(0, MAX_VALUE_LENGTH) : text;
        }
    }
}
