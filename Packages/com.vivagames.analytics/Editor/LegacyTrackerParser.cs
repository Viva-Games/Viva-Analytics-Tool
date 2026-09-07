using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Viva.Services.Analytics
{
    /// <summary>
    /// Lee el FirebaseAnalytics.cs de la versión .unitypackage y extrae los parámetros comunes que el
    /// desarrollador había añadido en InsertCommonParameters, para trasladarlos al AnalyticsInit nuevo
    /// como llamadas a AnalyticsService.RegisterCommonParameter.
    /// </summary>
    internal static class LegacyTrackerParser
    {
        internal sealed class CommonParameter
        {
            /// <summary>Literal entre comillas o, si no se ha podido resolver, el identificador original.</summary>
            public string Key;

            /// <summary>Expresión del valor tal y como estaba escrita.</summary>
            public string ValueExpression;

            /// <summary>false si la clave era un identificador que no se ha encontrado como constante del fichero.</summary>
            public bool KeyResolved;
        }

        internal sealed class Result
        {
            public readonly List<string> Usings = new List<string>();
            public readonly List<CommonParameter> Parameters = new List<CommonParameter>();

            /// <summary>Nombres de las constantes que se han usado como clave y ya no hacen falta.</summary>
            public readonly List<string> ResolvedConstants = new List<string>();

            /// <summary>Líneas de código del cuerpo de InsertCommonParameters, sin comentarios ni vacías.</summary>
            public readonly List<string> BodyLines = new List<string>();

            /// <summary>
            /// true si el cuerpo solo tiene parameters.Add / stringParams.Add, y las expresiones se pueden trasladar
            /// tal cual. false si calcula los valores con lógica propia (variables locales, condiciones...): entonces
            /// las llamadas se generan comentadas, junto al código original, para reescribirlas a mano.
            /// </summary>
            public bool IsSimpleBody = true;
        }

        private static readonly Regex UsingRegex = new Regex(@"^\s*using\s+([\w\.]+)\s*;", RegexOptions.Multiline);
        private static readonly Regex ConstRegex = new Regex(@"const\s+string\s+(\w+)\s*=\s*""((?:[^""\\]|\\.)*)""\s*;");
        private static readonly Regex MethodRegex = new Regex(@"\bvoid\s+InsertCommonParameters\s*\(");

        // Namespaces que ya tiene la plantilla o que solo necesitaba el tracker antiguo.
        private static readonly string[] IgnoredUsings = { "System.Collections.Generic", "UnityEngine" };

        public static Result Parse(string source)
        {
            var result = new Result();
            if (string.IsNullOrEmpty(source)) return result;

            // Los comentarios se quitan antes de nada: la plantilla antigua llevaba ejemplos comentados.
            string code = StripComments(source);

            foreach (Match match in UsingRegex.Matches(code))
            {
                string ns = match.Groups[1].Value;
                if (Array.IndexOf(IgnoredUsings, ns) >= 0) continue;
                if (ns.StartsWith("Firebase", StringComparison.Ordinal)) continue;
                if (!result.Usings.Contains(ns)) result.Usings.Add(ns);
            }

            var constants = new Dictionary<string, string>();
            foreach (Match match in ConstRegex.Matches(code))
            {
                constants[match.Groups[1].Value] = match.Groups[2].Value;
            }

            string body = ExtractMethodBody(code);
            if (body == null) return result;

            foreach (var rawLine in body.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0) continue;
                result.BodyLines.Add(line);
                if (!line.StartsWith("parameters.Add(", StringComparison.Ordinal)
                    && !line.StartsWith("stringParams.Add(", StringComparison.Ordinal))
                {
                    result.IsSimpleBody = false;
                }
            }

            int index = 0;
            while (true)
            {
                int start = body.IndexOf("new Parameter", index, StringComparison.Ordinal);
                if (start < 0) break;

                int open = body.IndexOf('(', start);
                if (open < 0) break;

                int close = FindClosing(body, open, '(', ')');
                if (close < 0) break;

                string arguments = body.Substring(open + 1, close - open - 1);
                int comma = FindTopLevelComma(arguments);
                if (comma > 0)
                {
                    string key = arguments.Substring(0, comma).Trim();
                    string value = arguments.Substring(comma + 1).Trim();

                    var parameter = new CommonParameter { ValueExpression = value };
                    string literal;
                    if (key.StartsWith("\"", StringComparison.Ordinal))
                    {
                        parameter.Key = key;
                        parameter.KeyResolved = true;
                    }
                    else if (constants.TryGetValue(key, out literal))
                    {
                        parameter.Key = "\"" + literal + "\"";
                        parameter.KeyResolved = true;
                        if (!result.ResolvedConstants.Contains(key)) result.ResolvedConstants.Add(key);
                    }
                    else
                    {
                        parameter.Key = key;
                        parameter.KeyResolved = false;
                    }

                    result.Parameters.Add(parameter);
                }

                index = close + 1;
            }

            return result;
        }

        /// <summary>
        /// Una línea por parámetro, lista para RegisterCommonParameters(). Si el cuerpo antiguo tenía lógica propia
        /// o la clave no se ha podido resolver, la línea va comentada con un TODO en vez de como código.
        /// </summary>
        public static List<string> BuildRegistrationLines(Result result)
        {
            var lines = new List<string>();
            foreach (var parameter in result.Parameters)
            {
                string line = "AnalyticsService.RegisterCommonParameter(" + parameter.Key + ", () => " + parameter.ValueExpression + ");";
                if (!parameter.KeyResolved)
                {
                    line = "// TODO: the key could not be resolved to a string, fix it and uncomment: " + line;
                }
                else if (!result.IsSimpleBody)
                {
                    line = "// TODO: compute the value inside the lambda (see the original code above) and uncomment: " + line;
                }
                lines.Add(line);
            }
            return lines;
        }

        private static string StripComments(string code)
        {
            code = Regex.Replace(code, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            code = Regex.Replace(code, @"//[^\r\n]*", string.Empty);
            return code;
        }

        private static string ExtractMethodBody(string code)
        {
            Match match = MethodRegex.Match(code);
            if (!match.Success) return null;

            int open = code.IndexOf('{', match.Index);
            if (open < 0) return null;

            int close = FindClosing(code, open, '{', '}');
            if (close < 0) return null;

            return code.Substring(open + 1, close - open - 1);
        }

        /// <summary>
        /// Índice del cierre que empareja con la apertura en openIndex, ignorando lo que hay dentro de cadenas.
        /// </summary>
        private static int FindClosing(string text, int openIndex, char open, char close)
        {
            int depth = 0;
            bool inString = false;

            for (int i = openIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (c == '\\')
                    {
                        i++;
                        continue;
                    }
                    if (c == '"') inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == open)
                {
                    depth++;
                }
                else if (c == close)
                {
                    depth--;
                    if (depth == 0) return i;
                }
            }

            return -1;
        }

        /// <summary>Índice de la primera coma que no está dentro de paréntesis, corchetes, llaves ni cadenas.</summary>
        private static int FindTopLevelComma(string text)
        {
            int depth = 0;
            bool inString = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (c == '\\')
                    {
                        i++;
                        continue;
                    }
                    if (c == '"') inString = false;
                    continue;
                }

                switch (c)
                {
                    case '"':
                        inString = true;
                        break;
                    case '(':
                    case '[':
                    case '{':
                        depth++;
                        break;
                    case ')':
                    case ']':
                    case '}':
                        depth--;
                        break;
                    case ',':
                        if (depth == 0) return i;
                        break;
                }
            }

            return -1;
        }
    }
}
