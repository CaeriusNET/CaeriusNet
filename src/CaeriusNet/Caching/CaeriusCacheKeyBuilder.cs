using System.Data.SqlTypes;

namespace CaeriusNet.Caching;

/// <summary>Builds deterministic, collision-resistant cache keys for Caerius commands.</summary>
/// <remarks>
///     The parameter digest deliberately has its own binary protocol.  Cache values are allowed to be
///     arbitrary application input, so falling back to <see cref="object.ToString" /> would make a cache key
///     depend on a presentation format and can silently collide (for example, two <see cref="Memory{T}" />
///     instances with different bytes but the same length).  Add an explicit case here before allowing a new
///     SQL input representation to participate in a cache key.
/// </remarks>
public static class CaeriusCacheKeyBuilder
{
    private const byte HashFormatMagic = 0xC7;
    private const byte HashFormatVersion = 1;

    public static string Create(
        string applicationNamespace,
        string databaseIdentity,
        StoredProcedureCommand command,
        string resultShape,
        string? resultContractHash = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationNamespace);
        ArgumentNullException.ThrowIfNull(databaseIdentity);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentException.ThrowIfNullOrWhiteSpace(resultShape);

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Start(hash, HashPurpose.Parameters);
        foreach (var parameter in command.Parameters)
        {
            AppendCanonicalValue(hash, parameter.Name);
            AppendInt32(hash, parameter.DbType);
            AppendInt32(hash, parameter.Direction);
            AppendCanonicalValue(hash, parameter.Value);
        }

