namespace CaeriusNet.Caching;

/// <summary>Encodes every result set of a multi-result execution as one atomic cache value.</summary>
internal sealed class CaeriusAggregateCacheCodec : ICaeriusCacheCodec<object[]>
{
    private readonly ICaeriusCacheCodec[] _codecs;

    internal CaeriusAggregateCacheCodec(IReadOnlyList<ICaeriusCacheCodec> codecs)
    {
        ArgumentNullException.ThrowIfNull(codecs);
        _codecs = [.. codecs];
        if (_codecs.Length is < 2 or > 10)
            throw new ArgumentOutOfRangeException(nameof(codecs),
                "A multi-result cache codec requires between two and ten result-set codecs.");

        CodecId = string.Concat("caerius/multi-result/v1/", _codecs.Length.ToString(CultureInfo.InvariantCulture));
        ContractHash = CaeriusCacheKeyBuilder.HashAggregateCodecContract(_codecs);
    }

    public string CodecId { get; }

    public string ContractHash { get; }

    public void Encode(IBufferWriter<byte> writer, object[] value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != _codecs.Length)
            throw new CaeriusContractException("The multi-result cache value does not match its codec arity.");

        var binary = new CaeriusBinaryWriter(writer);
        binary.WriteInt32(value.Length);
        var itemWriter = new ArrayBufferWriter<byte>();
        for (var index = 0; index < value.Length; index++)
        {
            itemWriter.Clear();
            _codecs[index].EncodeObject(itemWriter, value[index]);
            binary.WriteBytes(itemWriter.WrittenSpan);
        }
    }

    public object[] Decode(ReadOnlySpan<byte> payload)
    {
        var binary = new CaeriusBinaryReader(payload);
        var count = binary.ReadInt32();
        if (count != _codecs.Length)
            throw new CaeriusContractException("The multi-result cache payload does not match its codec arity.");

        var result = new object[count];
        for (var index = 0; index < count; index++)
            result[index] = _codecs[index].DecodeObject(binary.ReadLengthPrefixedBytes());
        if (binary.Remaining != 0)
            throw new CaeriusContractException("The multi-result cache payload contains trailing data.");
        return result;
    }
}
