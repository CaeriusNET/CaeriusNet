namespace CaeriusNet.Tests.Caches;

/// <summary>
///     xUnit collection marker for all test classes that share the process-wide static
///     <see cref="FrozenCacheManager" /> state.
///     Tests in this collection are serialised (never run in parallel with each other),
///     preventing race conditions where <c>Clear()</c> wipes entries stored by a concurrent test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class FrozenCacheStateCollection
{
    /// <summary>Collection name referenced by <see cref="CollectionAttribute" />.</summary>
    public const string Name = "FrozenCacheState";
}