        var parameterHash = FinalizeHash(hash);
        var databaseToken = string.Concat("database-", HashDatabaseIdentity(databaseIdentity));
        return string.Join(':',
            applicationNamespace,
            databaseToken,
            command.FullName,
            command.ContractHash,
            resultContractHash ?? "runtime",
            resultShape,
            parameterHash);
    }

    private static string HashDatabaseIdentity(string databaseIdentity)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Start(hash, HashPurpose.DatabaseIdentity);
        AppendCanonicalValue(hash, databaseIdentity);
        return FinalizeHash(hash);
    }

    internal static string HashContract(
        string schema,
        string procedure,
        ImmutableArray<StoredProcedureParameterDescriptor> parameters)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Start(hash, HashPurpose.Contract);
        AppendCanonicalValue(hash, schema);
        AppendCanonicalValue(hash, procedure);
        foreach (var parameter in parameters)
        {
            AppendCanonicalValue(hash, parameter.Name);
            AppendInt32(hash, parameter.DbType);
            AppendInt32(hash, parameter.Direction);
            AppendNullableInt32(hash, parameter.Size);
            AppendNullableByte(hash, parameter.Precision);
            AppendNullableByte(hash, parameter.Scale);
            AppendCanonicalValue(hash, parameter.TypeName);
        }

        return FinalizeHash(hash);
    }

    internal static string HashAggregateCodecContract(IReadOnlyList<ICaeriusCacheCodec> codecs)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Start(hash, HashPurpose.AggregateCodec);
        AppendInt32(hash, CanonicalTag.Int32, codecs.Count);
        foreach (var codec in codecs)
        {
            ArgumentNullException.ThrowIfNull(codec);
            ArgumentException.ThrowIfNullOrWhiteSpace(codec.CodecId);
            ArgumentException.ThrowIfNullOrWhiteSpace(codec.ContractHash);
            AppendCanonicalValue(hash, codec.CodecId);
            AppendCanonicalValue(hash, codec.ContractHash);
        }

        return FinalizeHash(hash);
    }

    /// <summary>Appends a versioned, binary canonical representation of a SQL parameter value.</summary>
    internal static void AppendCanonicalValue(IncrementalHash hash, object? value)
    {
        ArgumentNullException.ThrowIfNull(hash);

        switch (value)
        {
            case null or DBNull:
                AppendTag(hash, CanonicalTag.Null);
                return;
            case ITvpParameterValue tvp:
                tvp.AppendCacheKey(hash);
                return;
            case string text:
                AppendUtf8(hash, CanonicalTag.String, text.AsSpan());
                return;
            case char[] characters:
                AppendUtf8(hash, CanonicalTag.Characters, characters);
                return;
            case byte[] bytes:
                AppendBytes(hash, CanonicalTag.Binary, bytes);
                return;
            case ReadOnlyMemory<byte> memory:
                AppendBytes(hash, CanonicalTag.Binary, memory.Span);
                return;
            case Memory<byte> memory:
                AppendBytes(hash, CanonicalTag.Binary, memory.Span);
                return;
            case ArraySegment<byte> segment:
                AppendBytes(hash, CanonicalTag.Binary, segment.AsSpan());
                return;
            case SqlBytes sqlBytes:
                AppendSqlBytes(hash, sqlBytes);
                return;
            case SqlBinary sqlBinary:
                if (sqlBinary.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendBytes(hash, CanonicalTag.Binary, sqlBinary.Value);
                return;
            case SqlChars sqlChars:
                if (sqlChars.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendUtf8(hash, CanonicalTag.Characters, sqlChars.Value);
                return;
            case SqlString sqlString:
                if (sqlString.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendUtf8(hash, CanonicalTag.String, sqlString.Value.AsSpan());
                return;
            case SqlXml sqlXml:
                if (sqlXml.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendUtf8(hash, CanonicalTag.String, sqlXml.Value.AsSpan());
                return;
            case SqlBoolean sqlBoolean:
                if (sqlBoolean.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendBoolean(hash, sqlBoolean.Value);
                return;
            case SqlByte sqlByte:
                if (sqlByte.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendByte(hash, CanonicalTag.Byte, sqlByte.Value);
                return;
            case SqlInt16 sqlInt16:
                if (sqlInt16.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendInt16(hash, CanonicalTag.Int16, sqlInt16.Value);
                return;
            case SqlInt32 sqlInt32:
                if (sqlInt32.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendInt32(hash, CanonicalTag.Int32, sqlInt32.Value);
                return;
            case SqlInt64 sqlInt64:
                if (sqlInt64.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendInt64(hash, CanonicalTag.Int64, sqlInt64.Value);
                return;
            case SqlSingle sqlSingle:
                if (sqlSingle.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendSingle(hash, sqlSingle.Value);
                return;
            case SqlDouble sqlDouble:
                if (sqlDouble.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendDouble(hash, sqlDouble.Value);
                return;
            case SqlDecimal sqlDecimal:
                AppendSqlDecimal(hash, sqlDecimal);
                return;
            case SqlMoney sqlMoney:
                if (sqlMoney.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendDecimal(hash, sqlMoney.Value);
                return;
            case SqlDateTime sqlDateTime:
                if (sqlDateTime.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendDateTime(hash, sqlDateTime.Value);
                return;
            case SqlGuid sqlGuid:
                if (sqlGuid.IsNull) AppendTag(hash, CanonicalTag.Null);
                else AppendGuid(hash, sqlGuid.Value);
                return;
            case bool boolean:
                AppendBoolean(hash, boolean);
                return;
            case byte unsignedByte:
                AppendByte(hash, CanonicalTag.Byte, unsignedByte);
                return;
            case sbyte signedByte:
                AppendByte(hash, CanonicalTag.SByte, unchecked((byte)signedByte));
                return;
            case short int16:
                AppendInt16(hash, CanonicalTag.Int16, int16);
                return;
            case ushort uint16:
                AppendUInt16(hash, CanonicalTag.UInt16, uint16);
                return;
            case int int32:
                AppendInt32(hash, CanonicalTag.Int32, int32);
                return;
            case uint uint32:
                AppendUInt32(hash, CanonicalTag.UInt32, uint32);
                return;
            case long int64:
                AppendInt64(hash, CanonicalTag.Int64, int64);
                return;
            case ulong uint64:
                AppendUInt64(hash, CanonicalTag.UInt64, uint64);
                return;
            case float single:
                AppendSingle(hash, single);
                return;
            case double @double:
                AppendDouble(hash, @double);
                return;
            case decimal decimalValue:
                AppendDecimal(hash, decimalValue);
                return;
            case DateTime dateTime:
                AppendDateTime(hash, dateTime);
                return;
            case DateTimeOffset dateTimeOffset:
                AppendDateTimeOffset(hash, dateTimeOffset);
                return;
            case DateOnly dateOnly:
                AppendInt32(hash, CanonicalTag.DateOnly, dateOnly.DayNumber);
                return;
            case TimeOnly timeOnly:
                AppendInt64(hash, CanonicalTag.TimeOnly, timeOnly.Ticks);
                return;
            case TimeSpan timeSpan:
                AppendInt64(hash, CanonicalTag.TimeSpan, timeSpan.Ticks);
                return;
            case Guid guid:
                AppendGuid(hash, guid);
                return;
            case char character:
                AppendUInt16(hash, CanonicalTag.Character, character);
                return;
            case Enum enumValue:
                AppendEnum(hash, enumValue);
                return;
            default:
                throw new CaeriusContractException(
                    $"Cache key generation cannot canonically encode SQL parameter type '{value.GetType().FullName}'. " +
                    "Register an explicit supported representation or disable caching for this command.");
        }
    }

    private static void Start(IncrementalHash hash, HashPurpose purpose)
    {
        Span<byte> header = stackalloc byte[3] { HashFormatMagic, HashFormatVersion, (byte)purpose };
        hash.AppendData(header);
    }

    private static void AppendSqlBytes(IncrementalHash hash, SqlBytes value)
    {
        if (value.IsNull)
        {
            AppendTag(hash, CanonicalTag.Null);
            return;
        }

        AppendBytes(hash, CanonicalTag.Binary, value.Value);
    }

    private static void AppendSqlDecimal(IncrementalHash hash, SqlDecimal value)
    {
        if (value.IsNull)
        {
            AppendTag(hash, CanonicalTag.Null);
            return;
        }

        AppendTag(hash, CanonicalTag.SqlDecimal);
        AppendRawByte(hash, value.Precision);
        AppendRawByte(hash, value.Scale);
        AppendRawByte(hash, value.IsPositive ? (byte)1 : (byte)0);
        AppendLengthPrefixedBytes(hash, value.BinData);
    }

    private static void AppendEnum(IncrementalHash hash, Enum value)
    {
        switch (Type.GetTypeCode(Enum.GetUnderlyingType(value.GetType())))
        {
            case TypeCode.SByte:
                AppendByte(hash, CanonicalTag.SByte,
                    unchecked((byte)Convert.ToSByte(value, CultureInfo.InvariantCulture)));
                return;
            case TypeCode.Byte:
                AppendByte(hash, CanonicalTag.Byte, Convert.ToByte(value, CultureInfo.InvariantCulture));
                return;
            case TypeCode.Int16:
                AppendInt16(hash, CanonicalTag.Int16, Convert.ToInt16(value, CultureInfo.InvariantCulture));
                return;
            case TypeCode.UInt16:
                AppendUInt16(hash, CanonicalTag.UInt16, Convert.ToUInt16(value, CultureInfo.InvariantCulture));
                return;
            case TypeCode.Int32:
                AppendInt32(hash, CanonicalTag.Int32, Convert.ToInt32(value, CultureInfo.InvariantCulture));
                return;
            case TypeCode.UInt32:
                AppendUInt32(hash, CanonicalTag.UInt32, Convert.ToUInt32(value, CultureInfo.InvariantCulture));
                return;
            case TypeCode.Int64:
                AppendInt64(hash, CanonicalTag.Int64, Convert.ToInt64(value, CultureInfo.InvariantCulture));
                return;
            case TypeCode.UInt64:
                AppendUInt64(hash, CanonicalTag.UInt64, Convert.ToUInt64(value, CultureInfo.InvariantCulture));
                return;
            default:
                throw new CaeriusContractException($"Unsupported enum underlying type '{value.GetType().FullName}'.");
        }
    }

    private static void AppendBoolean(IncrementalHash hash, bool value)
    {
        AppendByte(hash, CanonicalTag.Boolean, value ? (byte)1 : (byte)0);
    }

    private static void AppendSingle(IncrementalHash hash, float value)
    {
        AppendInt32(hash, CanonicalTag.Single, BitConverter.SingleToInt32Bits(value));
    }

    private static void AppendDouble(IncrementalHash hash, double value)
    {
        AppendInt64(hash, CanonicalTag.Double, BitConverter.DoubleToInt64Bits(value));
    }

    private static void AppendDecimal(IncrementalHash hash, decimal value)
    {
        Span<int> parts = stackalloc int[4];
        decimal.GetBits(value, parts);
        AppendTag(hash, CanonicalTag.Decimal);
        foreach (var part in parts)
            AppendRawInt32(hash, part);
    }

    private static void AppendDateTime(IncrementalHash hash, DateTime value)
    {
        AppendTag(hash, CanonicalTag.DateTime);
        AppendRawInt64(hash, value.Ticks);
        AppendRawByte(hash, (byte)value.Kind);
    }

    private static void AppendDateTimeOffset(IncrementalHash hash, DateTimeOffset value)
    {
        AppendTag(hash, CanonicalTag.DateTimeOffset);
        AppendRawInt64(hash, value.Ticks);
        AppendRawInt64(hash, value.Offset.Ticks);
    }

    private static void AppendGuid(IncrementalHash hash, Guid value)
    {
        AppendTag(hash, CanonicalTag.Guid);
        Span<byte> bytes = stackalloc byte[16];
        if (!value.TryWriteBytes(bytes))
            throw new InvalidOperationException("Unable to encode a Guid cache-key value.");
        hash.AppendData(bytes);
    }

    private static void AppendBytes(IncrementalHash hash, CanonicalTag tag, ReadOnlySpan<byte> value)
    {
        AppendTag(hash, tag);
        AppendLengthPrefixedBytes(hash, value);
    }

    private static void AppendUtf8(IncrementalHash hash, CanonicalTag tag, ReadOnlySpan<char> value)
    {
        AppendTag(hash, tag);
        var byteCount = Encoding.UTF8.GetByteCount(value);
        AppendRawInt32(hash, byteCount);
        if (byteCount == 0)
            return;

        const int StackThreshold = 256;
        if (byteCount <= StackThreshold)
        {
            Span<byte> buffer = stackalloc byte[StackThreshold];
            var written = Encoding.UTF8.GetBytes(value, buffer);
            hash.AppendData(buffer[..written]);
            return;
        }

        var rented = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var written = Encoding.UTF8.GetBytes(value, rented);
            hash.AppendData(rented.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static void AppendLengthPrefixedBytes(IncrementalHash hash, ReadOnlySpan<byte> value)
    {
        AppendRawInt32(hash, value.Length);
        hash.AppendData(value);
    }

    private static void AppendNullableInt32(IncrementalHash hash, int? value)
    {
        AppendRawByte(hash, value.HasValue ? (byte)1 : (byte)0);
        if (value is { } actual)
            AppendRawInt32(hash, actual);
    }

    private static void AppendNullableByte(IncrementalHash hash, byte? value)
    {
        AppendRawByte(hash, value.HasValue ? (byte)1 : (byte)0);
        if (value is { } actual)
            AppendRawByte(hash, actual);
    }

    private static void AppendByte(IncrementalHash hash, CanonicalTag tag, byte value)
    {
        AppendTag(hash, tag);
        AppendRawByte(hash, value);
    }

    private static void AppendInt16(IncrementalHash hash, CanonicalTag tag, short value)
    {
        AppendTag(hash, tag);
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16LittleEndian(buffer, value);
        hash.AppendData(buffer);
    }

    private static void AppendUInt16(IncrementalHash hash, CanonicalTag tag, ushort value)
    {
        AppendTag(hash, tag);
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, value);
        hash.AppendData(buffer);
    }

    private static void AppendInt32(IncrementalHash hash, CanonicalTag tag, int value)
    {
        AppendTag(hash, tag);
        AppendRawInt32(hash, value);
    }

    private static void AppendInt32<TEnum>(IncrementalHash hash, TEnum value) where TEnum : struct, Enum
    {
        AppendInt32(hash, CanonicalTag.Int32, Convert.ToInt32(value, CultureInfo.InvariantCulture));
    }

    private static void AppendUInt32(IncrementalHash hash, CanonicalTag tag, uint value)
    {
        AppendTag(hash, tag);
        Span<byte> buffer = stackalloc byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        hash.AppendData(buffer);
    }

    private static void AppendInt64(IncrementalHash hash, CanonicalTag tag, long value)
    {
        AppendTag(hash, tag);
        AppendRawInt64(hash, value);
    }

    private static void AppendUInt64(IncrementalHash hash, CanonicalTag tag, ulong value)
    {
        AppendTag(hash, tag);
        Span<byte> buffer = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(buffer, value);
        hash.AppendData(buffer);
    }

    private static void AppendTag(IncrementalHash hash, CanonicalTag tag)
    {
        AppendRawByte(hash, (byte)tag);
    }

    private static void AppendRawByte(IncrementalHash hash, byte value)
    {
        Span<byte> buffer = stackalloc byte[1];
        buffer[0] = value;
        hash.AppendData(buffer);
    }

    private static void AppendRawInt32(IncrementalHash hash, int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        hash.AppendData(buffer);
    }

    private static void AppendRawInt64(IncrementalHash hash, long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        hash.AppendData(buffer);
    }

    private static string FinalizeHash(IncrementalHash hash)
    {
        Span<byte> hashBytes = stackalloc byte[32];
        var written = hash.GetHashAndReset(hashBytes);
        return Convert.ToHexStringLower(hashBytes[..written]);
    }

    private static string ToLowerUtf8Hex(string value)
    {
        var byteCount = Encoding.UTF8.GetByteCount(value);
        if (byteCount == 0)
            return string.Empty;

        const int StackThreshold = 256;
        if (byteCount <= StackThreshold)
        {
            Span<byte> bytes = stackalloc byte[StackThreshold];
            var written = Encoding.UTF8.GetBytes(value, bytes);
            return Convert.ToHexStringLower(bytes[..written]);
        }

        var rented = ArrayPool<byte>.Shared.Rent(byteCount);
        try
        {
            var written = Encoding.UTF8.GetBytes(value, rented);
            return Convert.ToHexStringLower(rented.AsSpan(0, written));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private enum HashPurpose : byte
    {
        Parameters = 1,
        Contract = 2,
        AggregateCodec = 3,
        DatabaseIdentity = 4
    }

    private enum CanonicalTag : byte
    {
        Null = 0,
        String = 1,
        Characters = 2,
        Binary = 3,
        Boolean = 4,
        Byte = 5,
        SByte = 6,
        Int16 = 7,
        UInt16 = 8,
        Int32 = 9,
        UInt32 = 10,
        Int64 = 11,
        UInt64 = 12,
        Single = 13,
        Double = 14,
        Decimal = 15,
        DateTime = 16,
        DateTimeOffset = 17,
        DateOnly = 18,
        TimeOnly = 19,
        TimeSpan = 20,
        Guid = 21,
        Character = 22,
        SqlDecimal = 23
    }
}
