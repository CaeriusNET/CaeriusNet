using System.Buffers;
using CaeriusNet.Abstractions;
using CaeriusNet.Caching;
using CaeriusNet.Exceptions;

namespace CaeriusNet.Tests.Runtime;

internal sealed class ManualCacheDtoCodec : ICaeriusCacheCodec<ManualCacheDto>
{
    public string CodecId => "tests/manual-cache-dto/v1";
    public string ContractHash => "3904beb2f313757e";

    public void Encode(IBufferWriter<byte> writer, ManualCacheDto value)
    {
        ArgumentNullException.ThrowIfNull(value);
        new CaeriusBinaryWriter(writer).WriteInt32(value.Id);
    }

    public ManualCacheDto Decode(ReadOnlySpan<byte> payload)
    {
        var reader = new CaeriusBinaryReader(payload);
        var value = new ManualCacheDto(reader.ReadInt32());
        if (reader.Remaining != 0)
            throw new CaeriusContractException("The manual test codec contains trailing data.");
        return value;
    }
}
