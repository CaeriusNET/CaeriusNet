namespace CaeriusNet.Tests.Caches;

/// <summary>
///     xUnit collection marker for test classes that mutate process-wide static cache state.
///     Tests in this collection are serialised (never run in parallel with each other),
///     preventing race conditions where <c>Clear()</c> or <c>Configure()</c> wipes entries or options
///     used by a concurrent test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class FrozenCacheStateCollection
{
    /// <summary>Collection name referenced by <see cref="CollectionAttribute" />.</summary>
    public const string Name = "FrozenCacheState";
}
