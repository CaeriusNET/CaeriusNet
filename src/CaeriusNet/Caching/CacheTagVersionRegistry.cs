namespace CaeriusNet.Caching;

internal sealed class CacheTagVersionRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, long> _generations = new(StringComparer.Ordinal);

    internal string CreateVersionedKey(string key, ImmutableArray<string> tags)
    {
        if (tags.IsDefaultOrEmpty)
            return key;

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        lock (_gate)
        {
            foreach (var tag in tags)
            {
                CaeriusCacheKeyBuilder.AppendCanonicalValue(hash, tag);
                CaeriusCacheKeyBuilder.AppendCanonicalValue(
                    hash,
                    _generations.TryGetValue(tag, out var generation) ? generation : 0L);
            }
        }

        return key + ":tags:" + Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    internal void Invalidate(IEnumerable<string> tags)
    {
        lock (_gate)
        {
            foreach (var tag in tags)
                _generations[tag] = checked((_generations.TryGetValue(tag, out var value) ? value : 0L) + 1L);
        }
    }
}
