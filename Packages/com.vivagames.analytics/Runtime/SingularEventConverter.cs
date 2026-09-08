using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Viva.Services.Analytics.Singular
{
    /// <summary>
    /// Reglas de Singular y conversión de un evento a los atributos de SingularSDK.Event. Sin dependencia del
    /// SDK. Nombre de evento de hasta 32 caracteres ASCII; atributos y valores de hasta 500.
    /// </summary>
    public static class SingularEventConverter
    {
        public const int MAX_NAME_LENGTH = 32;
        public const int MAX_ATTRIBUTE_LENGTH = 500;

        /// <summary>null si el nombre vale para Singular; si no, el motivo.</summary>
        public static string ValidateName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "the name is empty.";
            if (!IsAscii(name)) return $"\"{name}\" must be ASCII for Singular.";
            if (name.Length > MAX_NAME_LENGTH) return $"\"{name}\" is longer than {MAX_NAME_LENGTH} characters, the Singular limit for event names.";
            return null;
        }

        /// <summary>Atributos para SingularSDK.Event: números como números, bool como texto, el resto como texto recortado a 500.</summary>
        public static Dictionary<string, object> ToAttributes(IReadOnlyDictionary<string, object> fields)
        {
            var attributes = new Dictionary<string, object>();
            if (fields == null) return attributes;
            foreach (var pair in fields)
            {
                attributes[Truncate(pair.Key)] = ToValue(pair.Value);
            }
            return attributes;
        }

        public static object ToValue(object value)
        {
            switch (value)
            {
                case null: return string.Empty;
                case bool flag: return flag ? "true" : "false";
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
            return text != null && text.Length > MAX_ATTRIBUTE_LENGTH ? text.Substring(0, MAX_ATTRIBUTE_LENGTH) : text;
        }

        private static bool IsAscii(string text)
        {
            foreach (char c in text)
            {
                if (c > 127) return false;
            }
            return true;
        }
    }
}
