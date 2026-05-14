using System.Globalization;

namespace CaeriusNet.Generator.AutoContracts;

internal static class AutoContractsManifestParser
{
    private const string DefaultNamespace = "CaeriusNet.Generated";

    internal static AutoContractsManifest? ParseOrDefault(AdditionalText text, CancellationToken cancellationToken)
    {
        var source = text.GetText(cancellationToken)?.ToString();
        if (string.IsNullOrWhiteSpace(source))
            return null;

        try
        {
            var json = SimpleJsonParser.Parse(source!);
            var root = json.AsObject();
            var version = root.GetInt32("version", 1);
            var ns = root.GetString("namespace", DefaultNamespace);
            var tableTypes = ParseTableTypes(root.GetArray("tableTypes"));
            var procedures = ParseProcedures(root.GetArray("procedures"));

            return new AutoContractsManifest(version, ns, tableTypes, procedures);
        }
        catch
        {
            return null;
        }
    }

    private static EquatableArray<AutoContractsTableType> ParseTableTypes(IReadOnlyList<JsonValue> values)
    {
        var tableTypes = new AutoContractsTableType[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i].AsObject();
            tableTypes[i] = new AutoContractsTableType(
                value.GetRequiredString("schema"),
                value.GetRequiredString("name"),
                value.GetRequiredString("clrName"),
                ParseColumns(value.GetArray("columns")),
                value.GetString("contractHash", string.Empty));
        }

