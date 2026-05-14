using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CaeriusNet.Analyzer.AutoContracts;

internal static class AutoContractsManifestParser
{
    internal const int SupportedManifestVersion = 1;

    internal static AutoContractsManifest Parse(string source)
    {
        var root = JsonParser.Parse(source).AsObject();
        return new AutoContractsManifest(
            root.GetRequiredInt32("version"),
            root.GetRequiredString("namespace"),
            ParseTableTypes(root.GetRequiredArray("tableTypes")),
            ParseProcedures(root.GetRequiredArray("procedures")));
    }

    private static EquatableArray<AutoContractsTableType> ParseTableTypes(IReadOnlyList<JsonValue> values)
    {
        var tableTypes = ImmutableArray.CreateBuilder<AutoContractsTableType>(values.Count);
        foreach (var value in values)
        {
            var obj = value.AsObject();
            tableTypes.Add(new AutoContractsTableType(
                obj.GetRequiredString("schema"),
                obj.GetRequiredString("name"),
                obj.GetRequiredString("clrName"),
                ParseColumns(obj.GetRequiredArray("columns"))));
        }

        return new EquatableArray<AutoContractsTableType>(tableTypes.MoveToImmutable());
    }

    private static EquatableArray<AutoContractsProcedure> ParseProcedures(IReadOnlyList<JsonValue> values)
    {
        var procedures = ImmutableArray.CreateBuilder<AutoContractsProcedure>(values.Count);
        foreach (var value in values)
        {
            var obj = value.AsObject();
            procedures.Add(new AutoContractsProcedure(
                obj.GetRequiredString("schema"),
                obj.GetRequiredString("name"),
                obj.GetRequiredString("clrName"),
                obj.GetRequiredString("parametersClrName"),
                obj.GetString("resultClrName", null),
                ParseParameters(obj.GetRequiredArray("parameters")),
                obj.TryGetObject("resultSet", out var resultSet)
                    ? ParseResultSet(resultSet)
                    : new AutoContractsResultSet("None", EquatableArray<AutoContractsColumn>.Empty)));
        }

        return new EquatableArray<AutoContractsProcedure>(procedures.MoveToImmutable());
    }

    private static AutoContractsResultSet ParseResultSet(JsonObject value)
    {
        return new AutoContractsResultSet(
            value.GetString("status", "None"),
            ParseColumns(value.GetRequiredArray("columns")));
    }

    private static EquatableArray<AutoContractsParameter> ParseParameters(IReadOnlyList<JsonValue> values)
    {
        var parameters = ImmutableArray.CreateBuilder<AutoContractsParameter>(values.Count);
        foreach (var value in values)
        {
            var obj = value.AsObject();
            parameters.Add(new AutoContractsParameter(
                obj.GetInt32("ordinal", parameters.Count + 1),
                obj.GetRequiredString("name"),
                obj.GetRequiredString("sqlType"),
                obj.GetRequiredString("clrType"),
                obj.GetBoolean("isTableType", false),
                obj.GetBoolean("isOutput", false),
                obj.GetBoolean("nullable", false),
                obj.GetNullableInt32("maxLength"),
                obj.GetNullableByte("precision"),
                obj.GetNullableByte("scale")));
        }

        return new EquatableArray<AutoContractsParameter>(parameters.MoveToImmutable());
    }

    private static EquatableArray<AutoContractsColumn> ParseColumns(IReadOnlyList<JsonValue> values)
    {
        var columns = ImmutableArray.CreateBuilder<AutoContractsColumn>(values.Count);
        foreach (var value in values)
        {
            var obj = value.AsObject();
            columns.Add(new AutoContractsColumn(
                obj.GetInt32("ordinal", columns.Count + 1),
                obj.GetRequiredString("name"),
                obj.GetRequiredString("sqlType"),
                obj.GetRequiredString("clrType"),
                obj.GetBoolean("nullable", false),
                obj.GetNullableInt32("maxLength"),
                obj.GetNullableByte("precision"),
                obj.GetNullableByte("scale")));
        }

        return new EquatableArray<AutoContractsColumn>(columns.MoveToImmutable());
    }

    private sealed class JsonParser(string text)
    {
        private int _position;

        private bool IsEnd => _position >= text.Length;

        internal static JsonValue Parse(string text)
        {
            var parser = new JsonParser(text);
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

            return double.Parse(text.Substring(start, _position - start), CultureInfo.InvariantCulture);
        }

        private JsonValue ParseLiteral(string literal, JsonValue value)
        {
            if (_position + literal.Length > text.Length ||
                !string.Equals(text.Substring(_position, literal.Length), literal, StringComparison.Ordinal))
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
            return _value is double value &&
                   value >= int.MinValue &&
                   value <= int.MaxValue &&
                   Math.Truncate(value) == value
                ? (int)value
                : throw new FormatException("Expected JSON integer.");
        }

        internal byte AsByte()
        {
            return _value is double value &&
                   value >= byte.MinValue &&
                   value <= byte.MaxValue &&
                   Math.Truncate(value) == value
                ? (byte)value
                : throw new FormatException("Expected JSON byte.");
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
            if (!values.TryGetValue(name, out var value) || value.IsNull)
                throw new FormatException($"Required manifest property '{name}' is missing.");

            var text = value.AsString();
            return string.IsNullOrWhiteSpace(text)
                ? throw new FormatException($"Required manifest property '{name}' is empty.")
                : text;
        }

        internal string GetString(string name, string? defaultValue)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsString()
                : defaultValue ?? string.Empty;
        }

        internal int GetRequiredInt32(string name)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsInt32()
                : throw new FormatException($"Required manifest property '{name}' is missing.");
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

        internal IReadOnlyList<JsonValue> GetRequiredArray(string name)
        {
            return values.TryGetValue(name, out var value) && !value.IsNull
                ? value.AsArray()
                : throw new FormatException($"Required manifest property '{name}' is missing.");
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
