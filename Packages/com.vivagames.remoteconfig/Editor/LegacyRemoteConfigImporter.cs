using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Viva.Services.RemoteConfig.Editor
{
    /// <summary>
    /// Lee las llamadas RemoteConfigManager.GetX("clave", default) del código del proyecto para rellenar la
    /// lista de parámetros, y localiza el RemoteConfigManager antiguo para sustituirlo por el envoltorio de
    /// compatibilidad. No escribe nada por su cuenta: la ventana decide qué hacer con el resultado.
    /// </summary>
    public static class LegacyRemoteConfigImporter
    {
        public const string WRAPPER_MARK = "Compatibility wrapper written by Viva Remote Config";

        public sealed class ImportedParameter
        {
            public string Key;
            public string Type;
            /// <summary>Default literal encontrado en el código, o vacío si no era un literal.</summary>
            public string DefaultValue = string.Empty;
            public bool HasLiteralDefault;
            /// <summary>Expresión del default cuando no es un literal (config.livesPerLevel), para la nota.</summary>
            public string DefaultExpression;
            public readonly List<string> CallSites = new List<string>();
            public readonly List<string> Conflicts = new List<string>();

            /// <summary>Descripción con la que entra en la ventana: avisa de lo que falta o choca.</summary>
            public string Note
            {
                get
                {
                    var notes = new List<string>();
                    if (!HasLiteralDefault)
                        notes.Add("Needs a default value" + (string.IsNullOrEmpty(DefaultExpression) ? string.Empty : $" (the code used {DefaultExpression})") + ".");
                    notes.AddRange(Conflicts);
                    if (CallSites.Count > 0) notes.Add("Used in " + string.Join(", ", CallSites) + ".");
                    return string.Join(" ", notes);
                }
            }
        }

        public sealed class ImportResult
        {
            public readonly List<ImportedParameter> Parameters = new List<ImportedParameter>();
            public readonly List<string> Unresolved = new List<string>();
            public int ScannedFiles;
            public int CallSites;

            public int WithoutLiteralDefault
            {
                get
                {
                    int count = 0;
                    foreach (var parameter in Parameters) if (!parameter.HasLiteralDefault) count++;
                    return count;
                }
            }
        }

        private static readonly Regex CallPattern = new Regex(@"RemoteConfigManager\s*\.\s*Get(Int|String|Bool|Json)\b", RegexOptions.Compiled);
        private static readonly Regex LegacyClassPattern = new Regex(@"\bstatic\s+(?:partial\s+)?class\s+RemoteConfigManager\b", RegexOptions.Compiled);
        private static readonly Regex IntLiteral = new Regex(@"^-?\d+$", RegexOptions.Compiled);
        private static readonly Regex StringLiteral = new Regex("^\"((?:[^\"\\\\]|\\\\.)*)\"$", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex VerbatimStringLiteral = new Regex("^@\"((?:[^\"]|\"\")*)\"$", RegexOptions.Compiled | RegexOptions.Singleline);

        #region Legacy manager

        /// <summary>Ruta del script que declara el RemoteConfigManager antiguo bajo Assets, o null.</summary>
        public static string FindLegacyManager(string root = "Assets")
        {
            if (!Directory.Exists(root)) return null;
            foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                string content;
                try { content = File.ReadAllText(file); }
                catch { continue; }
                if (LegacyClassPattern.IsMatch(content)) return file.Replace('\\', '/');
            }
            return null;
        }

        public static bool IsCompatibilityWrapper(string path)
        {
            try
            {
                return File.Exists(path) && File.ReadAllText(path).Contains(WRAPPER_MARK);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Carpeta de backup junto al manager: Assets/VivaAnalytics/Scripts/X.cs → Assets/VivaAnalytics/Legacy.</summary>
        public static string BackupPathFor(string legacyManagerPath)
        {
            string folder = Path.GetDirectoryName(legacyManagerPath)?.Replace('\\', '/') ?? "Assets";
            if (string.Equals(Path.GetFileName(folder), "Scripts", StringComparison.OrdinalIgnoreCase))
                folder = Path.GetDirectoryName(folder)?.Replace('\\', '/') ?? folder;
            return folder + "/Legacy/RemoteConfigManager.legacy.txt";
        }

        #endregion

        #region Code scan

        /// <summary>Recorre los .cs de root (sin Packages) saltando los ficheros indicados.</summary>
        public static ImportResult ScanCode(string root = "Assets", params string[] excludedPaths)
        {
            var sources = new List<KeyValuePair<string, string>>();
            if (Directory.Exists(root))
            {
                var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var path in excludedPaths) if (!string.IsNullOrEmpty(path)) excluded.Add(Path.GetFullPath(path));

                foreach (var file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    if (excluded.Contains(Path.GetFullPath(file))) continue;
                    try
                    {
                        sources.Add(new KeyValuePair<string, string>(file.Replace('\\', '/'), File.ReadAllText(file)));
                    }
                    catch
                    {
                        // Fichero ilegible: se ignora.
                    }
                }
            }
            return ScanSources(sources);
        }

        /// <summary>Analiza fuentes ya leídas (ruta, contenido). Separado de disco para poder probarlo.</summary>
        public static ImportResult ScanSources(IEnumerable<KeyValuePair<string, string>> sources)
        {
            var result = new ImportResult();
            var byKey = new Dictionary<string, ImportedParameter>(StringComparer.Ordinal);

            foreach (var source in sources)
            {
                string path = source.Key;
                string content = source.Value ?? string.Empty;
                if (content.Contains(WRAPPER_MARK) || LegacyClassPattern.IsMatch(content)) continue; // el propio manager o su envoltorio
                result.ScannedFiles++;

                var constants = ReadStringConstants(content);
                foreach (Match match in CallPattern.Matches(content))
                {
                    if (!TryReadArguments(content, match.Index + match.Length, out var arguments, out int argumentsStart)) continue;
                    if (arguments.Count == 0) continue;

                    string callSite = $"{Path.GetFileName(path)}:{LineOf(content, match.Index)}";
                    result.CallSites++;

                    if (!TryResolveKey(arguments[0], constants, out string key))
                    {
                        result.Unresolved.Add($"{callSite}: key {arguments[0]} could not be resolved");
                        continue;
                    }

                    string type = TypeFor(match.Groups[1].Value);
                    if (!byKey.TryGetValue(key, out var parameter))
                    {
                        parameter = new ImportedParameter { Key = key, Type = type };
                        byKey[key] = parameter;
                        result.Parameters.Add(parameter);
                    }
                    parameter.CallSites.Add(callSite);

                    if (parameter.Type != type)
                    {
                        // El default de una lectura con otro tipo no se compara: el conflicto de tipo ya lo cubre.
                        parameter.Conflicts.Add($"Read as {type} in {callSite} but as {parameter.Type} elsewhere.");
                        continue;
                    }

                    if (arguments.Count < 2) continue;
                    string defaultArgument = arguments[1].Trim();
                    if (TryReadLiteral(type, defaultArgument, out string literal))
                    {
                        if (!parameter.HasLiteralDefault)
                        {
                            parameter.HasLiteralDefault = true;
                            parameter.DefaultValue = literal;
                        }
                        else if (parameter.DefaultValue != literal)
                        {
                            parameter.Conflicts.Add($"Default {literal} in {callSite} differs from {parameter.DefaultValue}.");
                        }
                    }
                    else if (string.IsNullOrEmpty(parameter.DefaultExpression))
                    {
                        parameter.DefaultExpression = defaultArgument;
                    }
                }
            }

            return result;
        }

        private static string TypeFor(string getter)
        {
            switch (getter)
            {
                case "Int": return RemoteConfigParameterTypes.INT;
                case "Bool": return RemoteConfigParameterTypes.BOOL;
                case "Json": return RemoteConfigParameterTypes.JSON;
                default: return RemoteConfigParameterTypes.STRING;
            }
        }

        /// <summary>const string NOMBRE = "valor"; del fichero, para resolver claves pasadas por constante.</summary>
        private static Dictionary<string, string> ReadStringConstants(string content)
        {
            var constants = new Dictionary<string, string>(StringComparer.Ordinal);
            var pattern = new Regex("const\\s+string\\s+([A-Za-z_][A-Za-z0-9_]*)\\s*=\\s*(\"(?:[^\"\\\\]|\\\\.)*\")\\s*;", RegexOptions.Compiled);
            foreach (Match match in pattern.Matches(content))
            {
                if (TryReadStringLiteral(match.Groups[2].Value, out string value)) constants[match.Groups[1].Value] = value;
            }
            return constants;
        }

        private static bool TryResolveKey(string argument, Dictionary<string, string> constants, out string key)
        {
            argument = argument.Trim();
            if (TryReadStringLiteral(argument, out key)) return true;

            // Identificador o Clase.Identificador: se busca la constante en el mismo fichero.
            string name = argument;
            int dot = name.LastIndexOf('.');
            if (dot >= 0) name = name.Substring(dot + 1);
            return constants.TryGetValue(name, out key);
        }

        private static bool TryReadLiteral(string type, string argument, out string literal)
        {
            literal = null;
            switch (type)
            {
                case RemoteConfigParameterTypes.INT:
                    if (!IntLiteral.IsMatch(argument)) return false;
                    literal = argument;
                    return true;
                case RemoteConfigParameterTypes.BOOL:
                    if (argument != "true" && argument != "false") return false;
                    literal = argument;
                    return true;
                case RemoteConfigParameterTypes.STRING:
                    if (argument == "string.Empty" || argument == "String.Empty")
                    {
                        literal = string.Empty;
                        return true;
                    }
                    return TryReadStringLiteral(argument, out literal);
                default:
                    // json: los defaults son objetos o default(T), nunca un literal de texto.
                    return TryReadStringLiteral(argument, out literal);
            }
        }

        private static bool TryReadStringLiteral(string argument, out string value)
        {
            value = null;
            var verbatim = VerbatimStringLiteral.Match(argument);
            if (verbatim.Success)
            {
                value = verbatim.Groups[1].Value.Replace("\"\"", "\"");
                return true;
            }

            var regular = StringLiteral.Match(argument);
            if (!regular.Success) return false;
            value = Unescape(regular.Groups[1].Value);
            return true;
        }

        private static string Unescape(string text)
        {
            var result = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '\\' || i + 1 >= text.Length)
                {
                    result.Append(c);
                    continue;
                }
                char next = text[++i];
                switch (next)
                {
                    case 'n': result.Append('\n'); break;
                    case 'r': result.Append('\r'); break;
                    case 't': result.Append('\t'); break;
                    case '0': result.Append('\0'); break;
                    case '\\': result.Append('\\'); break;
                    case '"': result.Append('"'); break;
                    case 'u':
                        if (i + 4 < text.Length && int.TryParse(text.Substring(i + 1, 4), System.Globalization.NumberStyles.HexNumber, null, out int code))
                        {
                            result.Append((char)code);
                            i += 4;
                        }
                        break;
                    default: result.Append(next); break;
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// Lee los argumentos de la llamada que empieza en position (tras el nombre del método): salta un
        /// argumento genérico opcional, exige '(' y separa por comas de primer nivel respetando paréntesis,
        /// corchetes, llaves y cadenas.
        /// </summary>
        private static bool TryReadArguments(string content, int position, out List<string> arguments, out int argumentsStart)
        {
            arguments = new List<string>();
            argumentsStart = -1;
            int i = position;
            while (i < content.Length && char.IsWhiteSpace(content[i])) i++;

            if (i < content.Length && content[i] == '<')
            {
                int depth = 0;
                while (i < content.Length)
                {
                    if (content[i] == '<') depth++;
                    else if (content[i] == '>' && --depth == 0)
                    {
                        i++;
                        break;
                    }
                    i++;
                }
                while (i < content.Length && char.IsWhiteSpace(content[i])) i++;
            }

            if (i >= content.Length || content[i] != '(') return false;
            argumentsStart = ++i;

            var current = new StringBuilder();
            int nesting = 0;
            bool inString = false, verbatim = false;
            for (; i < content.Length; i++)
            {
                char c = content[i];
                if (inString)
                {
                    current.Append(c);
                    if (verbatim)
                    {
                        if (c == '"')
                        {
                            if (i + 1 < content.Length && content[i + 1] == '"')
                            {
                                current.Append('"');
                                i++;
                            }
                            else inString = false;
                        }
                    }
                    else if (c == '\\' && i + 1 < content.Length)
                    {
                        current.Append(content[++i]);
                    }
                    else if (c == '"') inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    verbatim = current.Length > 0 && current[current.Length - 1] == '@';
                    current.Append(c);
                    continue;
                }
                if (c == '(' || c == '[' || c == '{') nesting++;
                if (c == ')' || c == ']' || c == '}')
                {
                    if (nesting == 0)
                    {
                        arguments.Add(current.ToString().Trim());
                        return true;
                    }
                    nesting--;
                }
                if (c == ',' && nesting == 0)
                {
                    arguments.Add(current.ToString().Trim());
                    current.Clear();
                    continue;
                }
                current.Append(c);
            }
            return false;
        }

        private static int LineOf(string content, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < content.Length; i++) if (content[i] == '\n') line++;
            return line;
        }

        #endregion
    }
}
