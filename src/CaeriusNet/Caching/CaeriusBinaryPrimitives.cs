using System.Data.SqlTypes;
using Microsoft.Data.SqlTypes;

namespace CaeriusNet.Caching;

internal static class CaeriusBinaryEncoding
{
    internal static readonly UTF8Encoding StrictUtf8 = new(false, true);
}

public readonly ref struct CaeriusBinaryWriter(IBufferWriter<byte> writer)
{
    private readonly IBufferWriter<byte> _writer = writer ?? throw new ArgumentNullException(nameof(writer));

    public void WriteByte(byte value)
    {
        var span = _writer.GetSpan(1);
        span[0] = value;
        _writer.Advance(1);
    }

    public void WriteBoolean(bool value)
    {
        WriteByte(value ? (byte)1 : (byte)0);
    }

    public void WriteInt16(short value)
    {
        WriteLittleEndian(value);
    }

    public void WriteUInt16(ushort value)
    {
        WriteLittleEndian(value);
    }

    public void WriteInt32(int value)
    {
        WriteLittleEndian(value);
    }

    public void WriteUInt32(uint value)
    {
        WriteLittleEndian(value);
    }

    public void WriteInt64(long value)
    {
        WriteLittleEndian(value);
    }

    public void WriteUInt64(ulong value)
    {
        WriteLittleEndian(value);
    }

    public void WriteSingle(float value)
    {
        WriteInt32(BitConverter.SingleToInt32Bits(value));
    }

    public void WriteDouble(double value)
    {
        WriteInt64(BitConverter.DoubleToInt64Bits(value));
    }

    public void WriteChar(char value)
    {
        WriteUInt16(value);
    }

    public void WriteGuid(Guid value)
    {
        WriteInt32(16);
        var span = _writer.GetSpan(16);
        if (!value.TryWriteBytes(span))
            throw new InvalidOperationException("Unable to write a Guid cache value.");
        _writer.Advance(16);
    }

    public void WriteDateTime(DateTime value)
    {
        WriteInt64(value.Ticks);
        WriteByte((byte)value.Kind);
    }

    public void WriteDateTimeOffset(DateTimeOffset value)
    {
        WriteInt64(value.Ticks);
        WriteInt16((short)value.Offset.TotalMinutes);
    }

    public void WriteDateOnly(DateOnly value)
    {
        WriteInt32(value.DayNumber);
    }

    public void WriteTimeOnly(TimeOnly value)
    {
        WriteInt64(value.Ticks);
    }

    public void WriteTimeSpan(TimeSpan value)
    {
        WriteInt64(value.Ticks);
    }

    public void WriteDecimal(decimal value)
    {
        Span<int> parts = stackalloc int[4];
        decimal.GetBits(value, parts);
        foreach (var part in parts)
            WriteInt32(part);
    }

    public void WriteSqlDecimal(SqlDecimal value)
    {
        if (value.IsNull)
            throw new ArgumentNullException(nameof(value));
        WriteByte(value.Precision);
        WriteByte(value.Scale);
        WriteBoolean(value.IsPositive);
        var data = value.Data;
        if (data.Length != 4)
            throw new InvalidOperationException("SqlDecimal must expose four 32-bit data words.");
        for (var index = 0; index < data.Length; index++)
            WriteInt32(data[index]);
    }

    /// <summary>Writes a non-null SQL Server 2025 float32 vector with its exact dimension.</summary>
    public void WriteSqlVector(SqlVector<float> value)
    {
        if (value.IsNull)
            throw new ArgumentNullException(nameof(value));
        WriteInt32(value.Length);
        foreach (var element in value.Memory.Span)
            WriteSingle(element);
    }

    public void WriteString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var byteCount = CaeriusBinaryEncoding.StrictUtf8.GetByteCount(value);
        WriteInt32(byteCount);
        var span = _writer.GetSpan(byteCount);
        CaeriusBinaryEncoding.StrictUtf8.GetBytes(value, span);
        _writer.Advance(byteCount);
    }

    public void WriteBytes(scoped ReadOnlySpan<byte> value)
    {
        WriteInt32(value.Length);
        var span = _writer.GetSpan(value.Length);
        value.CopyTo(span);
        _writer.Advance(value.Length);
    }

    private void WriteLittleEndian<T>(T value) where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        var span = _writer.GetSpan(size);
        Unsafe.WriteUnaligned(ref span[0], value);
        if (!BitConverter.IsLittleEndian)
            span[..size].Reverse();
        _writer.Advance(size);
    }
}