        return new EquatableArray<AutoContractsTableType>(tableTypes.ToImmutableArray());
    }

    private static EquatableArray<AutoContractsProcedure> ParseProcedures(IReadOnlyList<JsonValue> values)
    {
        var procedures = new AutoContractsProcedure[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i].AsObject();
            var resultSet = value.TryGetObject("resultSet", out var resultSetObject)
                ? ParseResultSet(resultSetObject)
                : new AutoContractsResultSet(
                    "None",
                    EquatableArray<AutoContractsColumn>.Empty,
                    null);

            procedures[i] = new AutoContractsProcedure(
                value.GetRequiredString("schema"),
                value.GetRequiredString("name"),
                value.GetRequiredString("clrName"),
                value.GetRequiredString("parametersClrName"),
                value.GetString("resultClrName", null),
                ParseParameters(value.GetArray("parameters")),
                resultSet,
                value.GetString("contractHash", string.Empty));
        }

        return new EquatableArray<AutoContractsProcedure>(procedures.ToImmutableArray());
    }

    private static AutoContractsResultSet ParseResultSet(JsonObject value)
    {
        return new AutoContractsResultSet(
            value.GetString("status", "None"),
            ParseColumns(value.GetArray("columns")),
            value.GetString("errorMessage", null));
    }

    private static EquatableArray<AutoContractsParameter> ParseParameters(IReadOnlyList<JsonValue> values)
    {
        var parameters = new AutoContractsParameter[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i].AsObject();
            parameters[i] = new AutoContractsParameter(
                value.GetInt32("ordinal", i + 1),
                value.GetRequiredString("name"),
                value.GetRequiredString("sqlType"),
                value.GetRequiredString("clrType"),
                value.GetBoolean("isTableType", false),
                value.GetBoolean("isOutput", false),
                value.GetBoolean("nullable", false),
                value.GetNullableInt32("maxLength"),
                value.GetNullableByte("precision"),
                value.GetNullableByte("scale"));
        }

        return new EquatableArray<AutoContractsParameter>(parameters.ToImmutableArray());
    }

    private static EquatableArray<AutoContractsColumn> ParseColumns(IReadOnlyList<JsonValue> values)
    {
        var columns = new AutoContractsColumn[values.Count];
        for (var i = 0; i < values.Count; i++)
        {
            var value = values[i].AsObject();
            columns[i] = new AutoContractsColumn(
                value.GetInt32("ordinal", i + 1),
                value.GetRequiredString("name"),
                value.GetRequiredString("sqlType"),
                value.GetRequiredString("clrType"),
                value.GetBoolean("nullable", false),
                value.GetNullableInt32("maxLength"),
                value.GetNullableByte("precision"),
                value.GetNullableByte("scale"));
        }

        return new EquatableArray<AutoContractsColumn>(columns.ToImmutableArray());
    }

    private sealed class SimpleJsonParser(string text)
    {
        private int _position;

        private bool IsEnd => _position >= text.Length;

        internal static JsonValue Parse(string text)
        {
            var parser = new SimpleJsonParser(text);
            var value = parser.ParseValue();
            parser.SkipWhiteSpace();
            if (!parser.IsEnd)
                throw new FormatException("Unexpected trailing content in contract manifest.");

            return value;
        }

        private JsonValue ParseValue()
        {
            SkipWhiteSpace();
            if (IsEnd)
                throw new FormatException("Unexpected end of JSON.");

            return text[_position] switch
            {
                '{' => ParseObject(),
                '[' => ParseArray(),
                '"' => new JsonValue(ParseString()),
                't' => ParseLiteral("true", new JsonValue(true)),
                'f' => ParseLiteral("false", new JsonValue(false)),
                'n' => ParseLiteral("null", JsonValue.Null),
                '-' or >= '0' and <= '9' => new JsonValue(ParseNumber()),
                _ => throw new FormatException($"Unexpected character '{text[_position]}' in JSON.")
            };
        }

        private JsonValue ParseObject()
        {
            Expect('{');
            var values = new Dictionary<string, JsonValue>(StringComparer.Ordinal);
            SkipWhiteSpace();
            if (TryConsume('}'))
                return new JsonValue(new JsonObject(values));

            while (true)
            {
                SkipWhiteSpace();
                var name = ParseString();
                SkipWhiteSpace();
                Expect(':');
                values[name] = ParseValue();
                SkipWhiteSpace();
                if (TryConsume('}'))
                    return new JsonValue(new JsonObject(values));
                Expect(',');
            }
        }

        private JsonValue ParseArray()
        {
            Expect('[');
            var values = new List<JsonValue>();
            SkipWhiteSpace();
            if (TryConsume(']'))
                return new JsonValue(values);

            while (true)
            {
                values.Add(ParseValue());
                SkipWhiteSpace();
                if (TryConsume(']'))
                    return new JsonValue(values);
                Expect(',');
            }
        }

        private string ParseString()
        {
            Expect('"');
            var sb = new StringBuilder();
            while (!IsEnd)
            {
                var ch = text[_position++];
                if (ch == '"')
                    return sb.ToString();

                if (ch != '\\')
                {
                    sb.Append(ch);
                    continue;
                }

                if (IsEnd)
                    throw new FormatException("Unterminated JSON escape sequence.");

                var escaped = text[_position++];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        sb.Append(escaped);
                        break;
                    case 'b':
                        sb.Append('\b');
                        break;
                    case 'f':
                        sb.Append('\f');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'u':
                        sb.Append(ParseUnicodeEscape());
                        break;
                    default:
                        throw new FormatException($"Unsupported JSON escape sequence '\\{escaped}'.");
                }
            }

            throw new FormatException("Unterminated JSON string.");
        }

        private char ParseUnicodeEscape()
        {
            if (_position + 4 > text.Length)
                throw new FormatException("Invalid JSON unicode escape.");

            var value = Convert.ToInt32(text.Substring(_position, 4), 16);
            _position += 4;
            return (char)value;
        }

        private double ParseNumber()
        {
            var start = _position;
            if (text[_position] == '-')
                _position++;

            while (!IsEnd && char.IsDigit(text[_position]))
                _position++;

            if (!IsEnd && text[_position] == '.')
            {
                _position++;
                while (!IsEnd && char.IsDigit(text[_position]))
                    _position++;
            }

            if (!IsEnd && (text[_position] == 'e' || text[_position] == 'E'))
            {
                _position++;
                if (!IsEnd && (text[_position] == '+' || text[_position] == '-'))
                    _position++;
                while (!IsEnd && char.IsDigit(text[_position]))
                    _position++;
            }

            return double.Parse(
                text.Substring(start, _position - start),
                CultureInfo.InvariantCulture);
        }

        private JsonValue ParseLiteral(string literal, JsonValue value)
        {
            if (_position + literal.Length > text.Length ||
                !string.Equals(
                    text.Substring(_position, literal.Length),
                    literal,
                    StringComparison.Ordinal))
                throw new FormatException($"Invalid JSON literal at position {_position}.");

            _position += literal.Length;
            return value;
        }

        private void SkipWhiteSpace()
        {
            while (!IsEnd && char.IsWhiteSpace(text[_position]))
                _position++;
        }

        private bool TryConsume(char expected)
        {
            if (IsEnd || text[_position] != expected)
                return false;

            _position++;
            return true;
        }

        private void Expect(char expected)
        {
            if (!TryConsume(expected))
                throw new FormatException($"Expected '{expected}' at position {_position}.");
        }
    }

    private sealed class JsonValue
    {
        internal static readonly JsonValue Null = new();

        private readonly object? _value;

        private JsonValue()
        {
        }

        internal JsonValue(string value)
        {
            _value = value;
        }

        internal JsonValue(double value)
        {
            _value = value;
        }

        internal JsonValue(bool value)
        {
            _value = value;
        }

        internal JsonValue(JsonObject value)
        {
            _value = value;
        }

        internal JsonValue(IReadOnlyList<JsonValue> value)
        {
            _value = value;
        }

        internal bool IsNull => _value is null;

        internal JsonObject AsObject()
        {
            return _value as JsonObject
                   ?? throw new FormatException("Expected JSON object.");
        }

        internal IReadOnlyList<JsonValue> AsArray()
        {
            return _value as IReadOnlyList<JsonValue>
                   ?? throw new FormatException("Expected JSON array.");
        }

        internal string AsString()
        {
            return _value as string
                   ?? throw new FormatException("Expected JSON string.");
        }

        internal int AsInt32()
        {
            return Convert.ToInt32(_value, CultureInfo.InvariantCulture);
        }

        internal byte AsByte()
        {
            return Convert.ToByte(_value, CultureInfo.InvariantCulture);
        }

        internal bool AsBoolean()
        {
            return _value is bool value
                ? value
                : throw new FormatException("Expected JSON boolean.");
        }
    }

    private sealed class JsonObject(Dictionary<string, JsonValue> values)
    {
        internal string GetRequiredString(string name)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsString()
                : throw new FormatException($"Required manifest property '{name}' is missing.");
        }

        internal string GetString(string name, string? defaultValue)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsString()
                : defaultValue ?? string.Empty;
        }

        internal int GetInt32(string name, int defaultValue)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsInt32()
                : defaultValue;
        }

        internal int? GetNullableInt32(string name)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsInt32()
                : null;
        }

        internal byte? GetNullableByte(string name)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsByte()
                : null;
        }

        internal bool GetBoolean(string name, bool defaultValue)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsBoolean()
                : defaultValue;
        }

        internal IReadOnlyList<JsonValue> GetArray(string name)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsArray()
                : [];
        }

        internal bool TryGetObject(string name, out JsonObject obj)
        {
            if (values.TryGetValue(name, out var value) && !value.IsNull)
            {
                obj = value.AsObject();
                return true;
            }

            obj = null!;
            return false;
        }
    }
}