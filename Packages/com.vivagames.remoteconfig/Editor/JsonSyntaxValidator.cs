using System.Globalization;

namespace Viva.Services.RemoteConfig.Editor
{
    /// <summary>
    /// Comprueba que un texto es JSON válido (RFC 8259) sin construir ningún modelo: objetos, arrays,
    /// cadenas con escapes, números, true, false y null. Sirve para validar el valor por defecto de los
    /// parámetros de tipo json sin depender de ninguna librería.
    /// </summary>
    public static class JsonSyntaxValidator
    {
        /// <summary>true si es JSON válido. Si no, error describe el problema y dónde está (línea y columna).</summary>
        public static bool Validate(string json, out string error)
        {
            error = null;
            if (json == null)
            {
                error = "empty";
                return false;
            }

            var parser = new Parser(json);
            parser.SkipWhitespace();
            if (parser.AtEnd)
            {
                error = "empty";
                return false;
            }

            if (!parser.ParseValue(out error)) return false;

            parser.SkipWhitespace();
            if (!parser.AtEnd)
            {
                error = parser.Describe("unexpected content after the JSON value");
                return false;
            }
            return true;
        }

        private struct Parser
        {
            private readonly string _text;
            private int _index;

            public Parser(string text)
            {
                _text = text;
                _index = 0;
            }

            public bool AtEnd => _index >= _text.Length;

            private char Current => _text[_index];

            public void SkipWhitespace()
            {
                while (!AtEnd && (Current == ' ' || Current == '\t' || Current == '\n' || Current == '\r')) _index++;
            }

            public string Describe(string message)
            {
                int line = 1, column = 1;
                for (int i = 0; i < _index && i < _text.Length; i++)
                {
                    if (_text[i] == '\n')
                    {
                        line++;
                        column = 1;
                    }
                    else
                    {
                        column++;
                    }
                }
                return $"{message} (line {line}, column {column})";
            }

            public bool ParseValue(out string error)
            {
                error = null;
                if (AtEnd) return Fail("unexpected end of JSON", out error);

                switch (Current)
                {
                    case '{': return ParseObject(out error);
                    case '[': return ParseArray(out error);
                    case '"': return ParseString(out error);
                    case 't': return ParseLiteral("true", out error);
                    case 'f': return ParseLiteral("false", out error);
                    case 'n': return ParseLiteral("null", out error);
                    default:
                        if (Current == '-' || char.IsDigit(Current)) return ParseNumber(out error);
                        if (Current == '\'') return Fail("strings must use double quotes", out error);
                        return Fail($"unexpected character '{Current}'", out error);
                }
            }

            private bool ParseObject(out string error)
            {
                _index++; // {
                SkipWhitespace();
                if (!AtEnd && Current == '}')
                {
                    _index++;
                    error = null;
                    return true;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (AtEnd) return Fail("unterminated object", out error);
                    if (Current != '"') return Fail("object keys must be double-quoted strings", out error);
                    if (!ParseString(out error)) return false;

                    SkipWhitespace();
                    if (AtEnd || Current != ':') return Fail("expected ':' after the object key", out error);
                    _index++;

                    SkipWhitespace();
                    if (!ParseValue(out error)) return false;

                    SkipWhitespace();
                    if (AtEnd) return Fail("unterminated object", out error);
                    if (Current == ',')
                    {
                        _index++;
                        SkipWhitespace();
                        if (!AtEnd && Current == '}') return Fail("trailing comma in object", out error);
                        continue;
                    }
                    if (Current == '}')
                    {
                        _index++;
                        return true;
                    }
                    return Fail("expected ',' or '}' in object", out error);
                }
            }

            private bool ParseArray(out string error)
            {
                _index++; // [
                SkipWhitespace();
                if (!AtEnd && Current == ']')
                {
                    _index++;
                    error = null;
                    return true;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (!ParseValue(out error)) return false;

                    SkipWhitespace();
                    if (AtEnd) return Fail("unterminated array", out error);
                    if (Current == ',')
                    {
                        _index++;
                        SkipWhitespace();
                        if (!AtEnd && Current == ']') return Fail("trailing comma in array", out error);
                        continue;
                    }
                    if (Current == ']')
                    {
                        _index++;
                        return true;
                    }
                    return Fail("expected ',' or ']' in array", out error);
                }
            }

            private bool ParseString(out string error)
            {
                _index++; // "
                while (true)
                {
                    if (AtEnd) return Fail("unterminated string", out error);
                    char c = Current;
                    if (c == '"')
                    {
                        _index++;
                        error = null;
                        return true;
                    }
                    if (c < ' ') return Fail("control character inside a string (use \\n, \\t...)", out error);
                    if (c == '\\')
                    {
                        _index++;
                        if (AtEnd) return Fail("unterminated escape sequence", out error);
                        char escaped = Current;
                        switch (escaped)
                        {
                            case '"':
                            case '\\':
                            case '/':
                            case 'b':
                            case 'f':
                            case 'n':
                            case 'r':
                            case 't':
                                _index++;
                                break;
                            case 'u':
                                _index++;
                                for (int i = 0; i < 4; i++)
                                {
                                    if (AtEnd || !IsHex(Current)) return Fail("\\u must be followed by four hex digits", out error);
                                    _index++;
                                }
                                break;
                            default:
                                return Fail($"invalid escape sequence '\\{escaped}'", out error);
                        }
                        continue;
                    }
                    _index++;
                }
            }

            private bool ParseNumber(out string error)
            {
                int start = _index;
                if (Current == '-') _index++;
                if (AtEnd || !char.IsDigit(Current)) return Fail("invalid number", out error);

                if (Current == '0')
                {
                    _index++;
                    if (!AtEnd && char.IsDigit(Current)) return Fail("numbers cannot have leading zeros", out error);
                }
                else
                {
                    while (!AtEnd && char.IsDigit(Current)) _index++;
                }

                if (!AtEnd && Current == '.')
                {
                    _index++;
                    if (AtEnd || !char.IsDigit(Current)) return Fail("expected digits after the decimal point", out error);
                    while (!AtEnd && char.IsDigit(Current)) _index++;
                }

                if (!AtEnd && (Current == 'e' || Current == 'E'))
                {
                    _index++;
                    if (!AtEnd && (Current == '+' || Current == '-')) _index++;
                    if (AtEnd || !char.IsDigit(Current)) return Fail("expected digits in the exponent", out error);
                    while (!AtEnd && char.IsDigit(Current)) _index++;
                }

                // Solo para asegurar que el número cabe en un double, como haría cualquier parser.
                var text = _text.Substring(start, _index - start);
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    return Fail("number out of range", out error);

                error = null;
                return true;
            }

            private bool ParseLiteral(string literal, out string error)
            {
                if (string.CompareOrdinal(_text, _index, literal, 0, literal.Length) != 0)
                    return Fail($"invalid literal (expected {literal})", out error);
                _index += literal.Length;
                error = null;
                return true;
            }

            private bool Fail(string message, out string error)
            {
                error = Describe(message);
                return false;
            }

            private static bool IsHex(char c)
            {
                return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
            }
        }
    }
}
