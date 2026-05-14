namespace CaeriusNet.Generator.AutoContracts;

internal static class AutoContractsCacheKeyEmitter
{
    internal static void Append(
        StringBuilder sb,
        AutoContractsProcedure procedure,
        IReadOnlyDictionary<string, AutoContractsTableType> tableTypesBySqlName)
    {
        sb.Append("public static partial class ")
            .Append(procedure.ClrName)
            .AppendLine("CacheKeys");
        sb.AppendLine("{");
        sb.Append("    public static string ByParameters(")
            .Append(procedure.ParametersClrName)
            .AppendLine(" parameters)");
        sb.AppendLine("    {");
        sb.AppendLine("        ArgumentNullException.ThrowIfNull(parameters);");
        sb.AppendLine("        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);");
        sb.Append("        AppendString(hash, ")
            .Append(procedure.ClrName)
            .AppendLine(".FullName);");
        sb.Append("        AppendString(hash, ")
            .Append(procedure.ClrName)
            .AppendLine(".ContractHash);");

        foreach (var parameter in procedure.Parameters)
            AppendParameter(sb, parameter, tableTypesBySqlName);

        sb.Append("        return ")
            .Append(procedure.ClrName)
            .AppendLine(".FullName + \":\" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();");
        sb.AppendLine("    }");
        sb.AppendLine();
        AppendHelpers(sb);
        sb.AppendLine("}");
    }

    private static void AppendParameter(
        StringBuilder sb,
        AutoContractsParameter parameter,
        IReadOnlyDictionary<string, AutoContractsTableType> tableTypesBySqlName)
    {
        sb.Append("        AppendString(hash, ")
            .Append(AutoContractsSqlEmitter.ToStringLiteral(parameter.Name))
            .AppendLine(");");

        var property = "parameters." + AutoContractsSqlEmitter.ToIdentifier(parameter.Name);
        if (parameter.IsTableType && tableTypesBySqlName.TryGetValue(parameter.SqlType, out var tableType))
            AppendTvpValue(sb, property, tableType);
        else
            AppendValue(sb, property, parameter.ClrType, parameter.Nullable, 8);
    }

    private static void AppendTvpValue(StringBuilder sb, string property, AutoContractsTableType tableType)
    {
        sb.Append("        AppendInt32(hash, ")
            .Append(property)
            .AppendLine(".Length);");
        sb.Append("        foreach (var row in ")
            .Append(property)
            .AppendLine(".Span)");
        sb.AppendLine("        {");
        foreach (var column in tableType.Columns)
        {
            var value = "row." + AutoContractsSqlEmitter.ToIdentifier(column.Name);
            AppendValue(sb, value, column.ClrType, column.Nullable, 12);
        }

        sb.AppendLine("        }");
    }

    private static void AppendValue(
        StringBuilder sb,
        string expression,
        string clrType,
        bool nullable,
        int indent)
    {
        var normalizedClrType = clrType.TrimEnd('?');
        var pad = new string(' ', indent);

        if (nullable && AutoContractsClrTypes.IsNullableValueType(normalizedClrType))
        {
            sb.Append(pad).Append("if (").Append(expression).AppendLine(".HasValue)");
            sb.Append(pad).AppendLine("{");
            sb.Append(pad).AppendLine("    AppendByte(hash, 1);");
            AppendValue(sb, expression + ".Value", normalizedClrType, false, indent + 4);
            sb.Append(pad).AppendLine("}");
            sb.Append(pad).AppendLine("else");
            sb.Append(pad).AppendLine("{");
            sb.Append(pad).AppendLine("    AppendByte(hash, 0);");
            sb.Append(pad).AppendLine("}");
            return;
        }

        var method = GetAppendMethod(normalizedClrType);

        sb.Append(pad)
            .Append(method)
            .Append("(hash, ");

        if (method == "AppendString" && normalizedClrType != "string")
            sb.Append(expression).Append(".ToString()");
        else
            sb.Append(expression);

        sb.AppendLine(");");
    }

    private static string GetAppendMethod(string clrType)
    {
        return clrType switch
        {
            "bool" => "AppendBoolean",
            "byte" => "AppendByte",
            "short" => "AppendInt16",
            "int" => "AppendInt32",
            "long" => "AppendInt64",
            "float" => "AppendSingle",
            "double" => "AppendDouble",
            "decimal" => "AppendDecimal",
            "DateOnly" => "AppendDateOnly",
            "TimeOnly" => "AppendTimeOnly",
            "DateTime" => "AppendDateTime",
            "DateTimeOffset" => "AppendDateTimeOffset",
            "Guid" => "AppendGuid",
            "string" => "AppendString",
            "byte[]" => "AppendBytes",
            _ => "AppendString"
        };
    }

