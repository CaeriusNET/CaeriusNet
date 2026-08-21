using System.Runtime.InteropServices;

namespace CaeriusNet.Caching;

internal abstract class CaeriusCollectionCacheCodec<TCollection, TElement> : ICaeriusCacheCodec<TCollection>
{
    // A malformed cache payload must never be able to allocate an unbounded array merely by claiming a count.
    // This comfortably exceeds the 100,000-row cache acceptance/benchmark target while remaining a hard ceiling.
    private const int MaximumElementCount = 1_000_000;

    private readonly ICaeriusCacheCodec<TElement> _elementCodec;

    protected CaeriusCollectionCacheCodec(ICaeriusCacheCodec<TElement> elementCodec, string shape)
    {
        _elementCodec = elementCodec ?? throw new ArgumentNullException(nameof(elementCodec));
        ArgumentException.ThrowIfNullOrWhiteSpace(shape);
        CodecId = string.Concat(_elementCodec.CodecId, "/", shape, "/v1");
        ContractHash = string.Concat(_elementCodec.ContractHash, ":", shape, ":v1");
    }

    public string CodecId { get; }

    public string ContractHash { get; }

    public void Encode(IBufferWriter<byte> writer, TCollection value)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);
        var items = Enumerate(value);
        var binary = new CaeriusBinaryWriter(writer);
        binary.WriteInt32(items.Count);

        // The framing needs an element length, so a temporary contiguous buffer is required for a generic codec.
        // Reusing one buffer instead of allocating one per element removes O(n) temporary writers and arrays.
        var itemWriter = new ArrayBufferWriter<byte>();
        foreach (var item in items)
        {
            itemWriter.Clear();
            _elementCodec.Encode(itemWriter, item);
            binary.WriteBytes(itemWriter.WrittenSpan);
        }
    }

    public TCollection Decode(ReadOnlySpan<byte> payload)
    {
        var reader = new CaeriusBinaryReader(payload);
        var count = reader.ReadInt32();
        if (count < 0 || count > MaximumElementCount || count > reader.Remaining / sizeof(int))
            throw new CaeriusContractException("A cache collection contains an invalid element count.");

        var items = new TElement[count];
        for (var index = 0; index < count; index++)
            items[index] = _elementCodec.Decode(reader.ReadLengthPrefixedBytes());
        if (reader.Remaining != 0)
            throw new CaeriusContractException("A cache collection contains trailing data.");
        return Create(items);
    }

    protected abstract IReadOnlyList<TElement> Enumerate(TCollection value);
    protected abstract TCollection Create(TElement[] items);
}

internal sealed class CaeriusArrayCacheCodec<T>(ICaeriusCacheCodec<T> elementCodec)
    : CaeriusCollectionCacheCodec<T[], T>(elementCodec, "array")
{
    protected override IReadOnlyList<T> Enumerate(T[] value)
    {
        return value;
    }

    protected override T[] Create(T[] items)
    {
        return items;
    }
}

internal sealed class CaeriusReadOnlyCollectionCacheCodec<T>(ICaeriusCacheCodec<T> elementCodec)
    : CaeriusCollectionCacheCodec<ReadOnlyCollection<T>, T>(elementCodec, "readonly")
{
    protected override IReadOnlyList<T> Enumerate(ReadOnlyCollection<T> value)
    {
        return value;
    }

    protected override ReadOnlyCollection<T> Create(T[] items)
    {
        return new ReadOnlyCollection<T>(items);
    }
}

internal sealed class CaeriusImmutableArrayCacheCodec<T>(ICaeriusCacheCodec<T> elementCodec)
    : CaeriusCollectionCacheCodec<ImmutableArray<T>, T>(elementCodec, "immutable")
{
    protected override IReadOnlyList<T> Enumerate(ImmutableArray<T> value)
    {
        return value;
    }

    protected override ImmutableArray<T> Create(T[] items)
    {
        return ImmutableCollectionsMarshal.AsImmutableArray(items);
    }
}
