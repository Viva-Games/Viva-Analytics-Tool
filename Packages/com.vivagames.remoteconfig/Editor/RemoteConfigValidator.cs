using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Viva.Services.RemoteConfig.Editor
{
    public sealed class ValidationIssue
    {
        /// <summary>Índice del parámetro en la lista, o -1 si el aviso es de la lista entera.</summary>
        public int Index;
        public string Message;

        public ValidationIssue(int index, string message)
        {
            Index = index;
            Message = message;
        }

        public override string ToString() => Index < 0 ? Message : $"#{Index + 1}: {Message}";
    }

    public sealed class ValidationResult
    {
        public readonly List<ValidationIssue> Errors = new List<ValidationIssue>();
        public readonly List<ValidationIssue> Warnings = new List<ValidationIssue>();

        public bool IsValid => Errors.Count == 0;

        public bool HasErrorAt(int index)
        {
            foreach (var error in Errors)
            {
                if (error.Index == index) return true;
            }
            return false;
        }

        public string ErrorsAt(int index)
        {
            var messages = new List<string>();
            foreach (var error in Errors)
            {
                if (error.Index == index) messages.Add(error.Message);
            }
            return string.Join(" ", messages);
        }
    }

    /// <summary>
    /// Reglas que deben cumplir los parámetros antes de guardarse: claves con el formato que exige Firebase,
    /// valores por defecto convertibles a su tipo, e identificadores C# válidos y únicos en la clase generada.
    /// Los límites de Firebase (3000 parámetros, 1.000.000 caracteres en valores) solo avisan.
    /// </summary>
    public static class RemoteConfigValidator
    {
        // Firebase: hasta 256 caracteres, empieza por letra ASCII o guion bajo, puede llevar dígitos.
        private static readonly Regex KeyPattern = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
        public const int MAX_KEY_LENGTH = 256;
        public const int MAX_PARAMETERS = 3000;
        public const int MAX_TOTAL_VALUE_LENGTH = 1000000;

        public static ValidationResult Validate(IReadOnlyList<RemoteConfigParameterDefinition> parameters)
        {
            var result = new ValidationResult();
            var keys = new Dictionary<string, int>();
            var identifiers = new Dictionary<string, int>();
            long totalValueLength = 0;

            for (int i = 0; i < parameters.Count; i++)
            {
                var parameter = parameters[i];
                string key = parameter.key ?? string.Empty;

                string keyError = ValidateKey(key);
                if (keyError != null)
                {
                    result.Errors.Add(new ValidationIssue(i, keyError));
                }
                else
                {
                    if (keys.TryGetValue(key, out int previous))
                        result.Errors.Add(new ValidationIssue(i, $"Duplicated key \"{key}\" (also #{previous + 1})."));
                    else
                        keys[key] = i;

                    string identifier = RemoteConfigNaming.ToIdentifier(key);
                    string identifierError = ValidateIdentifier(identifier);
                    if (identifierError != null)
                        result.Errors.Add(new ValidationIssue(i, identifierError));
                    else if (identifiers.TryGetValue(identifier, out int other))
                        result.Errors.Add(new ValidationIssue(i, $"\"{key}\" and \"{parameters[other].key}\" (#{other + 1}) would both generate the property {identifier}."));
                    else
                        identifiers[identifier] = i;
                }

                if (!RemoteConfigParameterTypes.IsKnown(parameter.type))
                {
                    result.Errors.Add(new ValidationIssue(i, $"Unknown type \"{parameter.type}\"."));
                }
                else
                {
                    string valueError = ValidateValue(parameter.type, parameter.defaultValue);
                    if (valueError != null) result.Errors.Add(new ValidationIssue(i, valueError));
                }

                totalValueLength += (parameter.defaultValue ?? string.Empty).Length;
            }

            if (parameters.Count > MAX_PARAMETERS)
                result.Warnings.Add(new ValidationIssue(-1, $"Firebase allows up to {MAX_PARAMETERS} parameters per project; there are {parameters.Count}."));
            if (totalValueLength > MAX_TOTAL_VALUE_LENGTH)
                result.Warnings.Add(new ValidationIssue(-1, $"Firebase limits the total length of parameter values to {MAX_TOTAL_VALUE_LENGTH} characters; the defaults alone add up to {totalValueLength}."));

            return result;
        }

        /// <summary>null si la clave es válida; si no, el motivo.</summary>
        public static string ValidateKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return "The key is empty.";
            if (key.Length > MAX_KEY_LENGTH) return $"The key is longer than {MAX_KEY_LENGTH} characters.";
            if (!KeyPattern.IsMatch(key)) return $"Invalid key \"{key}\": it must start with a letter or underscore and contain only letters, digits and underscores.";
            return null;
        }

        /// <summary>null si el valor por defecto convierte al tipo; si no, el motivo.</summary>
        public static string ValidateValue(string type, string value)
        {
            value = value ?? string.Empty;
            switch (type)
            {
                case RemoteConfigParameterTypes.INT:
                    return RemoteConfigConverter.TryToInt(value, out _) ? null : $"\"{value}\" is not a valid int.";
                case RemoteConfigParameterTypes.LONG:
                    return RemoteConfigConverter.TryToLong(value, out _) ? null : $"\"{value}\" is not a valid long.";
                case RemoteConfigParameterTypes.FLOAT:
                    if (!RemoteConfigConverter.TryToFloat(value, out float asFloat)) return $"\"{value}\" is not a valid float (use a dot as decimal separator).";
                    return float.IsNaN(asFloat) || float.IsInfinity(asFloat) ? "NaN and infinity are not allowed." : null;
                case RemoteConfigParameterTypes.DOUBLE:
                    if (!RemoteConfigConverter.TryToDouble(value, out double asDouble)) return $"\"{value}\" is not a valid double (use a dot as decimal separator).";
                    return double.IsNaN(asDouble) || double.IsInfinity(asDouble) ? "NaN and infinity are not allowed." : null;
                case RemoteConfigParameterTypes.BOOL:
                    return value == "true" || value == "false" ? null : $"\"{value}\" must be true or false.";
                case RemoteConfigParameterTypes.STRING:
                    return null;
                case RemoteConfigParameterTypes.JSON:
                    return JsonSyntaxValidator.Validate(value, out string error) ? null : "Invalid JSON: " + error + ".";
                default:
                    return $"Unknown type \"{type}\".";
            }
        }

        private static string ValidateIdentifier(string identifier)
        {
            if (!RemoteConfigNaming.IsValidIdentifier(identifier)) return $"The key does not produce a valid C# property name ({identifier}).";
            if (RemoteConfigNaming.IsReserved(identifier)) return $"The property name {identifier} is reserved in the generated class; choose another key.";
            return null;
        }

        /// <summary>Texto normalizado de un valor numérico válido (cultura invariante), o el original si no convierte.</summary>
        public static string NormalizeValue(string type, string value)
        {
            value = value ?? string.Empty;
            switch (type)
            {
                case RemoteConfigParameterTypes.INT:
                    return RemoteConfigConverter.TryToInt(value, out int i) ? i.ToString(CultureInfo.InvariantCulture) : value;
                case RemoteConfigParameterTypes.LONG:
                    return RemoteConfigConverter.TryToLong(value, out long l) ? l.ToString(CultureInfo.InvariantCulture) : value;
                case RemoteConfigParameterTypes.FLOAT:
                    return RemoteConfigConverter.TryToFloat(value, out float f) ? f.ToString("R", CultureInfo.InvariantCulture) : value;
                case RemoteConfigParameterTypes.DOUBLE:
                    return RemoteConfigConverter.TryToDouble(value, out double d) ? d.ToString("R", CultureInfo.InvariantCulture) : value;
                case RemoteConfigParameterTypes.BOOL:
                    return value.Trim().ToLowerInvariant();
                default:
                    return value;
            }
        }
    }
}
