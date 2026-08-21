namespace CaeriusNet.Examples.Common.Repositories;

/// <summary>
///     Data-access implementation for user-related stored procedures.
///     Split into partial files by concern:
///     <list type="bullet">
///         <item><see cref="UsersRepository" /> — base declaration (this file)</item>
///         <item><c>UsersRepository.Reads.cs</c> — single-result-set reads + cache tiers</item>
///         <item><c>UsersRepository.Tvp.cs</c> — Table-Valued Parameter reads</item>
///         <item><c>UsersRepository.MultiResultSets.cs</c> — multi-result-set reads</item>
///         <item><c>UsersRepository.Transactions.cs</c> — transactional write scenarios</item>
///     </list>
/// </summary>
public sealed partial class UsersRepository(
    ICaeriusNetDbContext dbContext,
    IFrozenCacheManager frozenCache) : IUsersRepository
{
    // Frozen cache is a deliberately application-built snapshot. Its misses do not trigger
    // read-through population in the runtime, so this example replaces the snapshot explicitly.
    private const string AllUsersFrozenSnapshotKey = "examples.users.all.snapshot.v1";
}