public ref struct CaeriusBinaryReader
{
    private readonly ReadOnlySpan<byte> _payload;
    private int _offset;

    public CaeriusBinaryReader(ReadOnlySpan<byte> payload)
    {
        _payload = payload;
    }

    public int Remaining => _payload.Length - _offset;

    public byte ReadByte()
    {
        return ReadSpan(1)[0];
    }

    public bool ReadBoolean()
    {
        return ReadByte() switch { 0 => false, 1 => true, _ => throw InvalidPayload() };
    }

    public short ReadInt16()
    {
        return ReadLittleEndian<short>();
    }

    public ushort ReadUInt16()
    {
        return ReadLittleEndian<ushort>();
    }

    public int ReadInt32()
    {
        return ReadLittleEndian<int>();
    }

    public uint ReadUInt32()
    {
        return ReadLittleEndian<uint>();
    }

    public long ReadInt64()
    {
        return ReadLittleEndian<long>();
    }

    public ulong ReadUInt64()
    {
        return ReadLittleEndian<ulong>();
    }

    public float ReadSingle()
    {
        return BitConverter.Int32BitsToSingle(ReadInt32());
    }

    public double ReadDouble()
    {
        return BitConverter.Int64BitsToDouble(ReadInt64());
    }

    public char ReadChar()
    {
        return (char)ReadUInt16();
    }

    public Guid ReadGuid()
    {
        var bytes = ReadLengthPrefixedBytes();
        if (bytes.Length != 16)
            throw InvalidPayload();
        return new Guid(bytes);
    }

    public DateTime ReadDateTime()
    {
        return new DateTime(ReadInt64(), (DateTimeKind)ReadByte());
    }

    public DateTimeOffset ReadDateTimeOffset()
    {
        return new DateTimeOffset(ReadInt64(), TimeSpan.FromMinutes(ReadInt16()));
    }

    public DateOnly ReadDateOnly()
    {
        return DateOnly.FromDayNumber(ReadInt32());
    }

    public TimeOnly ReadTimeOnly()
    {
        return new TimeOnly(ReadInt64());
    }

    public TimeSpan ReadTimeSpan()
    {
        return new TimeSpan(ReadInt64());
    }

    public decimal ReadDecimal()
    {
        var low = ReadInt32();
        var middle = ReadInt32();
        var high = ReadInt32();
        var flags = ReadInt32();
        if ((flags & 0x7F00FFFF) != 0)
            throw InvalidPayload();

        var scale = (byte)(flags >> 16);
        if (scale > 28)
            throw InvalidPayload();
        return new decimal(low, middle, high, flags < 0, scale);
    }

    public SqlDecimal ReadSqlDecimal()
    {
        var precision = ReadByte();
        var scale = ReadByte();
        var positive = ReadBoolean();
        if (precision is 0 or > 38 || scale > precision)
            throw InvalidPayload();
        var data = new[] { ReadInt32(), ReadInt32(), ReadInt32(), ReadInt32() };
        try
        {
            return new SqlDecimal(precision, scale, positive, data);
        }
        catch (SqlTypeException)
        {
            throw InvalidPayload();
        }
    }

    /// <summary>Reads a SQL Server 2025 float32 vector written by <see cref="CaeriusBinaryWriter.WriteSqlVector" />.</summary>
    public SqlVector<float> ReadSqlVector()
    {
        var length = ReadInt32();
        if (length is < 1 or > 1998)
            throw InvalidPayload();
        var values = new float[length];
        for (var index = 0; index < values.Length; index++)
            values[index] = ReadSingle();
        return new SqlVector<float>(values);
    }

    public string ReadString()
    {
        try
        {
            return CaeriusBinaryEncoding.StrictUtf8.GetString(ReadLengthPrefixedBytes());
        }
        catch (DecoderFallbackException)
        {
            throw InvalidPayload();
        }
    }

    public byte[] ReadBytes()
    {
        return ReadLengthPrefixedBytes().ToArray();
    }

    public ReadOnlySpan<byte> ReadLengthPrefixedBytes()
    {
        var length = ReadInt32();
        if (length < 0)
            throw InvalidPayload();
        return ReadSpan(length);
    }

    private T ReadLittleEndian<T>() where T : unmanaged
    {
        var size = Unsafe.SizeOf<T>();
        var span = ReadSpan(size);
        if (BitConverter.IsLittleEndian)
            return Unsafe.ReadUnaligned<T>(ref Unsafe.AsRef(in span[0]));
        Span<byte> copy = stackalloc byte[size];
        span.CopyTo(copy);
        copy.Reverse();
        return Unsafe.ReadUnaligned<T>(ref copy[0]);
    }

    private ReadOnlySpan<byte> ReadSpan(int length)
    {
        if (length < 0 || length > Remaining)
            throw InvalidPayload();
        var value = _payload.Slice(_offset, length);
        _offset += length;
        return value;
    }

    private static CaeriusContractException InvalidPayload()
    {
        return new CaeriusContractException("The binary cache payload is truncated or malformed.");
    }
}