    private static void AppendHelpers(StringBuilder sb)
    {
        sb.AppendLine(
            "    private static void AppendBoolean(IncrementalHash hash, bool value) => AppendByte(hash, value ? (byte)1 : (byte)0);");
        sb.AppendLine();
        sb.AppendLine("    private static void AppendByte(IncrementalHash hash, byte value)");
        sb.AppendLine("    {");
        sb.AppendLine("        Span<byte> buffer = stackalloc byte[1];");
        sb.AppendLine("        buffer[0] = value;");
        sb.AppendLine("        hash.AppendData(buffer);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static void AppendInt16(IncrementalHash hash, short value)");
        sb.AppendLine("    {");
        sb.AppendLine("        Span<byte> buffer = stackalloc byte[2];");
        sb.AppendLine("        BinaryPrimitives.WriteInt16LittleEndian(buffer, value);");
        sb.AppendLine("        hash.AppendData(buffer);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static void AppendInt32(IncrementalHash hash, int value)");
        sb.AppendLine("    {");
        sb.AppendLine("        Span<byte> buffer = stackalloc byte[4];");
        sb.AppendLine("        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);");
        sb.AppendLine("        hash.AppendData(buffer);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static void AppendInt64(IncrementalHash hash, long value)");
        sb.AppendLine("    {");
        sb.AppendLine("        Span<byte> buffer = stackalloc byte[8];");
        sb.AppendLine("        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);");
        sb.AppendLine("        hash.AppendData(buffer);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine(
            "    private static void AppendSingle(IncrementalHash hash, float value) => AppendInt32(hash, BitConverter.SingleToInt32Bits(value));");
        sb.AppendLine();
        sb.AppendLine(
            "    private static void AppendDouble(IncrementalHash hash, double value) => AppendInt64(hash, BitConverter.DoubleToInt64Bits(value));");
        sb.AppendLine();
        sb.AppendLine("    private static void AppendDecimal(IncrementalHash hash, decimal value)");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var part in decimal.GetBits(value))");
        sb.AppendLine("            AppendInt32(hash, part);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine(
            "    private static void AppendDateOnly(IncrementalHash hash, DateOnly value) => AppendInt32(hash, value.DayNumber);");
        sb.AppendLine();
        sb.AppendLine(
            "    private static void AppendTimeOnly(IncrementalHash hash, TimeOnly value) => AppendInt64(hash, value.Ticks);");
        sb.AppendLine();
        sb.AppendLine(
            "    private static void AppendDateTime(IncrementalHash hash, DateTime value) => AppendInt64(hash, value.ToBinary());");
        sb.AppendLine();
        sb.AppendLine("    private static void AppendDateTimeOffset(IncrementalHash hash, DateTimeOffset value)");
        sb.AppendLine("    {");
        sb.AppendLine("        AppendInt64(hash, value.UtcDateTime.ToBinary());");
        sb.AppendLine("        AppendInt64(hash, value.Offset.Ticks);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static void AppendGuid(IncrementalHash hash, Guid value)");
        sb.AppendLine("    {");
        sb.AppendLine("        Span<byte> buffer = stackalloc byte[16];");
        sb.AppendLine("        value.TryWriteBytes(buffer);");
        sb.AppendLine("        hash.AppendData(buffer);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static void AppendString(IncrementalHash hash, string? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            AppendByte(hash, 0);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        AppendByte(hash, 1);");
        sb.AppendLine("        var bytes = Encoding.UTF8.GetBytes(value);");
        sb.AppendLine("        AppendInt32(hash, bytes.Length);");
        sb.AppendLine("        hash.AppendData(bytes);");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine("    private static void AppendBytes(IncrementalHash hash, byte[]? value)");
        sb.AppendLine("    {");
        sb.AppendLine("        if (value is null)");
        sb.AppendLine("        {");
        sb.AppendLine("            AppendByte(hash, 0);");
        sb.AppendLine("            return;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        AppendByte(hash, 1);");
        sb.AppendLine("        AppendInt32(hash, value.Length);");
        sb.AppendLine("        hash.AppendData(value);");
        sb.AppendLine("    }");
    }
}